using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 화면 왼쪽 아래의 채팅.
///
/// Enter 로 입력창을 열고, Enter 로 보내고, ESC 로 닫는다.
/// 보낸 사람 이름은 지금 조작 중인 캐릭터(한결·윤서)를 찾아서 붙인다. 둘 중 누구를 골라도 같다.
///
/// 입력하는 동안에는 이동·조준·사격·상호작용·단축키를 잠시 끈다. 게임 코드 대부분이
/// 키보드를 직접 읽어서, 'i' 를 치면 인벤토리가 열리고 'r' 을 치면 장전하기 때문이다.
/// 인벤토리 화면과 달리 시간은 멈추지 않는다(교전 중에도 채팅할 수 있다).
///
/// 지금은 이 컴퓨터 안에서만 보인다. 멀티플레이 연결이 완성되면 Post 를 상대에게도 보내면 된다.
/// </summary>
public class AetherChat : MonoBehaviour
{
    private const string FontPath = "UI/Fonts/NanumGothicCoding-Bold SDF";
    private const int MaxLength = 80;
    private const int MaxHistory = 50;
    private const int LinesWhileTyping = 12;
    private const int LinesWhileIdle = 6;
    private const float VisibleSeconds = 10f;
    private const float FadeSeconds = 2f;
    private const int OpenGraceFrames = 5;

    /// <summary>입력창이 열려 있는지. 다른 코드가 키 입력을 무시할 때 본다.</summary>
    public static bool IsTyping { get; private set; }

    /// <summary>방금 ESC 로 채팅을 닫았는지. 같은 ESC 로 일시정지 메뉴가 열리지 않게 한다.</summary>
    public static bool BlocksEscape => IsTyping || Time.frameCount - lastCloseFrame <= 1;

    /// <summary>새 메시지가 올라올 때. (보낸 사람, 내용)</summary>
    public static event System.Action<string, string> MessagePosted;

    private static AetherChat instance;
    private static int lastCloseFrame = -10;

    // 입력하는 동안 꺼 둘 부품. 이름으로 찾으므로 씬에 없는 것은 그냥 지나간다.
    // 한결·윤서 양쪽 것을 모두 넣는다.
    private static readonly HashSet<string> SuspendTypes = new HashSet<string>
    {
        // 이동·시점
        "Move_Hankyul", "Move_Yunseo", "Hankyul_Move", "Yunseo_Move", "PlayerMouseLook", "AetherCameraController",
        // 전투·능력
        "Hankyul_Fire", "Hankyul_Battle", "Yunseo_Battle", "EMPThrow", "PlayerEMPThrower",
        "HangyeolPartAbilityController", "YunseoPartAbilityController",
        // 상호작용
        "PlayerInteractor", "PlayerHoldInteractor",
        "ProximityHoldPrompt", "ElevatorProximityHoldPrompt", "ProximityPuzzleOpener", "ProximitySceneTransition",
        "PartsWorkbenchInteractable",
        "Lab_DoorQTEInteractor", "ShopManager",
        // 화면 단축키(I·P·숫자)
        "QuickSlotManager", "InventoryUI", "PaperInventoryPreviewUI", "AetherInventoryScreen",
        "AetherPartsEquipScreen", "AetherPartsCraftingScreen", "PartsUIHotkeyController",
        "PartsSlot2ToggleController",
    };

    private struct Message
    {
        public string Sender;
        public string Text;
        public Color Color;
        public float Time;
    }

    private readonly List<Message> history = new List<Message>();
    private readonly List<Behaviour> suspended = new List<Behaviour>();

