using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public class ElevatorFloorOption
{
    public string floorTitle = "옥상";
    public string floorSubtitle = "1구역";
    public string sceneName = "08.1구역_2";
    public bool unlocked = true;
}

public class ElevatorFloorSelectionUI : MonoBehaviour
{
    [Header("?곹깭")]
    [SerializeField] private ElevatorFloorOption[] floors =
    {
        new ElevatorFloorOption { floorTitle = "옥상", floorSubtitle = "1구역", sceneName = "08.1구역_2", unlocked = true },
        new ElevatorFloorOption { floorTitle = "2구역", floorSubtitle = "SECTION 02", sceneName = "09.2구역_2", unlocked = false },
        new ElevatorFloorOption { floorTitle = "3구역", floorSubtitle = "SECTION 03", sceneName = "11.3구역_찐", unlocked = false },
        new ElevatorFloorOption { floorTitle = "1층 연구실", floorSubtitle = "LAB 1F", sceneName = "06.연구소 메인 장소", unlocked = true }
    };

    [Header("?곗텧")]
    [SerializeField] private float fadeDuration = 0.16f;
    [SerializeField] private TMP_FontAsset uiFontAsset;
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.28f);
    [SerializeField] private Color rowHoverColor = new Color(1f, 1f, 1f, 0.42f);
    [SerializeField] private Color rowPressedColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color rowLockedColor = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color rowBorderColor = new Color(1f, 1f, 1f, 0.88f);
    [SerializeField] private Color rowLockedBorderColor = new Color(1f, 1f, 1f, 0.38f);
    [SerializeField] private Color mainTextColor = new Color(0.08f, 0.10f, 0.12f, 0.96f);
    [SerializeField] private Color subTextColor = new Color(0.18f, 0.22f, 0.26f, 0.78f);
    [SerializeField] private Color lockedTextColor = new Color(0.30f, 0.32f, 0.35f, 0.58f);

    [Header("Events")]
    [SerializeField] private UnityEvent onOpened;
    [SerializeField] private UnityEvent onClosed;

    public event Action<ElevatorFloorOption> FloorSelected;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform rowContainer;
    private Coroutine fadeRoutine;
    private bool built;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// 층 안내판에 쓸 글꼴. 인스펙터에서 꽂아 두지 않았으면 여기서 채운다.
    ///
    /// 엘리베이터로 씬을 옮기면 타고 온 칸이 따라오는데, 씬에 있던 이 안내판은 같이 오지 못해서
    /// 도착한 뒤에는 코드로 새로 만들어진다. 그때는 인스펙터 값이 비어 있어 기본 영문 글꼴이 잡히고,
    /// "옥상 1구역" 같은 한글이 전부 네모로 나왔다. HUD와 같은 한글 글꼴로 맞춘다.
    /// </summary>
    private const string FallbackFontResource = "UI/Fonts/NanumGothicCoding-Bold SDF";

    private void Awake()
    {
        EnsureFontAsset();
        EnsureBuilt();
        HideImmediate();
    }

    private void EnsureFontAsset()
    {
        if (uiFontAsset != null)
            return;

        uiFontAsset = Resources.Load<TMP_FontAsset>(FallbackFontResource);

        if (uiFontAsset == null)
            uiFontAsset = TMP_Settings.defaultFontAsset;
    }

    public void ConfigureFloors(ElevatorFloorOption[] configuredFloors)
    {
        if (configuredFloors == null || configuredFloors.Length == 0)
            return;

        floors = configuredFloors;
        ApplyProgressLocks();

        if (built)
            RebuildFloorRows();
    }

    public void Open()
    {
        EnsureBuilt();
        ApplyProgressLocks();
        RebuildFloorRows();
        gameObject.SetActive(true);
        IsOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeCanvas(1f));
        onOpened?.Invoke();
    }

    public void Close()
    {
        if (!built)
            return;

        IsOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeCanvas(0f));
        onClosed?.Invoke();
    }

    public void HideImmediate()
    {
        EnsureBuilt();
        IsOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeCanvas(float targetAlpha)
    {
        canvasGroup.interactable = targetAlpha > 0.5f;
        canvasGroup.blocksRaycasts = targetAlpha > 0.5f;

        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, fadeDuration));
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Smooth01(t));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void EnsureBuilt()
    {
        if (built)
            return;

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        BuildEventSystemIfNeeded();
        BuildHierarchy();
        built = true;
    }

    private void BuildHierarchy()
    {
        RectTransform root = GetOrAddRectTransform(gameObject);
        ClearChildren(root);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        GameObject containerObject = new GameObject("Floor Row Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
        containerObject.transform.SetParent(root, false);
        rowContainer = containerObject.GetComponent<RectTransform>();
        rowContainer.anchorMin = new Vector2(0.5f, 0.5f);
        rowContainer.anchorMax = new Vector2(0.5f, 0.5f);
        rowContainer.pivot = new Vector2(0.5f, 0.5f);
        rowContainer.sizeDelta = new Vector2(760f, 390f);
        rowContainer.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = containerObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 24f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RebuildFloorRows();
    }

    private void RebuildFloorRows()
    {
        if (rowContainer == null)
            return;

        ClearChildren(rowContainer);
        ResizeRowContainerForFloorCount();

        for (int i = 0; i < floors.Length; i++)
            BuildFloorRow(floors[i], i);
    }

    private void ApplyProgressLocks()
    {
        if (floors == null)
            return;

        for (int i = 0; i < floors.Length; i++)
        {
            ElevatorFloorOption floor = floors[i];
            if (floor == null)
                continue;

            if (IsZoneFloor(floor, 1))
                floor.unlocked = true;
            else if (IsZoneFloor(floor, 2))
                floor.unlocked = ElevatorZoneProgress.IsZoneUnlocked(2);
            else if (IsZoneFloor(floor, 3))
                floor.unlocked = ElevatorZoneProgress.IsZoneUnlocked(3);
            // 제작실 판정은 연구실보다 먼저 봐야 한다. 연구실 판정이 부제의 'LAB'을 잡아
            // 제작실까지 항상 열어 버리기 때문이다.
            else if (IsPartsWorkshopFloor(floor))
                floor.unlocked = ElevatorZoneProgress.IsAnyZoneCleared();
            else if (IsLabFloor(floor))
                floor.unlocked = true;
        }
    }

    private static bool IsPartsWorkshopFloor(ElevatorFloorOption floor)
    {
        string title = floor?.floorTitle ?? string.Empty;
        string scene = floor?.sceneName ?? string.Empty;
        return title.Contains("제작") || scene.Contains("파츠 제작실");
    }

    private static bool IsZoneFloor(ElevatorFloorOption floor, int zoneNumber)
    {
        string title = floor?.floorTitle ?? string.Empty;
        string subtitle = floor?.floorSubtitle ?? string.Empty;
        string scene = floor?.sceneName ?? string.Empty;
        string zoneText = $"{zoneNumber}구역";
        return title.Contains(zoneText) || subtitle.Contains(zoneText) || scene.Contains(zoneText);
    }

    private static bool IsLabFloor(ElevatorFloorOption floor)
    {
        string title = floor?.floorTitle ?? string.Empty;
        string subtitle = floor?.floorSubtitle ?? string.Empty;
        string scene = floor?.sceneName ?? string.Empty;
        return title.Contains("연구") || subtitle.Contains("LAB") || scene.Contains("연구소");
    }

    private void ResizeRowContainerForFloorCount()
    {
        int floorCount = floors == null ? 0 : floors.Length;
        if (floorCount <= 0)
            floorCount = 1;

        const float rowHeight = 114f;
        const float rowSpacing = 24f;
        float height = rowHeight * floorCount + rowSpacing * Mathf.Max(0, floorCount - 1);
        rowContainer.sizeDelta = new Vector2(760f, height);
    }

    private void BuildFloorRow(ElevatorFloorOption floor, int index)
    {
        bool unlocked = floor != null && floor.unlocked;
        GameObject row = new GameObject($"Floor Row {index + 1}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(rowContainer, false);

        LayoutElement layoutElement = row.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 114f;
        layoutElement.minHeight = 114f;

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = unlocked ? rowColor : rowLockedColor;

        Outline outline = row.GetComponent<Outline>();
        outline.effectColor = unlocked ? rowBorderColor : rowLockedBorderColor;
        outline.effectDistance = unlocked ? new Vector2(2.5f, 2.5f) : new Vector2(1.5f, 1.5f);

        Button button = row.GetComponent<Button>();
        button.interactable = unlocked;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = rowHoverColor;
        colors.pressedColor = rowPressedColor;
        colors.selectedColor = rowHoverColor;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        if (floor != null)
        {
            ElevatorFloorOption capturedFloor = floor;
            button.onClick.AddListener(() => HandleFloorClicked(capturedFloor));
        }

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 114f);

        string title = BuildDisplayTitle(floor);
        TMP_Text titleText = CreateText("Floor Title", rowRect, title, 36f, unlocked ? mainTextColor : lockedTextColor, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Left;
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.offsetMin = new Vector2(34f, -4f);
        titleRect.offsetMax = new Vector2(unlocked ? -250f : -126f, 46f);

        string statusTextValue = unlocked ? "ACCESS GRANTED" : "LOCKED";
        TMP_Text statusText = CreateText("Floor Status", rowRect, statusTextValue, 24f, unlocked ? subTextColor : lockedTextColor, FontStyles.Bold);
        statusText.alignment = TextAlignmentOptions.Left;
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0.5f);
        statusRect.anchorMax = new Vector2(1f, 0.5f);
        statusRect.pivot = new Vector2(0f, 0.5f);
        statusRect.offsetMin = new Vector2(34f, -42f);
        statusRect.offsetMax = new Vector2(-260f, -10f);

        if (unlocked)
            BuildRightLabel(rowRect, "SELECT");
        else
            BuildLockIcon(rowRect);
    }

    private string BuildDisplayTitle(ElevatorFloorOption floor)
    {
        if (floor == null)
            return "";

        if (string.IsNullOrEmpty(floor.floorSubtitle))
            return floor.floorTitle;

        return $"{floor.floorTitle}  {floor.floorSubtitle}";
    }

    private void BuildRightLabel(RectTransform rowRect, string text)
    {
        TMP_Text rightText = CreateText("Right Label", rowRect, text, 24f, subTextColor, FontStyles.Bold);
        rightText.alignment = TextAlignmentOptions.Right;
        RectTransform rightRect = rightText.rectTransform;
        rightRect.anchorMin = new Vector2(1f, 0.5f);
        rightRect.anchorMax = new Vector2(1f, 0.5f);
        rightRect.pivot = new Vector2(1f, 0.5f);
        rightRect.sizeDelta = new Vector2(180f, 44f);
        rightRect.anchoredPosition = new Vector2(-32f, 0f);
    }

    private void BuildLockIcon(RectTransform rowRect)
    {
        Color lockColor = new Color(0.30f, 0.32f, 0.35f, 0.58f);

        GameObject iconRoot = new GameObject("Lock Icon", typeof(RectTransform));
        iconRoot.transform.SetParent(rowRect, false);
        RectTransform iconRect = iconRoot.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(70f, 70f);
        iconRect.anchoredPosition = new Vector2(-48f, 0f);

        Image body = CreateImage("Lock Body", iconRect, lockColor);
        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 0f);
        bodyRect.anchorMax = new Vector2(0.5f, 0f);
        bodyRect.pivot = new Vector2(0.5f, 0f);
        bodyRect.sizeDelta = new Vector2(40f, 32f);
        bodyRect.anchoredPosition = new Vector2(0f, 8f);
        body.raycastTarget = false;

        Image shackleLeft = CreateImage("Lock Shackle Left", iconRect, lockColor);
        RectTransform leftRect = shackleLeft.rectTransform;
        leftRect.anchorMin = new Vector2(0.5f, 0.5f);
        leftRect.anchorMax = new Vector2(0.5f, 0.5f);
        leftRect.pivot = new Vector2(0.5f, 0.5f);
        leftRect.sizeDelta = new Vector2(7f, 30f);
        leftRect.anchoredPosition = new Vector2(-15f, 15f);
        shackleLeft.raycastTarget = false;

        Image shackleTop = CreateImage("Lock Shackle Top", iconRect, lockColor);
        RectTransform topRect = shackleTop.rectTransform;
        topRect.anchorMin = new Vector2(0.5f, 0.5f);
        topRect.anchorMax = new Vector2(0.5f, 0.5f);
        topRect.pivot = new Vector2(0.5f, 0.5f);
        topRect.sizeDelta = new Vector2(37f, 7f);
        topRect.anchoredPosition = new Vector2(0f, 28f);
        shackleTop.raycastTarget = false;

        Image shackleRight = CreateImage("Lock Shackle Right", iconRect, lockColor);
        RectTransform rightRect = shackleRight.rectTransform;
        rightRect.anchorMin = new Vector2(0.5f, 0.5f);
        rightRect.anchorMax = new Vector2(0.5f, 0.5f);
        rightRect.pivot = new Vector2(0.5f, 0.5f);
        rightRect.sizeDelta = new Vector2(7f, 30f);
        rightRect.anchoredPosition = new Vector2(15f, 15f);
        shackleRight.raycastTarget = false;

        Image keyhole = CreateImage("Lock Keyhole", iconRect, new Color(0.10f, 0.11f, 0.12f, 0.48f));
        RectTransform keyholeRect = keyhole.rectTransform;
        keyholeRect.anchorMin = new Vector2(0.5f, 0f);
        keyholeRect.anchorMax = new Vector2(0.5f, 0f);
        keyholeRect.pivot = new Vector2(0.5f, 0f);
        keyholeRect.sizeDelta = new Vector2(8f, 16f);
        keyholeRect.anchoredPosition = new Vector2(0f, 15f);
        keyhole.raycastTarget = false;
    }

    private void HandleFloorClicked(ElevatorFloorOption floor)
    {
        if (floor == null || !floor.unlocked)
            return;

        FloorSelected?.Invoke(floor);
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        if (uiFontAsset != null)
            tmp.font = uiFontAsset;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform GetOrAddRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
            rect = target.AddComponent<RectTransform>();

        return rect;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static float Smooth01(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static void BuildEventSystemIfNeeded()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }
}
