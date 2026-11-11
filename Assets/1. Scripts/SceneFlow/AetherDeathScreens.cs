using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 쓰러졌을 때(DOWNED)와 작전 실패(ELIMINATED) 화면을 만든다.
///
/// 디자인 이미지를 통째로 배경에 깔고, 비어 있는 칸에만 살아 있는 값을 얹는다.
/// 글자를 다시 그리지 않으니 디자인이 그대로 유지된다.
/// </summary>
public class AetherDeathScreens : MonoBehaviour
{
    private const string FontResource = "UI/Fonts/NanumGothicCoding-Bold SDF";
    private const string DownedImage = "UI/DeathScreens/DownedScreen";
    private const string EliminatedImage = "UI/DeathScreens/EliminatedScreen";

    // 디자인 이미지의 실제 크기. 아래 좌표는 전부 이 이미지의 픽셀 좌표다.
    private const float DownedWidth = 1920f;
    private const float DownedHeight = 1080f;
    private const float ElimWidth = 2000f;
    private const float ElimHeight = 1125f;

    public event Action Spectate;
    public event Action LeaveMatch;
    public event Action Retry;
    public event Action Lobby;

    private TMP_FontAsset font;
    private Canvas canvas;

    private RectTransform downedRoot;
    private TextMeshProUGUI downedTimer;
    private RectTransform downedBarFill;
    private TextMeshProUGUI downedMate;
    private TextMeshProUGUI downedNotice;
    private Button spectateButton;

    private RectTransform elimRoot;
    private TextMeshProUGUI[] elimValues;
    private TextMeshProUGUI elimAetherLine;

    private float barMaxWidth;

    public bool IsShowing => canvas != null && canvas.gameObject.activeSelf;

    // ───────────────────────── 바깥에서 쓰는 것 ─────────────────────────

    public void ShowDowned(string mateLine, string noticeLine, bool canSpectate)
    {
        Build();

        canvas.gameObject.SetActive(true);
        downedRoot.gameObject.SetActive(true);
        elimRoot.gameObject.SetActive(false);

        downedMate.text = mateLine;
        downedNotice.text = noticeLine;
        leaveHold = 0f;
        if (leaveHint != null) leaveHint.text = string.Empty;

        if (spectateButton != null)
        {
            spectateButton.interactable = canSpectate;

            // 관전할 동료가 없으면 버튼이 눌리는 것처럼 보이면 안 된다.
            Image dim = spectateButton.GetComponent<Image>();
            if (dim != null)
                dim.color = canSpectate ? new Color(1f, 1f, 1f, 0f) : new Color(0f, 0f, 0f, 0.55f);
        }
    }

    /// <summary>남은 구조 대기 시간을 매 프레임 갱신한다.</summary>
    public void UpdateDownedTimer(float secondsLeft, float totalSeconds)
    {
        if (downedTimer == null)
            return;

        downedTimer.text = AetherRunStats.FormatTime(secondsLeft);

        float ratio = totalSeconds > 0.01f ? Mathf.Clamp01(secondsLeft / totalSeconds) : 0f;
        downedBarFill.sizeDelta = new Vector2(barMaxWidth * ratio, downedBarFill.sizeDelta.y);
    }

    public void ShowEliminated()
    {
        Build();

        canvas.gameObject.SetActive(true);
        downedRoot.gameObject.SetActive(false);
        elimRoot.gameObject.SetActive(true);
        leaveHold = 0f; // 쓰러짐 화면에서 누르던 ESC 가 이어서 세어지지 않게
        if (elimLeaveHint != null) elimLeaveHint.text = string.Empty;

        elimValues[0].text = AetherRunStats.FormatTime(AetherRunStats.SurvivedSeconds);
        elimValues[1].text = AetherRunStats.Kills.ToString();
        elimValues[2].text = Mathf.RoundToInt(AetherRunStats.DamageDealt).ToString();
        elimValues[3].text = AetherRunStats.Sector;

        // 이번 판에서 도달한 에테르 레벨. 죽어도 모은 에테르는 남는다(다시 시도해도 이어진다).
        if (elimAetherLine != null)
            elimAetherLine.text = AetherLevel.Summary();
    }

