using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 선택 화면(02.플레이어선택)을 받은 디자인 이미지대로 다시 꾸민다.
///
/// 로비와 같은 방식이다. <b>씬의 오브젝트를 지우지 않고 자리와 그림만 바꾼다.</b>
/// 다만 선택 버튼 두 개는 씬에 저장된 onClick이 예전 배경 이미지를 갈아끼우는
/// FullscreenImageSwitcher_Fade 를 부르고 있었다. 그 그림은 새 디자인과 맞지 않아
/// 가운데 구멍으로 예전 화면이 비친다. 그래서 버튼의 이벤트만 새로 꽂고
/// 같은 토글 동작(같은 걸 또 누르면 선택 해제)을 여기서 그대로 다시 구현했다.
/// 씬 파일은 건드리지 않으므로 인스펙터의 연결은 그대로 남아 있다.
///
/// 좌표는 받은 COORDS_1.txt 그대로 디자인 원본(3840 x 2160) 기준으로 적는다.
///   상단 제목 칸      1040,  320   1760 x 240
///   좌측 선택 프레임   200,  560    570 x 1120
///   우측 선택 프레임  3070,  560    570 x 1120
///   좌측 네임 플레이트 200, 1712    570 x 88
///   우측 네임 플레이트 3070, 1712   570 x 88
///   하단 확인 버튼    1480, 1880    880 x 150
///   가운데 개구부     중심 (1920, 1150) 약 2180 x 1440
///
/// 캐릭터 그림 위치는 받은 README 값을 따른다.
///   중앙 대형 : 가로 중앙 1920, 발끝 y=1860, 높이 1380
///   프레임용  : 좌 중심 x=485 / 우 중심 x=3355, 발끝 y=1620, 높이 1000
/// </summary>
public class AetherCharacterSelectScreen : MonoBehaviour
{
    private const string SceneKeyword = "플레이어선택";

    private const string OverlayResource = "UI/CharSelectOverlay";
    private const string ArtResourceRoot = "UI/CharSelect/";

    /// <summary>씬의 글자는 기본 영문 폰트라 한글이 네모로 나온다. HUD와 같은 글꼴을 쓴다.</summary>
    private const string FontResource = "UI/Fonts/NanumGothicCoding-Bold SDF";

    // 디자인 원본 크기. 이 좌표계로 적어 두고 캔버스(1920x1080) 기준으로 환산한다.
    private const float DesignWidth = 3840f;
    private const float DesignHeight = 2160f;
    private const float ToCanvas = 1920f / DesignWidth;   // 0.5

    // COORDS_1.txt 의 색상
    private static readonly Color Amber = new Color32(0xF0, 0xA8, 0x30, 0xFF);
    private static readonly Color LabelNormal = new Color32(0xD9, 0xD7, 0xD1, 0xFF);
    private static readonly Color Backdrop = new Color32(0x0B, 0x0D, 0x0F, 0xFF);

    private static readonly Color ThumbDim = new Color(0.52f, 0.54f, 0.57f, 0.85f);
    private static readonly Color ThumbLit = Color.white;

    /// <summary>0 = 아무도 안 고름, 1 = 왼쪽(윤서), 2 = 오른쪽(한결)</summary>
    private int selected;

    private Image artImage;
    private Image leftThumb;
    private Image rightThumb;
    private Image leftGlow;
    private Image rightGlow;
    private TMP_Text leftName;
    private TMP_Text rightName;
    private TMP_Text hint;
    private TMP_Text confirmLabel;
    private Button confirmButton;

    private Sprite leftCenterArt;
    private Sprite rightCenterArt;

    private TMP_FontAsset uiFont;
    private Transform confirmTransform;
    private Transform titleTransform;

    private Coroutine artFade;

