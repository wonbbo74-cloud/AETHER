using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제작설계서(종이)를 더블클릭하면 뜨는 설계서 그림.
///
/// 예전 그림(PaperInventoryPreview)은 파츠 제작 화면이 5x5 격자이던 때 만든 것이라, 지금 제작 화면(도면 슬롯 다섯 칸)과
/// 맞지 않았다. 이제 제작 화면 도면과 같은 모양(가운데 파츠, 1 동력·2 제어·3 보조 모듈, 4·5 보조 슬롯)으로
/// 제작법 데이터에서 직접 그린다. 그래서 그림과 실제 제작 판정이 항상 같다.
/// 제작법을 찾지 못한 종이는 예전 그림을 그대로 보여 준다.
/// </summary>
public class PaperInventoryPreviewUI : MonoBehaviour
{
    private const string PreviewSpritePath = "AETHERParts/PaperInventoryPreview";
    private const string FallbackSpritePath = "AETHERParts/PaperRecipePreview";

    // 제작 화면과 같은 색
    private static readonly Color PanelColor = new Color(0.027f, 0.067f, 0.11f, 0.98f);
    private static readonly Color GridColor = new Color(0.45f, 0.83f, 1f, 0.06f);
    private static readonly Color LineColor = new Color(0.45f, 0.83f, 1f, 0.55f);
    private static readonly Color SlotColor = new Color(0.05f, 0.11f, 0.17f, 1f);
    private static readonly Color BorderColor = new Color(0.45f, 0.83f, 1f, 0.75f);

    private const float PanelWidth = 1180f;
    private const float PanelHeight = 780f;
    private static readonly Vector2 SlotSize = new Vector2(150f, 128f);

    // 제작 화면 도면 슬롯 중심을 가운데(파츠) 기준으로 줄인 자리 (제작 화면 배경 3840 폭 → 0.47배)
    private static readonly Vector2[] SlotOffsets =
    {
        new Vector2(0f, 175f),
        new Vector2(-250f, 5f),
        new Vector2(250f, 5f),
        new Vector2(-112f, -205f),
        new Vector2(112f, -205f),
    };
    private static readonly Vector2 DiagramCenter = new Vector2(0f, -10f);

    private static PaperInventoryPreviewUI instance;

    private CanvasGroup canvasGroup;
    private Image previewImage;
    private RectTransform blueprintRoot;
    private ItemData currentItem;
    private CraftingRecipeData currentRecipe;

    public static void Toggle(ItemData item)
    {
        if (item == null)
            return;

        PaperInventoryPreviewUI ui = GetOrCreate();
        if (ui.gameObject.activeSelf && ui.currentItem == item)
        {
            ui.Hide();
            return;
        }

        ui.Show(item, AetherBlueprint.FindRecipeFor(item));
    }