    public void Hide()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsShowing)
            return;

        if (downedRoot != null && downedRoot.gameObject.activeSelf)
        {
            if (WasPressed(KeyCode.Tab) && spectateButton != null && spectateButton.interactable)
                Spectate?.Invoke();

            // 예전엔 ESC 한 번에 바로 판을 나가 타이틀로 갔다(8초 뒤 되살아나는데도 실수로 판을 날리기 쉬웠다).
            // 이제 ESC 를 1.5초 동안 계속 눌러야 나간다. 떼면 처음부터.
            UpdateLeaveHold(leaveHint, downedLeavePress, () => LeaveMatch?.Invoke());
            return;
        }

        if (elimRoot != null && elimRoot.gameObject.activeSelf)
        {
            if (WasPressed(KeyCode.F))
                Retry?.Invoke();
            else
                // 작전 실패 화면도 ESC 한 번에 타이틀로 나가 '다시 시도' 기회를 잃기 쉬웠다. 같은 방식으로 길게 눌러야 나간다.
                UpdateLeaveHold(elimLeaveHint, elimLeavePress, () => Lobby?.Invoke());
        }
    }

    private const float LeaveHoldSeconds = 1.5f;
    private TextMeshProUGUI leaveHint;
    private TextMeshProUGUI elimLeaveHint;
    private float leaveHold;
    private float leaveHintUntil;

    private AetherPressHold downedLeavePress;
    private AetherPressHold elimLeavePress;

    private void UpdateLeaveHold(TextMeshProUGUI hint, AetherPressHold button, System.Action leave)
    {
        bool held = IsHeld(KeyCode.Escape) || (button != null && button.IsHeld);
        if (held)
        {
            leaveHold += Time.unscaledDeltaTime;
            if (hint != null)
                hint.text = $"계속 누르면 나갑니다  {Mathf.Max(0f, LeaveHoldSeconds - leaveHold):0.0}";

            if (leaveHold >= LeaveHoldSeconds)
            {
                leaveHold = 0f;
                if (hint != null) hint.text = string.Empty;
                leave?.Invoke();
            }
            return;
        }

        if (leaveHold > 0f)
        {
            // 짧게 눌렀다 뗐다: 나가지 않고 방법만 알려 준다.
            leaveHold = 0f;
            leaveHintUntil = Time.unscaledTime + 2.5f;
            if (hint != null) hint.text = "길게 누르면 나갑니다 (ESC · 클릭)";
        }
        else if (hint != null && hint.text.Length > 0 && Time.unscaledTime > leaveHintUntil)
        {
            hint.text = string.Empty;
        }
    }

    private static bool IsHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && key == KeyCode.Escape)
            return keyboard.escapeKey.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#else
        return false;
#endif
    }

    /// <summary>쓰러짐·작전 실패 화면이 떠 있는가(이때 ESC 가 일시정지 메뉴를 열면 안 된다).</summary>
    public static bool AnyShowing
    {
        get
        {
            foreach (var s in FindObjectsByType<AetherDeathScreens>(FindObjectsSortMode.None))
                if (s != null && s.IsShowing) return true;
            return false;
        }
    }

    /// <summary>이 프로젝트는 옛 입력과 새 입력을 같이 쓴다. 양쪽 다 본다.</summary>
    private static bool WasPressed(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            switch (key)
            {
                case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
            }
        }
