using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전체화면 UI(인벤토리·파츠 장착·파츠 제작)가 겹치지 않도록 관리하고,
/// 화면이 열려 있는 동안 플레이어 조작과 시간을 멈춘다.
///
/// 조작을 멈추지 않으면 인벤토리를 열어 둔 채로 계속 움직이고 총이 발사되며,
/// 적도 그대로 공격해 온다.
/// </summary>
public static class AetherUIState
{
    public static int OpenScreenCount { get; private set; }
    public static bool AnyScreenOpen => OpenScreenCount > 0;

    /// <summary>
    /// 마지막으로 화면이 닫힌 프레임. ESC로 인벤토리를 닫은 바로 그 프레임에
    /// 일시정지 메뉴가 같은 ESC를 또 받아 열리는 것을 막는다.
    /// </summary>
    public static int LastCloseFrame { get; private set; } = -10;

    public static bool ConsumedCloseThisFrame => Time.frameCount - LastCloseFrame <= 1;

    private static readonly List<Behaviour> suspended = new List<Behaviour>();
    private static float previousTimeScale = 1f;
    private static CursorLockMode previousLockState = CursorLockMode.Locked;
    private static bool previousCursorVisible;
    private static bool gameplaySuspended;

    /// <summary>화면을 열 때 호출. 첫 화면에서만 실제로 게임을 멈춘다.</summary>
    /// <param name="freezeTime">false 면 조작만 막고 시간은 흐르게 둔다(제한 시간이 있는 퍼즐 화면).</param>
    public static void BeginScreen(bool freezeTime = true)
    {
        OpenScreenCount++;
        if (OpenScreenCount != 1)
            return;

        SuspendGameplay(freezeTime);
    }

    /// <summary>화면을 닫을 때 호출. 마지막 화면이 닫힐 때만 되돌린다.</summary>
    public static void EndScreen()
    {
        OpenScreenCount = Mathf.Max(0, OpenScreenCount - 1);
        LastCloseFrame = Time.frameCount;

        if (OpenScreenCount != 0)
            return;

        RestoreGameplay();
    }

    private static void SuspendGameplay(bool freezeTime)
    {
        if (gameplaySuspended)
            return;

        gameplaySuspended = true;

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        suspended.Clear();
        SuspendAll<Move_Hankyul>();
        SuspendAll<Move_Yunseo>();
        SuspendAll<PlayerMouseLook>();
        SuspendAll<Hankyul_Fire>();
        SuspendAll<Hankyul_Battle>();
        SuspendAll<Yunseo_Battle>();
        SuspendAll<PlayerInteractor>();
        // 화면이 열린 채 숫자 키로 아이템이 쓰이고, Q(탭 전환)가 브레이커를, 우클릭이 실드를 같이 발동했다.
        SuspendAll<QuickSlotManager>();
        SuspendAll<HangyeolPartAbilityController>();
        SuspendAll<YunseoPartAbilityController>();

        if (freezeTime)
            Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void RestoreGameplay()
    {
        if (!gameplaySuspended)
            return;

        gameplaySuspended = false;

        for (int i = 0; i < suspended.Count; i++)
        {
            if (suspended[i] != null)
                suspended[i].enabled = true;
        }

        suspended.Clear();

        // 일시정지 메뉴가 떠 있다면 시간을 되돌리면 안 된다.
        if (!AetherPauseMenu.IsPaused)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;

            // 07 작업대는 화면을 열기 전에 커서를 먼저 풀어서 '풀린 상태'가 저장됐고, 닫은 뒤 커서가 남았다.
            // 조작하는 캐릭터가 있는 장면이면 1인칭 조작(커서 잠금)으로 되돌린다.
            if (Object.FindAnyObjectByType<Move_Hankyul>() != null || Object.FindAnyObjectByType<Move_Yunseo>() != null)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private static void SuspendAll<T>() where T : Behaviour
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || !found[i].enabled)
                continue;

            found[i].enabled = false;
            suspended.Add(found[i]);
        }
    }
}

