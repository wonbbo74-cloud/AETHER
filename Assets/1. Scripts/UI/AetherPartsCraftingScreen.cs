using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 디자인 이미지(Resources/UI/PartsCraftingPanel)를 배경으로 쓰는 파츠 제작 화면.
///
/// 왼쪽에서 제작설계서(레시피)를 고르고, 오른쪽 보유 모듈을 집어 도면의 슬롯에 놓은 뒤
/// '제작 시작'을 누르면 파츠가 만들어진다.
/// 기존 레시피는 5x5 격자 좌표로 저장돼 있어, 순서대로 도면 슬롯 1~5에 대응시킨다.
/// </summary>
public class AetherPartsCraftingScreen : MonoBehaviour
{
    private const string SpritePath = "UI/PartsCraftingPanel";
    private const int RecipeRows = 5;
    private const int ModuleCells = 8;
    private const int DiagramSlots = 5;

    private static readonly float[] RecipeY = { 496f, 784f, 1068f, 1352f, 1636f };
    private const float RecipeX0 = 172f;
    private const float RecipeX1 = 1088f;
    private const float RecipeH = 258f;

    private static readonly float[] ModuleX = { 2736f, 2976f, 3216f, 3456f };
    private static readonly float[] ModuleY = { 1132f, 1404f };
    private const float ModuleW = 216f;
    private const float ModuleH = 252f;

    // 도면 슬롯 (1.동력, 2.제어, 3.보조, 4.보조슬롯, 5.보조슬롯)
    private static readonly float[,] DiagramRect =
    {
        { 1763f,  548f, 2060f,  814f },
        { 1244f,  898f, 1524f, 1136f },
        { 2300f,  898f, 2580f, 1136f },
        { 1536f, 1338f, 1816f, 1574f },
        { 2008f, 1338f, 2288f, 1574f },
    };

    private static readonly Color InfoPanelColor = new Color(0.031f, 0.075f, 0.122f, 1f);
    private static readonly Color RecipeRowColor = new Color(0.055f, 0.106f, 0.165f, 1f);

    private static AetherPartsCraftingScreen instance;
    private static bool subscribed;

    public static bool IsOpen { get; private set; }

    private Canvas canvas;
    private RectTransform design;
    private PlayerInventory inventory;

    private readonly List<CraftingRecipeData> recipes = new List<CraftingRecipeData>();
    private readonly List<ItemData> ownedModules = new List<ItemData>();
    private readonly ItemData[] placed = new ItemData[DiagramSlots];

    private readonly List<TMP_Text> recipeNames = new List<TMP_Text>();
    private readonly List<AetherHoverGlow> recipeGlows = new List<AetherHoverGlow>();
    private readonly List<GameObject> recipeCovers = new List<GameObject>();

    private readonly List<Image> moduleIcons = new List<Image>();
    private readonly List<AetherHoverGlow> moduleGlows = new List<AetherHoverGlow>();

    private readonly List<Image> diagramIcons = new List<Image>();
    private readonly List<TMP_Text> moduleLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> diagramLabels = new List<TMP_Text>();
    private readonly List<GameObject> diagramPlates = new List<GameObject>();
    private readonly List<AetherHoverGlow> diagramGlows = new List<AetherHoverGlow>();

    private GameObject infoCover;
    private Image resultIcon;
    private TMP_Text infoKind;
    private TMP_Text infoEffect;
    private TMP_Text infoApplied;
    private TMP_Text recipeCountText;
    private TMP_Text moduleCountText;
    private TMP_Text hintText;

    private int selectedRecipe = -1;
    private int selectedModule = -1;

    // 끌어다 놓기
    private const float GhostSize = 190f;
    private const float SnapSeconds = 0.13f;
    private static readonly Color DropMatch = new Color(0.35f, 1f, 0.55f, 1f);
    private static readonly Color DropOther = new Color(1f, 0.72f, 0.25f, 1f);
    private static readonly Color DropNeutral = new Color(0.45f, 0.83f, 1f, 1f);

    private readonly List<RectTransform> diagramRects = new List<RectTransform>();
    private readonly List<Image> dropHighlights = new List<Image>();
    private RectTransform dragGhost;
    private Image dragGhostImage;
    private ItemData dragItem;
    private int dragFromSlot = -1;
    private int dragHoverSlot = -1;
    private bool snapping;

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
        Ensure();

