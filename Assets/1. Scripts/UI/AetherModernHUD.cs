using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the gameplay HUD to match the reference layout with a single canvas.
/// It reads existing gameplay values only; player, camera, combat, and movement logic stay untouched.
/// </summary>
[ExecuteAlways]
public class AetherModernHUD : MonoBehaviour
{
    private const bool UseReferenceImageHud = true;
    private const float CornerHudScale = 0.72f;
    private const float CompassHudScale = 0.74f;
    private const float HudPanelAlpha = 0.72f;
    private const float HudTextAlpha = 0.86f;
    private const float HudSecondaryTextAlpha = 0.66f;
    private const float HudLineAlpha = 0.42f;

    public const string RootName = "PF_GameplayHUD_ReferenceExact";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private static AetherModernHUD instance;

    private static readonly string[] LegacyHudNameTokens =
    {
        "hpbar",
        "hpslider",
        "shieldbar",
        "shieldslider",
        "currentammo",
        "totalammo",
        "firemode",
        "energyslot",
        "weaponui",
        "oldhud",
        "modernhud",
        "compasshud",
        "objectivehud",
        "aetherlevelhud",
        "aetherlevel",
        "alterlevel",
        "alertlevel",
        "playerstatushud",
        "ammohud",
        "quickslot",
        "quickslotbar",
        "minimapui"
    };

    [Header("Runtime Values")]
    [SerializeField] private string objectiveText = "\uC5F0\uAD6C\uC18C \uB0B4\uBD80\uB85C \uC774\uB3D9\uD558\uC5EC\n\uC5D0\uD14C\uB974 \uC81C\uC5B4\uC2E4\uB85C \uC9C4\uC785\uD558\uB77C.";

    [Header("Options")]
    [SerializeField] private bool autoHideLegacyHud = true;
    [SerializeField] private bool showOnlyWhenGameplayPlayerExists = false;
    [SerializeField] private bool autoDetectYunseoHud = true;

    private Canvas canvas;
    private TMP_FontAsset uiFont;
    private PlayerStatus playerStatus;
    private Hankyul_Fire hankyulFire;
    private Move_Yunseo moveYunseo;
    private Camera mainCamera;

    private TextMeshProUGUI objectiveBodyText;
    private RectTransform objectivePanel;
    private TextMeshProUGUI aetherLevelText;
    private TextMeshProUGUI aetherExpText;
    private Image aetherExpFill;
    private Image hpFill;
    private Image shieldFill;
    private Image thirstFill;
    private TextMeshProUGUI hpValueText;
    private TextMeshProUGUI shieldValueText;   // Shiri 실드 숫자. 얇은 줄만으로는 맞아도 체력이 그대로라 "HP 가 안 닳는다"로 보였다
    private int lastShield = -1;
    private TextMeshProUGUI thirstValueText;
    private TextMeshProUGUI currentAmmoText;
    private TextMeshProUGUI reserveAmmoText;
    private TextMeshProUGUI ammoTypeText;
    private TextMeshProUGUI fireModeText;
    private TextMeshProUGUI reloadStateText;
    private Image magazineFill;
    private RectTransform ammoPanel;
    private RectTransform minimapPanel;
    private RawImage minimapImage;
    private TextMeshProUGUI minimapPlaceholderText;
    private Camera runtimeMinimapCamera;
    private RenderTexture runtimeMinimapTexture;
    private RectTransform yunseoQuickSlotPanel;
    private QuickSlotManager yunseoQuickSlotManager;
    private QuickSlotUI[] yunseoQuickSlotUIs;
    private readonly TextMeshProUGUI[] quickSlotKeyTexts = new TextMeshProUGUI[5];
    private RectTransform crosshairPanel;
    private RectTransform compassContent;
    private TextMeshProUGUI compassDegreeText;
    private readonly TextMeshProUGUI[] compassLabels = new TextMeshProUGUI[9];

    private float nextRebindTime;
    private int lastHp = int.MinValue;
    private int lastMaxHp = int.MinValue;
    private int lastThirst = int.MinValue;
    private int lastMaxThirst = int.MinValue;
    private int lastCurrentAmmo = int.MinValue;
    private int lastReserveAmmo = int.MinValue;
    private Hankyul_Fire.FireMode lastFireMode = (Hankyul_Fire.FireMode)(-1);
    private bool lastReloading;
    private int lastAetherLevel = int.MinValue;
    private int lastAetherExp = int.MinValue;
    private int lastMaxAetherExp = int.MinValue;
    private int lastCompassYaw = int.MinValue;
    private float objectiveHideAt;
    private bool objectiveShowingNotice;
    private string currentObjective;
    private static string currentObjectiveOverride;
    private static string currentObjectiveScene;
    private bool isYunseoHud;
    private const float ObjectiveBackgroundAlpha = 0.58f;

    private void Awake()
    {
        if (!UseReferenceImageHud)
        {
            DisableReferenceHud();
            return;
        }

        if (Application.isPlaying)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // 05 씬에는 이 부품이 '꺼짐' 상태로 저장되어 있었다.
            // 꺼져 있어도 Awake 는 돌기 때문에 HUD 모양은 멀쩡히 만들어지는데,
            // Update 가 안 돌아서 체력·탄약·갈증이 만들 때 넣어 둔 "100 / 100" 에 영영 멈춰 있었다.
            // (실제 체력이 0 이어도 화면은 100 이었다.) 켜 준다.
            if (!enabled)
            {
                enabled = true;
                Debug.Log("[ModernHUD] 꺼져 있던 HUD 갱신을 다시 켰습니다. (씬에 꺼진 채로 저장되어 있었음)");
            }
        }