/// <summary>
/// 디자인 이미지(Resources/UI/InventoryPanel)를 그대로 배경으로 쓰는 인벤토리 화면.
/// 슬롯·탭·아이템 정보만 이미지 위에 얹는다.
/// </summary>
public class AetherInventoryScreen : MonoBehaviour
{
    private const string SpritePath = "UI/InventoryPanel";
    private const int Columns = 6;
    private const int Rows = 4;
    private const int SlotCount = Columns * Rows;

    // 이미지에서 잰 좌표 (3840 x 2160 기준)
    private static readonly float[] SlotX = { 172f, 564f, 952f, 1340f, 1728f, 2116f };
    private static readonly float[] SlotY = { 496f, 860f, 1220f, 1580f };
    private const float SlotW = 352f;
    private const float SlotH = 332f;

    private static readonly (string label, float x0, float x1)[] Tabs =
    {
        ("전체", 304f, 542f),
        ("정비", 535f, 773f),
        ("소모품", 778f, 1016f),
        ("재료", 1016f, 1254f),
        ("기타", 1250f, 1488f),
    };

    private const float TabY0 = 350f;
    private const float TabY1 = 442f;

    private static readonly Color InfoPanelColor = new Color(0.031f, 0.071f, 0.118f, 1f);
    private static readonly Color FooterColor = new Color(0.024f, 0.059f, 0.102f, 1f);

    private static AetherInventoryScreen instance;
    private static bool subscribed;

    public static bool IsOpen { get; private set; }

    private Canvas canvas;
    private RectTransform design;
    private PlayerInventory inventory;
    private InventoryUI legacyUI;

    private readonly List<Image> slotIcons = new List<Image>();
    private readonly List<TMP_Text> slotCounts = new List<TMP_Text>();
    private readonly List<AetherHoverGlow> slotGlows = new List<AetherHoverGlow>();
    private readonly List<int> visibleSlotIndices = new List<int>();

    private readonly List<AetherHoverGlow> tabGlows = new List<AetherHoverGlow>();

    private GameObject infoCover;
    private Image infoIcon;
    private TMP_Text infoName;
    private TMP_Text infoCategory;
    private TMP_Text infoDescription;
    private TMP_Text capacityText;
    private Image capacityFill;

    private int activeTab;
    private int selectedSlot = -1;
    private float lastClickTime = -10f;
    private int lastClickedSlot = -1;

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

        if (instance == null)
            return;

        instance.Close();
        instance.inventory = null;
        instance.legacyUI = null;

