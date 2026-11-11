using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 멀티플레이 로비(LobbyScene)를 디자인 이미지대로 다시 꾸민다.
///
/// 씬에 있던 유니티 기본 UI를 지우지 않고 <b>자리와 그림만 바꾼다.</b>
/// 버튼의 onClick(CreateRoom / JoinRoom / StartGame)과 MultiplayerManager의 참조가
/// 씬에 저장돼 있어서, 새로 만들면 그 연결을 전부 다시 이어야 하기 때문이다.
///
/// 좌표는 디자인 원본(3840 x 2160) 기준 그대로 쓴다. 받은 에셋의 README 값이다.
///   제목        1040, 300    1760 x 264
///   구분선      1040, 624    1760 x 2
///   입력칸      1040, 1120   1760 x 176
///   CREATE      1040, 1412    552 x 152
///   JOIN        1644, 1412    552 x 152
///   START       2248, 1412    552 x 152
///   상태 메시지 1040, 1692   1760 x 224
/// </summary>
public class AetherLobbyScreen : MonoBehaviour
{
    private const string ScenePrefix = "LobbyScene";

    private const string PlateResource = "UI/LobbyPanel";
    private const string ButtonResourceRoot = "UI/LobbyButtons/btn_";

    // 디자인 원본 크기. 이 좌표계로 적어 두고 화면 크기에 맞춰 환산한다.
    private const float DesignWidth = 3840f;
    private const float DesignHeight = 2160f;

    // README의 색상
    private static readonly Color Amber = new Color32(0xF0, 0xA8, 0x30, 0xFF);
    private static readonly Color LabelNormal = new Color32(0xD9, 0xD7, 0xD1, 0xFF);

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

        if (!SceneManager.GetActiveScene().name.StartsWith(ScenePrefix, System.StringComparison.Ordinal))
            return;

        builtThisScene = true;