        if (instance != null)
        {
            instance.Close();
            instance.inventory = null;
        }
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherPartsCraftingScreen");
        instance = host.AddComponent<AetherPartsCraftingScreen>();
        DontDestroyOnLoad(host);
    }

    /// <summary>작업대(PartsWorkbenchInteractable)가 열릴 때 호출한다.</summary>
    public static void OpenScreen()
    {
        Ensure();
        if (instance != null)
            instance.Open();
    }

    /// <summary>작업대가 닫힐 때 호출한다.</summary>
    public static void CloseScreen()
    {
        if (instance != null)
            instance.Close();
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
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (IsOpen)
        {
            bool close = keyboard != null ? keyboard.escapeKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Escape);
            bool craft = keyboard != null ? keyboard.fKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.F);
            bool reset = keyboard != null ? keyboard.rKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.R);
            bool blueprint = keyboard != null ? keyboard.bKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.B);

            // 설계서가 펼쳐져 있으면 ESC 는 설계서만 닫는다.
            if (close && PaperInventoryPreviewUI.IsOpen)
                PaperInventoryPreviewUI.HideIfOpen();
            else if (blueprint)
                OpenBlueprint();
            else if (close && !PaperInventoryPreviewUI.ClosedThisFrame)
                Close();
            else if (craft)
                TryCraft();
            else if (reset)
                ResetDiagram();

            return;
        }
    }

    public void Open()
    {
        if (IsOpen || !Resolve())
            return;

        IsOpen = true;
        AetherUIState.BeginScreen();

        canvas.gameObject.SetActive(true);
        AetherAudio.Play2D("ui_open", 0.6f, 1f, 0f);

        ResetDiagram();
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        CancelDrag();
        PaperInventoryPreviewUI.HideIfOpen();
        AetherAudio.Play2D("ui_close", 0.55f, 1f, 0f);

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        AetherUIState.EndScreen();

        // 작업대가 시간 정지·조작 잠금·커서를 직접 관리하므로 반드시 닫아 줘야 한다.
        // (빠뜨리면 Time.timeScale이 0에 묶여 게임이 영영 멈춘다.)
        PartsWorkbenchInteractable[] benches =
            FindObjectsByType<PartsWorkbenchInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < benches.Length; i++)
        {
            if (benches[i] != null)
                benches[i].CloseOpenedPartsUI();
        }
    }

    private bool Resolve()
    {
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (inventory == null)
            return false;

        recipes.Clear();
        CraftingRecipeData[] all = Resources.LoadAll<CraftingRecipeData>("AETHERParts");
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].resultPart != null)
                recipes.Add(all[i]);
        }

        if (selectedRecipe < 0 && recipes.Count > 0)
            selectedRecipe = 0;

        inventory.OnInventoryChanged -= Refresh;
        inventory.OnInventoryChanged += Refresh;
        return true;
    }

    // ------------------------------------------------------------------ 표시

    private void Refresh()
    {
        if (inventory == null)
            return;

        ownedModules.Clear();
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            string id = ItemNameRuleUtility.GetBestItemId(slot.item);
            bool isModule = ItemNameRuleUtility.IsModuleItem(id) || ItemNameRuleUtility.IsPaperObject(id);
            if (isModule && !ownedModules.Contains(slot.item))
                ownedModules.Add(slot.item);
        }

        for (int i = 0; i < RecipeRows; i++)
        {
            CraftingRecipeData recipe = i < recipes.Count ? recipes[i] : null;
            bool has = recipe != null;

            if (recipeCovers[i] != null)
                recipeCovers[i].SetActive(has);

            if (recipeNames[i] != null)
                recipeNames[i].text = has ? GetRecipeName(recipe) : string.Empty;

            if (recipeGlows[i] != null)
                recipeGlows[i].SetSelected(has && i == selectedRecipe);
        }

        for (int i = 0; i < ModuleCells; i++)
        {
            ItemData module = i < ownedModules.Count ? ownedModules[i] : null;

            if (moduleIcons[i] != null)
            {
                moduleIcons[i].sprite = module != null ? module.itemSprite : null;
                moduleIcons[i].color = module != null && module.itemSprite == null
                    ? new Color(0.45f, 0.78f, 0.95f, 0.85f)
                    : Color.white;
                moduleIcons[i].enabled = module != null;
            }

            if (moduleGlows[i] != null)
                moduleGlows[i].SetSelected(module != null && i == selectedModule);

            if (i < moduleLabels.Count && moduleLabels[i] != null)
                moduleLabels[i].text = module != null ? ShortModuleName(module) : string.Empty;
        }

        for (int i = 0; i < DiagramSlots; i++)
        {
            if (diagramIcons[i] != null)
            {
                diagramIcons[i].sprite = placed[i] != null ? placed[i].itemSprite : null;
                diagramIcons[i].color = placed[i] != null && placed[i].itemSprite == null
                    ? new Color(0.45f, 0.78f, 0.95f, 0.85f)
                    : Color.white;
                diagramIcons[i].enabled = placed[i] != null;
            }

            if (i < diagramLabels.Count && diagramLabels[i] != null)
                diagramLabels[i].text = placed[i] != null ? ShortModuleName(placed[i]) : string.Empty;

            if (i < diagramPlates.Count && diagramPlates[i] != null)
                diagramPlates[i].SetActive(placed[i] != null);
        }

        if (recipeCountText != null)
            recipeCountText.text = $"{recipes.Count:00} / {RecipeRows:00}";

        if (moduleCountText != null)
            moduleCountText.text = $"{Mathf.Min(ownedModules.Count, ModuleCells):00} / {ModuleCells:00}";

        ShowRecipeInfo();
        UpdateHint();
    }

    private CraftingRecipeData ActiveRecipe =>
        selectedRecipe >= 0 && selectedRecipe < recipes.Count ? recipes[selectedRecipe] : null;

    /// <summary>"Breaker Core Module" → "Breaker Core". 칸이 좁아 'Module' 은 뺀다.</summary>
    private static string ShortModuleName(ItemData item)
    {
        string name = !string.IsNullOrWhiteSpace(item.displayName) ? item.displayName : ItemNameRuleUtility.GetBestItemId(item);
        name = name.Replace(" Module", string.Empty).Replace(" 모듈", string.Empty).Replace("_module", string.Empty).Replace('_', ' ').Trim();
        return name;
    }

    private static TMP_Text CreateCellLabel(RectTransform cell)
    {
        RectTransform rect = AetherScreenKit.CreateRect(cell, "Name", 0f, 0f, 0f, 0f,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        rect.anchorMin = new Vector2(0.04f, 0.04f);
        rect.anchorMax = new Vector2(0.96f, 0.28f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text text = rect.GetComponent<TextMeshProUGUI>();
        text.fontSize = AetherScreenKit.Font(26f);
        text.enableAutoSizing = true;
        text.fontSizeMin = AetherScreenKit.Font(16f);
        text.fontSizeMax = AetherScreenKit.Font(26f);
        text.alignment = TextAlignmentOptions.Center;
        text.color = AetherScreenKit.TextBright;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static string GetRecipeName(CraftingRecipeData recipe)
    {
        PartData part = recipe.resultPart;
        if (part == null)
            return recipe.name;

        if (!string.IsNullOrWhiteSpace(part.displayName))
            return part.displayName;

        return string.IsNullOrWhiteSpace(part.id) ? part.name : part.id;
    }

    private void ShowRecipeInfo()
    {
        CraftingRecipeData recipe = ActiveRecipe;
        PartData part = recipe != null ? recipe.resultPart : null;
        bool has = part != null;

        if (infoCover != null)
            infoCover.SetActive(has);

        if (resultIcon != null)
        {
            resultIcon.sprite = has ? part.itemSprite : null;
            resultIcon.enabled = has && part.itemSprite != null;
        }

        if (infoKind != null)
            infoKind.text = has ? PartSlotTypeUtility.GetKoreanLabel(part.slotType) + " 파츠" : string.Empty;

        if (infoEffect != null)
            infoEffect.text = has ? part.description : string.Empty;

        if (infoApplied != null)
            infoApplied.text = has ? GetRequirementSummary(recipe) : string.Empty;
    }

    private string GetRequirementSummary(CraftingRecipeData recipe)
    {
        if (recipe.expectedPositions == null || recipe.expectedPositions.Length == 0)
            return string.Empty;

        // 강화파츠(설계서가 필요한 제작법)는 정답을 적지 않는다. 설계서를 보고 놓아야 한다.
        if (AetherBlueprint.RequiresBlueprint(recipe))
        {
            int needed = Mathf.Min(recipe.expectedPositions.Length, DiagramSlots);
            int filled = 0;
            for (int i = 0; i < needed; i++)
                if (placed[i] != null) filled++;

            // 설계서는 만들 때 함께 쓰인다. 이미 만든 파츠인데 "설계서를 찾아오라"고 하면 다시 05 로 돌아가게 된다.
            if (!HasBlueprint(recipe) && PartSystemRuntimeState.IsPartOwned(recipe.resultPart))
                return "강화파츠 — 이미 제작했습니다.\n[P] 파츠 장착 화면에서 장착하세요.";

            return HasBlueprint(recipe)
                ? $"강화파츠: 설계서의 배치대로 놓아야 제작\n[B] 또는 [?] 제작설계서 보기\n배치 {filled} / {needed}"
                : "강화파츠 — 제작설계서가 필요합니다.\n폐허된 도시에서 제작설계서를 찾아오세요.";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < recipe.expectedPositions.Length && i < DiagramSlots; i++)
        {
            string wanted = ItemNameRuleUtility.NormalizeModuleAlias(recipe.expectedPositions[i].itemId);
            bool ok = placed[i] != null &&
                      ItemNameRuleUtility.NormalizeModuleAlias(ItemNameRuleUtility.GetBestItemId(placed[i])) == wanted;
            ItemData wantedItem = PartResourceResolver.FindItemData(recipe.expectedPositions[i].itemId);
            string label = wantedItem != null ? ShortModuleName(wantedItem) : wanted;
            sb.AppendLine($"{i + 1}. {label}  {(ok ? "OK" : "-")}");
        }

        return sb.ToString();
    }

    private void UpdateHint()
    {
        if (hintText == null)
            return;

        CraftingRecipeData recipe = ActiveRecipe;
        if (recipe == null)
        {
            hintText.text = "왼쪽에서 제작설계서를 선택하세요.";
            return;
        }

        // 강화파츠: 맞았는지 미리 알려 주지 않는다. 다 채우면 제작을 눌러 확인한다.
        if (AetherBlueprint.RequiresBlueprint(recipe))
        {
            if (!HasBlueprint(recipe) && PartSystemRuntimeState.IsPartOwned(recipe.resultPart))
            {
                // 방금 만든 직후의 "제작 완료" 문구는 그대로 둔다.
                if (string.IsNullOrEmpty(hintText.text) || !hintText.text.Contains("제작 완료"))
                    hintText.text = "이미 만든 강화파츠입니다. [P] 파츠 장착 화면에서 장착하세요.";
            }
            else if (!HasBlueprint(recipe))
                hintText.text = "제작설계서가 없어 강화파츠를 만들 수 없습니다.";
            else if (AllNeededSlotsFilled(recipe))
                hintText.text = "모든 슬롯을 채웠습니다.  F 키로 제작을 시작하세요.";
            else
                hintText.text = "[B] 제작설계서를 보고, 그림과 같은 자리에 모듈을 놓으세요.";
            return;
        }

        if (IsDiagramComplete(recipe))
            hintText.text = $"'{GetRecipeName(recipe)}' 제작 준비 완료.  F 키로 제작을 시작하세요.";
        else
            hintText.text = "보유 모듈을 끌어 도면의 슬롯에 붙이세요.";
    }

    private bool HasBlueprint(CraftingRecipeData recipe) => AetherBlueprint.Owns(inventory, recipe);

    private bool AllNeededSlotsFilled(CraftingRecipeData recipe)
    {
        int needed = Mathf.Min(recipe.expectedPositions.Length, DiagramSlots);
        for (int i = 0; i < needed; i++)
            if (placed[i] == null) return false;
        return true;
    }

    /// <summary>[B]·[?]: 고른 제작법의 설계서를 제작 화면 위에 펼친다(설계서를 가지고 있을 때만).</summary>
    private void OpenBlueprint()
    {
        CraftingRecipeData recipe = ActiveRecipe;
        if (recipe == null || !AetherBlueprint.RequiresBlueprint(recipe))
        {
            if (hintText != null)
                hintText.text = "이 파츠는 설계서 없이 오른쪽 안내대로 만들 수 있습니다.";
            return;
        }

        if (!HasBlueprint(recipe))
        {
            if (hintText != null)
                hintText.text = "제작설계서가 없습니다. 폐허된 도시에서 찾아오세요.";
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);
            return;
        }

        PaperInventoryPreviewUI.ShowRecipe(recipe);
    }

    // ------------------------------------------------------------------ 조작

    private void SelectRecipe(int index)
    {
        if (index < 0 || index >= recipes.Count)
            return;

        selectedRecipe = index;
        ResetDiagram(); // 클릭음은 버튼 공용 소리(AetherUiButtonSound)가 낸다
        Refresh();
    }

    private void SelectModule(int index)
    {
        selectedModule = index >= 0 && index < ownedModules.Count ? index : -1;
        Refresh();
    }

    private void PlaceInSlot(int slotIndex)
    {
        CraftingRecipeData recipe = ActiveRecipe;
        if (recipe == null)
            return;

        // 이미 놓인 것을 다시 누르면 회수
        if (selectedModule < 0)
        {
            placed[slotIndex] = null;
            Refresh();
            return;
        }

        ItemData module = ownedModules[selectedModule];

        // 같은 모듈이 다른 슬롯에 있으면 옮긴다
        for (int i = 0; i < placed.Length; i++)
        {
            if (placed[i] == module)
                placed[i] = null;
        }

        placed[slotIndex] = module;
        selectedModule = -1;
        Refresh();
    }

    private void ResetDiagram()
    {
        if (dragItem != null || snapping)
            CancelDrag();

        for (int i = 0; i < placed.Length; i++)
            placed[i] = null;

        selectedModule = -1;

        // 리셋 버튼·R 키는 여기만 불러서, 예전엔 도면을 비워도 아이콘이 화면에 그대로 남았다.
        Refresh();
    }

    private bool IsDiagramComplete(CraftingRecipeData recipe)
    {
        if (recipe == null || recipe.expectedPositions == null || recipe.expectedPositions.Length == 0)
            return false;

        int needed = Mathf.Min(recipe.expectedPositions.Length, DiagramSlots);
        for (int i = 0; i < needed; i++)
        {
            string wanted = ItemNameRuleUtility.NormalizeModuleAlias(recipe.expectedPositions[i].itemId);
            if (placed[i] == null)
                return false;

            string actual = ItemNameRuleUtility.NormalizeModuleAlias(ItemNameRuleUtility.GetBestItemId(placed[i]));
            if (actual != wanted)
                return false;
        }

        // 필요 이상으로 올려 둔 것이 있으면 제작 불가
        for (int i = needed; i < DiagramSlots; i++)
        {
            if (placed[i] != null)
                return false;
        }

        return true;
    }

    private void TryCraft()
    {
        FinishSnapNow();   // 붙는 중이던 모듈부터 마저 붙이고 판정한다
        CraftingRecipeData recipe = ActiveRecipe;
        if (recipe == null || inventory == null)
            return;

        if (AetherBlueprint.RequiresBlueprint(recipe) && !HasBlueprint(recipe))
        {
            if (hintText != null)
                hintText.text = "제작설계서가 없어 강화파츠를 만들 수 없습니다.";
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);
            return;
        }

        if (!IsDiagramComplete(recipe))
        {
            if (hintText != null)
                hintText.text = AetherBlueprint.RequiresBlueprint(recipe)
                    ? (AllNeededSlotsFilled(recipe) ? "배치가 제작설계서와 다릅니다.  [B] 설계서를 다시 확인하세요." : "아직 빈 슬롯이 있습니다.  [B] 설계서를 보고 채우세요.")
                    : "도면의 슬롯이 설계서와 맞지 않습니다.";
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);
            return;
        }

        // 도면에 올린 모듈을 실제로 소모한다.
        int needed = Mathf.Min(recipe.expectedPositions.Length, DiagramSlots);
        for (int i = 0; i < needed; i++)
        {
            if (!inventory.ConsumeItem(placed[i], 1))
            {
                if (hintText != null)
                    hintText.text = "재료가 부족합니다.";

                Refresh();
                return;
            }
        }

        // 강화파츠는 인벤토리가 아니라 파츠 보관함(파츠 장착 화면, P)으로 간다.
        PartSystemRuntimeState.RegisterCraftedEquipmentPart(recipe.resultPart);
        AetherLevel.ReportCraft(recipe.resultPart);

        // "craft_success" 키가 소리 목록에 없어 제작 완료가 무음이었다.
        AetherAudio.Play2D("craft_success", 0.8f, 1f, 0f);

        ResetDiagram();
        Refresh();

        if (hintText != null)
            hintText.text = AetherPlayerCharacter.CanEquipParts(AetherPlayerCharacter.FindActive())
                ? $"'{GetRecipeName(recipe)}' 제작 완료.  파츠 장착 화면(P)으로 보냈습니다."
                : $"'{GetRecipeName(recipe)}' 제작 완료.  (파츠 장착은 Meka만 할 수 있습니다)";   // 윤서는 P 화면이 열리지 않는다
    }

    // ------------------------------------------------------------------ 끌어다 놓기
    //
    // 보유 모듈(또는 도면에 놓인 모듈)을 누른 채 끌면 아이콘이 손을 따라오고,
    // 도면 슬롯 가까이 가면 그 슬롯이 빛난다(맞는 모듈이면 초록). 놓으면 아이콘이 슬롯으로
    // 빨려 들어가 붙는다. 슬롯이 아닌 곳에 놓으면 원래대로(도면에서 끌어낸 것은 빠진다).
    // 작업대가 열려 있으면 Time.timeScale 이 0 이라 연출은 실제 시간으로 돌린다.

    private bool BeginModuleDrag(int index, UnityEngine.EventSystems.PointerEventData e)
    {
        FinishSnapNow();
        if (ActiveRecipe == null || index < 0 || index >= ownedModules.Count)
            return false;

        dragItem = ownedModules[index];
        dragFromSlot = -1;
        selectedModule = index;
        Refresh();
        ShowGhost(e);
        AetherAudio.Play2D("drag_pick", 0.5f, 1f, 0.04f);
        return true;
    }

    private bool BeginDiagramDrag(int slot, UnityEngine.EventSystems.PointerEventData e)
    {
        FinishSnapNow();
        if (ActiveRecipe == null || slot < 0 || slot >= placed.Length || placed[slot] == null)
            return false;

        dragItem = placed[slot];
        dragFromSlot = slot;
        placed[slot] = null;
        selectedModule = -1;
        Refresh();
        ShowGhost(e);
        AetherAudio.Play2D("drag_pick", 0.5f, 0.95f, 0.04f);
        return true;
    }

    private void UpdateDrag(UnityEngine.EventSystems.PointerEventData e)
    {
        if (dragItem == null || snapping)
            return;

        Vector2 local = PointerToDesign(e);
        dragGhost.localPosition = local;
        SetHoverSlot(FindSnapSlot(local));
    }

    private void FinishDrag(UnityEngine.EventSystems.PointerEventData e)
    {
        if (dragItem == null || snapping)
            return;

        int target = FindSnapSlot(PointerToDesign(e));
        SetHoverSlot(-1);

        if (target < 0)
        {
            // 슬롯이 아닌 곳: 보유 목록에서 끈 것은 그냥 취소, 도면에서 끈 것은 빠진다.
            HideGhost();
            dragItem = null;
            dragFromSlot = -1;
            selectedModule = -1;
            Refresh();
            AetherAudio.Play2D("drag_cancel", 0.4f, 1f, 0.04f);
            return;
        }

        snapSlot = target;
        snapRoutine = StartCoroutine(SnapIntoSlot(target));
    }

    private Coroutine snapRoutine;
    private int snapSlot = -1;

    /// <summary>
    /// 붙는 연출(0.13초) 도중에 다음 모듈을 집으면, 연출을 건너뛰고 바로 붙인다.
    /// 예전엔 그동안 집기가 무시되어, 빠르게 연달아 옮기면 하나 걸러 하나씩 안 집혔다.
    /// </summary>
    private void FinishSnapNow()
    {
        if (!snapping)
            return;

        if (snapRoutine != null)
            StopCoroutine(snapRoutine);
        snapRoutine = null;
        CommitSnap(snapSlot);
    }

    private void CommitSnap(int slot)
    {
        // 같은 모듈이 다른 슬롯에 있으면 옮긴다(클릭으로 놓을 때와 같은 규칙)
        for (int i = 0; i < placed.Length; i++)
        {
            if (placed[i] == dragItem)
                placed[i] = null;
        }

        if (slot >= 0 && slot < placed.Length)
            placed[slot] = dragItem;
        dragItem = null;
        dragFromSlot = -1;
        selectedModule = -1;
        snapping = false;
        HideGhost();
        Refresh();
        AetherAudio.Play2D("drag_snap", 0.6f, 1f, 0.04f); // 붙는 순간 딸깍
    }

    private System.Collections.IEnumerator SnapIntoSlot(int slot)
    {
        snapping = true;

        Vector3 from = dragGhost.localPosition;
        Vector3 to = SlotCenter(slot);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SnapSeconds;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            dragGhost.localPosition = Vector3.LerpUnclamped(from, to, k);
            dragGhost.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.72f, k);
            yield return null;
        }

        CommitSnap(slot);
        snapRoutine = null;

        // 붙는 순간 슬롯을 한 번 번쩍여 '딸깍' 붙은 느낌을 준다. 강화파츠는 맞고 틀림을 드러내지 않게 청록.
        Image flash = dropHighlights[slot];
        Color flashColor = AetherBlueprint.RequiresBlueprint(ActiveRecipe) ? DropNeutral : DropMatch;
        float f = 0f;
        while (f < 1f && dragHoverSlot != slot)
        {
            f += Time.unscaledDeltaTime / 0.25f;
            Color c = flashColor;
            c.a = Mathf.Lerp(0.45f, 0f, f);
            flash.color = c;
            yield return null;
        }
        if (dragHoverSlot != slot)
            flash.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>손에서 가장 가까운 도면 슬롯. 슬롯 크기만큼 떨어진 곳까지는 끌려가 붙는다.</summary>
    private int FindSnapSlot(Vector2 local)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < diagramRects.Count; i++)
        {
            RectTransform r = diagramRects[i];
            float reach = Mathf.Max(r.rect.width, r.rect.height) * 0.85f;
            float d = Vector2.Distance(local, SlotCenter(i));
            if (d <= reach && d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }

        return best;
    }

    private void SetHoverSlot(int slot)
    {
        if (slot == dragHoverSlot)
            return;

        if (dragHoverSlot >= 0)
            dropHighlights[dragHoverSlot].color = new Color(1f, 1f, 1f, 0f);

        dragHoverSlot = slot;

        if (slot >= 0)
        {
            // 강화파츠는 맞는 자리를 초록으로 알려 주지 않는다(설계서를 봐야 한다). 붙을 자리만 청록으로.
            Color c = AetherBlueprint.RequiresBlueprint(ActiveRecipe) ? DropNeutral
                : SlotAccepts(slot, dragItem) ? DropMatch : DropOther;
            c.a = 0.28f;
            dropHighlights[slot].color = c;
        }
    }

    /// <summary>설계서가 이 슬롯에 요구하는 모듈인지.</summary>
    private bool SlotAccepts(int slot, ItemData item)
    {
        CraftingRecipeData recipe = ActiveRecipe;
        if (recipe == null || item == null || recipe.expectedPositions == null || slot >= recipe.expectedPositions.Length)
            return false;

        string wanted = ItemNameRuleUtility.NormalizeModuleAlias(recipe.expectedPositions[slot].itemId);
        string actual = ItemNameRuleUtility.NormalizeModuleAlias(ItemNameRuleUtility.GetBestItemId(item));
        return wanted == actual;
    }

    private Vector3 SlotCenter(int slot)
    {
        RectTransform r = diagramRects[slot];
        return design.InverseTransformPoint(r.TransformPoint(r.rect.center));
    }

    private Vector2 PointerToDesign(UnityEngine.EventSystems.PointerEventData e)
    {
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(design, e.position, cam, out Vector2 local);
        return local;
    }

    private void ShowGhost(UnityEngine.EventSystems.PointerEventData e)
    {
        dragGhostImage.sprite = dragItem != null ? dragItem.itemSprite : null;
        dragGhostImage.enabled = dragGhostImage.sprite != null;
        dragGhost.SetAsLastSibling();

        // 화면 해상도에 따라 도면 크기가 달라지므로 슬롯 크기를 기준으로 맞춘다.
        if (diagramRects.Count > 0)
        {
            Rect slotRect = diagramRects[0].rect;
            float size = Mathf.Min(slotRect.width, slotRect.height) * 0.78f;
            dragGhost.sizeDelta = new Vector2(size, size);
        }

        dragGhost.localScale = Vector3.one * 1.08f;
        dragGhost.localPosition = PointerToDesign(e);
        dragGhost.gameObject.SetActive(true);
    }

    private void HideGhost()
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
    }

    private void CancelDrag()
    {
        StopAllCoroutines();
        snapping = false;
        SetHoverSlot(-1);
        for (int i = 0; i < dropHighlights.Count; i++)
            dropHighlights[i].color = new Color(1f, 1f, 1f, 0f);
        HideGhost();
        dragItem = null;
        dragFromSlot = -1;
    }

    private void BuildDragGhost()
    {
        dragGhost = AetherScreenKit.CreateRect(design, "DragGhost", 0f, 0f, GhostSize, GhostSize,
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragGhost.anchorMin = dragGhost.anchorMax = dragGhost.pivot = new Vector2(0.5f, 0.5f);
        dragGhost.sizeDelta = new Vector2(GhostSize, GhostSize);

        dragGhostImage = dragGhost.GetComponent<Image>();
        dragGhostImage.preserveAspect = true;
        dragGhostImage.raycastTarget = false;
        dragGhostImage.color = new Color(1f, 1f, 1f, 0.92f);

        CanvasGroup group = dragGhost.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        dragGhost.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ UI 생성

    private void BuildUI()
    {
        canvas = AetherScreenKit.CreateCanvas(transform, "PartsCraftingCanvas", 30020);

        RectTransform dim = AetherScreenKit.CreateRect(canvas.transform as RectTransform, "Dim", 0f, 0f, 0f, 0f,
            typeof(RectTransform), typeof(Image));
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero;
        dim.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        design = AetherScreenKit.CreateBackdrop(canvas.transform, SpritePath, out _);

        BuildRecipeList();
        BuildDiagram();
        BuildModuleGrid();
        BuildInfoPanel();

        // 하단 버튼
        AetherScreenKit.CreateHitButton(design, "ResetButton", 1220f, 1876f, 1620f, 2000f, ResetDiagram, out _);
        AetherScreenKit.CreateHitButton(design, "CraftButton", 1664f, 1876f, 2616f, 2000f, TryCraft, out _);

        // 배경 그림에 원래 적힌 안내 문구 위에 덮개를 깔아, 바뀌는 안내와 두 문구가 겹쳐 보이지 않게 한다.
        RectTransform hintCover = AetherScreenKit.CreateRect(design, "HintCover", 1246f, 1736f, 2604f, 1844f,
            typeof(RectTransform), typeof(Image));
        Image hintCoverImage = hintCover.GetComponent<Image>();
        hintCoverImage.color = new Color(0.039f, 0.08f, 0.13f, 1f);
        hintCoverImage.raycastTarget = false;

        hintText = AetherScreenKit.CreateText(design, "Hint", 1280f, 1740f, 2600f, 1840f,
            string.Empty, AetherScreenKit.Font(34f), TextAlignmentOptions.MidlineLeft, AetherScreenKit.TextDim);
        hintText.enableWordWrapping = false;
        hintText.overflowMode = TextOverflowModes.Ellipsis;

        // 배경 그림에 "00 / 05", "00 / 08" 이 박혀 있어 실제 숫자와 겹쳐 보였다. 같은 색으로 덮는다.
        CreateCover("RecipeCountCover", 700f, 362f, 985f, 434f, new Color(0.035f, 0.075f, 0.122f, 1f));
        CreateCover("ModuleCountCover", 3380f, 1000f, 3680f, 1072f, new Color(0.031f, 0.067f, 0.118f, 1f));
        // 왼쪽 아래 "R 즐겨찾기" 도 그림에 박혀 있는데 즐겨찾기 기능은 없고 R 은 초기화다(가운데 버튼). 헷갈리지 않게 가린다.
        AetherScreenKit.CreatePatch(design, "FavoriteHintCover", 160f, 1904f, 470f, 2010f, 340f);

        recipeCountText = AetherScreenKit.CreateText(design, "RecipeCount", 700f, 362f, 980f, 434f,
            "00 / 05", AetherScreenKit.Font(38f), TextAlignmentOptions.Right, AetherScreenKit.Accent);
        moduleCountText = AetherScreenKit.CreateText(design, "ModuleCount", 3380f, 1000f, 3660f, 1072f,
            "00 / 08", AetherScreenKit.Font(38f), TextAlignmentOptions.Right, AetherScreenKit.Accent);

        AetherScreenKit.CreateHitButton(design, "Close", 3490f, 130f, 3790f, 226f, Close, out _);

        // 도면 제목 옆 [?] 동그라미: 제작설계서 펼치기
        AetherScreenKit.CreateHitButton(design, "BlueprintHelp", 1520f, 354f, 1604f, 434f, OpenBlueprint, out _);

        BuildDragGhost();

        canvas.gameObject.SetActive(false);
    }

    private void CreateCover(string name, float x0, float y0, float x1, float y1, Color color)
    {
        RectTransform cover = AetherScreenKit.CreateRect(design, name, x0, y0, x1, y1,
            typeof(RectTransform), typeof(Image));
        Image image = cover.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void BuildRecipeList()
    {
        for (int i = 0; i < RecipeRows; i++)
        {
            int index = i;
            float y0 = RecipeY[i];

            AetherScreenKit.CreateHitButton(design, $"Recipe_{i}", RecipeX0, y0, RecipeX1, y0 + RecipeH,
                () => SelectRecipe(index), out _);

            RectTransform row = design.Find($"Recipe_{i}") as RectTransform;
            recipeGlows.Add(row.GetComponent<AetherHoverGlow>());

            RectTransform cover = AetherScreenKit.CreateRect(row, "Cover", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            cover.anchorMin = new Vector2(0.02f, 0.08f);
            cover.anchorMax = new Vector2(0.98f, 0.92f);
            cover.offsetMin = Vector2.zero;
            cover.offsetMax = Vector2.zero;
            Image coverImage = cover.GetComponent<Image>();
            coverImage.color = RecipeRowColor;
            coverImage.raycastTarget = false;
            cover.SetAsFirstSibling();
            recipeCovers.Add(cover.gameObject);
            cover.gameObject.SetActive(false);

            RectTransform nameRect = AetherScreenKit.CreateRect(row, "Name", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            nameRect.anchorMin = new Vector2(0.06f, 0f);
            nameRect.anchorMax = new Vector2(0.96f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            TMP_Text nameText = nameRect.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = AetherScreenKit.Font(42f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = AetherScreenKit.TextBright;
            nameText.raycastTarget = false;
            recipeNames.Add(nameText);
        }
    }

    private void BuildDiagram()
    {
        for (int i = 0; i < DiagramSlots; i++)
        {
            int index = i;
            AetherScreenKit.CreateHitButton(design, $"Diagram_{i}",
                DiagramRect[i, 0], DiagramRect[i, 1], DiagramRect[i, 2], DiagramRect[i, 3],
                () => PlaceInSlot(index), out _);

            RectTransform slot = design.Find($"Diagram_{i}") as RectTransform;
            diagramGlows.Add(slot.GetComponent<AetherHoverGlow>());
            diagramRects.Add(slot);

            // 끄는 동안 붙을 자리를 알려 주는 테두리 빛(맞는 모듈이면 초록, 아니면 호박색)
            RectTransform dropRect = AetherScreenKit.CreateRect(slot, "DropHighlight", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            dropRect.anchorMin = new Vector2(0.04f, 0.04f);
            dropRect.anchorMax = new Vector2(0.96f, 0.96f);
            dropRect.offsetMin = Vector2.zero;
            dropRect.offsetMax = Vector2.zero;
            Image drop = dropRect.GetComponent<Image>();
            drop.color = new Color(1f, 1f, 1f, 0f);
            drop.raycastTarget = false;
            dropHighlights.Add(drop);

            // 도면에 놓인 모듈을 끌어 다른 슬롯으로 옮기거나, 바깥에 놓아 뺀다.
            AetherDragHandle slotDrag = slot.gameObject.AddComponent<AetherDragHandle>();
            slotDrag.BeginDrag = e => BeginDiagramDrag(index, e);
            slotDrag.Drag = UpdateDrag;
            slotDrag.EndDrag = FinishDrag;

            RectTransform iconRect = AetherScreenKit.CreateRect(slot, "Icon", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            iconRect.anchorMin = new Vector2(0.18f, 0.18f);
            iconRect.anchorMax = new Vector2(0.82f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image icon = iconRect.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            diagramIcons.Add(icon);

            iconRect.anchorMin = new Vector2(0.08f, 0.25f);   // 아이템 그림을 크게(칸의 84%)
            iconRect.anchorMax = new Vector2(0.92f, 0.97f);

            // 도면 슬롯 배경 그림에 'EMPTY' 가 적혀 있어 이름 뒤로 비친다. 모듈이 놓이면 슬롯 전체를 바탕으로 덮는다.
            RectTransform plate = AetherScreenKit.CreateRect(slot, "FilledPlate", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            plate.anchorMin = new Vector2(0.03f, 0.03f);
            plate.anchorMax = new Vector2(0.97f, 0.97f);
            plate.offsetMin = Vector2.zero;
            plate.offsetMax = Vector2.zero;
            Image plateImage = plate.GetComponent<Image>();
            plateImage.color = new Color(0.05f, 0.1f, 0.16f, 1f);
            plateImage.raycastTarget = false;
            plate.SetAsFirstSibling(); // 강조·붙는 순간 번쩍임은 바탕 위로 보이게
            diagramPlates.Add(plate.gameObject);
            plate.gameObject.SetActive(false);

            diagramLabels.Add(CreateCellLabel(slot));
        }

        // 가운데 결과 표시(팔각형)
        RectTransform result = AetherScreenKit.CreateRect(design, "ResultIcon", 1786f, 896f, 2038f, 1148f,
            typeof(RectTransform), typeof(Image));
        resultIcon = result.GetComponent<Image>();
        resultIcon.preserveAspect = true;
        resultIcon.raycastTarget = false;
        resultIcon.enabled = false;
    }

    private void BuildModuleGrid()
    {
        for (int row = 0; row < ModuleY.Length; row++)
        {
            for (int col = 0; col < ModuleX.Length; col++)
            {
                int index = row * ModuleX.Length + col;
                float x0 = ModuleX[col];
                float y0 = ModuleY[row];

                AetherScreenKit.CreateHitButton(design, $"Module_{index}", x0, y0, x0 + ModuleW, y0 + ModuleH,
                    () => SelectModule(index), out _);

                RectTransform cell = design.Find($"Module_{index}") as RectTransform;
                moduleGlows.Add(cell.GetComponent<AetherHoverGlow>());

                AetherDragHandle cellDrag = cell.gameObject.AddComponent<AetherDragHandle>();
                cellDrag.BeginDrag = e => BeginModuleDrag(index, e);
                cellDrag.Drag = UpdateDrag;
                cellDrag.EndDrag = FinishDrag;

                RectTransform iconRect = AetherScreenKit.CreateRect(cell, "Icon", 0f, 0f, 0f, 0f,
                    typeof(RectTransform), typeof(Image));
                iconRect.anchorMin = new Vector2(0.16f, 0.16f);
                iconRect.anchorMax = new Vector2(0.84f, 0.84f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image icon = iconRect.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                moduleIcons.Add(icon);

                // 아이콘만으로는 비슷한 모듈을 구분하기 어려워 이름을 아래에 적는다.
                iconRect.anchorMin = new Vector2(0.08f, 0.25f);   // 아이템 그림을 크게
                iconRect.anchorMax = new Vector2(0.92f, 0.97f);
                moduleLabels.Add(CreateCellLabel(cell));
            }
        }
    }

    private void BuildInfoPanel()
    {
        RectTransform cover = AetherScreenKit.CreateRect(design, "InfoCover", 2708f, 364f, 3696f, 904f,
            typeof(RectTransform), typeof(Image));
        Image coverImage = cover.GetComponent<Image>();
        coverImage.color = InfoPanelColor;
        coverImage.raycastTarget = false;
        infoCover = cover.gameObject;
        infoCover.SetActive(false);

        infoKind = AetherScreenKit.CreateText(design, "InfoKind", 2740f, 420f, 3660f, 500f,
            string.Empty, AetherScreenKit.Font(40f), TextAlignmentOptions.Left, AetherScreenKit.Accent);

        infoEffect = AetherScreenKit.CreateText(design, "InfoEffect", 2740f, 520f, 3660f, 660f,
            string.Empty, AetherScreenKit.Font(36f), TextAlignmentOptions.TopLeft, AetherScreenKit.TextDim);
        infoEffect.enableWordWrapping = true;

        infoApplied = AetherScreenKit.CreateText(design, "InfoApplied", 2740f, 670f, 3660f, 890f,
            string.Empty, AetherScreenKit.Font(34f), TextAlignmentOptions.TopLeft, AetherScreenKit.TextDim);
        infoApplied.enableWordWrapping = true;
    }
}