        // 씬이 바뀌면 그 씬의 예전 UI들을 미리 꺼 둔다.
        // (처음 I/P를 누를 때 새 화면과 예전 화면이 같이 열리는 것을 막는다.)
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.SuppressLegacyUI());
    }

    /// <summary>플레이어가 늦게 생성될 수 있으므로 잠시 지켜보며 예전 UI를 정지시킨다.</summary>
    private System.Collections.IEnumerator SuppressLegacyUI()
    {
        for (int frame = 0; frame < 300; frame++)
        {
            InventoryUI[] inventories = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < inventories.Length; i++)
            {
                if (inventories[i] == null)
                    continue;

                if (legacyUI == null)
                    legacyUI = inventories[i];

                if (inventories[i].inventoryPanel != null)
                    inventories[i].inventoryPanel.SetActive(false);

                inventories[i].enabled = false;
            }

            PartsUIHotkeyController[] hotkeys =
                FindObjectsByType<PartsUIHotkeyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < hotkeys.Length; i++)
            {
                if (hotkeys[i] != null)
                    hotkeys[i].enabled = false;
            }

            if (inventories.Length > 0)
                yield break;

            yield return null;
        }
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherInventoryScreen");
        instance = host.AddComponent<AetherInventoryScreen>();
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

        // 첫 씬에서도 예전 UI를 정지시킨다(sceneLoaded는 첫 씬에 대해 불리지 않는다).
        StartCoroutine(SuppressLegacyUI());
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ------------------------------------------------------------------ 입력

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        bool togglePressed = keyboard != null
            ? keyboard.iKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.I);

        if (IsOpen)
        {
            bool closePressed = keyboard != null
                ? keyboard.escapeKey.wasPressedThisFrame
                : Input.GetKeyDown(KeyCode.Escape);

            // 설계서 미리보기가 떠 있으면 ESC 는 미리보기만 닫는다(인벤토리까지 한꺼번에 닫히지 않게).
            if (closePressed && PaperInventoryPreviewUI.IsOpen)
            {
                PaperInventoryPreviewUI.HideIfOpen();
                return;
            }

            if (closePressed || togglePressed)
            {
                PaperInventoryPreviewUI.HideIfOpen();
                Close();
                return;
            }

            bool prev = keyboard != null ? keyboard.qKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Q);
            bool next = keyboard != null ? keyboard.eKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.E);

            bool use = keyboard != null ? keyboard.fKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.F);
            bool drop = keyboard != null ? keyboard.xKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.X);

            if (prev)
                SetTab((activeTab - 1 + Tabs.Length) % Tabs.Length);
            else if (next)
                SetTab((activeTab + 1) % Tabs.Length);
            else if (use)
                UseSelected();
            else if (drop)
                DropSelected();

            return;
        }

        if (togglePressed && !AetherPauseMenu.IsPaused && !AetherUIState.AnyScreenOpen)
            Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        if (!ResolveInventory())
        {
            Debug.LogWarning("[AetherInventoryScreen] 플레이어 인벤토리를 찾지 못했습니다.");
            return;
        }

        IsOpen = true;
        AetherUIState.BeginScreen();
        AetherAudio.Play2D("ui_open", 0.55f, 1.05f, 0f);

        canvas.gameObject.SetActive(true);

        SetTab(activeTab);
        Select(-1);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        AetherAudio.Play2D("ui_close", 0.5f, 1.05f, 0f);

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        AetherUIState.EndScreen();
    }

    /// <summary>플레이어 인벤토리를 찾고, 예전 인벤토리 UI는 끈다(I키 중복 방지).</summary>
    private bool ResolveInventory()
    {
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (inventory == null)
            return false;

        // 디자인이 24칸이므로 용량을 맞춘다.
        if (inventory.slots != null)
        {
            while (inventory.slots.Count < SlotCount)
                inventory.slots.Add(new PlayerInventory.Slot());

            // 24칸을 넘는 자리는 비어 있을 때만 잘라낸다(아이템 유실 방지).
            for (int i = inventory.slots.Count - 1; i >= SlotCount; i--)
            {
                PlayerInventory.Slot extra = inventory.slots[i];
                if (extra != null && extra.item != null && extra.count > 0)
                    break;

                inventory.slots.RemoveAt(i);
            }
        }

        inventory.slotCount = inventory.slots != null ? inventory.slots.Count : SlotCount;

        if (legacyUI == null)
        {
            InventoryUI[] all = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;

                if (legacyUI == null)
                    legacyUI = all[i];

                // 예전 UI가 I키로 같이 열리지 않도록 정지시킨다.
                if (all[i].inventoryPanel != null)
                    all[i].inventoryPanel.SetActive(false);

                all[i].enabled = false;
            }
        }

        inventory.OnInventoryChanged -= Refresh;
        inventory.OnInventoryChanged += Refresh;
        return true;
    }

    // ------------------------------------------------------------------ 표시

    private void SetTab(int index)
    {
        activeTab = Mathf.Clamp(index, 0, Tabs.Length - 1);

        for (int i = 0; i < tabGlows.Count; i++)
        {
            if (tabGlows[i] != null)
                tabGlows[i].SetSelected(i == activeTab);
        }

        Select(-1);
        Refresh();

        // 강화파츠는 인벤토리가 아니라 파츠 장착 화면의 보관함에 있다. '정비' 탭에서 그 사실을 알려 준다.
        if (activeTab == 1)
            SetActionMessage("강화파츠는 [P] 파츠 장착 화면에서 관리합니다.", false);
    }

    private void Refresh()
    {
        if (inventory == null)
            return;

        visibleSlotIndices.Clear();
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            if (MatchesTab(slot.item, activeTab))
                visibleSlotIndices.Add(i);
        }

        for (int cell = 0; cell < SlotCount; cell++)
        {
            ItemData item = null;
            int count = 0;

            if (cell < visibleSlotIndices.Count)
            {
                PlayerInventory.Slot slot = inventory.slots[visibleSlotIndices[cell]];
                item = slot.item;
                count = slot.count;
            }

            if (slotIcons[cell] != null)
            {
                Sprite sprite = item != null ? item.itemSprite : null;
                slotIcons[cell].sprite = sprite;
                slotIcons[cell].color = sprite != null
                    ? Color.white
                    : new Color(0.45f, 0.78f, 0.95f, item != null ? 0.85f : 0f);
                slotIcons[cell].enabled = item != null;
            }

            if (slotCounts[cell] != null)
                slotCounts[cell].text = count > 1 ? count.ToString() : string.Empty;
        }

        int occupied = 0;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot != null && slot.item != null && slot.count > 0)
                occupied++;
        }

        if (capacityText != null)
            capacityText.text = $"{occupied:00} / {SlotCount}";

        if (capacityFill != null)
            capacityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(occupied / (float)SlotCount), 1f);

        if (selectedSlot >= visibleSlotIndices.Count)
            Select(-1);
        else
            ShowInfo(selectedSlot);
    }

    private static bool MatchesTab(ItemData item, int tab)
    {
        if (tab == 0)
            return true;

        string id = ItemNameRuleUtility.GetBestItemId(item);
        bool isPart = item is PartData;
        bool isModule = ItemNameRuleUtility.IsModuleItem(id) || ItemNameRuleUtility.IsPaperObject(id);
        bool isConsumable = !isPart && !isModule && item.effects != null && item.effects.Length > 0;

        switch (tab)
        {
            case 1: return isPart;                              // 정비
            case 2: return isConsumable;                        // 소모품
            case 3: return isModule;                            // 재료
            default: return !isPart && !isModule && !isConsumable; // 기타
        }
    }

    private void Select(int cell)
    {
        if (cell != selectedSlot)
            SetActionMessage(string.Empty, false);
        selectedSlot = cell;

        for (int i = 0; i < slotGlows.Count; i++)
        {
            if (slotGlows[i] != null)
                slotGlows[i].SetSelected(i == cell);
        }

        ShowInfo(cell);
    }

    private void ShowInfo(int cell)
    {
        ItemData item = null;
        int count = 0;

        if (cell >= 0 && cell < visibleSlotIndices.Count)
        {
            PlayerInventory.Slot slot = inventory.slots[visibleSlotIndices[cell]];
            item = slot.item;
            count = slot.count;
        }

        bool has = item != null;

        // 디자인 이미지에 "아이템을 선택하세요." 문구가 그려져 있어 선택 시 같은 색으로 덮는다.
        if (infoCover != null)
            infoCover.SetActive(has);

        if (infoIcon != null)
        {
            infoIcon.sprite = has ? item.itemSprite : null;
            infoIcon.enabled = has && item.itemSprite != null;
        }

        if (infoName != null)
            infoName.text = has ? GetDisplayName(item) + (count > 1 ? $"  x{count}" : string.Empty) : string.Empty;

        if (infoCategory != null)
            infoCategory.text = has ? GetCategoryLabel(item) : string.Empty;

        if (infoDescription != null)
            infoDescription.text = has ? item.description : string.Empty;

        UpdateActionButtons(item);
    }

    private static string GetDisplayName(ItemData item)
    {
        if (!string.IsNullOrWhiteSpace(item.displayName))
            return item.displayName;

        return string.IsNullOrWhiteSpace(item.id) ? item.name : item.id;
    }

    private static string GetCategoryLabel(ItemData item)
    {
        string id = ItemNameRuleUtility.GetBestItemId(item);

        if (item is PartData)
            return "정비 · 파츠";
        if (ItemNameRuleUtility.IsPaperObject(id))
            return "재료 · 제작설계서";
        if (ItemNameRuleUtility.IsModuleItem(id))
            return "재료 · 모듈";
        if (item.effects != null && item.effects.Length > 0)
            return "소모품";

        return "기타";
    }

    private void HandleSlotClicked(int cell)
    {
        bool doubleClick = cell == lastClickedSlot && Time.unscaledTime - lastClickTime <= 0.35f;
        lastClickedSlot = cell;
        lastClickTime = Time.unscaledTime;

        Select(cell);

        if (!doubleClick || cell >= visibleSlotIndices.Count)
            return;

        // 더블클릭: 소모품은 바로 사용, 설계서는 미리보기.
        ItemData clicked = inventory.GetSlot(visibleSlotIndices[cell])?.item;
        if (AetherItemUse.IsUsable(clicked))
        {
            UseSelected();
            return;
        }

        // 더블클릭: 설계도 미리보기 등 예전 인벤토리 UI의 처리를 그대로 빌려 쓴다.
        // 예전 UI는 꺼둔 상태라 내부 상태가 온전하지 않을 수 있어 예외를 막는다.
        if (legacyUI == null)
            return;

        try
        {
            legacyUI.OnSlotDoubleClicked(visibleSlotIndices[cell]);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AetherInventoryScreen] 아이템 사용 처리 중 예외: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ UI 생성

    private void BuildUI()
    {
        canvas = AetherScreenKit.CreateCanvas(transform, "InventoryCanvas", 30000);

        // 뒤쪽 화면을 가리는 어두운 막
        RectTransform dim = AetherScreenKit.CreateRect(canvas.transform as RectTransform, "Dim", 0f, 0f, 0f, 0f,
            typeof(RectTransform), typeof(Image));
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero;
        dim.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        design = AetherScreenKit.CreateBackdrop(canvas.transform, SpritePath, out _);

        BuildTabs();
        BuildSlots();
        BuildInfoPanel();
        BuildFooter();

        // ESC 닫기 버튼
        AetherScreenKit.CreateHitButton(design, "Close", 3490f, 130f, 3790f, 226f, Close, out _);

        canvas.gameObject.SetActive(false);
    }

    private void BuildTabs()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i;
            (string label, float x0, float x1) tab = Tabs[i];

            AetherScreenKit.CreateHitButton(design, $"Tab_{tab.label}", tab.x0, TabY0, tab.x1, TabY1,
                () => SetTab(index), out _);

            AetherHoverGlow glow = design.Find($"Tab_{tab.label}").GetComponent<AetherHoverGlow>();
            tabGlows.Add(glow);
        }

        // Q / E 키 버튼
        AetherScreenKit.CreateHitButton(design, "TabPrev", 172f, 360f, 264f, 432f,
            () => SetTab((activeTab - 1 + Tabs.Length) % Tabs.Length), out _);
        AetherScreenKit.CreateHitButton(design, "TabNext", 2376f, 360f, 2468f, 432f,
            () => SetTab((activeTab + 1) % Tabs.Length), out _);
    }

    private void BuildSlots()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int cell = row * Columns + col;
                float x0 = SlotX[col];
                float y0 = SlotY[row];

                AetherScreenKit.CreateHitButton(design, $"Slot_{cell:00}", x0, y0, x0 + SlotW, y0 + SlotH,
                    () => HandleSlotClicked(cell), out _);

                RectTransform slotRect = design.Find($"Slot_{cell:00}") as RectTransform;
                slotGlows.Add(slotRect.GetComponent<AetherHoverGlow>());

                // 아이콘 (칸 안쪽으로 여백)
                RectTransform iconRect = AetherScreenKit.CreateRect(slotRect, "Icon", 0f, 0f, 0f, 0f,
                    typeof(RectTransform), typeof(Image));
                iconRect.anchorMin = new Vector2(0.18f, 0.18f);
                iconRect.anchorMax = new Vector2(0.82f, 0.82f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                Image icon = iconRect.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                slotIcons.Add(icon);

                // 개수
                RectTransform countRect = AetherScreenKit.CreateRect(slotRect, "Count", 0f, 0f, 0f, 0f,
                    typeof(RectTransform), typeof(TextMeshProUGUI));
                countRect.anchorMin = new Vector2(0.1f, 0.06f);
                countRect.anchorMax = new Vector2(0.92f, 0.32f);
                countRect.offsetMin = Vector2.zero;
                countRect.offsetMax = Vector2.zero;

                TMP_Text count = countRect.GetComponent<TextMeshProUGUI>();
                count.fontSize = AetherScreenKit.Font(40f);
                count.alignment = TextAlignmentOptions.BottomRight;
                count.color = AetherScreenKit.TextBright;
                count.raycastTarget = false;
                slotCounts.Add(count);
            }
        }
    }

    private void BuildInfoPanel()
    {
        // 기본 문구를 덮는 판
        RectTransform cover = AetherScreenKit.CreateRect(design, "InfoCover", 2580f, 364f, 3700f, 1684f,
            typeof(RectTransform), typeof(Image));
        cover.GetComponent<Image>().color = InfoPanelColor;
        cover.GetComponent<Image>().raycastTarget = false;
        infoCover = cover.gameObject;
        infoCover.SetActive(false);

        RectTransform iconRect = AetherScreenKit.CreateRect(design, "InfoIcon", 2900f, 500f, 3380f, 980f,
            typeof(RectTransform), typeof(Image));
        infoIcon = iconRect.GetComponent<Image>();
        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;
        infoIcon.enabled = false;

        infoName = AetherScreenKit.CreateText(design, "InfoName", 2640f, 1040f, 3640f, 1130f,
            string.Empty, AetherScreenKit.Font(52f), TextAlignmentOptions.Center, AetherScreenKit.TextBright);

        infoCategory = AetherScreenKit.CreateText(design, "InfoCategory", 2640f, 1140f, 3640f, 1210f,
            string.Empty, AetherScreenKit.Font(38f), TextAlignmentOptions.Center, AetherScreenKit.Accent);

        infoDescription = AetherScreenKit.CreateText(design, "InfoDescription", 2660f, 1260f, 3620f, 1650f,
            string.Empty, AetherScreenKit.Font(36f), TextAlignmentOptions.TopLeft, AetherScreenKit.TextDim);
        infoDescription.enableWordWrapping = true;

        // 사용·버리기. 새 화면에 이 둘이 없어서 물·식량·팩을 인벤토리에서 쓸 수 없었다.
        actionMessage = AetherScreenKit.CreateText(design, "ActionMessage", 2640f, 1700f, 3640f, 1770f,
            string.Empty, AetherScreenKit.Font(32f), TextAlignmentOptions.Center, AetherScreenKit.Accent);
        useButton = CreateActionButton("UseButton", 2640f, 1790f, 3120f, 1900f, "[F]  사용", UseSelected);
        dropButton = CreateActionButton("DropButton", 3160f, 1790f, 3640f, 1900f, "[X]  버리기", DropSelected);
    }

    private TMP_Text actionMessage;
    private Button useButton;
    private Button dropButton;

    private Button CreateActionButton(string name, float x0, float y0, float x1, float y1, string label, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = AetherScreenKit.CreateRect(design, name, x0, y0, x1, y1,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        Image image = rect.GetComponent<Image>();
        image.color = new Color(0.06f, 0.13f, 0.2f, 1f);
        Outline outline = rect.GetComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.83f, 1f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = rect.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.75f, 0.92f, 1f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        button.colors = colors;
        button.onClick.AddListener(action);
        rect.gameObject.AddComponent<AetherUiButtonSound>();

        TMP_Text text = AetherScreenKit.CreateText(rect, "Label", 0f, 0f, 0f, 0f, label,
            AetherScreenKit.Font(40f), TextAlignmentOptions.Center, AetherScreenKit.TextBright);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private int SelectedInventoryIndex =>
        selectedSlot >= 0 && selectedSlot < visibleSlotIndices.Count ? visibleSlotIndices[selectedSlot] : -1;

    private void UseSelected()
    {
        int index = SelectedInventoryIndex;
        if (index < 0 || inventory == null)
            return;

        ItemData item = inventory.GetSlot(index)?.item;
        string name = item != null ? GetDisplayName(item) : string.Empty;

        // 설계서: '보기' = 제작법 펼치기
        if (IsBlueprint(item))
        {
            PaperInventoryPreviewUI.Toggle(item);
            return;
        }

        if (!AetherItemUse.IsUsable(item))
            return;

        bool used = AetherItemUse.UseAt(inventory, index, out string message);
        SetActionMessage(used ? $"{name} 사용" : message, !used);
    }

    private void DropSelected()
    {
        int index = SelectedInventoryIndex;
        if (index < 0 || inventory == null)
            return;

        ItemData item = inventory.GetSlot(index)?.item;
        string name = item != null ? GetDisplayName(item) : string.Empty;
        if (item != null && !AetherItemUse.IsDroppable(item))
        {
            SetActionMessage("제작 재료·설계서는 버릴 수 없습니다.", true);
            return;
        }

        bool dropped = AetherItemUse.DropAt(inventory, index, out string message);
        SetActionMessage(dropped ? $"{name}: 바닥에 내려놓았습니다 (F 로 다시 줍기)" : message, !dropped);
    }

    private void SetActionMessage(string text, bool warning)
    {
        if (actionMessage == null)
            return;
        actionMessage.text = text ?? string.Empty;
        actionMessage.color = warning ? new Color(1f, 0.62f, 0.4f, 1f) : AetherScreenKit.Accent;
    }

    /// <summary>
    /// 고른 아이템으로 할 수 있는 것만 버튼으로 보인다. 설계서는 '사용' 대신 '보기'(제작법), 버릴 수 없는 것은 '버리기'를 숨긴다.
    /// (못 누르는 버튼이 밝게 떠 있어 눌러도 되는 것처럼 보였다.)
    /// </summary>
    private void UpdateActionButtons(ItemData item)
    {
        bool paper = IsBlueprint(item);

        if (useButton != null)
        {
            useButton.gameObject.SetActive(item != null && (paper || AetherItemUse.IsUsable(item)));
            TMP_Text label = useButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = paper ? "[F]  보기" : "[F]  사용";
        }

        if (dropButton != null)
            dropButton.gameObject.SetActive(item != null && AetherItemUse.IsDroppable(item));
    }

    private static bool IsBlueprint(ItemData item)
    {
        return item != null && ItemNameRuleUtility.IsPaperObject(ItemNameRuleUtility.GetBestItemId(item));
    }

    private void BuildFooter()
    {
        // 이미지에 "00 / 24"가 그려져 있어 같은 색으로 덮고 실제 값을 쓴다.
        RectTransform cover = AetherScreenKit.CreateRect(design, "FooterCover", 216f, 1920f, 452f, 2030f,
            typeof(RectTransform), typeof(Image));
        cover.GetComponent<Image>().color = FooterColor;
        cover.GetComponent<Image>().raycastTarget = false;

        capacityText = AetherScreenKit.CreateText(design, "Capacity", 216f, 1930f, 452f, 2020f,
            "00 / 24", AetherScreenKit.Font(44f), TextAlignmentOptions.Left, AetherScreenKit.TextBright);

        // 용량 바
        RectTransform barRect = AetherScreenKit.CreateRect(design, "CapacityBar", 456f, 1968f, 2468f, 1982f,
            typeof(RectTransform), typeof(Image));
        barRect.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.22f, 1f);
        barRect.GetComponent<Image>().raycastTarget = false;

        RectTransform fillRect = AetherScreenKit.CreateRect(barRect, "Fill", 0f, 0f, 0f, 0f,
            typeof(RectTransform), typeof(Image));
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        capacityFill = fillRect.GetComponent<Image>();
        capacityFill.color = AetherScreenKit.Accent;
        capacityFill.raycastTarget = false;
    }
}