    /// <summary>제작 화면에서 [B]·[?] 로 설계서를 펼칠 때.</summary>
    public static void ShowRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null)
            return;

        PaperInventoryPreviewUI ui = GetOrCreate();
        if (ui.gameObject.activeSelf && ui.currentRecipe == recipe)
        {
            ui.Hide();
            return;
        }

        ui.Show(PartResourceResolver.FindItemData(AetherBlueprint.BlueprintId(recipe)), recipe);
    }

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    /// <summary>
    /// 이번(또는 바로 전) 프레임에 설계서가 닫혔는지. 같은 ESC 로 뒤의 화면(제작 화면·작업대)까지 닫히지 않게
    /// ESC 를 받는 다른 곳이 확인한다. 어느 쪽 Update 가 먼저 도는지와 상관없이 맞게 동작한다.
    /// </summary>
    public static bool ClosedThisFrame => Time.frameCount - closedFrame <= 1;
    private static int closedFrame = -10;

    public static void HideIfOpen()
    {
        if (instance != null)
            instance.Hide();
    }

    private static PaperInventoryPreviewUI GetOrCreate()
    {
        if (instance != null)
            return instance;

        GameObject canvasObject = new GameObject("Paper Inventory Preview UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        if (Application.isPlaying)
            Object.DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 인벤토리(30000)·파츠 제작 화면(30020) 위에 와야 한다.
        canvas.sortingOrder = 31000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        instance = canvasObject.AddComponent<PaperInventoryPreviewUI>();
        instance.BuildUI(canvasObject.GetComponent<CanvasGroup>());
        instance.Hide();
        return instance;
    }

    private void BuildUI(CanvasGroup group)
    {
        canvasGroup = group;

        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dimObject.transform.SetParent(transform, false);
        Stretch(dimObject.GetComponent<RectTransform>());

        Image dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        Button dimButton = dimObject.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Hide);

        GameObject imageObject = new GameObject("Paper Preview Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = imageRect.anchorMax = imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;

        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;
    }

    private void Update()
    {
        // 인벤토리·제작 화면이 열려 있으면 그쪽이 ESC 를 받아 설계서만 닫는다(같은 프레임에 둘 다 닫히지 않게).
        if (gameObject.activeSelf && !AetherInventoryScreen.IsOpen && !AetherPartsCraftingScreen.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Hide();

        if (gameObject.activeSelf)
            EnsureUICursor();
    }

    private void LateUpdate()
    {
        if (gameObject.activeSelf)
            EnsureUICursor();
    }

    private void Show(ItemData item, CraftingRecipeData recipe)
    {
        currentItem = item;
        currentRecipe = recipe;

        if (blueprintRoot != null)
            Destroy(blueprintRoot.gameObject);
        blueprintRoot = null;

        if (recipe != null)
        {
            previewImage.enabled = false;
            BuildBlueprint(recipe, item);
        }
        else
        {
            ShowLegacyImage();
        }

        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        AetherAudio.Play2D("paper_open", 0.55f, 1f, 0f);
        EnsureUICursor();
    }

    private void ShowLegacyImage()
    {
        Sprite sprite = Resources.Load<Sprite>(PreviewSpritePath);
        if (sprite == null)
            sprite = Resources.Load<Sprite>(FallbackSpritePath);

        previewImage.sprite = sprite;
        previewImage.enabled = sprite != null;

        RectTransform rect = previewImage.rectTransform;
        if (sprite != null)
        {
            Vector2 size = sprite.rect.size * 2f;
            size.x = Mathf.Min(size.x, 1400f);
            size.y = Mathf.Min(size.y, 820f);
            rect.sizeDelta = size;
        }
        else
        {
            rect.sizeDelta = new Vector2(1100f, 620f);
        }
    }

    // ------------------------------------------------------------------ 설계서 그림

    private void BuildBlueprint(CraftingRecipeData recipe, ItemData paper)
    {
        blueprintRoot = NewRect("Blueprint", transform, Vector2.zero, new Vector2(PanelWidth, PanelHeight));
        Image panel = blueprintRoot.gameObject.AddComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = true; // 설계서를 눌러도 닫히지 않게(바깥 어두운 곳을 누르면 닫힌다)
        AddBorder(blueprintRoot.gameObject, BorderColor, 2f);

        // 청사진 격자
        for (float x = -PanelWidth * 0.5f + 40f; x < PanelWidth * 0.5f; x += 40f)
            Fill(NewRect("GridV", blueprintRoot, new Vector2(x, 0f), new Vector2(1f, PanelHeight - 4f)), GridColor);
        for (float y = -PanelHeight * 0.5f + 40f; y < PanelHeight * 0.5f; y += 40f)
            Fill(NewRect("GridH", blueprintRoot, new Vector2(0f, y), new Vector2(PanelWidth - 4f, 1f)), GridColor);

        // 머리글
        string paperName = paper != null && !string.IsNullOrWhiteSpace(paper.displayName) ? paper.displayName : "제작설계서";
        Text(blueprintRoot, "Kind", new Vector2(-PanelWidth * 0.5f + 40f, PanelHeight * 0.5f - 46f), new Vector2(700f, 30f),
            "BLUEPRINT  ·  " + paperName, 20f, AetherScreenKit.Accent, TextAlignmentOptions.MidlineLeft);
        Text(blueprintRoot, "Title", new Vector2(-PanelWidth * 0.5f + 40f, PanelHeight * 0.5f - 90f), new Vector2(800f, 50f),
            PartName(recipe), 40f, AetherScreenKit.TextBright, TextAlignmentOptions.MidlineLeft);
        Text(blueprintRoot, "Close", new Vector2(PanelWidth * 0.5f - 40f, PanelHeight * 0.5f - 46f), new Vector2(300f, 30f),
            "ESC  닫기", 20f, AetherScreenKit.TextDim, TextAlignmentOptions.MidlineRight);
        Fill(NewRect("HeadLine", blueprintRoot, new Vector2(0f, PanelHeight * 0.5f - 124f), new Vector2(PanelWidth - 80f, 2f)), LineColor);

        // 연결선: 가운데 → 1(위), 2(왼쪽), 3(오른쪽), 아래로 내려가 4·5 를 잇는 가로줄
        Vector2 c = DiagramCenter;
        Line(c, c + SlotOffsets[0]);
        Line(c + SlotOffsets[1], c + SlotOffsets[2]);
        float busY = c.y + SlotOffsets[3].y + 100f;
        Line(c, new Vector2(c.x, busY));
        Line(new Vector2(c.x + SlotOffsets[3].x, busY), new Vector2(c.x + SlotOffsets[4].x, busY));
        Line(new Vector2(c.x + SlotOffsets[3].x, busY), c + SlotOffsets[3]);
        Line(new Vector2(c.x + SlotOffsets[4].x, busY), c + SlotOffsets[4]);

        // 가운데: 만들어질 파츠
        RectTransform core = NewRect("Core", blueprintRoot, c, new Vector2(104f, 104f));
        Fill(core, SlotColor);
        AddBorder(core.gameObject, BorderColor, 2f);
        core.localRotation = Quaternion.Euler(0f, 0f, 45f);
        if (recipe.resultPart != null && recipe.resultPart.itemSprite != null)
        {
            RectTransform partIcon = NewRect("PartIcon", blueprintRoot, c, new Vector2(84f, 84f));
            Image icon = partIcon.gameObject.AddComponent<Image>();
            icon.sprite = recipe.resultPart.itemSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        // 다섯 칸: 설계서가 정한 모듈
        int count = recipe.expectedPositions != null ? recipe.expectedPositions.Length : 0;
        for (int i = 0; i < SlotOffsets.Length; i++)
        {
            ItemData module = i < count ? PartResourceResolver.FindItemData(recipe.expectedPositions[i].itemId) : null;
            BuildSlot(i, c + SlotOffsets[i], module, i < count ? recipe.expectedPositions[i].itemId : null);
        }

        // 아래 안내
        Fill(NewRect("FootLine", blueprintRoot, new Vector2(0f, -PanelHeight * 0.5f + 70f), new Vector2(PanelWidth - 80f, 2f)), LineColor);
        Text(blueprintRoot, "Foot", new Vector2(0f, -PanelHeight * 0.5f + 38f), new Vector2(PanelWidth - 80f, 36f),
            "파츠 제작실 작업대의 도면 슬롯에 이 그림과 같은 자리로 모듈을 놓아야 제작됩니다.", 22f,
            AetherScreenKit.TextDim, TextAlignmentOptions.Center);
    }

    private void BuildSlot(int index, Vector2 center, ItemData module, string fallbackId)
    {
        Text(blueprintRoot, "SlotTitle" + index, center + new Vector2(0f, SlotSize.y * 0.5f + 18f), new Vector2(220f, 28f),
            AetherBlueprint.SlotTitles[index], 20f, AetherScreenKit.TextDim, TextAlignmentOptions.Center);

        RectTransform slot = NewRect("Slot" + index, blueprintRoot, center, SlotSize);
        Fill(slot, SlotColor);
        AddBorder(slot.gameObject, module != null || fallbackId != null ? BorderColor : new Color(0.45f, 0.83f, 1f, 0.25f), 2f);

        if (module == null && fallbackId == null)
        {
            Text(slot, "Empty", Vector2.zero, SlotSize, "비움", 20f, new Color(0.62f, 0.72f, 0.8f, 0.5f), TextAlignmentOptions.Center);
            return;
        }

        if (module != null && module.itemSprite != null)
        {
            RectTransform iconRect = NewRect("Icon", slot, new Vector2(0f, 14f), new Vector2(78f, 78f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = module.itemSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        string name = module != null && !string.IsNullOrWhiteSpace(module.displayName) ? module.displayName : fallbackId;
        TMP_Text label = Text(slot, "Name", new Vector2(0f, -SlotSize.y * 0.5f + 18f), new Vector2(SlotSize.x - 12f, 26f),
            name, 19f, AetherScreenKit.TextBright, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;   // "강화파츠 제작설계서" 처럼 긴 이름은 칸에 맞게 줄인다
        label.fontSizeMin = 12f;
        label.fontSizeMax = 19f;
    }

    private static string PartName(CraftingRecipeData recipe)
    {
        PartData part = recipe.resultPart;
        if (part != null && !string.IsNullOrWhiteSpace(part.displayName))
            return part.displayName;
        return part != null ? part.name : recipe.name;
    }

    private void Line(Vector2 a, Vector2 b)
    {
        Vector2 mid = (a + b) * 0.5f;
        bool horizontal = Mathf.Abs(a.y - b.y) < 0.5f;
        Vector2 size = horizontal ? new Vector2(Mathf.Abs(a.x - b.x), 2f) : new Vector2(2f, Mathf.Abs(a.y - b.y));
        Fill(NewRect("Line", blueprintRoot, mid, size), LineColor);
    }

    // ------------------------------------------------------------------ 도움

    private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Fill(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void AddBorder(GameObject go, Color color, float width)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
        Outline outline2 = go.AddComponent<Outline>();
        outline2.effectColor = color;
        outline2.effectDistance = new Vector2(-width, width);
    }

    private static TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent, position, size);
        // 왼쪽 정렬 글자는 position 이 왼쪽 끝, 오른쪽 정렬은 오른쪽 끝이 되게 한다(가운데 기준이면 상자 밖으로 밀려났다).
        int horizontal = (int)alignment & 0xFF;   // TMP: 1 = 왼쪽, 2 = 가운데, 4 = 오른쪽
        if (horizontal == 1)
            rect.pivot = new Vector2(0f, 0.5f);
        else if (horizontal == 4)
            rect.pivot = new Vector2(1f, 0.5f);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Hide()
    {
        if (gameObject.activeSelf)
            closedFrame = Time.frameCount;
        currentItem = null;
        currentRecipe = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private static void EnsureUICursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