    private TMP_FontAsset font;
    private RectTransform logPanel;
    private Image logBackground;
    private TextMeshProUGUI logText;
    private GameObject inputRoot;
    private TMP_InputField input;
    private int openedFrame;
    private bool logDirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableScreens)
            return;

        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_Chat");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherChat>();
        instance.Build();
        SceneManager.sceneLoaded += (_, __) => instance?.CloseInput(false);
    }

    /// <summary>채팅창에 한 줄 올린다. sender 가 비어 있으면 시스템 안내로 표시한다.</summary>
    public static void Post(string sender, string text)
    {
        if (instance == null || string.IsNullOrWhiteSpace(text))
            return;

        instance.Add(sender, text.Trim());
    }

    // ------------------------------------------------------------ 화면

    private void Build()
    {
        font = Resources.Load<TMP_FontAsset>(FontPath);

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        gameObject.AddComponent<GraphicRaycaster>();

        // 기록: 왼쪽 아래, HUD 퀵슬롯 위.
        logPanel = NewRect("Log", transform, new Vector2(24f, 300f), new Vector2(620f, 300f));
        logBackground = logPanel.gameObject.AddComponent<Image>();
        logBackground.color = new Color(0.02f, 0.05f, 0.08f, 0.55f);
        logBackground.raycastTarget = false;

        logText = NewText("Text", logPanel, 22f, TextAlignmentOptions.BottomLeft);
        RectTransform textRect = logText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);
        logText.overflowMode = TextOverflowModes.Truncate;
        logText.richText = true;

        // 입력창: 기록 바로 아래.
        RectTransform inputRect = NewRect("Input", transform, new Vector2(24f, 248f), new Vector2(620f, 44f));
        inputRoot = inputRect.gameObject;

        Image inputBackground = inputRoot.AddComponent<Image>();
        inputBackground.color = new Color(0.03f, 0.08f, 0.12f, 0.9f);

        RectTransform viewport = NewRect("Viewport", inputRect, Vector2.zero, Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14f, 4f);
        viewport.offsetMax = new Vector2(-14f, -4f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI typed = NewText("Text", viewport, 22f, TextAlignmentOptions.MidlineLeft);
        Stretch(typed.rectTransform);
        typed.richText = false;

        TextMeshProUGUI placeholder = NewText("Placeholder", viewport, 22f, TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform);
        placeholder.text = "메시지 입력 — Enter 보내기 · ESC 닫기";
        placeholder.color = new Color(0.62f, 0.72f, 0.80f, 0.7f);

        input = inputRoot.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = typed;
        input.placeholder = placeholder;
        input.characterLimit = MaxLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.fontAsset = font;
        input.caretColor = new Color(0.45f, 0.83f, 1f, 1f);
        input.customCaretColor = true;
        input.selectionColor = new Color(0.45f, 0.83f, 1f, 0.35f);
        input.restoreOriginalTextOnEscape = false;
        input.onSubmit.AddListener(Submit);

        inputRoot.SetActive(false);
        RefreshLog();
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 bottomLeft, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = bottomLeft;
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI NewText(string name, RectTransform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        // 기본 글꼴(LiberationSans)에는 한글이 없어 □ 로 나온다. 반드시 나눔고딕을 쓴다.
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.92f, 0.96f, 1f, 1f);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------ 입력

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (IsTyping)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseInput(true);
                return;
            }

            // 마우스 클릭이나 창 전환으로 입력창이 풀려도 닫지 않고 다시 잡는다.
            // 닫는 건 Enter(보내기)와 ESC 뿐이다. 그래야 조작 잠금이 엉뚱하게 풀리지 않는다.
            // (여는 프레임은 아직 포커스 전이다.)
            if (Time.frameCount > openedFrame + 2 && !input.isFocused)
                input.ActivateInputField();

            // 입력 중에 쓰러지면 닫는다. 쓰러짐 처리가 조작을 잠그므로 되살리지 않는다.
            if (LocalPlayerDead())
                CloseInput(true);
        }
        else if (keyboard != null &&
                 (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                 CanOpen())
        {
            OpenInput();
        }

        UpdateLogVisibility();
    }

    private static bool CanOpen()
    {
        // 일시정지 메뉴는 Enter 로 항목을 고르고, 인벤토리 등은 자기 화면이 입력을 가진다.
        if (AetherCinematics.IsPlaying || AetherPauseMenu.IsPaused || AetherUIState.AnyScreenOpen || Time.timeScale <= 0f)
            return false;

        // 보내기 Enter 가 같은 프레임에 입력창을 다시 열지 않게 한다.
        if (Time.frameCount - lastCloseFrame <= 1)
            return false;

        return FindLocalPlayer(out _, out _) && !LocalPlayerDead();
    }

    private void OpenInput()
    {
        EnsureEventSystem();

        IsTyping = true;
        AetherAudio.Play2D("chat_open", 0.4f, 1f, 0f);
        openedFrame = Time.frameCount;
        SuspendGameplay();

        inputRoot.SetActive(true);
        input.text = string.Empty;
        Input.imeCompositionMode = IMECompositionMode.On;   // 한글 입력
        EventSystem.current?.SetSelectedGameObject(inputRoot);
        input.ActivateInputField();
        logDirty = true;
    }

    private void CloseInput(bool fromPlayer)
    {
        if (!IsTyping)
            return;

        IsTyping = false;
        if (fromPlayer)
            lastCloseFrame = Time.frameCount;

        input.DeactivateInputField();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == inputRoot)
            EventSystem.current.SetSelectedGameObject(null);

        inputRoot.SetActive(false);
        Input.imeCompositionMode = IMECompositionMode.Auto;
        RestoreGameplay();
        logDirty = true;
    }

    private void Submit(string text)
    {
        if (!IsTyping)
            return;

        // 입력창을 연 그 Enter 를 UI 모듈이 '확인'으로 한 번 더 받아 곧바로 닫히는 일이 있었다.
        // 연 직후 몇 프레임 동안 들어온 보내기는 무시하고 입력창을 다시 잡는다.
        if (Time.frameCount <= openedFrame + OpenGraceFrames)
        {
            input.ActivateInputField();
            return;
        }

        string trimmed = text != null ? text.Trim() : string.Empty;
        if (trimmed.Length > 0)
        {
            FindLocalPlayer(out _, out string sender);
            Add(sender, trimmed);
        }

        CloseInput(true);
    }

    // ------------------------------------------------------------ 기록

    private void Add(string sender, string text)
    {
        if (text.Length > MaxLength)
            text = text.Substring(0, MaxLength);

        history.Add(new Message
        {
            Sender = sender,
            Text = text,
            Color = ColorFor(sender),
            Time = Time.unscaledTime,
        });

        if (history.Count > MaxHistory)
            history.RemoveAt(0);

        logDirty = true;
        MessagePosted?.Invoke(sender, text);
    }

    private static Color ColorFor(string sender)
    {
        if (sender == AetherCharacterChoice.HankyulDisplayName)
            return new Color(0.45f, 0.83f, 1f);

        if (sender == AetherCharacterChoice.YunseoDisplayName)
            return new Color(1f, 0.62f, 0.78f);

        return new Color(1f, 0.85f, 0.45f);
    }

    private void UpdateLogVisibility()
    {
        // 입력하지 않을 때는 최근 메시지만 잠깐 보였다가 사라진다.
        float alpha = 1f;
        if (!IsTyping)
        {
            float newest = history.Count > 0 ? history[history.Count - 1].Time : float.NegativeInfinity;
            float age = Time.unscaledTime - newest;
            alpha = age < VisibleSeconds ? 1f : Mathf.Clamp01(1f - (age - VisibleSeconds) / FadeSeconds);

            // 오래된 줄이 사라지는 순간에 맞춰 다시 그린다.
            if (alpha > 0f && Time.frameCount % 15 == 0)
                logDirty = true;
        }

        logPanel.gameObject.SetActive(alpha > 0f && history.Count > 0 || IsTyping);
        logText.alpha = alpha;
        logBackground.color = new Color(0.02f, 0.05f, 0.08f, (IsTyping ? 0.6f : 0.35f) * alpha);

        if (logDirty)
            RefreshLog();
    }

    private void RefreshLog()
    {
        logDirty = false;
        if (logText == null)
            return;

        int limit = IsTyping ? LinesWhileTyping : LinesWhileIdle;
        StringBuilder builder = new StringBuilder();
        int start = Mathf.Max(0, history.Count - limit);

        for (int i = start; i < history.Count; i++)
        {
            Message message = history[i];
            if (!IsTyping && Time.unscaledTime - message.Time > VisibleSeconds + FadeSeconds)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            string safe = message.Text.Replace("</noparse>", "</ noparse>");
            if (string.IsNullOrEmpty(message.Sender))
            {
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(message.Color)).Append("><noparse>")
                       .Append(safe).Append("</noparse></color>");
            }
            else
            {
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(message.Color)).Append('>')
                       .Append(message.Sender).Append("</color>  <noparse>").Append(safe).Append("</noparse>");
            }
        }

        logText.text = builder.ToString();
    }

    // ------------------------------------------------------------ 플레이어

    /// <summary>지금 조작 중인 캐릭터와 화면에 보일 이름(Shiri·Meka). 어느 캐릭터든 찾는다.</summary>
    private static bool FindLocalPlayer(out Transform root, out string name)
    {
        root = null;
        name = string.Empty;

        GameObject active = AetherPlayerCharacter.FindActive();
        if (active == null)
            return false;

        root = active.transform;

        AetherCharacter? who = AetherPlayerCharacter.Identify(active);
        name = who.HasValue ? AetherCharacterChoice.DisplayName(who.Value) : "나";
        return true;
    }

    private static bool LocalPlayerDead()
    {
        if (!FindLocalPlayer(out Transform root, out _))
            return false;

        PlayerStatus status = root.GetComponentInChildren<PlayerStatus>();
        return status != null && status.IsDead;
    }

    private void SuspendGameplay()
    {
        suspended.Clear();

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour == this || !behaviour.enabled)
                continue;

            if (!SuspendTypes.Contains(behaviour.GetType().Name))
                continue;

            behaviour.enabled = false;
            suspended.Add(behaviour);
        }

        // 이동 스크립트를 꺼도 몸은 미끄러진다. 수평 속도를 지운다.
        if (FindLocalPlayer(out Transform root, out _))
        {
            Rigidbody body = root.GetComponentInChildren<Rigidbody>();
            if (body != null && !body.isKinematic)
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
    }

    private void RestoreGameplay()
    {
        // 입력 중에 쓰러졌으면 쓰러짐 처리(AetherPlayerDeath)가 조작을 잠갔다가 되살릴 때 푼다.
        // 여기서 다시 켜면 쓰러진 채로 움직이게 된다.
        bool dead = LocalPlayerDead();

        for (int i = 0; i < suspended.Count; i++)
        {
            Behaviour behaviour = suspended[i];
            if (behaviour == null)
                continue;

            if (dead && IsMovementOrCombat(behaviour))
                continue;

            behaviour.enabled = true;
        }

        suspended.Clear();
    }

    private static bool IsMovementOrCombat(Behaviour behaviour)
    {
        switch (behaviour.GetType().Name)
        {
            case "Move_Hankyul":
            case "Move_Yunseo":
            case "Hankyul_Fire":
            case "Hankyul_Battle":
            case "Yunseo_Battle":
            case "AetherCameraController":
            case "PlayerMouseLook":
                return true;
            default:
                return false;
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>();
    }
}