        GameObject host = new GameObject("AetherLobbyScreen");
        host.AddComponent<AetherLobbyScreen>();
    }

    private void Start()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.Log("[로비] 캔버스를 찾지 못해 꾸미기를 건너뜁니다.");
            return;
        }

        NormalizeCanvas(canvas);
        AddBackground(canvas);

        PlaceInputField(canvas);
        PlaceTitle(canvas);
        PlaceStatus(canvas);

        StyleButton(canvas, "CreateRoomButton", "create", 1040f);
        StyleButton(canvas, "JoinRoomButton", "join", 1644f);
        StyleButton(canvas, "StartGameButton", "start", 2248f);

        CreateSoloButton(canvas);

        Debug.Log("[로비] 디자인 이미지로 화면을 다시 꾸몄습니다.");
        Destroy(gameObject);
    }

    /// <summary>
    /// 로비 UI가 들어 있는 캔버스를 찾는다.
    ///
    /// 화면에는 다른 캔버스(안내 토스트 등)도 떠 있을 수 있어서, 그냥 첫 번째를 집으면
    /// 엉뚱한 곳을 꾸미고 버튼을 못 찾는다. 로비 버튼을 실제로 품고 있는 캔버스를 고른다.
    /// </summary>
    private static Canvas FindCanvas()
    {
        Canvas fallback = null;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            // 이 씬에 속한 것만 본다(다른 씬에서 넘어온 DontDestroyOnLoad 캔버스 제외).
            if (canvas.gameObject.scene != SceneManager.GetActiveScene())
                continue;

            if (FindDeep(canvas.transform, "CreateRoomButton") != null)
                return canvas;

            if (fallback == null)
                fallback = canvas;
        }

        return fallback;
    }

    /// <summary>
    /// 로비 캔버스만 기준 해상도가 800x600이라 다른 화면과 크기가 따로 놀았다.
    /// 스타트씬과 같은 1920x1080 기준으로 맞춘다.
    /// </summary>
    private static void NormalizeCanvas(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>배경 이미지를 화면에 꽉 차게 깔고 맨 뒤로 보낸다.</summary>
    private static void AddBackground(Canvas canvas)
    {
        if (canvas.transform.Find("Lobby Background") != null)
            return;

        Sprite plate = Resources.Load<Sprite>(PlateResource);
        if (plate == null)
        {
            Debug.Log($"[로비] 배경 이미지를 찾지 못했습니다: Resources/{PlateResource}");
            return;
        }

        GameObject background = new GameObject("Lobby Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvas.transform, false);
        background.transform.SetAsFirstSibling();

        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = background.GetComponent<Image>();
        image.sprite = plate;
        image.raycastTarget = false;

        // 화면 비율이 달라도 그림이 찌그러지지 않게 덮는다.
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
    }

    private void StyleButton(Canvas canvas, string objectName, string label, float designX)
    {
        Transform found = FindDeep(canvas.transform, objectName);
        if (found == null)
        {
            Debug.Log($"[로비] '{objectName}' 을(를) 찾지 못했습니다.");
            return;
        }

        PlaceByDesignRect(found as RectTransform, designX, 1412f, 552f, 152f);

        Image image = found.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = LoadButton(label, "normal");
            image.type = Image.Type.Simple;
            image.color = Color.white;

            // 버튼 그림에 글로우 여백이 들어 있어, 본체보다 조금 넓게 그려야 잘리지 않는다.
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(
                rect.sizeDelta.x * (592f / 552f),
                rect.sizeDelta.y * (192f / 152f));
        }

        Button button = found.GetComponent<Button>();
        if (button != null)
        {
            // 눌림·올림 상태도 받은 그림으로 바꾼다.
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState
            {
                highlightedSprite = LoadButton(label, "hover"),
                pressedSprite = LoadButton(label, "pressed"),
                selectedSprite = LoadButton(label, "hover"),
                disabledSprite = LoadButton(label, "disabled"),
            };

            button.spriteState = state;
        }

        // 그림에 글자가 들어 있으므로 기본 라벨은 치운다.
        foreach (TMP_Text text in found.GetComponentsInChildren<TMP_Text>(true))
            text.enabled = false;

        foreach (Text text in found.GetComponentsInChildren<Text>(true))
            text.enabled = false;
    }

    /// <summary>
    /// [혼자 하기] 버튼. 제목 구분선과 코드 입력칸 사이의 빈 자리에, 디자인의 버튼과 같은 색(어두운 판·회색 테두리·
    /// 왼쪽 위 주황 모서리)으로 만든다. 받은 버튼 그림은 글자가 박혀 있어(START 등) 새로 그린다.
    /// 아래쪽 방 만들기·참가는 친구와 같이 할 때만 쓴다.
    /// </summary>
    private static void CreateSoloButton(Canvas canvas)
    {
        if (FindDeep(canvas.transform, "SoloPlayButton") != null)
            return;

        MultiplayerManager manager = FindFirstObjectByType<MultiplayerManager>();
        if (manager == null)
            return;

        TMP_FontAsset korean = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");

        // 저장이 있으면 [이어하기] | [새로 시작] 두 칸, 없으면 [혼자 하기] 한 칸.
        string saved = AetherSave.Describe();
        if (saved != null)
        {
            MakeModeButton(canvas, "ContinueButton", 1040f, 860f, "CONTINUE", saved, manager.ContinueSolo, korean, 34f);
            MakeModeButton(canvas, "SoloPlayButton", 1940f, 860f, "NEW GAME", "새로 시작 · 혼자 하기", manager.StartSolo, korean, 36f);
        }
        else
        {
            MakeModeButton(canvas, "SoloPlayButton", 1040f, 1760f, "SOLO PLAY", "혼자 하기  ·  인터넷 연결 없이 바로 시작", manager.StartSolo, korean, 40f);
        }

        AddMultiplayerCaption(canvas, korean);
    }

    private static void MakeModeButton(Canvas canvas, string name, float designX, float designWidth, string titleText, string subText,
        UnityEngine.Events.UnityAction action, TMP_FontAsset korean, float subSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        PlaceByDesignRect(rect, designX, 760f, designWidth, 200f);

        Image plate = go.GetComponent<Image>();
        plate.color = new Color(0.13f, 0.14f, 0.15f, 0.97f);
        go.AddComponent<Outline>().effectColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);

        // 왼쪽 위 주황 모서리(받은 버튼 그림과 같은 장식)
        AddCorner(rect, new Vector2(0f, 1f), new Vector2(14f, 2f));
        AddCorner(rect, new Vector2(0f, 1f), new Vector2(2f, 14f));

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.3f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(action);
        go.AddComponent<AetherUiButtonSound>();

        TMP_Text title = AddLabel(rect, "Label", titleText, Font(76f), LabelNormal, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
        title.characterSpacing = 24f;
        title.fontStyle = FontStyles.Bold;

        TMP_Text sub = AddLabel(rect, "SubLabel", subText, Font(subSize), Amber, new Vector2(0.03f, 0f), new Vector2(0.97f, 0.46f));
        if (korean != null) sub.font = korean;
        sub.textWrappingMode = TextWrappingModes.NoWrap;   // 한 줄로, 넘치면 글자를 줄인다
        sub.enableAutoSizing = true;
        sub.fontSizeMax = Font(subSize);
        sub.fontSizeMin = Font(24f);
    }

    private static void AddMultiplayerCaption(Canvas canvas, TMP_FontAsset korean)
    {
        // 아래 방 만들기 줄이 친구와 하는 것임을 알 수 있게 작은 제목을 단다.
        Transform input = FindDeep(canvas.transform, "JoinCodeInputField");
        if (input != null && FindDeep(canvas.transform, "MultiplayerCaption") == null)
        {
            GameObject cap = new GameObject("MultiplayerCaption", typeof(RectTransform));
            cap.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI capText = cap.AddComponent<TextMeshProUGUI>();
            PlaceByDesignRect(capText.rectTransform, 1040f, 1030f, 1760f, 70f);
            capText.text = "친구와 함께 (온라인 2인)";
            if (korean != null) capText.font = korean;
            capText.fontSize = Font(40f);
            capText.color = new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.7f);
            capText.alignment = TextAlignmentOptions.Left;
            capText.raycastTarget = false;
        }
    }

    private static void AddCorner(RectTransform parent, Vector2 anchor, Vector2 size)
    {
        GameObject c = new GameObject("Corner", typeof(RectTransform), typeof(Image));
        c.transform.SetParent(parent, false);
        RectTransform r = c.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
        Image i = c.GetComponent<Image>();
        i.color = Amber;
        i.raycastTarget = false;
    }

    private static TMP_Text AddLabel(RectTransform parent, string name, string text, float size, Color color, Vector2 min, Vector2 max)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        RectTransform r = t.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static Sprite LoadButton(string label, string state)
    {
        return Resources.Load<Sprite>(ButtonResourceRoot + label + "_" + state);
    }

    private void PlaceInputField(Canvas canvas)
    {
        Transform found = FindDeep(canvas.transform, "JoinCodeInputField");
        if (found == null)
            return;

        PlaceByDesignRect(found as RectTransform, 1040f, 1120f, 1760f, 176f);

        // 배경에 이미 칸 테두리가 그려져 있으니 기본 흰 상자는 지운다.
        Image image = found.GetComponent<Image>();
        if (image != null)
            image.enabled = false;

        TMP_InputField input = found.GetComponent<TMP_InputField>();
        if (input == null)
            return;

        if (input.textComponent != null)
        {
            input.textComponent.color = LabelNormal;
            input.textComponent.fontSize = Font(52f);
            input.textComponent.alignment = TextAlignmentOptions.Center;
        }

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "ENTER JOIN CODE";
            placeholder.color = new Color(LabelNormal.r, LabelNormal.g, LabelNormal.b, 0.35f);
            placeholder.fontSize = Font(52f);
            placeholder.alignment = TextAlignmentOptions.Center;
        }

        input.characterLimit = 12;
    }

    /// <summary>
    /// 제목을 올린다.
    ///
    /// 씬의 "TitleText"는 글자 컴포넌트가 없는 빈 오브젝트였다(안에 Canvas만 들어 있었다).
    /// 그래서 자리만 옮겨 놓으면 아무것도 보이지 않는다. 글자는 새로 만들어 얹는다.
    /// </summary>
    private void PlaceTitle(Canvas canvas)
    {
        // 쓰이지 않는 빈 오브젝트는 화면 밖으로 치워 둔다(지우면 참조가 깨질 수 있다).
        Transform legacy = FindDeep(canvas.transform, "TitleText");
        if (legacy != null)
            legacy.gameObject.SetActive(false);

        const string titleName = "Lobby Title";
        Transform existing = FindDeep(canvas.transform, titleName);

        TMP_Text title;
        if (existing != null)
        {
            title = existing.GetComponent<TMP_Text>();
        }
        else
        {
            GameObject titleObject = new GameObject(titleName, typeof(RectTransform));
            titleObject.transform.SetParent(canvas.transform, false);
            titleObject.layer = canvas.gameObject.layer;
            title = titleObject.AddComponent<TextMeshProUGUI>();
        }

        if (title == null)
            return;

        PlaceByDesignRect(title.rectTransform, 1040f, 300f, 1760f, 264f);

        // 혼자 하기가 생겨 "MULTIPLAYER" 는 맞지 않는다.
        title.text = "GAME MODE";
        title.fontSize = Font(132f);
        title.color = Amber;
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = 20f;
        title.raycastTarget = false;
    }

    private void PlaceStatus(Canvas canvas)
    {
        Transform status = FindDeep(canvas.transform, "StatusText");
        if (status != null)
        {
            PlaceByDesignRect(status as RectTransform, 1040f, 1692f, 1760f, 224f);

            TMP_Text text = status.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.fontSize = Font(44f);
                text.color = LabelNormal;
                // 위에 붙여 두었더니 아래 칸(배경 상자) 안에서 글이 너무 위로 올라가 보였다. 칸 가운데에 둔다.
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        // 발급된 방 코드는 입력칸 바로 아래에 둔다.
        Transform code = FindDeep(canvas.transform, "JoinCodeText");
        if (code == null)
            return;

        PlaceByDesignRect(code as RectTransform, 1040f, 1320f, 1760f, 72f);

        TMP_Text codeText = code.GetComponent<TMP_Text>();
        if (codeText != null)
        {
            codeText.fontSize = Font(48f);
            codeText.color = Amber;
            codeText.alignment = TextAlignmentOptions.Center;
            codeText.characterSpacing = 8f;

            // 방을 만들기 전까지는 비워 둔다. 기본값 "New Text"가 그대로 보이고 있었다.
            codeText.text = string.Empty;
        }
    }

    /// <summary>
    /// 디자인 원본(3840x2160) 좌상단 기준 좌표를 캔버스(1920x1080) 좌표로 옮긴다.
    /// 캔버스는 가운데가 (0,0)이고 위가 +y라, 좌상단 기준 값을 그대로 쓰면 뒤집힌다.
    /// </summary>
    private static void PlaceByDesignRect(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null)
            return;

        const float scale = 1920f / DesignWidth;   // 0.5

        float w = width * scale;
        float h = height * scale;

        float centerX = (x + width * 0.5f) * scale - 1920f * 0.5f;
        float centerY = 1080f * 0.5f - (y + height * 0.5f) * scale;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    /// <summary>디자인 원본 기준 글자 크기를 캔버스 기준으로 옮긴다.</summary>
    private static float Font(float designSize)
    {
        return designSize * (1920f / DesignWidth);
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