#endif
        return Input.GetKeyDown(key);
    }

    // ───────────────────────── 화면 만들기 ─────────────────────────

    private void Build()
    {
        if (canvas != null)
            return;

        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>(FontResource);
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            // 이 폰트는 글자를 그때그때 아틀라스에 넣는 방식인데, 1024x1024 한 장이 꽉 차면
            // 더 못 넣어서 처음 보는 한글이 네모(□)로 나온다. 장수 제한을 풀어 준다.
            if (font != null)
                font.isMultiAtlasTexturesEnabled = true;
        }

        GameObject root = new GameObject("AETHER 사망 화면", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;   // HUD(20) 보다 위

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DownedWidth, DownedHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        root.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        BuildDowned(root.transform);
        BuildEliminated(root.transform);

        canvas.gameObject.SetActive(false);
    }

    private void BuildDowned(Transform parent)
    {
        downedRoot = CreateDesignRoot(parent, "Downed", DownedImage, DownedWidth, DownedHeight);

        // 구조 대기 시간. 디자인의 "— : —" 자리(오른쪽 끝 x=1340, y=625).
        downedTimer = CreateText(downedRoot, "RescueTimer", 26, TextAlignmentOptions.MidlineRight, new Color(0.95f, 0.96f, 0.98f, 1f));
        Place(downedTimer.rectTransform, DownedWidth, DownedHeight, 1040f, 610f, 300f, 34f);

        // 대기 막대. 왼쪽 끝(x=578, y=649)에서 오른쪽으로 줄어든다.
        barMaxWidth = 762f;
        RectTransform barTrack = CreateRect(downedRoot, "RescueBar");
        barTrack.pivot = new Vector2(0f, 0.5f);
        barTrack.anchorMin = barTrack.anchorMax = new Vector2(0.5f, 0.5f);
        barTrack.anchoredPosition = new Vector2(578f - DownedWidth * 0.5f, DownedHeight * 0.5f - 649f);
        barTrack.sizeDelta = new Vector2(barMaxWidth, 5f);

        Image fill = barTrack.gameObject.AddComponent<Image>();
        fill.color = new Color(0.86f, 0.22f, 0.18f, 0.95f);
        fill.raycastTarget = false;
        downedBarFill = barTrack;

        // 동료 칸 안쪽(x 718~1340, y 700~745).
        downedMate = CreateText(downedRoot, "MateStatus", 22, TextAlignmentOptions.MidlineLeft, new Color(0.80f, 0.84f, 0.88f, 1f));
        Place(downedMate.rectTransform, DownedWidth, DownedHeight, 740f, 703f, 580f, 40f);

        // 아래 안내 문구. 원본 이미지의 문장은 협동 구조를 전제로 해서 지금 규칙과 다르다.
        // 이미지에서 그 줄을 지워 두었으므로 여기에 실제 규칙을 적는다.
        downedNotice = CreateText(downedRoot, "Notice", 18, TextAlignmentOptions.Center, new Color(0.62f, 0.64f, 0.67f, 1f));
        Place(downedNotice.rectTransform, DownedWidth, DownedHeight, 460f, 922f, 1000f, 34f);

        // ESC 를 길게 눌러야 나간다는 안내(나가기 버튼 바로 아래). 누르는 동안 남은 시간을 보여 준다.
        leaveHint = CreateText(downedRoot, "LeaveHint", 16, TextAlignmentOptions.Center, new Color(0.86f, 0.36f, 0.30f, 1f));
        Place(leaveHint.rectTransform, DownedWidth, DownedHeight, 974f, 888f, 299f, 30f);
        leaveHint.text = string.Empty;

        spectateButton = CreateHotspot(downedRoot, "관전", DownedWidth, DownedHeight, 648f, 807f, 298f, 78f, () => Spectate?.Invoke());
        // 클릭 한 번으로 바로 나가지 않는다. 버튼도 ESC 처럼 1.5초 길게 눌러야 나간다(UpdateLeaveHold 가 처리).
        downedLeavePress = CreateHotspot(downedRoot, "매치 나가기", DownedWidth, DownedHeight, 974f, 807f, 299f, 78f, () => { })
            .gameObject.AddComponent<AetherPressHold>();
    }

    private void BuildEliminated(Transform parent)
    {
        elimRoot = CreateDesignRoot(parent, "Eliminated", EliminatedImage, ElimWidth, ElimHeight);

        // 네 칸의 값. 제목(y=620)과 밑줄(y=697) 사이가 비어 있다.
        float[] columnCentres = { 562f, 853f, 1147f, 1437f };
        elimValues = new TextMeshProUGUI[columnCentres.Length];

        for (int i = 0; i < columnCentres.Length; i++)
        {
            elimValues[i] = CreateText(elimRoot, "Value" + i, 34, TextAlignmentOptions.Center, new Color(0.94f, 0.96f, 0.98f, 1f));
            Place(elimValues[i].rectTransform, ElimWidth, ElimHeight, columnCentres[i] - 140f, 640f, 280f, 44f);
        }

        // 값 줄(y 640~684)과 버튼(y 840) 사이의 빈 띠에 에테르 레벨 한 줄.
        elimAetherLine = CreateText(elimRoot, "AetherLine", 22, TextAlignmentOptions.Center, new Color(0.31f, 0.77f, 0.91f, 0.95f));
        Place(elimAetherLine.rectTransform, ElimWidth, ElimHeight, 700f, 762f, 600f, 40f);

        CreateHotspot(elimRoot, "다시 시도", ElimWidth, ElimHeight, 672f, 840f, 315f, 77f, () => Retry?.Invoke());
        elimLeavePress = CreateHotspot(elimRoot, "로비로", ElimWidth, ElimHeight, 1013f, 840f, 314f, 77f, () => { })
            .gameObject.AddComponent<AetherPressHold>();

        elimLeaveHint = CreateText(elimRoot, "LeaveHint", 16, TextAlignmentOptions.Center, new Color(0.86f, 0.36f, 0.30f, 1f));
        Place(elimLeaveHint.rectTransform, ElimWidth, ElimHeight, 1013f, 920f, 314f, 30f);
        elimLeaveHint.text = string.Empty;
    }

    /// <summary>디자인 이미지를 통째로 깔고, 그 위에 값을 얹을 틀을 만든다.</summary>
    private RectTransform CreateDesignRoot(Transform parent, string name, string imageResource, float width, float height)
    {
        RectTransform rect = CreateRect(parent, name);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);

        Image background = rect.gameObject.AddComponent<Image>();
        background.sprite = LoadSprite(imageResource);
        background.color = background.sprite != null ? Color.white : new Color(0.03f, 0.03f, 0.04f, 0.96f);
        background.raycastTarget = true;   // 뒤쪽 조작을 막는다

        // 화면 비율이 16:9 가 아니어도 디자인이 화면을 덮게 한다.
        // (글자 자리가 그림에 묶여 있으므로 그림과 같이 움직여야 한다.)
        rect.gameObject.AddComponent<AetherDesignCoverScaler>().Configure(width, height);

        return rect;
    }

    /// <summary>
    /// 이미지를 스프라이트로 읽는다.
    /// 텍스처로만 들어와 있어도 쓸 수 있게 직접 스프라이트를 만들어 둔다.
    /// </summary>
    private static Sprite LoadSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[사망화면] Resources/{resourcePath} 를 찾지 못했습니다.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>디자인 이미지의 픽셀 좌표(왼쪽 위 기준)를 캔버스 자리로 옮긴다.</summary>
    private static void Place(RectTransform rect, float designWidth, float designHeight, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(
            left + width * 0.5f - designWidth * 0.5f,
            designHeight * 0.5f - (top + height * 0.5f));
    }

    /// <summary>그림에 이미 그려진 버튼 위에 투명한 누름 영역을 올린다.</summary>
    private Button CreateHotspot(Transform parent, string name, float designWidth, float designHeight,
        float left, float top, float width, float height, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreateRect(parent, name);
        Place(rect, designWidth, designHeight, left, top, width, height);

        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);   // 보이지 않지만 눌린다

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.86f, 0.45f, 1f);
        colors.pressedColor = new Color(1f, 0.72f, 0.25f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.AddListener(action);
        return button;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent, false);
        return host.GetComponent<RectTransform>();
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(parent, name);

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;

        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            return;

        GameObject host = new GameObject("AETHER EventSystem");
        host.transform.SetParent(transform, false);
        host.AddComponent<UnityEngine.EventSystems.EventSystem>();
        host.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }
}

