using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC로 열리는 일시정지 메뉴.
///
/// 디자인은 전부 이미지 한 장(Resources/UI/PauseMenuPanel)이 담당한다.
/// 이 스크립트는 그 이미지를 화면 가운데 띄우고, 그림 속 버튼 위치에 <b>투명한 클릭 영역</b>만
/// 얹어서 눌리게 만든다. 글자나 테두리를 코드로 그리지 않는다.
///
/// 클릭 영역이 그림과 어긋나면 ShowHitAreas를 true로 바꿔 빨갛게 보이게 한 뒤
/// ButtonRects의 숫자만 조정하면 된다.
/// </summary>
public class AetherPauseMenu : MonoBehaviour
{
    private const string PanelSpritePath = "UI/PauseMenuPanel";
    private const string SettingsSpritePath = "UI/SettingsPanel";
    private const string MainMenuSceneName = "01.스타트씬";

    /// <summary>true로 두면 클릭 영역이 빨갛게 보인다. 위치를 맞출 때만 켜자.</summary>
    private const bool ShowHitAreas = false;

    /// <summary>패널 이미지 높이가 화면 높이에서 차지하는 비율.</summary>
    private const float PanelHeightRatio = 0.86f;

    /// <summary>
    /// 이미지 안에서 각 버튼이 차지하는 영역 (왼쪽 아래가 0,0 / 오른쪽 위가 1,1).
    /// 순서: 게임 계속하기 → 설정 → 메인 메뉴로 이동 → 게임 종료
    /// </summary>
    // 원본 이미지 1152 x 1366 기준으로 잰 값.
    private static readonly Rect[] ButtonRects =
    {
        Rect.MinMaxRect(0.1736f, 0.5856f, 0.8403f, 0.6896f), // 게임 계속하기
        Rect.MinMaxRect(0.1736f, 0.4678f, 0.8403f, 0.5637f), // 설정
        Rect.MinMaxRect(0.1736f, 0.3485f, 0.8403f, 0.4436f), // 메인 메뉴로 이동
        Rect.MinMaxRect(0.1736f, 0.2291f, 0.8403f, 0.3236f), // 게임 종료
    };

    // 일시정지를 띄우지 않을 씬 (메뉴·로딩 화면)
    private static readonly string[] IgnoredScenePrefixes = { "01.", "02.", "03.", "MainMenu", "Loading", "Tutorial0", "Cine_" };

    private static AetherPauseMenu instance;
    private static bool subscribed;

    public static bool IsPaused { get; private set; }

    private Canvas canvas;
    private RawImage blurImage;
    private GameObject panelRoot;
    private GameObject settingsRoot;
    private Texture2D capturedScreen;
    private RenderTexture blurTexture;