        gameObject.name = RootName;
        ResolveUIFont();
        BuildHud();
        if (Application.isPlaying)
        {
            RebindSceneReferences();
            ShowObjectiveForSceneStart();
        }
    }

    private void OnEnable()
    {
        if (!UseReferenceImageHud)
        {
            DisableReferenceHud();
            return;
        }

        if (Application.isPlaying)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            AetherLevel.ExpGained += HandleAetherGained;
            AetherLevel.LevelUp += HandleAetherLevelUp;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            AetherLevel.ExpGained -= HandleAetherGained;
            AetherLevel.LevelUp -= HandleAetherLevelUp;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        // 미니맵 카메라는 DontDestroyOnLoad 라 HUD 가 사라져도 남았다. 텍스처만 지워져
        // 대상 없는 카메라가 장면 전체를 화면에 한 번 더 그리고 있었다(씬을 넘길 때마다 하나씩 늘어남).
        if (runtimeMinimapCamera != null && Application.isPlaying)
        {
            runtimeMinimapCamera.targetTexture = null;
            Destroy(runtimeMinimapCamera.gameObject);
            runtimeMinimapCamera = null;
        }

        if (runtimeMinimapTexture != null)
        {
            runtimeMinimapTexture.Release();
            if (Application.isPlaying)
                Destroy(runtimeMinimapTexture);
            else
                DestroyImmediate(runtimeMinimapTexture);
            runtimeMinimapTexture = null;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        bool needsPlayerController = isYunseoHud ? moveYunseo == null : hankyulFire == null;
        if ((playerStatus == null || needsPlayerController || mainCamera == null) && Time.unscaledTime >= nextRebindTime)
        {
            nextRebindTime = Time.unscaledTime + 1f;
            RebindSceneReferences();
        }

        UpdateCanvasVisibility();
        if (canvas == null || !canvas.enabled)
            return;

        UpdateStatus();
        UpdateWeapon();
        UpdateMinimap();
        UpdateAetherLevel();
        UpdateCompass();
        UpdateObjectiveVisibility();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindSceneReferences();
        ShowObjectiveForSceneStart();
    }

    private void Reset()
    {
        if (!UseReferenceImageHud)
        {
            DisableReferenceHud();
            return;
        }

        gameObject.name = RootName;
        ResolveUIFont();
        BuildHud();
    }

    private void DisableReferenceHud()
    {
        canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = false;

        enabled = false;
    }


    private void BuildHud()
    {
        RemoveGeneratedChildren();

        // 새로 만든 글자에 에테르 값이 다시 써지도록.
        lastAetherLevel = lastAetherExp = lastMaxAetherExp = int.MinValue;
        aetherFillShown = -1f;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        RectTransform safeArea = CreateEmpty(root, "SafeArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
        safeArea.localScale = Vector3.one;

        BuildObjective(safeArea);
        BuildCompass(safeArea);
        BuildAetherLevel(safeArea);
        BuildStatus(safeArea);
        BuildAmmo(safeArea);
        BuildMinimap(safeArea);
        BuildYunseoQuickSlots(safeArea);
        BuildCrosshair(safeArea);
    }

    private void RemoveGeneratedChildren()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null)
            return;

        for (int i = rect.childCount - 1; i >= 0; i--)
            DestroyImmediate(rect.GetChild(i).gameObject);
    }

    private void BuildObjective(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "ObjectiveHUD", "UI/HUDExact/ObjectivePanel", AnchorTopLeft(), new Vector2(0f, 1f), new Vector2(28f, -56f), new Vector2(405f, 120f));
        panel.localScale = Vector3.one * CornerHudScale;
        RawImage background = panel.GetComponentInChildren<RawImage>(true);
        if (background != null)
            background.color = new Color(1f, 1f, 1f, ObjectiveBackgroundAlpha);
        objectivePanel = panel;
        objectiveBodyText = CreateTMP(panel, "DescriptionText", AnchorTopLeft(), AnchorTopLeft(), new Vector2(0f, 1f), new Vector2(79f, -57f), new Vector2(298f, 55f), objectiveText, 18, TextAlignmentOptions.TopLeft, SoftWhite(0.88f));
        objectiveBodyText.enableWordWrapping = true;
        objectiveBodyText.overflowMode = TextOverflowModes.Overflow;
        objectiveBodyText.lineSpacing = 6f; // -3 이면 한글 두 줄이 서로 겹쳤다
        // 안내가 길면(세 줄 이상) 칸 밖으로 삐져나왔다. 칸에 맞게 글자를 줄인다(18 → 최소 12).
        objectiveBodyText.enableAutoSizing = true;
        objectiveBodyText.fontSizeMax = 22f;
        objectiveBodyText.fontSizeMin = 16f;
        objectiveBodyText.fontStyle = FontStyles.Normal;
    }

    private void BuildCompass(RectTransform parent)
    {
        RectTransform panel = CreateEmpty(parent, "CompassHUD", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(900f, 78f));
        panel.localScale = Vector3.one * CompassHudScale;
        RectTransform viewport = CreateEmpty(panel, "CompassViewport", new Vector2(0f, 0f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateEmpty(viewport, "CompassContent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1800f, 52f));
        compassContent = content;
        RawImage compassArtwork = CreateTextureImage(content, "CompassArtwork", "UI/HUDExact/CompassPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), Vector2.zero);
        compassArtwork.color = new Color(1f, 1f, 1f, 0.62f);

        int[] offsets = { -90, -60, -30, -15, 0, 15, 30, 60, 90 };
        for (int i = 0; i < offsets.Length; i++)
        {
            float x = offsets[i] * 4.1f;
            compassLabels[i] = CreateTMP(panel, "Compass Label " + i, AnchorTopCenter(), AnchorTopCenter(), new Vector2(x, -40f), new Vector2(72f, 25f), string.Empty, i == 4 ? 22 : 16, TextAlignmentOptions.Center, SoftWhite(i == 4 ? 0.9f : HudSecondaryTextAlpha));
        }
        RectTransform marker = CreateEmpty(panel, "CenterMarker", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(26f, 20f));
        compassDegreeText = CreateTMP(panel, "CurrentAngleText", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0f, -64f), new Vector2(90f, 30f), "000", 24, TextAlignmentOptions.Center, SoftWhite(0.9f));
        compassDegreeText.fontStyle = FontStyles.Bold;
    }

    private void BuildAetherLevel(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "AetherLevelHUD", "UI/HUDExact/AetherLevelPanel", AnchorTopRight(), new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(315f, 96f));
        panel.localScale = Vector3.one * CornerHudScale;
        SetTexturePanelAlpha(panel, HudPanelAlpha);
        aetherPanel = panel;
        aetherLevelText = CreateTMP(panel, "LevelText", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0.5f, 0.5f), new Vector2(48f, 39f), new Vector2(58f, 48f), "1", 46, TextAlignmentOptions.Center, SoftWhite(0.88f));
        aetherExpText = CreateTMP(panel, "ExperienceText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-18f, 38f), new Vector2(96f, 20f), "0 / 300", 17, TextAlignmentOptions.MidlineRight, SoftWhite(0.78f));
        // 에테르를 얻을 때 패널 아래에 잠깐 뜨는 줄: "+60 AE ×1.5  로봇 처치".
        aetherGainText = CreateTMP(panel, "GainText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 1f), new Vector2(-18f, -4f), new Vector2(300f, 24f), string.Empty, 17, TextAlignmentOptions.TopRight, Cyan(0.95f));
        aetherGainText.overflowMode = TextOverflowModes.Overflow;
        ConfigureNumericText(aetherLevelText);
        ConfigureNumericText(aetherExpText);
        aetherExpFill = CreateSolidImage(panel, "ProgressBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(88f, 22f), new Vector2(166f, 8f), Cyan(0.78f));
        aetherExpFill.type = Image.Type.Filled;
        aetherExpFill.fillMethod = Image.FillMethod.Horizontal;
    }

    private void BuildStatus(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "PlayerStatusHUD", "UI/HUDExact/PlayerStatusPanel", AnchorBottomLeft(), new Vector2(0f, 0f), new Vector2(28f, 30f), new Vector2(470f, 115f));
        panel.localScale = Vector3.one * CornerHudScale;
        SetTexturePanelAlpha(panel, HudPanelAlpha);
        hpFill = CreateSolidImage(panel, "HPBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(116f, 79f), new Vector2(220f, 12f), new Color(0.92f, 0.95f, 0.98f, 0.82f));
        thirstFill = CreateSolidImage(panel, "ThirstBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(116f, 32f), new Vector2(220f, 12f), Cyan(0.78f));
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        // Shiri 의 실드. 예전 실드 막대는 숨기고 대신 체력 막대 바로 위에 얇은 파란 줄로 보인다(실드를 쓰는 캐릭터만).
        shieldFill = CreateSolidImage(panel, "ShieldBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(116f, 91f), new Vector2(220f, 7f), new Color(0.35f, 0.72f, 1f, 0.95f));
        shieldFill.type = Image.Type.Filled;
        shieldFill.fillMethod = Image.FillMethod.Horizontal;
        shieldFill.gameObject.SetActive(false);
        thirstFill.type = Image.Type.Filled;
        thirstFill.fillMethod = Image.FillMethod.Horizontal;
        CreateSolidImage(panel, "ValueMask", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-2f, 56f), new Vector2(112f, 101f), new Color(0.025f, 0.075f, 0.125f, 0.72f));
        hpValueText = CreateTMP(panel, "HPValueText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-12f, 79f), new Vector2(100f, 27f), "100 / 100", 18, TextAlignmentOptions.MidlineRight, SoftWhite(0.88f));
        thirstValueText = CreateTMP(panel, "ThirstValueText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-12f, 32f), new Vector2(100f, 27f), "100 / 100", 18, TextAlignmentOptions.MidlineRight, SoftWhite(0.88f));
        ConfigureNumericText(hpValueText);
        ConfigureNumericText(thirstValueText);
        shieldValueText = CreateTMP(panel, "ShieldValueText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-12f, 100f), new Vector2(110f, 18f), "", 13, TextAlignmentOptions.MidlineRight, new Color(0.45f, 0.78f, 1f, 0.95f));
        ConfigureNumericText(shieldValueText);
        shieldValueText.gameObject.SetActive(false);
    }

    private void BuildAmmo(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "AmmoHUD", "UI/HUDExact/AmmoPanel", AnchorBottomRight(), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(350f, 108f));
        ammoPanel = panel;
        panel.localScale = Vector3.one * CornerHudScale;
        SetTexturePanelAlpha(panel, HudPanelAlpha);
        currentAmmoText = CreateTMP(panel, "CurrentAmmoText", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(-103f, 13f), new Vector2(78f, 56f), "30", 48, TextAlignmentOptions.MidlineRight, SoftWhite(0.9f));
        CreateLine(panel, "AmmoDivider", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(0.5f, 0.5f), new Vector2(-91f, 13f), new Vector2(2f, 45f), new Color(1f, 1f, 1f, 0.18f));
        reserveAmmoText = CreateTMP(panel, "ReserveAmmoText", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(-43f, 14f), new Vector2(52f, 28f), "120", 19, TextAlignmentOptions.Center, SoftWhite(0.78f));
        ammoTypeText = CreateTMP(panel, "CaliberText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(-131f, 19f), new Vector2(92f, 22f), "5.56mm", 17, TextAlignmentOptions.Center, SoftWhite(0.76f));
        fireModeText = CreateTMP(panel, "FireModeText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(-40f, 19f), new Vector2(66f, 22f), "AUTO", 17, TextAlignmentOptions.Center, SoftWhite(0.76f));
        magazineFill = CreateSolidImage(panel, "MagazineProgress", AnchorBottomRight(), AnchorBottomRight(), new Vector2(0f, 0.5f), new Vector2(-173f, 7f), new Vector2(132f, 3f), Cyan(0.72f));
        magazineFill.type = Image.Type.Filled;
        magazineFill.fillMethod = Image.FillMethod.Horizontal;
        reloadStateText = CreateTMP(panel, "ReloadStateText", AnchorTopRight(), AnchorTopRight(), new Vector2(-81f, -14f), new Vector2(128f, 22f), string.Empty, 14, TextAlignmentOptions.Center, Cyan(0.82f));
        reloadStateText.fontStyle = FontStyles.Bold;
        ConfigureNumericText(currentAmmoText);
        ConfigureNumericText(reserveAmmoText);
    }

    private void BuildMinimap(RectTransform parent)
    {
        RectTransform panel = CreatePanel(parent, "YunseoMinimapHUD", AnchorBottomRight(), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(230f, 230f), new Color(0.012f, 0.04f, 0.07f, 0.44f));
        minimapPanel = panel;
        panel.localScale = Vector3.one * CornerHudScale;

        CreateLine(panel, "TopEdge", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(196f, 2f), Cyan(HudLineAlpha));
        CreateLine(panel, "BottomEdge", AnchorBottomCenter(), AnchorBottomCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(196f, 2f), Cyan(HudLineAlpha));
        CreateLine(panel, "LeftEdge", AnchorMiddleLeft(), AnchorMiddleLeft(), new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), new Vector2(2f, 196f), Cyan(HudLineAlpha));
        CreateLine(panel, "RightEdge", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(0.5f, 0.5f), new Vector2(-1f, 0f), new Vector2(2f, 196f), Cyan(HudLineAlpha));

        RectTransform viewport = CreateEmpty(panel, "MinimapViewport", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.offsetMin = new Vector2(10f, 32f);
        viewport.offsetMax = new Vector2(-10f, -10f);
        viewport.gameObject.AddComponent<RectMask2D>();

        minimapImage = viewport.gameObject.AddComponent<RawImage>();
        minimapImage.texture = FindMinimapTexture();
        minimapImage.color = new Color(1f, 1f, 1f, 0.78f);
        minimapImage.raycastTarget = false;

        minimapPlaceholderText = CreateTMP(panel, "MinimapPlaceholder", AnchorCenter(), AnchorCenter(), Vector2.zero, new Vector2(180f, 44f), "MINIMAP\nNO SIGNAL", 15, TextAlignmentOptions.Center, new Color(0.72f, 0.9f, 1f, 0.56f));
        minimapPlaceholderText.lineSpacing = -8f;
        CreateTMP(panel, "MinimapTitle", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(62f, 17f), new Vector2(96f, 20f), "MINIMAP", 16, TextAlignmentOptions.MidlineLeft, Cyan(0.72f));

        if (minimapPanel != null)
            minimapPanel.gameObject.SetActive(false);
    }

    private void BuildYunseoQuickSlots(RectTransform parent)
    {
        RectTransform panel = CreateEmpty(parent, "YunseoQuickSlotHUD", AnchorBottomCenter(), AnchorBottomCenter(), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(412f, 90f));
        yunseoQuickSlotPanel = panel;
        panel.localScale = Vector3.one;

        yunseoQuickSlotManager = panel.gameObject.AddComponent<QuickSlotManager>();
        yunseoQuickSlotManager.slotCount = 5;
        // 이 패널이 켜져 있는 동안(윤서 모드)은 숫자 키를 이 퀵슬롯만 받는다.
        QuickSlotManager.KeyOwner = yunseoQuickSlotManager;

        yunseoQuickSlotUIs = new QuickSlotUI[5];
        const float slotWidth = 84f;
        const float slotHeight = 84f;
        const float slotGap = -2f;
        float startX = -((slotWidth * 5f) + (slotGap * 4f)) * 0.5f + slotWidth * 0.5f;

        for (int i = 0; i < yunseoQuickSlotUIs.Length; i++)
        {
            RectTransform slotRoot = CreateEmpty(panel, "QuickSlot_" + (i + 1), AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(startX + (slotWidth + slotGap) * i, 0f), new Vector2(slotWidth, slotHeight));
            QuickSlotUI slotUI = slotRoot.gameObject.AddComponent<QuickSlotUI>();
            slotUI.quickSlotIndex = i;

            RawImage background = CreateTextureImage(slotRoot, "SlotFrame", "UI/HUDExact/YunseoQuickSlotSingle", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            background.color = new Color(1f, 1f, 1f, 0.86f);
            background.raycastTarget = false;

            Image icon = CreateSolidImage(slotRoot, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f), new Color(1f, 1f, 1f, 0.98f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            TextMeshProUGUI count = CreateTMP(slotRoot, "CountText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(-17f, 24f), new Vector2(36f, 20f), string.Empty, 14, TextAlignmentOptions.BottomRight, SoftWhite(0.96f));
            ConfigureNumericText(count);

            // 숫자가 칸 테두리 위에 걸쳐 반쯤 잘려 보였다. 칸 안쪽 모서리로 옮기고 조금 키운다.
            TextMeshProUGUI key = CreateTMP(slotRoot, "KeyText", AnchorTopLeft(), AnchorTopLeft(), new Vector2(18f, -15f), new Vector2(20f, 18f), (i + 1).ToString(), 14, TextAlignmentOptions.Center, new Color(0.78f, 0.93f, 1f, 0.9f));
            ConfigureNumericText(key);
            quickSlotKeyTexts[i] = key;

            slotUI.iconImage = icon;
            slotUI.countText = count;
            slotUI.Bind(yunseoQuickSlotManager);
            yunseoQuickSlotUIs[i] = slotUI;
        }

        yunseoQuickSlotManager.BindUI(yunseoQuickSlotUIs);
        if (yunseoQuickSlotPanel != null)
            yunseoQuickSlotPanel.gameObject.SetActive(false);
    }

    private void BuildCrosshair(RectTransform parent)
    {
        RectTransform group = CreateEmpty(parent, "CrosshairHUD", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
        crosshairPanel = group;
        CreateLine(group, "TopLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(2f, 9f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "BottomLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, -13f), new Vector2(2f, 9f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "LeftLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(-13f, 0f), new Vector2(9f, 2f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "RightLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(13f, 0f), new Vector2(9f, 2f), new Color(1f, 1f, 1f, 0.62f));
    }

    private void RebindSceneReferences()
    {
        hankyulFire = FindActiveFireController();
        moveYunseo = FindActiveYunseoController();
        isYunseoHud = autoDetectYunseoHud && IsYunseoGameplayActive(moveYunseo, hankyulFire);
        playerStatus = FindStatusForCurrentPlayer(isYunseoHud, moveYunseo, hankyulFire);
        mainCamera = Camera.main;
        if (minimapImage != null)
            minimapImage.texture = FindMinimapTexture();

        EnsureRuntimeMinimapCamera(false);
        BindYunseoQuickSlots();

        if (autoHideLegacyHud)
            HideLegacyHud();

        UpdateCanvasVisibility();
    }

    /// <summary>
    /// 왼쪽 위 목표 패널에 문구를 띄운다. 구역 진행 안내에 쓴다.
    /// HUD가 아직 없으면 아무 일도 하지 않는다(호출한 쪽이 신경 쓸 필요 없게).
    /// </summary>
    public static bool SetObjective(string text, float visibleSeconds = 12f)
    {
        if (instance == null || string.IsNullOrWhiteSpace(text))
            return false;

        instance.ShowObjective(text, visibleSeconds);
        return true;
    }

    private void ShowObjective(string text, float visibleSeconds)
    {
        // 알림(웨이브·레벨업 등)은 잠깐 띄웠다가 지금 목표로 되돌린다.
        // 예전엔 알림 시간이 끝나면 목표 칸이 통째로 꺼져, 그 뒤로 무엇을 해야 하는지 알 수 없었다.
        if (objectiveBodyText != null)
            objectiveBodyText.text = text;

        objectiveHideAt = Time.unscaledTime + Mathf.Max(1f, visibleSeconds);
        objectiveShowingNotice = true;

        if (objectivePanel != null)
            objectivePanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 지금 해야 할 일(목표)을 바꾼다. 알림이 끝나면 목표 칸은 늘 이 문구로 돌아온다.
    /// </summary>
    public static void SetCurrentObjective(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        currentObjectiveOverride = text;
        currentObjectiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (instance == null)
            return;

        instance.currentObjective = text;
        if (!instance.objectiveShowingNotice)
        {
            if (instance.objectiveBodyText != null)
                instance.objectiveBodyText.text = text;
            if (instance.objectivePanel != null)
                instance.objectivePanel.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 씬마다 시작할 때 보여 줄 목표. 예전엔 04~07 모두 같은 문구(에테르 제어실로 진입하라)라
    /// 튜토리얼이나 파츠 제작실에서도 엉뚱한 목표가 떴다.
    /// </summary>
    private static string ObjectiveForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;
        if (sceneName.StartsWith("04.")) return "벽에 그려진 조작법을 익히고\n출구를 찾아 지상으로 나가라.";
        // Shiri 는 총이 없어 높이 달린 표적을 충전 펀치(길게 눌렀다 떼기)의 충격파로만 맞힐 수 있다. 그 방법을 알려 준다.
        if (sceneName.StartsWith("05."))
            return AetherCharacterChoice.Selected == AetherCharacter.Yunseo
                ? "모듈을 모으고 빛나는 표적을\n충전 펀치로 차례로 맞혀라."
                : "모듈을 모으고 빛나는 표적을\n차례로 쏘아 연구소 문을 열어라.";
        // 파츠 제작실 문은 연구소 입구 바로 옆이고, 복도 끝 엘리베이터는 옥상(이번 판의 끝)으로 간다.
        // 예전 문구("연구소를 지나 파츠 제작실을 찾아라")를 따르면 제작실을 지나쳐 엘리베이터로 가게 됐다.
        if (sceneName.StartsWith("06."))
            return PartSystemRuntimeState.GetCraftedEquipmentParts().Count == 0
                ? "입구 옆 파츠 제작실 문으로 들어가\n모은 모듈로 강화파츠를 만들어라."
                : "연구소 복도 끝 엘리베이터를 타고\n옥상으로 올라가라.";
        if (sceneName.StartsWith("07.")) return "모은 재료로 파츠를 제작하라.\n(장착은 Meka 만 가능)";
        return null;
    }

    /// <summary>
    /// 04 씬에 예전 나침반(Canvas/CompassBar, CompassScroller·CompassTickBuilder)이 남아 있어
    /// 이 HUD 의 나침반과 두 줄로 겹쳐 방위 글자가 뒤엉켜 보였다. 이 HUD 가 떠 있으면 예전 것을 끈다.
    /// </summary>
    private static void HideLegacyCompass()
    {
        // 스크립트는 Canvas/CompassBar/CompassSystem 에 있다. 바탕(CompassBar)과 가운데 표시(CenterPointer)까지 같이 끈다.
        foreach (var s in FindObjectsByType<CompassScroller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s == null) continue;
            Transform bar = s.transform.parent != null ? s.transform.parent : s.transform;
            if (bar.parent != null)
            {
                Transform pointer = bar.parent.Find("CenterPointer");
                if (pointer != null) pointer.gameObject.SetActive(false);
            }
            bar.gameObject.SetActive(false);
        }
    }

    private void ShowObjectiveForSceneStart()
    {
        HideLegacyCompass();
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // 같은 장면 안에서 진행으로 바뀐 목표가 있으면 그것을 이어 쓴다(HUD 를 다시 만들 때 처음 목표로 되돌아가지 않게).
        string sceneObjective = currentObjectiveScene == sceneName && !string.IsNullOrEmpty(currentObjectiveOverride)
            ? currentObjectiveOverride
            : ObjectiveForScene(sceneName);
        if (currentObjectiveScene != sceneName)
            currentObjectiveOverride = null;

        currentObjective = sceneObjective ?? objectiveText;
        objectiveShowingNotice = false;
        if (objectiveBodyText != null)
            objectiveBodyText.text = currentObjective;

        if (objectivePanel != null)
            objectivePanel.gameObject.SetActive(true);
    }

    private void UpdateObjectiveVisibility()
    {
        if (!objectiveShowingNotice)
            return;

        // 퍼즐·상점·제작 화면처럼 커서가 풀린 동안엔 알림 시간을 세지 않는다.
        // 04 전력 퍼즐을 풀면 레벨업 알림이 퍼즐 화면 뒤에서 시간을 다 써서, 돌아오면 이미 사라져 있었다.
        if (Cursor.lockState != CursorLockMode.Locked)
            objectiveHideAt += Time.unscaledDeltaTime;

        if (Time.unscaledTime < objectiveHideAt)
            return;

        objectiveShowingNotice = false;
        bool hasGoal = !string.IsNullOrWhiteSpace(currentObjective);
        if (hasGoal && objectiveBodyText != null)
            objectiveBodyText.text = currentObjective;
        if (objectivePanel != null)
            objectivePanel.gameObject.SetActive(hasGoal);
    }

    private static Hankyul_Fire FindActiveFireController()
    {
        Hankyul_Fire[] fireControllers = FindObjectsOfType<Hankyul_Fire>(true);
        for (int i = 0; i < fireControllers.Length; i++)
        {
            Hankyul_Fire fire = fireControllers[i];
            if (fire != null && fire.gameObject.activeInHierarchy)
                return fire;
        }

        return null;
    }

    private static Move_Yunseo FindActiveYunseoController()
    {
        Move_Yunseo[] controllers = FindObjectsOfType<Move_Yunseo>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            Move_Yunseo controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy)
                return controller;
        }

        return null;
    }

    private static bool IsYunseoGameplayActive(Move_Yunseo yunseo, Hankyul_Fire fire)
    {
        if (yunseo == null)
            return false;

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            if (activeCamera.GetComponentInParent<Move_Yunseo>() != null)
                return true;

            Transform cameraRoot = activeCamera.transform.root;
            if (cameraRoot != null && cameraRoot.name.ToLowerInvariant().Contains("yunseo"))
                return true;

            if (fire != null && cameraRoot == fire.transform.root)
                return false;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            if (taggedPlayer.GetComponentInParent<Move_Yunseo>() != null)
                return true;

            if (taggedPlayer.name.ToLowerInvariant().Contains("yunseo"))
                return true;

            if (fire != null && taggedPlayer.transform.root == fire.transform.root)
                return false;
        }

        return fire == null || yunseo.gameObject.activeInHierarchy;
    }

    private static PlayerStatus FindStatusForCurrentPlayer(bool preferYunseo, Move_Yunseo yunseo, Hankyul_Fire fire)
    {
        Camera activeCamera = Camera.main;
        PlayerStatus status = FindPlayerStatusNear(activeCamera != null ? activeCamera.transform : null, preferYunseo);
        if (status != null)
            return status;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        status = FindPlayerStatusNear(taggedPlayer != null ? taggedPlayer.transform : null, preferYunseo);
        if (status != null)
            return status;

        if (preferYunseo && yunseo != null)
        {
            status = FindPlayerStatusNear(yunseo.transform, true);
            if (status != null)
                return status;
        }

        if (!preferYunseo && fire != null)
        {
            status = FindPlayerStatusNear(fire.transform, false);
            if (status != null)
                return status;
        }

        return FindStatusForActivePlayer(fire);
    }

    private static PlayerStatus FindPlayerStatusNear(Transform origin, bool preferYunseo)
    {
        if (origin == null)
            return null;

        PlayerStatus status = origin.GetComponent<PlayerStatus>()
            ?? origin.GetComponentInParent<PlayerStatus>()
            ?? origin.GetComponentInChildren<PlayerStatus>(true);
        if (IsUsablePlayerStatus(status, preferYunseo))
            return status;

        Transform root = origin.root;
        if (root != null)
        {
            status = root.GetComponent<PlayerStatus>() ?? root.GetComponentInChildren<PlayerStatus>(true);
            if (IsUsablePlayerStatus(status, preferYunseo))
                return status;
        }

        return null;
    }

    private static bool IsUsablePlayerStatus(PlayerStatus status, bool preferYunseo)
    {
        if (status == null || !status.gameObject.activeInHierarchy)
            return false;

        Transform root = status.transform.root;
        bool isYunseo = status.GetComponent<Move_Yunseo>() != null
            || status.GetComponentInChildren<Move_Yunseo>(true) != null
            || status.GetComponentInParent<Move_Yunseo>() != null
            || (root != null && root.name.ToLowerInvariant().Contains("yunseo"));
        bool isHankyul = status.GetComponent<Move_Hankyul>() != null
            || status.GetComponentInChildren<Move_Hankyul>(true) != null
            || status.GetComponentInParent<Move_Hankyul>() != null
            || (root != null && root.name.ToLowerInvariant().Contains("hankyul"));

        if (preferYunseo)
            return isYunseo || !isHankyul;

        return isHankyul || !isYunseo;
    }

    private static PlayerStatus FindStatusForActivePlayer(Hankyul_Fire fire)
    {
        if (fire != null)
        {
            PlayerStatus ownerStatus = fire.transform.root.GetComponent<PlayerStatus>();
            if (ownerStatus != null && ownerStatus.gameObject.activeInHierarchy)
                return ownerStatus;
        }

        PlayerStatus[] statuses = FindObjectsOfType<PlayerStatus>(true);
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status != null && status.gameObject.activeInHierarchy && status.GetComponent<Move_Hankyul>() != null)
                return status;
        }

        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status != null && status.gameObject.activeInHierarchy && status.GetComponent<Move_Yunseo>() != null)
                return status;
        }

        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status != null && status.gameObject.activeInHierarchy)
                return status;
        }

        return null;
    }

    private void HideLegacyHud()
    {
        HideComponentObjects<CompassScroller>();
        HideComponentObjects<CompassTickBuilder>();
        HideComponentObjects<Hankyul_WeaponUI>();
        HideComponentObjects<QuickSlotManager>();
        HideComponentObjects<QuickSlotUI>();

        if (playerStatus != null)
        {
            if (playerStatus.hpSlider != null)
                HideLegacySliderRoot(playerStatus.hpSlider);
            if (playerStatus.shieldSlider != null)
                HideLegacySliderRoot(playerStatus.shieldSlider);
            HideLegacyHudChildren(playerStatus.transform.root);
        }

        if (hankyulFire != null)
            HideLegacyHudChildren(hankyulFire.transform.root);

        if (moveYunseo != null)
            HideLegacyHudChildren(moveYunseo.transform.root);

        HideStandaloneLegacyHudObjects();

        PlayerUIManager[] playerUis = FindObjectsOfType<PlayerUIManager>(true);
        for (int i = 0; i < playerUis.Length; i++)
        {
            PlayerUIManager ui = playerUis[i];
            if (ui != null && ui.crosshairImage != null)
                ui.crosshairImage.gameObject.SetActive(false);
        }
    }

    private static void HideComponentObjects<T>() where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                continue;

            AetherModernHUD ownHud = components[i].GetComponentInParent<AetherModernHUD>(true);
            if (ownHud == null)
                components[i].gameObject.SetActive(false);
        }
    }

    private static void HideStandaloneLegacyHudObjects()
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform target = allTransforms[i];
            if (target == null || target.GetComponentInParent<AetherModernHUD>(true) != null)
                continue;

            if (!LooksLikeLegacyUi(target))
                continue;

            string normalizedName = NormalizeName(target.name);
            if (!normalizedName.Contains("quickslot")
                && !normalizedName.Contains("quickslotbar")
                && !normalizedName.Contains("minimapui"))
                continue;

            target.gameObject.SetActive(false);
        }
    }

    private void UpdateCanvasVisibility()
    {
        if (canvas == null)
            return;

        canvas.enabled = !showOnlyWhenGameplayPlayerExists || playerStatus != null || hankyulFire != null || moveYunseo != null;
        // 캐릭터마다 보이는 칸이 다르다: 미니맵은 Shiri 만, 총 관련 칸(탄약·조준점)은 Meka 만.
        bool gunHud = !isYunseoHud;
        if (ammoPanel != null)
            ammoPanel.gameObject.SetActive(gunHud);
        // 상점·인벤토리·제작 화면처럼 커서가 풀린 동안엔 조준점을 감춘다(상점 창 한가운데 조준점이 겹쳐 보였다).
        if (crosshairPanel != null)
            crosshairPanel.gameObject.SetActive(gunHud && Cursor.lockState == CursorLockMode.Locked);
        if (minimapPanel != null)
        {
            minimapPanel.gameObject.SetActive(!gunHud);
            minimapPanel.anchoredPosition = new Vector2(-30f, 30f);
        }
        // 퀵슬롯은 두 캐릭터 모두 쓴다(Meka 도 상점 에너지팩을 숫자 키로 쓴다).
        // 예전엔 Shiri 일 때만 켜서, Meka 는 예전 퀵슬롯이 숨겨진 채로 아이템을 볼 수도 쓸 수도 없었다.
        if (yunseoQuickSlotPanel != null)
            yunseoQuickSlotPanel.gameObject.SetActive(moveYunseo != null || hankyulFire != null);

        // Meka 는 1번이 총 꺼내기라 퀵슬롯을 2~6번으로 민다. 칸의 번호도 맞춘다.
        QuickSlotManager.KeyOffset = !isYunseoHud && hankyulFire != null ? 1 : 0;
        for (int i = 0; i < quickSlotKeyTexts.Length; i++)
            if (quickSlotKeyTexts[i] != null)
                quickSlotKeyTexts[i].text = (i + 1 + QuickSlotManager.KeyOffset).ToString();
    }

    private void BindYunseoQuickSlots()
    {
        if (yunseoQuickSlotManager == null)
            return;

        PlayerInventory inventory = null;
        PlayerStatus status = null;

        Component owner = isYunseoHud && moveYunseo != null ? moveYunseo : hankyulFire != null ? (Component)hankyulFire : moveYunseo;
        if (owner != null)
        {
            Transform root = owner.transform.root;
            inventory = root.GetComponentInChildren<PlayerInventory>(true) ?? owner.GetComponentInParent<PlayerInventory>();
            status = playerStatus != null ? playerStatus : root.GetComponentInChildren<PlayerStatus>(true);
        }

        yunseoQuickSlotManager.SetRuntimeReferences(inventory, status);
    }

    private static void HideLegacySliderRoot(Slider slider)
    {
        if (slider == null)
            return;

        Transform root = slider.transform;
        if (root.parent != null)
        {
            string parentName = root.parent.name.ToLowerInvariant();
            if (parentName.Contains("hp") || parentName.Contains("shield") || parentName.Contains("bar"))
                root = root.parent;
        }

        root.gameObject.SetActive(false);
    }

    private static void HideLegacyHudChildren(Transform root)
    {
        if (root == null)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
                continue;

            if (!LooksLikeLegacyUi(child))
                continue;

            string normalizedName = NormalizeName(child.name);
            for (int tokenIndex = 0; tokenIndex < LegacyHudNameTokens.Length; tokenIndex++)
            {
                if (!normalizedName.Contains(LegacyHudNameTokens[tokenIndex]))
                    continue;

                if (child.GetComponentInParent<AetherModernHUD>(true) == null)
                    child.gameObject.SetActive(false);
                break;
            }
        }
    }

    private static bool LooksLikeLegacyUi(Transform target)
    {
        if (target == null)
            return false;

        return target.GetComponent<RectTransform>() != null
            || target.GetComponent<Graphic>() != null
            || target.GetComponent<Slider>() != null
            || target.GetComponent<Canvas>() != null
            || target.GetComponentInChildren<Graphic>(true) != null
            || target.GetComponentInChildren<TMP_Text>(true) != null;
    }

    private static string NormalizeName(string rawName)
    {
        return string.IsNullOrEmpty(rawName)
            ? string.Empty
            : rawName.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private void UpdateStatus()
    {
        float hp = playerStatus != null ? playerStatus.currentHP : 100f;
        float maxHp = playerStatus != null ? playerStatus.maxHP : 100f;
        float hpRatio = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
        float thirst = playerStatus != null ? playerStatus.currentThirst : 100f;
        float maxThirst = playerStatus != null ? playerStatus.maxThirst : 100f;
        float thirstRatio = maxThirst > 0f ? Mathf.Clamp01(thirst / maxThirst) : 0f;

        if (hpFill != null) hpFill.fillAmount = hpRatio;
        if (shieldFill != null)
        {
            bool showShield = playerStatus != null && playerStatus.useShield && playerStatus.maxShield > 0f;
            if (shieldFill.gameObject.activeSelf != showShield)
                shieldFill.gameObject.SetActive(showShield);
            if (showShield)
                shieldFill.fillAmount = Mathf.Clamp01(playerStatus.currentShield / playerStatus.maxShield);
            if (shieldValueText != null)
            {
                if (shieldValueText.gameObject.activeSelf != showShield)
                    shieldValueText.gameObject.SetActive(showShield);
                int roundedShield = showShield ? Mathf.RoundToInt(playerStatus.currentShield) : -1;
                if (showShield && roundedShield != lastShield)
                {
                    lastShield = roundedShield;
                    shieldValueText.text = "실드 " + roundedShield + " / " + Mathf.RoundToInt(playerStatus.maxShield);
                }
            }
        }
        if (thirstFill != null)
        {
            // 바닥나면(체력 회복 멈춤) 빈 막대 전체가 붉게 깜빡이고, 얼마 안 남으면 주황으로 경고한다.
            if (thirstRatio <= 0f)
            {
                thirstFill.fillAmount = 1f;
                float pulse = 0.25f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                thirstFill.color = new Color(0.95f, 0.22f, 0.18f, pulse);
            }
            else
            {
                thirstFill.fillAmount = thirstRatio;
                thirstFill.color = thirstRatio < 0.2f ? new Color(1f, 0.62f, 0.2f, 0.85f) : Cyan(0.78f);
            }
        }

        int roundedHp = Mathf.RoundToInt(hp);
        int roundedMaxHp = Mathf.RoundToInt(maxHp);
        int roundedThirst = Mathf.RoundToInt(thirst);
        int roundedMaxThirst = Mathf.RoundToInt(maxThirst);

        if (hpValueText != null && (roundedHp != lastHp || roundedMaxHp != lastMaxHp))
        {
            lastHp = roundedHp;
            lastMaxHp = roundedMaxHp;
            hpValueText.text = roundedHp + " / " + roundedMaxHp;
        }

        if (thirstValueText != null && (roundedThirst != lastThirst || roundedMaxThirst != lastMaxThirst))
        {
            lastThirst = roundedThirst;
            lastMaxThirst = roundedMaxThirst;
            thirstValueText.text = roundedThirst + " / " + roundedMaxThirst;
        }
    }

    private void UpdateWeapon()
    {
        if (isYunseoHud)
        {
            lastCurrentAmmo = int.MinValue;
            lastReserveAmmo = int.MinValue;
            return;
        }

        if (hankyulFire == null)
            return;

        int currentAmmo = hankyulFire.CurrentMagazineAmmo;
        int reserveAmmo = hankyulFire.CurrentReserveAmmo;
        Hankyul_Fire.FireMode fireMode = hankyulFire.CurrentFireMode;
        bool isReloading = hankyulFire.IsReloading;

        if (currentAmmoText != null && currentAmmo != lastCurrentAmmo)
        {
            lastCurrentAmmo = currentAmmo;
            currentAmmoText.text = currentAmmo.ToString();
        }

        if (reserveAmmoText != null && reserveAmmo != lastReserveAmmo)
        {
            lastReserveAmmo = reserveAmmo;
            reserveAmmoText.text = reserveAmmo.ToString();
        }

        if (fireModeText != null && fireMode != lastFireMode)
        {
            lastFireMode = fireMode;
            fireModeText.text = fireMode == Hankyul_Fire.FireMode.Auto ? "AUTO" : "SINGLE";
        }

        if (magazineFill != null)
            magazineFill.fillAmount = hankyulFire.MagazineSize > 0 ? Mathf.Clamp01((float)currentAmmo / hankyulFire.MagazineSize) : 0f;

        if (reloadStateText != null && isReloading != lastReloading)
        {
            lastReloading = isReloading;
            reloadStateText.text = isReloading ? "RELOADING" : string.Empty;
            currentAmmoText.color = isReloading ? Cyan(0.82f) : SoftWhite(0.9f);
        }
    }

    // 에테르 레벨(AetherLevel)을 보여 준다. 예전에는 2 · 450/1000 고정값이었다.
    private RectTransform aetherPanel;
    private TextMeshProUGUI aetherGainText;
    private int aetherGainSum;
    private float aetherGainMultiplier = 1f;
    private string aetherGainReason;
    private float aetherGainShownAt = -999f;
    private float aetherLevelUpAt = -999f;
    private float aetherFillShown = -1f;
    private const float AetherGainHold = 1.4f;
    private const float AetherGainFade = 0.6f;
    private const float AetherPulseSeconds = 0.6f;

    private void HandleAetherGained(int amount, float multiplier, string reason)
    {
        // 짧은 사이에 연달아 얻으면 한 줄로 합친다.
        if (Time.unscaledTime - aetherGainShownAt > AetherGainHold)
            aetherGainSum = 0;

        aetherGainSum += amount;
        aetherGainMultiplier = multiplier;
        aetherGainReason = reason;
        aetherGainShownAt = Time.unscaledTime;
    }

    private void HandleAetherLevelUp(int level)
    {
        aetherLevelUpAt = Time.unscaledTime;
    }

    private void UpdateAetherLevel()
    {
        int level = AetherLevel.Level;
        float target = AetherLevel.LevelProgress;

        // 막대는 부드럽게 차고, 레벨이 오르면 비웠다가 다시 찬다.
        bool levelChanged = level != lastAetherLevel;
        if (aetherFillShown < 0f || levelChanged)
            aetherFillShown = levelChanged && lastAetherLevel != int.MinValue ? 0f : target;
        aetherFillShown = Mathf.MoveTowards(aetherFillShown, target, Time.unscaledDeltaTime * 1.5f);
        if (aetherExpFill != null)
            aetherExpFill.fillAmount = aetherFillShown;

        if (levelChanged)
        {
            lastAetherLevel = level;
            if (aetherLevelText != null)
                aetherLevelText.text = level.ToString();
        }

        int into = AetherLevel.ExpIntoLevel;
        int need = AetherLevel.ExpForNextLevel;
        if (aetherExpText != null && (into != lastAetherExp || need != lastMaxAetherExp))
        {
            lastAetherExp = into;
            lastMaxAetherExp = need;
            aetherExpText.text = AetherLevel.IsMaxLevel ? "MAX" : into + " / " + need;
        }

        // 얻은 양 표시.
        if (aetherGainText != null)
        {
            float age = Time.unscaledTime - aetherGainShownAt;
            if (age <= AetherGainHold + AetherGainFade)
            {
                string chain = aetherGainMultiplier > 1.01f ? $"  ×{aetherGainMultiplier:0.##} 공명" : string.Empty;
                aetherGainText.text = $"+{aetherGainSum} AE{chain}   {aetherGainReason}";
                float alpha = age <= AetherGainHold ? 1f : 1f - (age - AetherGainHold) / AetherGainFade;
                Color c = aetherGainMultiplier > 1.01f ? new Color(1f, 0.82f, 0.48f) : Cyan(1f);
                c.a = 0.95f * Mathf.Clamp01(alpha);
                aetherGainText.color = c;
            }
            else if (aetherGainText.text.Length > 0)
            {
                aetherGainText.text = string.Empty;
            }
        }

        // 레벨이 오르면 패널이 한 번 부풀고 숫자가 청록으로 빛난다.
        if (aetherPanel != null)
        {
            float t = (Time.unscaledTime - aetherLevelUpAt) / AetherPulseSeconds;
            float pulse = t >= 0f && t < 1f ? Mathf.Sin(t * Mathf.PI) * 0.12f : 0f;
            aetherPanel.localScale = Vector3.one * CornerHudScale * (1f + pulse);
            if (aetherLevelText != null)
                aetherLevelText.color = pulse > 0f ? Color.Lerp(SoftWhite(0.88f), Cyan(1f), pulse / 0.12f) : SoftWhite(0.88f);
        }
    }

    private void UpdateCompass()
    {
        Transform yawSource = GetCompassYawSource();
        if (yawSource == null)
            return;

        float rawYaw = yawSource.eulerAngles.y;
        int yaw = Mathf.RoundToInt(rawYaw) % 360;
        if (yaw == lastCompassYaw)
            return;

        lastCompassYaw = yaw;
        if (compassDegreeText != null)
            compassDegreeText.text = yaw.ToString("000");

        const float pixelsPerDegree = 4.8f;
        int centerStep = Mathf.RoundToInt(rawYaw / 15f) * 15;

        if (compassContent != null)
        {
            float wrapped = Mathf.Repeat(rawYaw, 360f);
            compassContent.anchoredPosition = new Vector2(-wrapped * 1.15f, compassContent.anchoredPosition.y);
        }

        for (int i = 0; i < compassLabels.Length; i++)
        {
            if (compassLabels[i] == null)
                continue;

            int slotOffset = i - 4;
            int deg = NormalizeDegree(centerStep + slotOffset * 15);
            float x = Mathf.DeltaAngle(rawYaw, centerStep + slotOffset * 15) * pixelsPerDegree;
            RectTransform labelRect = compassLabels[i].rectTransform;
            labelRect.anchoredPosition = new Vector2(x, labelRect.anchoredPosition.y);

            bool centerLabel = Mathf.Abs(Mathf.DeltaAngle(rawYaw, centerStep + slotOffset * 15)) < 8f;
            compassLabels[i].text = centerLabel ? GetNearestDirectionLabel(deg) : GetCompassLabel(deg);
            compassLabels[i].fontStyle = centerLabel ? FontStyles.Bold : FontStyles.Normal;
            compassLabels[i].color = centerLabel ? Cyan(0.86f) : SoftWhite(HudSecondaryTextAlpha);
        }
    }

    private Transform GetCompassYawSource()
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            mainCamera = Camera.main;

        if (mainCamera != null && mainCamera.isActiveAndEnabled)
            return mainCamera.transform;

        Move_Hankyul hankyulMove = FindObjectOfType<Move_Hankyul>(true);
        if (hankyulMove != null)
        {
            if (hankyulMove.camTransform != null)
                return hankyulMove.camTransform;

            if (hankyulMove.orientationTransform != null)
                return hankyulMove.orientationTransform;

            return hankyulMove.transform;
        }

        if (moveYunseo == null)
            moveYunseo = FindActiveYunseoController();

        if (moveYunseo != null)
        {
            if (moveYunseo.camTransform != null)
                return moveYunseo.camTransform;

            if (moveYunseo.orientationTransform != null)
                return moveYunseo.orientationTransform;

            return moveYunseo.transform;
        }

        return null;
    }

    private void UpdateMinimap()
    {
        if (minimapImage == null)
            return;

        // Meka 는 미니맵을 쓰지 않는다. 칸이 꺼져 있으면 미니맵 카메라도 찍지 않는다
        // (05 장면에 놓인 미니맵 카메라는 아무도 안 보는 그림을 매 프레임 그리고 있었다).
        bool minimapShown = minimapPanel == null || minimapPanel.gameObject.activeInHierarchy;
        SetSceneMinimapCameras(minimapShown);
        if (!minimapShown)
            return;

        if (minimapImage.texture == null)
            minimapImage.texture = FindMinimapTexture();

        if (minimapImage.texture == null)
        {
            EnsureRuntimeMinimapCamera(true);
            if (runtimeMinimapTexture != null)
                minimapImage.texture = runtimeMinimapTexture;
        }

        PositionRuntimeMinimapCamera();

        bool hasTexture = minimapImage.texture != null;
        minimapImage.enabled = hasTexture;
        if (minimapPlaceholderText != null)
            minimapPlaceholderText.gameObject.SetActive(!hasTexture);
    }

    private bool sceneMinimapCamerasOn = true;
    private int sceneMinimapCamerasScene = -1;

    /// <summary>장면에 놓인 미니맵 카메라(그림을 텍스처로 찍는 카메라)를 켜고 끈다. 바뀔 때만 찾는다.</summary>
    private void SetSceneMinimapCameras(bool on)
    {
        int sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (on == sceneMinimapCamerasOn && sceneHandle == sceneMinimapCamerasScene)
            return;

        sceneMinimapCamerasOn = on;
        sceneMinimapCamerasScene = sceneHandle;
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == null || cam == runtimeMinimapCamera || cam.targetTexture == null)
                continue;
            if (cam.name.ToLowerInvariant().Contains("minimap") || cam.targetTexture.name.ToLowerInvariant().Contains("minimap"))
                cam.enabled = on;
        }
    }

    private void EnsureRuntimeMinimapCamera(bool createIfMissing)
    {
        if (MinimapTarget() == null)
            return;

        if (runtimeMinimapCamera != null && runtimeMinimapTexture != null)
            return;

        if (!createIfMissing)
            return;

        // 투명도 칸이 없는 형식을 쓴다. ARGB32 로는 그려진 물체의 투명도가 0 으로 남아
        // 미니맵 칸에 뒤의 게임 화면(04 벽 등)이 비쳐 보였다.
        RenderTextureFormat minimapFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
            ? RenderTextureFormat.RGB111110Float
            : RenderTextureFormat.RGB565;
        runtimeMinimapTexture = new RenderTexture(512, 512, 16, minimapFormat);
        runtimeMinimapTexture.name = "RT_YunseoRuntimeMinimap";
        runtimeMinimapTexture.Create();

        GameObject cameraObject = new GameObject("YunseoRuntimeMinimapCamera");
        DontDestroyOnLoad(cameraObject);
        runtimeMinimapCamera = cameraObject.AddComponent<Camera>();
        runtimeMinimapCamera.clearFlags = CameraClearFlags.SolidColor;
        runtimeMinimapCamera.backgroundColor = new Color(0.025f, 0.055f, 0.075f, 1f);
        runtimeMinimapCamera.orthographic = true;
        runtimeMinimapCamera.orthographicSize = 22f;
        runtimeMinimapCamera.nearClipPlane = 0.3f;
        runtimeMinimapCamera.farClipPlane = 200f;
        runtimeMinimapCamera.depth = -20f;
        runtimeMinimapCamera.targetTexture = runtimeMinimapTexture;
        PositionRuntimeMinimapCamera();
    }

    private void PositionRuntimeMinimapCamera()
    {
        if (runtimeMinimapCamera == null || MinimapTarget() == null)
            return;

        Transform minimapTarget = MinimapTarget();
        Vector3 targetPosition = minimapTarget.position;
        runtimeMinimapCamera.transform.position = targetPosition + Vector3.up * 55f;

        // 실내(04 벙커·06 연구소·07 제작실)에서는 위에서 찍으면 지붕만 보여 미니맵이 회색 판이었다.
        // 머리 위에 천장이 있으면 그 바로 아래부터 그린다.
        float near = 0.3f;
        bool indoor = false;
        if (Physics.Raycast(targetPosition + Vector3.up * 1.5f, Vector3.up, out RaycastHit ceiling, 12f, ~0, QueryTriggerInteraction.Ignore)
            && !ceiling.collider.transform.IsChildOf(minimapTarget.root))
        {
            near = Mathf.Max(0.3f, 55f - (1.5f + ceiling.distance) + 0.05f);
            indoor = true;
        }
        runtimeMinimapCamera.nearClipPlane = near;
        ApplyMinimapBoost(indoor ? 3.5f : 1.4f);
        runtimeMinimapCamera.transform.rotation = Quaternion.Euler(90f, minimapTarget.eulerAngles.y, 0f);

        // The minimap camera drew the whole area from above every frame (3,000+ draws in the bunker, as much as the main view).
        // The minimap barely changes, so the automatic draw is off and it draws 8 times a second.
        if (runtimeMinimapCamera.enabled)
            runtimeMinimapCamera.enabled = false;
        if (Time.unscaledTime >= nextMinimapRender)
        {
            nextMinimapRender = Time.unscaledTime + 0.125f;
            // 55m 위에서 찍으니 장면 안개가 그림 전체를 거의 검게 덮었다. 미니맵을 찍는 동안만 안개를 끈다.
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            runtimeMinimapCamera.Render();
            RenderSettings.fog = fog;
        }
    }

    private float nextMinimapRender;
    private Material minimapBoostMaterial;

    private void ApplyMinimapBoost(float gain)
    {
        if (minimapImage == null || runtimeMinimapTexture == null || minimapImage.texture != runtimeMinimapTexture)
            return;
        if (minimapBoostMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("UI/AetherMinimapBoost");
            if (shader == null)
                return;
            minimapBoostMaterial = new Material(shader);
        }
        minimapBoostMaterial.SetFloat("_Gain", gain);
        if (minimapImage.material != minimapBoostMaterial)
            minimapImage.material = minimapBoostMaterial;
    }

    /// <summary>미니맵이 따라갈 지금 플레이어(Shiri·Meka 공통).</summary>
    private Transform MinimapTarget()
    {
        if (moveYunseo != null && moveYunseo.isActiveAndEnabled)
            return moveYunseo.transform;
        if (hankyulFire != null)
            return hankyulFire.transform.root;
        return AetherPlayerCharacter.FindActiveTransform();
    }

    private static Texture FindMinimapTexture()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.targetTexture == null)
                continue;

            string cameraName = camera.name.ToLowerInvariant();
            string textureName = camera.targetTexture.name.ToLowerInvariant();
            if (cameraName.Contains("minimap") || textureName.Contains("minimap"))
                return camera.targetTexture;
        }

        RawImage[] rawImages = FindObjectsOfType<RawImage>(true);
        for (int i = 0; i < rawImages.Length; i++)
        {
            RawImage rawImage = rawImages[i];
            if (rawImage == null || rawImage.texture == null)
                continue;

            if (rawImage.GetComponentInParent<AetherModernHUD>(true) != null)
                continue;

            if (rawImage.name.ToLowerInvariant().Contains("minimap") || rawImage.texture.name.ToLowerInvariant().Contains("minimap"))
                return rawImage.texture;
        }

        return null;
    }

    private static int NormalizeDegree(int degree)
    {
        degree %= 360;
        if (degree < 0) degree += 360;
        return degree;
    }

    private static string GetCompassLabel(int degree)
    {
        int rounded = Mathf.RoundToInt(degree / 15f) * 15;
        rounded = NormalizeDegree(rounded);

        switch (rounded)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return rounded.ToString();
        }
    }

    private static string GetNearestDirectionLabel(int degree)
    {
        int rounded = Mathf.RoundToInt(degree / 45f) * 45;
        rounded = NormalizeDegree(rounded);

        switch (rounded)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return "N";
        }
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color backgroundColor)
    {
        RectTransform panel = CreateEmpty(parent, name, anchor, anchor, pivot, anchoredPosition, size);
        RectTransform background = CreateEmpty(panel, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        Image image = background.gameObject.AddComponent<Image>();
        image.sprite = SolidSprite;
        image.color = backgroundColor;
        image.raycastTarget = false;
        return panel;
    }

    private RectTransform CreateTexturePanel(RectTransform parent, string name, string resourcePath, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform panel = CreateEmpty(parent, name, anchor, anchor, pivot, anchoredPosition, size);
        CreateTextureImage(panel, "Background", resourcePath, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return panel;
    }

    private RawImage CreateTextureImage(RectTransform parent, string name, string resourcePath, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        if (anchorMin != anchorMax)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>(resourcePath);
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void SetTexturePanelAlpha(RectTransform panel, float alpha)
    {
        if (panel == null)
            return;

        RawImage background = panel.Find("Background") != null ? panel.Find("Background").GetComponent<RawImage>() : null;
        if (background != null)
            background.color = new Color(1f, 1f, 1f, alpha);
    }

    private Image CreateSolidImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = SolidSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        return CreateSolidImage(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size, color).rectTransform;
    }

    private TextMeshProUGUI CreateTMP(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, string value, int fontSize, TextAlignmentOptions alignment, Color color)
    {
        return CreateTMP(parent, name, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, size, value, fontSize, alignment, color);
    }

    private TextMeshProUGUI CreateTMP(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, string value, int fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.text = value;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
        return text;
    }

    private void ResolveUIFont()
    {
        uiFont = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");
        if (uiFont == null)
            uiFont = TMP_Settings.defaultFontAsset;
    }

    private static void ConfigureNumericText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = text.fontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        text.characterSpacing = 0f;
    }

    private static RectTransform CreateEmpty(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Vector2 AnchorTopLeft() { return new Vector2(0f, 1f); }
    private static Vector2 AnchorTopCenter() { return new Vector2(0.5f, 1f); }
    private static Vector2 AnchorTopRight() { return new Vector2(1f, 1f); }
    private static Vector2 AnchorBottomLeft() { return new Vector2(0f, 0f); }
    private static Vector2 AnchorBottomRight() { return new Vector2(1f, 0f); }
    private static Vector2 AnchorBottomCenter() { return new Vector2(0.5f, 0f); }
    private static Vector2 AnchorMiddleLeft() { return new Vector2(0f, 0.5f); }
    private static Vector2 AnchorMiddleRight() { return new Vector2(1f, 0.5f); }
    private static Vector2 AnchorCenter() { return new Vector2(0.5f, 0.5f); }

    private static Color Cyan(float alpha)
    {
        return new Color(0.30f, 0.84f, 1f, alpha);
    }

    private static Color SoftWhite(float alpha)
    {
        return new Color(0.9f, 0.96f, 1f, alpha);
    }

    private static Sprite solidSprite;
    private static Sprite SolidSprite
    {
        get
        {
            if (solidSprite != null)
                return solidSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            return solidSprite;
        }
    }
}