/// <summary>
/// 디자인 이미지가 화면을 꽉 덮도록 크기를 맞춘다.
///
/// 글자 자리를 그림의 픽셀 좌표로 잡아 두었기 때문에, 그림만 늘리면 글자가 어긋난다.
/// 틀 전체를 같은 비율로 키워서 글자가 그림을 따라가게 한다.
/// </summary>
public class AetherDesignCoverScaler : MonoBehaviour
{
    private RectTransform self;
    private RectTransform parentRect;
    private float designWidth = 1920f;
    private float designHeight = 1080f;

    public void Configure(float width, float height)
    {
        designWidth = Mathf.Max(1f, width);
        designHeight = Mathf.Max(1f, height);
    }

    private void Awake()
    {
        self = GetComponent<RectTransform>();
        parentRect = transform.parent as RectTransform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (self == null || parentRect == null)
            return;

        Rect area = parentRect.rect;
        if (area.width < 1f || area.height < 1f)
            return;

        float scale = Mathf.Max(area.width / designWidth, area.height / designHeight);
        self.localScale = new Vector3(scale, scale, 1f);
    }
}


/// <summary>버튼을 누르고 있는 동안 true. 손을 떼거나 버튼 밖으로 나가면 false.</summary>
public class AetherPressHold : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
    {
        if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) IsHeld = true;
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { IsHeld = false; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { IsHeld = false; }
    private void OnDisable() { IsHeld = false; }
}