    // 클릭 영역이 그림과 조금 어긋나도 메뉴를 쓸 수 있도록 키보드 조작도 지원한다.
    private readonly List<Image> menuHighlights = new List<Image>();
    private readonly List<UnityEngine.Events.UnityAction> menuActions = new List<UnityEngine.Events.UnityAction>();
    private int selectedIndex;

    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;
    private float previousTimeScale = 1f;
    private readonly List<Behaviour> suspended = new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableScreens)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬을 넘어가면 항상 일시정지를 풀어 둔다(정지된 채로 다음 씬에 들어가는 사고 방지).
        if (instance != null && IsPaused)
            instance.Resume();

        // 메뉴 화면(시작·로비·캐릭터 선택)은 마우스로 누르는 곳이다. 게임 중 [메인으로]로 나오면
        // 일시정지 전의 잠긴 커서가 되돌려져 START 를 누를 수 없었다.
        if (scene.name.StartsWith("01.") || scene.name.StartsWith("02.") || scene.name == "LobbyScene")
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Ensure();
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherPauseMenu");
        instance = host.AddComponent<AetherPauseMenu>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        ReleaseBlur();
    }

    private static bool IsIgnoredScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        for (int i = 0; i < IgnoredScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(IgnoredScenePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        bool settingsOpen = IsPaused && settingsRoot != null && settingsRoot.activeSelf;

        if (IsPaused && !settingsOpen)
            HandleKeyboardNavigation(keyboard);

        // 설정 화면의 F(적용)
        if (settingsOpen)
        {
            bool applyPressed = keyboard != null
                ? keyboard.fKey.wasPressedThisFrame
                : Input.GetKeyDown(KeyCode.F);

            if (applyPressed)
            {
                AetherSettings.Save();
                ShowSettings(false);
                return;
            }
        }

        bool pressed = keyboard != null
            ? keyboard.escapeKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Escape);

        if (!pressed)
            return;

        if (IsPaused)
        {
            if (settingsRoot != null && settingsRoot.activeSelf)
            {
                ShowSettings(false);
                return;
            }

            Resume();
            return;
        }

        if (IsIgnoredScene(SceneManager.GetActiveScene().name))
            return;

        // 인벤토리·파츠 화면이 열려 있거나 방금 그 ESC로 닫혔다면 일시정지를 열지 않는다.
        if (AetherUIState.AnyScreenOpen || AetherUIState.ConsumedCloseThisFrame)
            return;

        // 채팅 입력 중이거나 방금 그 ESC 로 채팅을 닫았다면 일시정지를 열지 않는다.
        if (AetherChat.BlocksEscape)
            return;

        // 쓰러짐·작전 실패 화면에서 ESC 는 그 화면의 '나가기'(길게 누르기)에 쓴다. 일시정지가 같이 뜨면 안 된다.
        if (AetherDeathScreens.AnyShowing)
            return;

        Pause();
    }

    /// <summary>↑↓(또는 W/S)로 항목을 옮기고 Enter/Space로 실행한다.</summary>
    private void HandleKeyboardNavigation(Keyboard keyboard)
    {
        if (menuActions.Count == 0)
            return;

        bool down, up, submit;

        if (keyboard != null)
        {
            down = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame;
            up = keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame;
            submit = keyboard.enterKey.wasPressedThisFrame
                     || keyboard.numpadEnterKey.wasPressedThisFrame
                     || keyboard.spaceKey.wasPressedThisFrame;
        }
        else
        {
            down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
        }

        if (down)
        {
            selectedIndex = (selectedIndex + 1) % menuActions.Count;
            RefreshSelectionHighlight();
        }
        else if (up)
        {
            selectedIndex = (selectedIndex - 1 + menuActions.Count) % menuActions.Count;
            RefreshSelectionHighlight();
        }

        if (submit && selectedIndex >= 0 && selectedIndex < menuActions.Count)
            menuActions[selectedIndex]?.Invoke();
    }

    private void RefreshSelectionHighlight()
    {
        for (int i = 0; i < menuHighlights.Count; i++)
        {
            if (menuHighlights[i] == null)
                continue;

            // 이미지가 이미 1번 항목을 강조하고 있으므로, 키보드 선택은 아주 옅게만 표시한다.
            float alpha = ShowHitAreas ? 0.35f : (i == selectedIndex ? 0.12f : 0f);
            Color color = menuHighlights[i].color;
            menuHighlights[i].color = new Color(color.r, color.g, color.b, alpha);
        }
    }

    // ---------------------------------------------------------------- 열기/닫기

    public void Pause()
    {
        if (IsPaused)
            return;

        IsPaused = true;
        AetherAudio.Play2D("ui_open", 0.55f, 0.9f, 0f);
        // 일시정지 중엔 세상 소리(불·드론·환경음)를 멈춘다. UI 소리는 ignoreListenerPause 라 계속 난다.
        AudioListener.pause = true;
        StartCoroutine(PauseRoutine());
    }

    private IEnumerator PauseRoutine()
    {
        // 패널이 찍히지 않도록 화면을 먼저 캡처한다.
        yield return new WaitForEndOfFrame();
        CaptureBlurredBackground();

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        previousLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AetherSettings.Apply();
        SuspendPlayerControl();

        selectedIndex = 0;
        RefreshSelectionHighlight();

        canvas.gameObject.SetActive(true);
        ShowSettings(false);
    }

    public void Resume()
    {
        if (!IsPaused)
            return;

        IsPaused = false;
        AudioListener.pause = false;
        AetherAudio.Play2D("ui_close", 0.5f, 0.9f, 0f);

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;

        RestorePlayerControl();

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        ReleaseBlur();
    }

    private void GoToMainMenu()
    {
        // 씬을 바꾸기 전에 반드시 시간을 돌려놓아야 다음 씬이 멈춘 채로 시작하지 않는다.
        // 메인 메뉴로 나가기 전에 지금까지를 저장한다(로비의 [이어하기]로 돌아올 수 있게).
        AetherSave.SaveNow(false);
        Resume();
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            SceneManager.LoadScene(MainMenuSceneName);
        else
            SceneManager.LoadScene(0);
    }

    private void QuitGame()
    {
        AetherSave.SaveNow(false);
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- 플레이어 조작 정지

    /// <summary>timeScale 0으로도 마우스 시점이나 UI 단축키는 계속 동작하므로 직접 꺼 준다.</summary>
    private void SuspendPlayerControl()
    {
        suspended.Clear();
        SuspendAll<Move_Hankyul>();
        SuspendAll<Move_Yunseo>();
        SuspendAll<PlayerMouseLook>();
        SuspendAll<Hankyul_Fire>();
        SuspendAll<Hankyul_Battle>();
        SuspendAll<Yunseo_Battle>();
        SuspendAll<PlayerInteractor>();
        SuspendAll<InventoryUI>();
        SuspendAll<PartsUIHotkeyController>();
        // 일시정지 메뉴에서 우클릭하면 실드가 켜지고 꺼지며 소리가 났다.
        SuspendAll<HangyeolPartAbilityController>();
        SuspendAll<YunseoPartAbilityController>();
        SuspendAll<QuickSlotManager>(); // 일시정지 중 숫자 키로 아이템이 쓰였다
    }

    private void SuspendAll<T>() where T : Behaviour
    {
        T[] found = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || !found[i].enabled)
                continue;

            found[i].enabled = false;
            suspended.Add(found[i]);
        }
    }

    private void RestorePlayerControl()
    {
        for (int i = 0; i < suspended.Count; i++)
        {
            if (suspended[i] != null)
                suspended[i].enabled = true;
        }

        suspended.Clear();
    }

    // ---------------------------------------------------------------- 배경 흐림

    private void CaptureBlurredBackground()
    {
        ReleaseBlur();

        try
        {
            capturedScreen = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception)
        {
            capturedScreen = null;
        }

        if (capturedScreen == null)
        {
            blurImage.texture = null;
            blurImage.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);
            return;
        }

        int width = Mathf.Max(8, Screen.width / 14);
        int height = Mathf.Max(8, Screen.height / 14);

        blurTexture = new RenderTexture(width, height, 0)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        RenderTexture half = RenderTexture.GetTemporary(width * 3, height * 3, 0);
        half.filterMode = FilterMode.Bilinear;

        Graphics.Blit(capturedScreen, half);
        Graphics.Blit(half, blurTexture);
        RenderTexture.ReleaseTemporary(half);

        blurImage.texture = blurTexture;
        blurImage.color = new Color(0.45f, 0.5f, 0.58f, 1f);
    }

    private void ReleaseBlur()
    {
        if (blurImage != null)
            blurImage.texture = null;

        if (capturedScreen != null)
        {
            Destroy(capturedScreen);
            capturedScreen = null;
        }

        if (blurTexture != null)
        {
            blurTexture.Release();
            Destroy(blurTexture);
            blurTexture = null;
        }
    }

    // ---------------------------------------------------------------- UI 구성

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // 다른 UI 위에 확실히 올라오도록

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Expand: 어떤 해상도·화면비에서도 기준 영역이 통째로 화면 안에 들어간다.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // 크기 정규화 대상에서 제외
        canvasObject.AddComponent<AetherDesignCanvasMarker>();

        GameObject blurObject = new GameObject("BlurBackground", typeof(RectTransform), typeof(RawImage));
        blurObject.transform.SetParent(canvasObject.transform, false);
        Stretch(blurObject.GetComponent<RectTransform>());
        blurImage = blurObject.GetComponent<RawImage>();
        blurImage.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

        BuildPanel(canvasObject.transform);
        BuildSettings(canvasObject.transform);

        canvasObject.SetActive(false);
    }

    /// <summary>보내준 이미지 한 장 + 그 위의 투명 클릭 영역.</summary>
    private void BuildPanel(Transform parent)
    {
        panelRoot = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(parent, false);

        RectTransform rect = panelRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image background = panelRoot.GetComponent<Image>();
        background.raycastTarget = false;

        Sprite panelSprite = Resources.Load<Sprite>(PanelSpritePath);
        if (panelSprite != null)
        {
            background.sprite = panelSprite;
            background.color = Color.white;
            background.preserveAspect = true;

            // 화면 높이에 맞추고 가로는 원본 비율 그대로.
            float height = 1080f * PanelHeightRatio;
            float width = height * panelSprite.rect.width / Mathf.Max(1f, panelSprite.rect.height);
            rect.sizeDelta = new Vector2(width, height);
        }
        else
        {
            // 이미지가 아직 없을 때만 최소한의 대체 배경을 쓴다.
            background.color = new Color(0.03f, 0.06f, 0.09f, 0.96f);
            rect.sizeDelta = new Vector2(620f, 760f);

            Debug.LogWarning("[AetherPauseMenu] Resources/UI/PauseMenuPanel 스프라이트를 찾지 못했습니다. " +
                             "이미지를 Assets/Resources/UI/PauseMenuPanel.png 로 넣고 Texture Type을 " +
                             "Sprite(2D and UI)로 설정하세요.");
        }

        // 그림 속 버튼 위치에 투명 클릭 영역만 얹는다.
        CreateHitArea(rect, "Btn_Resume", ButtonRects[0], Resume);
        CreateHitArea(rect, "Btn_Settings", ButtonRects[1], () => ShowSettings(true));
        CreateHitArea(rect, "Btn_MainMenu", ButtonRects[2], GoToMainMenu);
        CreateHitArea(rect, "Btn_Quit", ButtonRects[3], QuitGame);
    }

    /// <summary>
    /// 눈에 보이지 않는 버튼. 이미지가 디자인을 담당하므로 여기서는 클릭만 받고,
    /// 마우스를 올렸을 때만 아주 옅게 밝아지도록 한다.
    /// </summary>
    private void CreateHitArea(RectTransform parent, string name, Rect area, UnityEngine.Events.UnityAction action)
    {
        GameObject hit = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        hit.transform.SetParent(parent, false);

        RectTransform rect = hit.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(area.xMin, area.yMin);
        rect.anchorMax = new Vector2(area.xMax, area.yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = hit.GetComponent<Image>();
        image.color = ShowHitAreas
            ? new Color(1f, 0f, 0f, 0.35f)          // 위치 확인용
            : new Color(1f, 1f, 1f, 0f);            // 평소에는 완전 투명
        image.raycastTarget = true;                  // 알파 0이어도 클릭은 받는다

        Button button = hit.GetComponent<Button>();
        button.targetGraphic = image;
        // 강조 표시는 아래 별도 이미지로 직접 처리하므로 기본 색 전환은 끈다.
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);

        // 마우스 hover / 키보드 선택 공용 강조 표시
        GameObject highlight = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        highlight.transform.SetParent(hit.transform, false);
        Stretch(highlight.GetComponent<RectTransform>());

        Image highlightImage = highlight.GetComponent<Image>();
        highlightImage.color = new Color(0.55f, 0.9f, 1f, 0f);
        highlightImage.raycastTarget = false;

        AetherPauseHitArea hover = hit.AddComponent<AetherPauseHitArea>();
        hover.Setup(this, menuActions.Count);

        menuHighlights.Add(highlightImage);
        menuActions.Add(action);
    }

    /// <summary>마우스를 올렸을 때도 키보드 선택과 같은 항목이 강조되도록 맞춘다.</summary>
    public void SetSelected(int index)
    {
        if (index < 0 || index >= menuActions.Count)
            return;

        selectedIndex = index;
        RefreshSelectionHighlight();
    }

    // ---------------------------------------------------------------- 설정 화면

    // 설정 이미지(1672 x 941) 안에서 각 항목 줄의 세로 중심 위치.
    private const float RowMaster = 0.695f;
    private const float RowBgm = 0.644f;
    private const float RowSfx = 0.593f;
    private const float RowSensitivity = 0.445f;
    private const float RowAds = 0.392f;
    private const float RowInvertY = 0.337f;
    private const float RowInvertX = 0.285f;
    private const float RowVibration = 0.135f;

    private const float RowHalfHeight = 0.022f;
    private const float ControlLeft = 0.390f;   // 슬라이더/선택기 왼쪽
    private const float ControlRight = 0.632f;  // 슬라이더/선택기 오른쪽
    private const float ValueLeft = 0.645f;     // 숫자 표기 왼쪽
    private const float ValueRight = 0.712f;

    private readonly Color accent = new Color(0.55f, 0.87f, 1f, 1f);

    /// <summary>설정 화면. 배경은 이미지 한 장이고, 그 위에 실제 조작 컨트롤만 얹는다.</summary>
    private void BuildSettings(Transform parent)
    {
        AetherSettings.Load();

        settingsRoot = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsRoot.transform.SetParent(parent, false);

        RectTransform rect = settingsRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image background = settingsRoot.GetComponent<Image>();
        background.raycastTarget = false;

        Sprite sprite = Resources.Load<Sprite>(SettingsSpritePath);
        if (sprite != null)
        {
            background.sprite = sprite;
            background.color = Color.white;
            background.preserveAspect = true;

            float height = 1080f * 0.97f;
            float width = height * sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            rect.sizeDelta = new Vector2(width, height);
        }
        else
        {
            background.color = new Color(0.03f, 0.06f, 0.09f, 0.97f);
            rect.sizeDelta = new Vector2(1600f, 900f);

            Debug.LogWarning("[AetherPauseMenu] Resources/UI/SettingsPanel 스프라이트를 찾지 못했습니다.");
        }

        // 음량
        CreateSettingSlider(rect, "Master", RowMaster, AetherSettings.MasterVolume, 0f, 100f,
            value => AetherSettings.SetMasterVolume(value));
        CreateSettingSlider(rect, "Bgm", RowBgm, AetherSettings.BgmVolume, 0f, 100f,
            value => AetherSettings.SetBgmVolume(value));
        CreateSettingSlider(rect, "Sfx", RowSfx, AetherSettings.SfxVolume, 0f, 100f,
            value => AetherSettings.SetSfxVolume(value));

        // 조작
        CreateSettingSlider(rect, "Sensitivity", RowSensitivity, AetherSettings.MouseSensitivity, 1f, 100f,
            value => AetherSettings.SetMouseSensitivity(value));
        CreateSettingSlider(rect, "Ads", RowAds, AetherSettings.AdsSensitivity, 1f, 100f,
            value => AetherSettings.SetAdsSensitivity(value));

        CreateSettingToggle(rect, "InvertY", RowInvertY, AetherSettings.InvertY,
            value => AetherSettings.SetInvertY(value));
        CreateSettingToggle(rect, "InvertX", RowInvertX, AetherSettings.InvertX,
            value => AetherSettings.SetInvertX(value));

        // 게임플레이
        CreateSettingToggle(rect, "Vibration", RowVibration, AetherSettings.Vibration,
            value => AetherSettings.SetVibration(value));

        // 하단 버튼 (이미지에는 없어서 여기서 만든다)
        CreateFooterButton(rect, "적용", "F", 0.300f, 0.452f, () =>
        {
            AetherSettings.Save();
            ShowSettings(false);
        });

        CreateFooterButton(rect, "닫기", "ESC", 0.472f, 0.624f, () => ShowSettings(false));

        settingsRoot.SetActive(false);
    }

    private void ShowSettings(bool show)
    {
        if (settingsRoot != null)
            settingsRoot.SetActive(show);

        if (panelRoot != null)
            panelRoot.SetActive(!show);

        if (!show)
            AetherSettings.Save();
    }

    /// <summary>이미지의 항목 줄 위에 얹는 슬라이더 + 숫자 표기.</summary>
    private void CreateSettingSlider(RectTransform parent, string id, float rowY, float value,
        float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject sliderObject = new GameObject($"Slider_{id}", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(ControlLeft, rowY - 0.011f);
        sliderRect.anchorMax = new Vector2(ControlRight, rowY + 0.011f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        GameObject track = new GameObject("Background", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(sliderRect, false);
        Stretch(track.GetComponent<RectTransform>());
        track.GetComponent<Image>().color = new Color(0.20f, 0.27f, 0.33f, 0.95f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderRect, false);
        Stretch(fillArea.GetComponent<RectTransform>());

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Stretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = accent;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderRect, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(7f, 0f);
        handleAreaRect.offsetMax = new Vector2(-7f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 26f);
        handle.GetComponent<Image>().color = new Color(0.93f, 0.98f, 1f, 1f);

        TMP_Text valueText = CreateText(parent, $"Value_{id}", Mathf.RoundToInt(value).ToString(), 26f,
            TextAlignmentOptions.Left,
            new Vector2(ValueLeft, rowY - RowHalfHeight), new Vector2(ValueRight, rowY + RowHalfHeight),
            new Color(0.92f, 0.96f, 1f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = Mathf.RoundToInt(v).ToString();
            onChanged(v);
        });
    }

    /// <summary>◀ 값 ▶ 형태의 켜기/끄기 선택기.</summary>
    private void CreateSettingToggle(RectTransform parent, string id, float rowY, bool value,
        UnityEngine.Events.UnityAction<bool> onChanged)
    {
        GameObject frame = new GameObject($"Toggle_{id}", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);

        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(ControlLeft, rowY - RowHalfHeight);
        frameRect.anchorMax = new Vector2(ControlRight, rowY + RowHalfHeight);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = new Color(0.14f, 0.22f, 0.28f, 0.55f);
        frameImage.raycastTarget = false;

        bool current = value;

        TMP_Text label = CreateText(frameRect, "Value", current ? "켜기" : "끄기", 24f,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color(0.92f, 0.96f, 1f));

        void SetValue(bool next)
        {
            current = next;
            label.text = current ? "켜기" : "끄기";
            onChanged(current);
        }

        CreateArrowButton(frameRect, "Prev", "‹", true, () => SetValue(!current));
        CreateArrowButton(frameRect, "Next", "›", false, () => SetValue(!current));
    }

    private void CreateArrowButton(RectTransform parent, string name, string glyph, bool left,
        UnityEngine.Events.UnityAction action)
    {
        GameObject button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(parent, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(left ? 0f : 0.86f, 0f);
        rect.anchorMax = new Vector2(left ? 0.14f : 1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = button.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;

        CreateText(rect, "Glyph", glyph, 30f, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Color(0.75f, 0.88f, 0.96f));

        Button component = button.GetComponent<Button>();
        component.transition = Selectable.Transition.None;
        component.onClick.AddListener(action);
    }

    private void CreateFooterButton(RectTransform parent, string label, string key, float xMin, float xMax,
        UnityEngine.Events.UnityAction action)
    {
        GameObject button = new GameObject($"Footer_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(parent, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        // 버튼이 안쪽 테두리 선에 걸쳐 있었다. 선 아래 칸으로 조금 내린다.
        rect.anchorMin = new Vector2(xMin, 0.036f);
        rect.anchorMax = new Vector2(xMax, 0.084f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = button.GetComponent<Image>();
        image.color = new Color(0.10f, 0.20f, 0.27f, 0.75f);

        CreateText(rect, "Key", key, 18f, TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.2f), new Vector2(0.22f, 0.8f), new Color(0.72f, 0.86f, 0.95f));

        CreateText(rect, "Label", label, 24f, TextAlignmentOptions.Center,
            new Vector2(0.24f, 0f), new Vector2(0.96f, 1f), new Color(0.94f, 0.98f, 1f));

        Button component = button.GetComponent<Button>();
        component.transition = Selectable.Transition.None;
        component.onClick.AddListener(action);
    }

    // ---------------------------------------------------------------- 공용

    private static TMP_Text CreateText(RectTransform parent, string name, string content, float size,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

/// <summary>일시정지 메뉴의 투명 클릭 영역. 마우스를 올리면 그 항목이 선택되게 한다.</summary>
public class AetherPauseHitArea : MonoBehaviour, IPointerEnterHandler
{
    private AetherPauseMenu menu;
    private int index;

    public void Setup(AetherPauseMenu owner, int menuIndex)
    {
        menu = owner;
        index = menuIndex;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu != null)
            menu.SetSelected(index);
    }
}