    private static bool builtThisScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableScreens)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Apply();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        builtThisScene = false;
        Apply();
    }

    private static void Apply()
    {
        if (builtThisScene)
            return;

        if (SceneManager.GetActiveScene().name.IndexOf(SceneKeyword, System.StringComparison.Ordinal) < 0)
            return;

        builtThisScene = true;

        GameObject host = new GameObject("AetherCharacterSelectScreen");
        host.AddComponent<AetherCharacterSelectScreen>();
    }

    private void Start()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.Log("[캐릭터선택] 캔버스를 찾지 못해 꾸미기를 건너뜁니다.");
            Destroy(gameObject);
            return;
        }

        NormalizeCanvas(canvas);

        uiFont = Resources.Load<TMP_FontAsset>(FontResource);
        if (uiFont == null)
            uiFont = TMP_Settings.defaultFontAsset;

        leftCenterArt = Resources.Load<Sprite>(ArtResourceRoot + "char_01_center");
        rightCenterArt = Resources.Load<Sprite>(ArtResourceRoot + "char_02_center");

        Transform left = FindDeep(canvas.transform, "Yunseo");
        Transform right = FindDeep(canvas.transform, "hankyul");
        confirmTransform = FindDeep(canvas.transform, "Button");

        Image backdrop = BuildBackdrop(canvas);
        BuildCenterArt(canvas);
        Image plate = BuildPlate(canvas);
        BuildHint(canvas);
        BuildTitle(canvas);

        leftName = DetachLabel(canvas, left, "Yunseo Name", 200f);
        rightName = DetachLabel(canvas, right, "Hankyul Name", 3070f);

        leftGlow = SetUpSelectButton(left, 200f, 1, "char_01_frame", out leftThumb);
        rightGlow = SetUpSelectButton(right, 3070f, 2, "char_02_frame", out rightThumb);

        SetUpConfirm(confirmTransform);
        CalmDownFadeOverlay(canvas);

        // 그리는 순서. 뒤 -> 앞.
        Order(
            backdrop == null ? null : backdrop.transform,
            artImage == null ? null : artImage.transform,
            plate == null ? null : plate.transform,
            left,
            right,
            leftName == null ? null : leftName.transform,
            rightName == null ? null : rightName.transform,
            hint == null ? null : hint.transform,
            titleTransform,
            confirmTransform);

        ApplySelection(false);

        Debug.Log("[캐릭터선택] 디자인 이미지로 화면을 다시 꾸몄습니다.");
    }

    // ---------------------------------------------------------------- 캔버스

    /// <summary>
    /// 선택 버튼을 실제로 품고 있는 캔버스를 고른다.
    /// 다른 씬에서 넘어온 캔버스(DontDestroyOnLoad)를 잘못 집지 않도록 걸러 낸다.
    /// </summary>
    private static Canvas FindCanvas()
    {
        Canvas fallback = null;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (canvas.gameObject.scene != SceneManager.GetActiveScene())
                continue;

            if (FindDeep(canvas.transform, "Yunseo") != null && FindDeep(canvas.transform, "hankyul") != null)
                return canvas;

            if (fallback == null)
                fallback = canvas;
        }

        return fallback;
    }

    /// <summary>
    /// 이 캔버스는 기준 해상도가 800x600에 Constant Pixel Size였다. 해상도가 바뀌면
    /// 글자와 버튼이 제각각으로 커지고 작아진다.
    ///
    /// 또한 이 화면은 큰 그림 한 장 위에 칸이 그려져 있어서, 칸과 그림이 어긋나면 안 된다.
    /// 그래서 화면비가 달라도 그림 전체가 들어가도록 Expand로 두고
    /// 남는 가장자리는 어두운 바탕으로 덮는다.
    /// </summary>
    private static void NormalizeCanvas(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // 전역 정규화가 이 설정을 다시 덮어쓰지 않도록 표시해 둔다.
        if (canvas.GetComponent<AetherDesignCanvasMarker>() == null)
            canvas.gameObject.AddComponent<AetherDesignCanvasMarker>();
    }

    // ---------------------------------------------------------------- 배경·그림

    private static Image BuildBackdrop(Canvas canvas)
    {
        Image image = EnsureImage(canvas, "CharSelect Backdrop");
        Stretch(image.rectTransform);
        image.sprite = null;
        image.color = Backdrop;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// 가운데에 세워 놓을 캐릭터 그림. 씬에 이미 있던 전체화면 Image를 그대로 쓴다.
    /// (예전 배경 그림이 꽂혀 있던 자리다.)
    /// </summary>
    private void BuildCenterArt(Canvas canvas)
    {
        Transform existing = FindDeep(canvas.transform, "Image");

        if (existing != null)
            artImage = existing.GetComponent<Image>();

        if (artImage == null)
            artImage = EnsureImage(canvas, "CharSelect Art");

        // 가로 중앙 1920, 발끝 y=1860. 높이 1380 이면 머리가 위의 "CHARACTER SELECT" 제목(아래끝 y≈560)에 걸쳐 1260 으로 줄였다.
        PlaceByDesignRect(artImage.rectTransform, 1920f - 260f, 1860f - 1260f, 520f, 1260f);

        artImage.sprite = null;
        artImage.preserveAspect = true;
        artImage.raycastTarget = false;
        artImage.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>받은 오버레이 판을 캐릭터 그림 위에 그대로 올린다.</summary>
    private static Image BuildPlate(Canvas canvas)
    {
        Image image = EnsureImage(canvas, "CharSelect Plate");

        Sprite plate = Resources.Load<Sprite>(OverlayResource);
        if (plate == null)
            Debug.Log($"[캐릭터선택] 오버레이 그림을 찾지 못했습니다: Resources/{OverlayResource}");

        // 그림의 칸 위치와 버튼 위치가 어긋나면 안 되므로, 늘이지 않고 디자인 크기 그대로 둔다.
        PlaceByDesignRect(image.rectTransform, 0f, 0f, DesignWidth, DesignHeight);

        image.sprite = plate;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    // ---------------------------------------------------------------- 글자

    private void BuildTitle(Canvas canvas)
    {
        TMP_Text title = EnsureText(canvas, "CharSelect Title");
        titleTransform = title.transform;
        PlaceByDesignRect(title.rectTransform, 1040f, 320f, 1760f, 240f);

        ApplyFont(title);
        title.text = "CHARACTER SELECT";
        title.fontSize = Font(112f);
        title.color = Amber;
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = 18f;
        title.raycastTarget = false;
    }

    private void BuildHint(Canvas canvas)
    {
        hint = EnsureText(canvas, "CharSelect Hint");
        PlaceByDesignRect(hint.rectTransform, 1040f, 1060f, 1760f, 140f);

        ApplyFont(hint);
        hint.text = "좌우 칸에서 함께할 대원을 고르세요";
        hint.fontSize = Font(60f);
        hint.color = new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.55f);
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
    }

    /// <summary>
    /// 이름표. 씬에서는 버튼 안에 빈 글자 오브젝트로 들어 있었다(내용이 비어 있어 아무것도 안 보였다).
    /// 디자인의 네임 플레이트는 프레임 바깥 아래에 있으므로 캔버스 바로 밑으로 옮긴다.
    /// </summary>
    private TMP_Text DetachLabel(Canvas canvas, Transform button, string newName, float designX)
    {
        TMP_Text label = null;

        if (button != null)
        {
            foreach (TMP_Text candidate in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label = candidate;
                break;
            }
        }

        if (label == null)
            label = EnsureText(canvas, newName);
        else
            label.transform.SetParent(canvas.transform, false);

        label.gameObject.name = newName;
        label.gameObject.SetActive(true);

        PlaceByDesignRect(label.rectTransform, designX, 1712f, 570f, 88f);

        ApplyFont(label);
        label.fontSize = Font(52f);
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 10f;
        label.raycastTarget = false;
        label.enabled = true;
        return label;
    }

    // ---------------------------------------------------------------- 버튼

    /// <summary>선택 프레임 하나를 꾸미고, 눌렀을 때의 동작을 새로 꽂는다.</summary>
    private Image SetUpSelectButton(Transform button, float designX, int index, string thumbName, out Image thumb)
    {
        thumb = null;

        if (button == null)
        {
            Debug.Log($"[캐릭터선택] 선택 버튼({index})을 찾지 못했습니다.");
            return null;
        }

        PlaceByDesignRect(button as RectTransform, designX, 560f, 570f, 1120f);

        Image surface = button.GetComponent<Image>();
        if (surface != null)
        {
            // 칸 테두리는 배경 그림에 이미 그려져 있다. 누를 수 있는 투명한 면만 남긴다.
            surface.sprite = null;
            surface.type = Image.Type.Simple;
            surface.color = new Color(1f, 1f, 1f, 0f);
            surface.raycastTarget = true;
        }

        // 선택했을 때 칸 안쪽을 은은하게 밝히는 판.
        Image glow = EnsureChildImage(button, "Selected Glow");
        Stretch(glow.rectTransform);
        glow.sprite = null;
        // 이 프로젝트는 선형 색 공간이라 같은 알파도 화면에서는 훨씬 밝게 섞인다.
        // 0.015 정도가 눈으로 보기에 "은은한 호박색 바탕"이 된다(0.05는 겨자색 판으로 보였다).
        glow.color = new Color(Amber.r, Amber.g, Amber.b, 0.015f);
        glow.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        // 칸 안에 세워 둘 작은 캐릭터 그림.
        // 프레임 중심과 그림 중심이 같은 자리라, 가운데 정렬에 크기만 맞추면 된다.
        thumb = EnsureChildImage(button, "Thumbnail");
        RectTransform thumbRect = thumb.rectTransform;
        thumbRect.anchorMin = new Vector2(0.5f, 0.5f);
        thumbRect.anchorMax = new Vector2(0.5f, 0.5f);
        thumbRect.pivot = new Vector2(0.5f, 0.5f);
        thumbRect.sizeDelta = new Vector2(400f * ToCanvas, 1000f * ToCanvas);
        thumbRect.anchoredPosition = Vector2.zero;
        thumbRect.localScale = Vector3.one;

        thumb.sprite = Resources.Load<Sprite>(ArtResourceRoot + thumbName);
        thumb.preserveAspect = true;
        thumb.raycastTarget = false;
        thumb.color = ThumbDim;

        Button control = button.GetComponent<Button>();
        if (control != null)
        {
            StyleTint(control);

            // 씬에 저장된 호출(예전 배경 갈아끼우기)을 떼어 내고 같은 토글 동작을 다시 꽂는다.
            control.onClick = new Button.ButtonClickedEvent();

            int captured = index;
            control.onClick.AddListener(() => Toggle(captured));
        }

        return glow;
    }

    private void SetUpConfirm(Transform confirm)
    {
        if (confirm == null)
        {
            Debug.Log("[캐릭터선택] 확인 버튼을 찾지 못했습니다.");
            return;
        }

        PlaceByDesignRect(confirm as RectTransform, 1480f, 1880f, 880f, 150f);

        Image surface = confirm.GetComponent<Image>();
        if (surface != null)
        {
            surface.sprite = null;
            surface.type = Image.Type.Simple;
            surface.color = new Color(1f, 1f, 1f, 0f);
            surface.raycastTarget = true;
        }

        foreach (TMP_Text text in confirm.GetComponentsInChildren<TMP_Text>(true))
        {
            confirmLabel = text;
            break;
        }

        if (confirmLabel == null)
            confirmLabel = EnsureChildText(confirm, "Confirm Label");

        Stretch(confirmLabel.rectTransform);
        confirmLabel.gameObject.SetActive(true);
        confirmLabel.enabled = true;
        ApplyFont(confirmLabel);
        confirmLabel.text = "CONFIRM";
        confirmLabel.fontSize = Font(64f);
        confirmLabel.alignment = TextAlignmentOptions.Center;
        confirmLabel.characterSpacing = 14f;
        confirmLabel.raycastTarget = false;

        confirmButton = confirm.GetComponent<Button>();
        if (confirmButton == null)
            return;

        StyleTint(confirmButton);

        // 씬에 저장된 onClick이 비어 있어서 이 버튼은 아무 일도 하지 않았다.
        // 같은 오브젝트에 붙어 있는 ButtonSceneLoader(03.LoadingScene)를 직접 부른다.
        if (confirmButton.onClick.GetPersistentEventCount() == 0)
        {
            ButtonSceneLoader loader = confirm.GetComponent<ButtonSceneLoader>();

            if (loader != null)
                confirmButton.onClick.AddListener(loader.LoadScene);
            else
                confirmButton.onClick.AddListener(() => AetherSceneFade.LoadScene("03.LoadingScene"));
        }
    }

    private static void StyleTint(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(Amber.r, Amber.g, Amber.b, 0.04f);
        colors.pressedColor = new Color(Amber.r, Amber.g, Amber.b, 0.10f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }

    /// <summary>
    /// 예전 전환 연출에 쓰던 검은 판(Overlay)은 화면 전체를 덮은 채 클릭까지 가로채고 있었다.
    /// 이제 쓰지 않으므로 보이지 않게 두고 입력도 통과시킨다.
    /// </summary>
    private static void CalmDownFadeOverlay(Canvas canvas)
    {
        Transform overlay = FindDeep(canvas.transform, "Overlay");
        if (overlay == null)
            return;

        Image image = overlay.GetComponent<Image>();
        if (image == null)
            return;

        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
    }

    // ---------------------------------------------------------------- 선택

    private void Toggle(int index)
    {
        selected = (selected == index) ? 0 : index;
        ApplySelection(true);

        // 고른 캐릭터를 남긴다. 예전에는 화면에만 보이고 어디에도 저장되지 않아 늘 한결로 시작했다.
        if (selected != 0)
            AetherCharacterChoice.Selected = selected == 1 ? AetherCharacter.Yunseo : AetherCharacter.Hankyul;
    }

    private void ApplySelection(bool fade)
    {
        if (leftThumb != null)
            leftThumb.color = (selected == 1) ? ThumbLit : ThumbDim;

        if (rightThumb != null)
            rightThumb.color = (selected == 2) ? ThumbLit : ThumbDim;

        if (leftGlow != null)
            leftGlow.gameObject.SetActive(selected == 1);

        if (rightGlow != null)
            rightGlow.gameObject.SetActive(selected == 2);

        if (leftName != null)
        {
            leftName.text = AetherCharacterChoice.YunseoDisplayName;
            leftName.color = (selected == 1) ? Amber : new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.6f);
        }

        if (rightName != null)
        {
            rightName.text = AetherCharacterChoice.HankyulDisplayName;
            rightName.color = (selected == 2) ? Amber : new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.6f);
        }

        if (hint != null)
            hint.gameObject.SetActive(selected == 0);

        if (confirmButton != null)
            confirmButton.interactable = selected != 0;

        if (confirmLabel != null)
        {
            confirmLabel.color = (selected == 0)
                ? new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.35f)
                : Amber;
        }

        ShowArt(selected == 1 ? leftCenterArt : selected == 2 ? rightCenterArt : null, fade);
    }

    private void ShowArt(Sprite sprite, bool fade)
    {
        if (artImage == null)
            return;

        if (artFade != null)
        {
            StopCoroutine(artFade);
            artFade = null;
        }

        artImage.sprite = sprite;

        if (sprite == null)
        {
            artImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        if (!fade)
        {
            artImage.color = Color.white;
            return;
        }

        artFade = StartCoroutine(FadeArtIn());
    }

    private IEnumerator FadeArtIn()
    {
        const float seconds = 0.18f;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / seconds);
            artImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        artImage.color = Color.white;
        artFade = null;
    }

    // ---------------------------------------------------------------- 도구

    /// <summary>뒤에서 앞 순서로 그리도록 형제 순서를 맞춘다.</summary>
    private static void Order(params Transform[] backToFront)
    {
        int index = 0;

        for (int i = 0; i < backToFront.Length; i++)
            Put(backToFront[i], ref index);
    }

    private static void Put(Transform target, ref int index)
    {
        if (target == null)
            return;

        target.SetSiblingIndex(index);
        index++;
    }

    private static Image EnsureImage(Canvas canvas, string name)
    {
        Transform existing = canvas.transform.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            if (found != null)
                return found;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);
        return go.GetComponent<Image>();
    }

    private static Image EnsureChildImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            if (found != null)
                return found;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static TMP_Text EnsureText(Canvas canvas, string name)
    {
        Transform existing = canvas.transform.Find(name);
        if (existing != null)
        {
            TMP_Text found = existing.GetComponent<TMP_Text>();
            if (found != null)
                return found;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    private static TMP_Text EnsureChildText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            TMP_Text found = existing.GetComponent<TMP_Text>();
            if (found != null)
                return found;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    /// <summary>한글이 들어가는 글자는 반드시 한글 글꼴로 바꾼다. 기본 글꼴에는 한글이 없다.</summary>
    private void ApplyFont(TMP_Text text)
    {
        if (text == null || uiFont == null)
            return;

        text.font = uiFont;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 디자인 원본(3840x2160) 좌상단 기준 좌표를 캔버스(1920x1080) 좌표로 옮긴다.
    /// 캔버스는 가운데가 (0,0)이고 위가 +y라, 좌상단 기준 값을 그대로 쓰면 뒤집힌다.
    /// </summary>
    private static void PlaceByDesignRect(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null)
            return;

        float centerX = (x + width * 0.5f) * ToCanvas - 1920f * 0.5f;
        float centerY = 1080f * 0.5f - (y + height * 0.5f) * ToCanvas;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width * ToCanvas, height * ToCanvas);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    /// <summary>디자인 원본 기준 글자 크기를 캔버스 기준으로 옮긴다.</summary>
    private static float Font(float designSize)
    {
        return designSize * ToCanvas;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}
