using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어 UI가 인스펙터에서 연결되지 않아 아무것도 안 뜨는 문제를 런타임에 복구한다.
///
/// 실제로 비어 있던 것들:
///  - PlayerUIManager.pickupPanel / pickupText / pickupIcon → "F : 아이템명" 안내가 절대 안 떴다.
///  - PlayerUIManager.quickSlotManager → 퀵슬롯 바인딩 블록 자체가 통째로 스킵됐다.
///  - 씬에 EventSystem이 없으면 버튼 클릭이 전혀 먹지 않는다.
///
/// 프리팹을 직접 수정하지 않고 실행 중에만 채워 넣으므로, 나중에 인스펙터에서 제대로
/// 연결하면 이 스크립트는 아무것도 건드리지 않고 물러난다.
/// </summary>
public class AetherUIBootstrap : MonoBehaviour
{
    private const string CanvasName = "AETHER Runtime UI Canvas";
    private const float BindWindowSeconds = 8f;

    private static bool subscribed;
    private static AetherUIBootstrap instance;

    private float bindTimer;
    private bool wired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableUIBootstrap)
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
            instance.Restart();
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherUIBootstrap");
        instance = host.AddComponent<AetherUIBootstrap>();
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
        Restart();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Restart()
    {
        wired = false;
        bindTimer = BindWindowSeconds;
        pickupToast = null;
        trackedInventory = null;
    }

    private void Update()
    {
        if (wired)
        {
            UpdateToast();
            return;
        }

        bindTimer -= Time.unscaledDeltaTime;

        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Exclude);
        if (interactor == null)
        {
            if (bindTimer <= 0f)
                wired = true; // 플레이어가 없는 씬(메뉴 등)에서는 조용히 포기한다.

            return;
        }

        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();

        WirePlayerUIManager(interactor, canvas);
        WireInventoryUI(interactor);
        WirePickupToast(interactor, canvas);

        wired = true;
    }

    // ------------------------------------------------------------ 기반 UI

    /// <summary>EventSystem이 없으면 버튼·드래그가 전혀 동작하지 않는다.</summary>
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<InputSystemUIInputModule>();
        Debug.Log("[AetherUIBootstrap] EventSystem이 없어 새로 만들었습니다. (버튼 입력 복구)");
    }

    /// <summary>플레이어가 쓰는 화면 캔버스를 찾고, 없으면 만든다.</summary>
    private static Canvas EnsureCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null;
        int bestOrder = int.MinValue;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (canvas.sortingOrder > bestOrder)
            {
                best = canvas;
                bestOrder = canvas.sortingOrder;
            }
        }

        if (best != null)
            return best;

        GameObject go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas created = go.GetComponent<Canvas>();
        created.renderMode = RenderMode.ScreenSpaceOverlay;
        created.sortingOrder = 100;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Debug.Log("[AetherUIBootstrap] 화면 캔버스가 없어 새로 만들었습니다.");
        return created;
    }

    // ------------------------------------------------------------ 픽업 안내

    private void WirePlayerUIManager(PlayerInteractor interactor, Canvas canvas)
    {
        PlayerUIManager ui = interactor.GetComponentInParent<PlayerUIManager>();
        if (ui == null)
            ui = FindFirstObjectByType<PlayerUIManager>(FindObjectsInactive.Include);

        if (ui == null)
            return;

        if (ui.interactor == null)
            ui.interactor = interactor;

        if (ui.pickupPanel == null)
            BuildPickupPanel(ui, PickupCanvasFor(ui, canvas));
        else if (!IsVisibleCanvas(ui.pickupPanel.GetComponentInParent<Canvas>(true)))
        {
            // 예전에 꺼진 캔버스(파츠 장착 화면) 밑에 만들어 둔 패널이면 플레이어 화면으로 옮긴다.
            Canvas target = PickupCanvasFor(ui, canvas);
            if (target != null)
                ui.pickupPanel.transform.SetParent(target.transform, false);
        }

        NormalizeCrosshair(ui);

        if (ui.quickSlotManager == null)
        {
            QuickSlotManager quickSlots = interactor.GetComponentInParent<QuickSlotManager>();
            if (quickSlots == null)
                quickSlots = FindFirstObjectByType<QuickSlotManager>(FindObjectsInactive.Include);

            if (quickSlots != null)
            {
                ui.quickSlotManager = quickSlots;

                QuickSlotUI[] slotUIs = ui.quickSlotUIs;
                if (slotUIs == null || slotUIs.Length == 0)
                    slotUIs = FindObjectsByType<QuickSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                // HUD 가 이미 칸을 순서대로 연결해 두었으면 건드리지 않는다. 예전엔 장면의 칸 10개(숨은 옛 칸 포함)를
                // 순서 없이 다시 물려, 숫자 키와 칸 그림이 서로 달랐다(2번을 누르면 가운데 칸 아이템이 쓰였다).
                if (quickSlots.HasBoundUI)
                    slotUIs = null;

                // HUD(AetherModernHUD) 의 퀵슬롯 칸은 HUD 의 관리자 몫이다. 여기서 옛 관리자(꺼져 있는 QuickSlotBar 등)에
                // 함께 물리면, 아이템을 얻을 때마다 옛 관리자가 그 칸들을 자기 순서로 다시 그려 비워 버렸다
                // (05 에서 상점 팩을 받자 물·식량 칸이 빈칸으로 보임).
                if (slotUIs != null)
                    slotUIs = System.Array.FindAll(slotUIs, s => s != null && s.GetComponentInParent<AetherModernHUD>(true) == null);

                if (slotUIs != null && slotUIs.Length > 0)
                {
                    ui.quickSlotUIs = slotUIs;
                    for (int i = 0; i < slotUIs.Length; i++)
                    {
                        if (slotUIs[i] != null)
                            slotUIs[i].Bind(quickSlots);
                    }

                    quickSlots.BindUI(slotUIs);
                }

                PlayerInventory inventory = interactor.Inventory;
                PlayerStatus status = interactor.GetComponentInParent<PlayerStatus>();
                if (inventory != null || status != null)
                    quickSlots.SetRuntimeReferences(inventory, status);

                Debug.Log("[AetherUIBootstrap] 퀵슬롯을 연결했습니다.");
            }
        }
    }

    // 화면(1920x1080) 기준 크로스헤어 지름. PC FPS는 보통 20~50px 사이를 쓴다.
    private const float CrosshairNormalPixels = 42f;
    private const float CrosshairHoverPixels = 54f;

    // 이 크기를 넘으면 "너무 크다"고 보고 줄인다(이미 알맞게 맞춰 둔 값은 건드리지 않는다).
    private const float CrosshairTooLargePixels = 80f;

    /// <summary>
    /// 크로스헤어가 화면에서 차지하는 크기를 PC 게임에 맞게 맞춘다.
    ///
    /// 기본값이 100x100 이미지에 배율 2.0이라 실제로는 200픽셀짜리 원이 그려지고,
    /// 상호작용 대상을 보면 225픽셀까지 커진다. 화면 한가운데 그만한 원이 뜨면
    /// 조준도 어렵고 시야도 가린다.
    /// </summary>
    private static void NormalizeCrosshair(PlayerUIManager ui)
    {
        if (ui == null || ui.crosshairImage == null)
            return;

        RectTransform rect = ui.crosshairImage.rectTransform;
        float baseSize = Mathf.Max(rect.rect.width, rect.rect.height);
        if (baseSize <= 1f)
            return;

        float currentPixels = baseSize * Mathf.Max(ui.normalScale.x, ui.hoverScale.x);
        if (currentPixels <= CrosshairTooLargePixels)
            return;

        ui.normalScale = Vector3.one * (CrosshairNormalPixels / baseSize);
        ui.hoverScale = Vector3.one * (CrosshairHoverPixels / baseSize);
        rect.localScale = ui.normalScale;

        Debug.Log($"[AetherUIBootstrap] 크로스헤어가 너무 커서 {currentPixels:0}px → {CrosshairNormalPixels:0}px로 줄였습니다.");
    }

    /// <summary>
    /// 줍기 안내를 둘 캔버스. 정렬 순서가 가장 높은 캔버스를 고르면 평소 꺼져 있는
    /// 파츠 장착 화면 캔버스가 뽑혀서, Meka 의 줍기 안내가 영영 안 보였다.
    /// 플레이어 자신의 화면 캔버스(Player_UI)를 먼저 쓴다.
    /// </summary>
    private static Canvas PickupCanvasFor(PlayerUIManager ui, Canvas fallback)
    {
        foreach (Canvas c in ui.transform.root.GetComponentsInChildren<Canvas>(false))
        {
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && IsVisibleCanvas(c))
                return c;
        }

        return IsVisibleCanvas(fallback) ? fallback : null;
    }

    private static bool IsVisibleCanvas(Canvas c)
    {
        return c != null && c.enabled && c.gameObject.activeInHierarchy && !c.name.StartsWith("PartsSlot2_");
    }

    /// <summary>"F : 아이템명" 안내 패널을 만들어 PlayerUIManager에 꽂아준다.</summary>
    private static void BuildPickupPanel(PlayerUIManager ui, Canvas canvas)
    {
        GameObject panel = new GameObject("Runtime Pickup Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        // 1080p 전체 화면에서 420x62·글자 26 은 너무 컸다(다른 FPS 의 상호작용 안내는 대개 글자 18~20). 글에 맞춰 폭을 줄인다.
        rect.sizeDelta = new Vector2(220f, 42f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.03f, 0.05f, 0.07f, 0.78f);
        background.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(panel.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(26f, 0f);
        iconRect.sizeDelta = new Vector2(28f, 28f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(50f, 0f);
        textRect.offsetMax = new Vector2(-14f, 0f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 19f;
        label.color = new Color(0.92f, 0.97f, 1f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;

        ui.pickupPanel = panel;
        ui.pickupIcon = icon;
        ui.pickupText = label;
        panel.SetActive(false);

        Debug.Log("[AetherUIBootstrap] 픽업 안내 패널이 비어 있어 런타임으로 만들었습니다.");
    }

    // ------------------------------------------------------------ 인벤토리

    /// <summary>
    /// InventoryUI가 엉뚱한 PlayerInventory를 잡고 있으면 아이템을 주워도 칸이 안 채워진다.
    /// (InventoryUI.Awake는 FindObjectOfType로 아무거나 집는다.)
    /// </summary>
    private static void WireInventoryUI(PlayerInteractor interactor)
    {
        PlayerInventory playerInventory = interactor.Inventory;
        if (playerInventory == null)
            playerInventory = interactor.GetComponentInParent<PlayerInventory>();

        if (playerInventory == null)
            return;

        InventoryUI[] uis = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < uis.Length; i++)
        {
            InventoryUI ui = uis[i];
            if (ui == null)
                continue;

            bool rebound = false;
            if (ui.inventory != playerInventory)
            {
                // InventoryUI.Awake는 예전 인벤토리의 OnInventoryChanged를 구독해 둔 상태다.
                // 필드만 바꾸면 첫 표시는 맞아도 이후 "아이템을 주웠을 때" 갱신 신호가
                // 엉뚱한 인벤토리에서 날아와 화면이 그대로 멈춰 있게 된다.
                PlayerInventory previous = ui.inventory;
                if (previous != null)
                    previous.OnInventoryChanged -= ui.RefreshAllSlots;

                ui.inventory = playerInventory;
                playerInventory.OnInventoryChanged -= ui.RefreshAllSlots;
                playerInventory.OnInventoryChanged += ui.RefreshAllSlots;

                rebound = true;
                Debug.Log("[AetherUIBootstrap] InventoryUI가 다른 인벤토리를 보고 있어 플레이어 것으로 바꾸고 갱신 신호도 다시 연결했습니다.");
            }

            bool builtSlots = EnsureInventorySlots(ui, playerInventory);

            if (rebound || builtSlots)
            {
                ui.SendMessage("CacheSlotsAndInitialize", SendMessageOptions.DontRequireReceiver);
                ui.RefreshAllSlots();
            }
        }

        // 씬을 넘어온 아이템이 화면에 반영되도록 한 번 갱신한다.
        playerInventory.NotifyInventoryChanged();
    }

    /// <summary>
    /// 인벤토리 칸(InventorySlotUI)이 하나도 없으면 만들어 준다.
    ///
    /// InventoryUI.CacheSlotsAndInitialize는 slotsParent 아래에 이미 존재하는 슬롯을 "찾기만" 한다.
    /// 그런데 플레이어 프리팹에는 InventorySlotUI가 단 하나도 없어서, 아이템을 주워도
    /// 칸에 아무것도 그려지지 않았다. 여기서 칸을 만들어 채운다.
    /// </summary>
    private static bool EnsureInventorySlots(InventoryUI ui, PlayerInventory inventory)
    {
        if (ui.slotsParent == null)
        {
            Debug.LogWarning("[AetherUIBootstrap] InventoryUI.slotsParent가 비어 있어 슬롯을 만들 수 없습니다.", ui);
            return false;
        }

        if (ui.slotsParent.GetComponentsInChildren<InventorySlotUI>(true).Length > 0)
            return false;

        int slotCount = inventory != null && inventory.slotCount > 0 ? inventory.slotCount : 30;

        RectTransform content = ui.slotsParent as RectTransform;
        if (content != null)
        {
            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 6;
                grid.cellSize = new Vector2(146f, 139f);
                grid.spacing = new Vector2(15f, 13f);
                grid.childAlignment = TextAnchor.UpperLeft;
            }

            // 스크롤 영역 안이라면 칸 수에 맞춰 높이가 늘어나야 한다.
            if (content.GetComponentInParent<ScrollRect>() != null
                && content.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        for (int i = 0; i < slotCount; i++)
            CreateInventorySlot(ui.slotsParent, i);

        // 새로 만든 칸에도 기존 스킨을 다시 입힌다.
        ModernInventorySkin.Apply(ui);

        Debug.Log($"[AetherUIBootstrap] 인벤토리 칸이 없어 {slotCount}개를 만들었습니다.", ui);
        return true;
    }

    private static void CreateInventorySlot(Transform parent, int index)
    {
        GameObject slotObject = new GameObject($"Slot_{index:00}",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(InventorySlotUI));
        slotObject.transform.SetParent(parent, false);

        // 클릭/드래그를 받기 위한 거의 투명한 히트 영역
        Image hitArea = slotObject.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0.06f);
        hitArea.raycastTarget = true;

        // 선택 표시 테두리
        GameObject checkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkObject.transform.SetParent(slotObject.transform, false);
        StretchFull(checkObject.GetComponent<RectTransform>());
        Image check = checkObject.GetComponent<Image>();
        check.color = new Color(1f, 1f, 1f, 0f);
        check.raycastTarget = false;

        // 아이템 아이콘
        GameObject iconObject = new GameObject("ItemImage", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.15f, 0.15f);
        iconRect.anchorMax = new Vector2(0.85f, 0.85f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        // 개수 뱃지
        GameObject badgeObject = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeObject.transform.SetParent(slotObject.transform, false);
        RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
        badgeRect.anchorMin = Vector2.zero;
        badgeRect.anchorMax = Vector2.one;
        badgeRect.offsetMin = new Vector2(4f, 4f);
        badgeRect.offsetMax = new Vector2(-6f, -4f);

        TextMeshProUGUI badge = badgeObject.GetComponent<TextMeshProUGUI>();
        badge.fontSize = 18f;
        badge.alignment = TextAlignmentOptions.BottomRight;
        badge.color = Color.white;
        badge.raycastTarget = false;
        badge.text = string.Empty;

        InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
        slot.iconImage = icon;
        slot.countText = badge;
        slot.checkmarkImage = check;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------ 획득 알림

    private TMP_Text pickupToast;
    private float toastTimer;
    private PlayerInventory trackedInventory;
    private readonly Dictionary<ItemData, int> lastCounts = new Dictionary<ItemData, int>();

    /// <summary>아이템을 실제로 주웠을 때 "+ 이름 xN"을 잠깐 띄운다.</summary>
    private void WirePickupToast(PlayerInteractor interactor, Canvas canvas)
    {
        PlayerInventory inventory = interactor.Inventory;
        if (inventory == null)
            inventory = interactor.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            return;

        if (pickupToast == null)
        {
            GameObject toastObject = new GameObject("Runtime Pickup Toast", typeof(RectTransform), typeof(TextMeshProUGUI));
            toastObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = toastObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 190f);
            rect.sizeDelta = new Vector2(720f, 44f);

            pickupToast = toastObject.GetComponent<TMP_Text>();
            pickupToast.fontSize = 26f;
            pickupToast.alignment = TextAlignmentOptions.Center;
            pickupToast.color = new Color(0.65f, 0.95f, 1f, 1f);
            pickupToast.raycastTarget = false;
            pickupToast.text = string.Empty;
        }

        if (trackedInventory != null)
            trackedInventory.OnInventoryChanged -= HandleInventoryChanged;

        trackedInventory = inventory;
        CaptureCounts();
        trackedInventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void CaptureCounts()
    {
        lastCounts.Clear();
        if (trackedInventory == null || trackedInventory.slots == null)
            return;

        for (int i = 0; i < trackedInventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = trackedInventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            lastCounts.TryGetValue(slot.item, out int current);
            lastCounts[slot.item] = current + slot.count;
        }
    }

    private void HandleInventoryChanged()
    {
        if (trackedInventory == null || pickupToast == null)
            return;

        Dictionary<ItemData, int> now = new Dictionary<ItemData, int>();
        for (int i = 0; i < trackedInventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = trackedInventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            now.TryGetValue(slot.item, out int current);
            now[slot.item] = current + slot.count;
        }

        foreach (KeyValuePair<ItemData, int> pair in now)
        {
            lastCounts.TryGetValue(pair.Key, out int before);
            int gained = pair.Value - before;
            if (gained <= 0)
                continue;

            string name = !string.IsNullOrWhiteSpace(pair.Key.displayName) ? pair.Key.displayName : pair.Key.name;
            pickupToast.text = gained > 1 ? $"+ {name} x{gained}" : $"+ {name}";
            toastTimer = 2.2f;
            break;
        }

        lastCounts.Clear();
        foreach (KeyValuePair<ItemData, int> pair in now)
            lastCounts[pair.Key] = pair.Value;
    }

    private void UpdateToast()
    {
        if (pickupToast == null)
            return;

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;

            Color color = pickupToast.color;
            color.a = Mathf.Clamp01(toastTimer / 0.6f);
            // 알림이 장면 전환용 최상단 캔버스에 붙어 인벤토리·제작 화면 칸 위에 "+ 식량" 이 겹쳐 보였다(10/12 최종 점검).
            if (AetherUIState.AnyScreenOpen)
                color.a = 0f;
            pickupToast.color = color;

            if (toastTimer <= 0f)
                pickupToast.text = string.Empty;
        }
    }
}
