using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentUIController : MonoBehaviour
{
    public enum EquipSlot { LeftArm, RightArm, LeftLeg, RightLeg }

    [Header("Tab Buttons")]
    [SerializeField] private Button tabArmsButton;
    [SerializeField] private Button tabLegsButton;

    [Header("Inventory Slots")]
    [SerializeField] private InventoryItemButton[] inventorySlots;

    [Header("Equipment Item Pool")]
    [SerializeField] private List<EquipmentItemData> allArmItems = new List<EquipmentItemData>();
    [SerializeField] private List<EquipmentItemData> allLegItems = new List<EquipmentItemData>();
    [SerializeField] private bool autoLoadEquipmentItems = true;

    [Header("Crafted Inventory Source")]
    [SerializeField] private PlayerInventory sourceInventory;
    [SerializeField] private bool onlyShowCraftedPartsInInventory = true;

    [Header("Equipped Icons")]
    [SerializeField] private Image equippedLeftArmIcon;
    [SerializeField] private Image equippedRightArmIcon;
    [SerializeField] private Image equippedLeftLegIcon;
    [SerializeField] private Image equippedRightLegIcon;

    [Header("Info Panel")]
    [SerializeField] private TMP_Text textPartName;
    [SerializeField] private TMP_Text textType;
    [SerializeField] private TMP_Text textAttack;
    [SerializeField] private TMP_Text textDefense;
    [SerializeField] private TMP_Text textSpeed;
    [SerializeField] private TMP_Text textDescription;

    public System.Action<EquipSlot, EquipmentItemData> OnEquipmentChanged;

    private EquipmentPartType currentTab = EquipmentPartType.Arm;
    private EquipmentItemData selectedItem;
    private InventoryItemButton selectedSlotButton;
    private EquipmentItemData eqLeftArm, eqRightArm, eqLeftLeg, eqRightLeg;
    private EquipmentManager equipmentManager;
    private readonly List<EquipmentItemData> remainingArms = new List<EquipmentItemData>();
    private readonly List<EquipmentItemData> remainingLegs = new List<EquipmentItemData>();
    private readonly List<EquipmentItemData> craftedRuntimeItems = new List<EquipmentItemData>();

    private void Awake()
    {
        if (sourceInventory == null)
            sourceInventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged += HandleSourceInventoryChanged;

        if (tabArmsButton != null)
            tabArmsButton.onClick.AddListener(() => SetTab(EquipmentPartType.Arm));

        if (tabLegsButton != null)
            tabLegsButton.onClick.AddListener(() => SetTab(EquipmentPartType.Leg));

        if (inventorySlots != null)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                inventorySlots[i].Setup(this);
            }
        }

        EnsureEquipmentPools();
        ResetRemainingLists();
        SetTab(EquipmentPartType.Arm);
        ShowEmptyInfo();
        RefreshEquippedIcons();
    }

    private void OnEnable()
    {
        RebindSourceInventory();
        EnsureEquipmentPools();
        RestoreCraftedRuntimeItems();
        SelectBestTabForCurrentInventory();
        ResetRemainingLists();
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
        RefreshEquippedIcons();
    }

    private void OnDestroy()
    {
        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged -= HandleSourceInventoryChanged;
    }

    public void SetSourceInventory(PlayerInventory inventory)
    {
        if (sourceInventory == inventory)
            return;

        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged -= HandleSourceInventoryChanged;

        sourceInventory = inventory;

        if (sourceInventory != null)
            sourceInventory.OnInventoryChanged += HandleSourceInventoryChanged;

        HandleSourceInventoryChanged();
    }

    public void SetEquipmentPools(List<EquipmentItemData> armItems, List<EquipmentItemData> legItems)
    {
        allArmItems = armItems ?? new List<EquipmentItemData>();
        allLegItems = legItems ?? new List<EquipmentItemData>();
        HandleSourceInventoryChanged();
    }

    public void RefreshFromInventory()
    {
        RebindSourceInventory();

        EnsureEquipmentPools();
        RestoreCraftedRuntimeItems();
        SelectBestTabForCurrentInventory();
        HandleSourceInventoryChanged();
    }

    public void FocusCraftedPart(PartData craftedPart)
    {
        if (craftedPart != null)
            currentTab = ToEquipmentPartType(craftedPart.slotType);

        RefreshFromInventory();
    }

    public void AddCraftedPartToSlots(PartData craftedPart)
    {
        if (craftedPart == null)
            return;

        EnsureEquipmentPools();

        EquipmentItemData equipmentItem = FindEquipmentItemForPart(craftedPart);
        if (equipmentItem == null)
        {
            Debug.LogWarning("[EquipmentUIController] Crafted part has no matching EquipmentItemData: " + craftedPart.name, this);
            return;
        }

        if (!craftedRuntimeItems.Contains(equipmentItem))
            craftedRuntimeItems.Add(equipmentItem);

        PartSystemRuntimeState.RegisterCraftedEquipmentPart(craftedPart);

        currentTab = equipmentItem.partType;
        ResetRemainingLists();
        AddCraftedRuntimeToRemaining(equipmentItem);
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
        RefreshEquippedIcons();
        SelectItem(equipmentItem, FindSlotButtonForItem(equipmentItem));
    }

    private void EnsureEquipmentPools()
    {
        if (!autoLoadEquipmentItems)
            return;

        EquipmentItemData[] loadedItems = Resources.LoadAll<EquipmentItemData>("AETHERParts");
        if (loadedItems == null)
            return;

        for (int i = 0; i < loadedItems.Length; i++)
        {
            EquipmentItemData item = loadedItems[i];
            if (item == null)
                continue;

            List<EquipmentItemData> targetList = item.partType == EquipmentPartType.Leg ? allLegItems : allArmItems;
            if (!ContainsEquipmentItem(targetList, item))
                targetList.Add(item);
        }
    }

    private void RestoreCraftedRuntimeItems()
    {
        IReadOnlyList<PartData> craftedParts = PartSystemRuntimeState.GetCraftedEquipmentParts();
        if (craftedParts == null)
            return;

        for (int i = 0; i < craftedParts.Count; i++)
        {
            EquipmentItemData equipmentItem = FindEquipmentItemForPart(craftedParts[i]);
            if (equipmentItem == null)
                continue;

            if (!craftedRuntimeItems.Contains(equipmentItem))
                craftedRuntimeItems.Add(equipmentItem);
        }
    }

    private static bool ContainsEquipmentItem(List<EquipmentItemData> list, EquipmentItemData item)
    {
        if (list == null || item == null)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            EquipmentItemData existing = list[i];
            if (existing == item)
                return true;

            if (existing != null
                && existing.sourceCraftedPart != null
                && item.sourceCraftedPart != null
                && existing.sourceCraftedPart == item.sourceCraftedPart)
                return true;
        }

        return false;
    }

    private static EquipmentPartType ToEquipmentPartType(PartSlotType slotType)
    {
        return slotType == PartSlotType.Leg ? EquipmentPartType.Leg : EquipmentPartType.Arm;
    }

    private void RebindSourceInventory()
    {
        PlayerInventory bestInventory = PartSystemRuntimeState.GetOrBindBestInventory();
        if (bestInventory != null && sourceInventory != bestInventory)
            SetSourceInventory(bestInventory);
        else if (sourceInventory == null)
            sourceInventory = bestInventory;
    }

    private void SelectBestTabForCurrentInventory()
    {
        if (HasAvailableInventoryItem(EquipmentPartType.Leg))
        {
            currentTab = EquipmentPartType.Leg;
            return;
        }

        if (HasAvailableInventoryItem(EquipmentPartType.Arm))
            currentTab = EquipmentPartType.Arm;
    }

    private bool HasAvailableInventoryItem(EquipmentPartType partType)
    {
        List<EquipmentItemData> source = partType == EquipmentPartType.Leg ? allLegItems : allArmItems;
        if (source == null)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            if (CanShowEquipmentItem(source[i]))
                return true;
        }

        return false;
    }

    private void ResetRemainingLists()
    {
        remainingArms.Clear();
        remainingLegs.Clear();

        for (int i = 0; i < allArmItems.Count; i++)
        {
            if (CanShowEquipmentItem(allArmItems[i]))
                remainingArms.Add(allArmItems[i]);
        }

        for (int i = 0; i < allLegItems.Count; i++)
        {
            if (CanShowEquipmentItem(allLegItems[i]))
                remainingLegs.Add(allLegItems[i]);
        }
    }

    private bool CanShowEquipmentItem(EquipmentItemData item)
    {
        if (item == null)
            return false;

        if (!onlyShowCraftedPartsInInventory)
            return true;

        if (craftedRuntimeItems.Contains(item))
            return true;

        if (item.sourceCraftedPart == null)
            return false;

        string id = ItemNameRuleUtility.GetBestItemId(item.sourceCraftedPart);
        string displayName = item.sourceCraftedPart.displayName;
        string bestName = ItemNameRuleUtility.GetBestItemName(null, item.sourceCraftedPart);

        bool isMaterial = ItemNameRuleUtility.IsModuleItem(id)
            || ItemNameRuleUtility.IsModuleItem(displayName)
            || ItemNameRuleUtility.IsModuleItem(bestName);

        bool isPaper = ItemNameRuleUtility.IsPaperObject(id)
            || ItemNameRuleUtility.IsPaperObject(displayName)
            || ItemNameRuleUtility.IsPaperObject(bestName);

        if (isMaterial || isPaper)
            return false;

        return sourceInventory != null && sourceInventory.HasItem(item.sourceCraftedPart, 1);
    }

    private void HandleSourceInventoryChanged()
    {
        ResetRemainingLists();
        RemoveEquippedFromRemaining();
        RebuildInventoryUIForCurrentTab();
    }

    private void RemoveEquippedFromRemaining()
    {
        RemoveFromRemaining(eqLeftArm);
        RemoveFromRemaining(eqRightArm);
        RemoveFromRemaining(eqLeftLeg);
        RemoveFromRemaining(eqRightLeg);
    }

    private void SetTab(EquipmentPartType tab)
    {
        currentTab = tab;
        ClearSelection();
        ShowEmptyInfo();
        RebuildInventoryUIForCurrentTab();
    }

    private void RebuildInventoryUIForCurrentTab()
    {
        if (inventorySlots == null)
            return;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
                continue;

            inventorySlots[i].item = null;
            inventorySlots[i].RefreshIcon();
            inventorySlots[i].RefreshInteractable();
        }

        List<EquipmentItemData> source = currentTab == EquipmentPartType.Arm ? remainingArms : remainingLegs;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null)
                continue;

            if (i < source.Count)
            {
                inventorySlots[i].item = source[i];
                inventorySlots[i].RefreshIcon();
                inventorySlots[i].RefreshInteractable();
            }
        }
    }

    public void SelectItem(EquipmentItemData item, InventoryItemButton slotButton)
    {
        if (item == null)
            return;

        selectedItem = item;
        selectedSlotButton = slotButton;
        ShowItemInfo(item);
    }

    private void ClearSelection()
    {
        selectedItem = null;
        selectedSlotButton = null;
    }

    public EquipmentItemData GetEquippedItem(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.LeftArm: return eqLeftArm;
            case EquipSlot.RightArm: return eqRightArm;
            case EquipSlot.LeftLeg: return eqLeftLeg;
            case EquipSlot.RightLeg: return eqRightLeg;
            default: return null;
        }
    }

    public void TryEquipFromDrag(EquipSlot slot, InventoryItemButton sourceInventorySlot)
    {
        if (sourceInventorySlot == null || sourceInventorySlot.item == null)
            return;

        // 파츠 장착은 한결만 한다. 이 UI 는 장비에 알리기 전에 인벤토리에서 먼저 빼므로
        // EquipmentManager 에서 막는 것만으로는 윤서의 파츠가 사라진다. 여기서 먼저 막는다.
        if (!AetherPlayerCharacter.CanEquipParts(AetherPlayerCharacter.FindActive()))
        {
            AetherObjectiveToast.Show(AetherPlayerCharacter.PartsEquipOnlyHankyulMessage, 3f);
            return;
        }

        EquipmentItemData draggedItem = sourceInventorySlot.item;
        bool isArmSlot = slot == EquipSlot.LeftArm || slot == EquipSlot.RightArm;
        bool isLegSlot = slot == EquipSlot.LeftLeg || slot == EquipSlot.RightLeg;

        if (isArmSlot && draggedItem.partType != EquipmentPartType.Arm)
            return;

        if (isLegSlot && draggedItem.partType != EquipmentPartType.Leg)
            return;

        ConsumeCraftedPartFromInventory(draggedItem);
        EquipInternal(slot, draggedItem);
        RemoveFromRemaining(draggedItem);
        sourceInventorySlot.Consume();

        if (selectedSlotButton == sourceInventorySlot)
        {
            ClearSelection();
            ShowEmptyInfo();
        }

        RebuildInventoryUIForCurrentTab();
    }

    public void TryMoveEquippedToInventory(EquipSlot fromEquipSlot, InventoryItemButton targetInventorySlot)
    {
        if (targetInventorySlot == null || targetInventorySlot.item != null)
            return;

        EquipmentItemData item = GetEquippedItem(fromEquipSlot);
        if (item == null)
            return;

        ClearEquipped(fromEquipSlot);
        ReturnCraftedPartToInventory(item);
        AddToRemaining(item);
        RefreshEquippedIcons();
        RebuildInventoryUIForCurrentTab();
    }

    public void TryMoveEquippedToFirstEmpty(EquipSlot fromEquipSlot)
    {
        EquipmentItemData item = GetEquippedItem(fromEquipSlot);
        if (item == null)
            return;

        ClearEquipped(fromEquipSlot);
        ReturnCraftedPartToInventory(item);
        AddToRemaining(item);
        RefreshEquippedIcons();
        RebuildInventoryUIForCurrentTab();
    }

    private void EquipInternal(EquipSlot slot, EquipmentItemData item)
    {
        EquipmentItemData previous = GetEquippedItem(slot);
        if (previous != null && previous != item)
        {
            ReturnCraftedPartToInventory(previous);
            AddToRemaining(previous);
        }

        switch (slot)
        {
            case EquipSlot.LeftArm: eqLeftArm = item; break;
            case EquipSlot.RightArm: eqRightArm = item; break;
            case EquipSlot.LeftLeg: eqLeftLeg = item; break;
            case EquipSlot.RightLeg: eqRightLeg = item; break;
        }

        OnEquipmentChanged?.Invoke(slot, item);
        SyncEquipmentManager(slot, item);
        RefreshEquippedIcons();
    }

    private void ClearEquipped(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.LeftArm: eqLeftArm = null; break;
            case EquipSlot.RightArm: eqRightArm = null; break;
            case EquipSlot.LeftLeg: eqLeftLeg = null; break;
            case EquipSlot.RightLeg: eqRightLeg = null; break;
        }

        OnEquipmentChanged?.Invoke(slot, null);
        SyncEquipmentManager(slot, null);
    }

    private void SyncEquipmentManager(EquipSlot slot, EquipmentItemData item)
    {
        // 지금 조작 중인 캐릭터에 장착한다. 예전에는 꺼진 한결까지 찾아서, 윤서로 플레이할 때
        // 파츠가 꺼진 한결에게 들어가고 윤서는 슈퍼점프 등을 못 썼다.
        GameObject activePlayer = AetherPlayerCharacter.FindActive();
        if (activePlayer != null && AetherPlayerCharacter.Identify(activePlayer) == AetherCharacter.Yunseo)
        {
            AetherPlayerCharacter.Prepare(activePlayer);
            equipmentManager = activePlayer.GetComponentInChildren<EquipmentManager>(true);
        }

        Move_Hankyul hankyulMove = GetComponentInParent<Move_Hankyul>();
        if (hankyulMove == null && activePlayer != null)
            hankyulMove = activePlayer.GetComponentInChildren<Move_Hankyul>();
        if (hankyulMove == null && activePlayer == null)
            hankyulMove = FindObjectOfType<Move_Hankyul>(true);

        if (hankyulMove != null)
        {
            equipmentManager = hankyulMove.GetComponent<EquipmentManager>();
            if (equipmentManager == null)
                equipmentManager = hankyulMove.gameObject.AddComponent<EquipmentManager>();

            PlayerInventory inventory = hankyulMove.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = hankyulMove.GetComponentInChildren<PlayerInventory>(true);
            if (inventory != null)
                equipmentManager.SetInventory(inventory);
        }

        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);

        PartSlotType partSlot = ConvertToPartSlot(slot);
        PartType partType = item != null ? item.abilityType : PartType.None;

        if (equipmentManager != null)
        {
            PartSystemRuntimeState.BindEquipment(equipmentManager);
            equipmentManager.SetExternalEquipped(partSlot, partType);
        }

        HangyeolPartAbilityController abilityController = null;
        if (hankyulMove != null)
            abilityController = hankyulMove.GetComponent<HangyeolPartAbilityController>();
        if (abilityController == null && hankyulMove != null)
            abilityController = hankyulMove.gameObject.AddComponent<HangyeolPartAbilityController>();
        if (abilityController == null && equipmentManager != null)
            abilityController = equipmentManager.GetComponent<HangyeolPartAbilityController>();
        if (abilityController == null)
            abilityController = GetComponentInParent<HangyeolPartAbilityController>();
        if (abilityController == null && hankyulMove != null)
            abilityController = FindObjectOfType<HangyeolPartAbilityController>(true);

        // 윤서의 능력(YunseoPartAbilityController)은 EquipmentManager.HasPart 를 매 프레임 직접 본다.
        if (abilityController != null)
            abilityController.RefreshSuperJumpState();

        if (hankyulMove != null && partSlot == PartSlotType.Leg)
        {
            bool hasSuperJump = partType == PartType.SuperJump
                || equipmentManager != null && equipmentManager.HasPart(PartType.SuperJump);
            hankyulMove.SetSuperJumpEnabled(hasSuperJump);
        }
    }

    private static PartSlotType ConvertToPartSlot(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.LeftLeg:
            case EquipSlot.RightLeg:
                return PartSlotType.Leg;
            case EquipSlot.LeftArm:
            case EquipSlot.RightArm:
                return PartSlotType.Arm;
            default:
                return PartSlotType.Utility;
        }
    }

    private void RemoveFromRemaining(EquipmentItemData item)
    {
        if (item == null)
            return;

        if (item.partType == EquipmentPartType.Arm)
            remainingArms.Remove(item);
        else if (item.partType == EquipmentPartType.Leg)
            remainingLegs.Remove(item);
    }

    private void AddToRemaining(EquipmentItemData item)
    {
        if (item == null || !CanShowEquipmentItem(item))
            return;

        if (item.partType == EquipmentPartType.Arm && !remainingArms.Contains(item))
            remainingArms.Add(item);
        else if (item.partType == EquipmentPartType.Leg && !remainingLegs.Contains(item))
            remainingLegs.Add(item);
    }

    private void AddCraftedRuntimeToRemaining(EquipmentItemData item)
    {
        if (item == null)
            return;

        if (item.partType == EquipmentPartType.Arm && !remainingArms.Contains(item))
            remainingArms.Add(item);
        else if (item.partType == EquipmentPartType.Leg && !remainingLegs.Contains(item))
            remainingLegs.Add(item);
    }

    private void ConsumeCraftedPartFromInventory(EquipmentItemData item)
    {
        if (item != null)
            craftedRuntimeItems.Remove(item);

        if (sourceInventory == null || item == null || item.sourceCraftedPart == null)
            return;

        sourceInventory.ConsumeItem(item.sourceCraftedPart, 1);
    }

    private void ReturnCraftedPartToInventory(EquipmentItemData item)
    {
        if (item != null && !craftedRuntimeItems.Contains(item))
            craftedRuntimeItems.Add(item);

        if (sourceInventory == null || item == null || item.sourceCraftedPart == null)
            return;

        sourceInventory.AddItem(item.sourceCraftedPart, 1);
    }

    private EquipmentItemData FindEquipmentItemForPart(PartData craftedPart)
    {
        if (craftedPart == null)
            return null;

        EquipmentItemData found = FindEquipmentItemForPart(allArmItems, craftedPart);
        if (found != null)
            return found;

        return FindEquipmentItemForPart(allLegItems, craftedPart);
    }

    private static EquipmentItemData FindEquipmentItemForPart(List<EquipmentItemData> source, PartData craftedPart)
    {
        if (source == null || craftedPart == null)
            return null;

        for (int i = 0; i < source.Count; i++)
        {
            EquipmentItemData item = source[i];
            if (item != null && item.sourceCraftedPart == craftedPart)
                return item;
        }

        return null;
    }

    private InventoryItemButton FindSlotButtonForItem(EquipmentItemData item)
    {
        if (inventorySlots == null || item == null)
            return null;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] != null && inventorySlots[i].item == item)
                return inventorySlots[i];
        }

        return null;
    }

    private void RefreshEquippedIcons()
    {
        ApplyEquippedIcon(equippedLeftArmIcon, eqLeftArm);
        ApplyEquippedIcon(equippedRightArmIcon, eqRightArm);
        ApplyEquippedIcon(equippedLeftLegIcon, eqLeftLeg);
        ApplyEquippedIcon(equippedRightLegIcon, eqRightLeg);
    }

    private void ApplyEquippedIcon(Image target, EquipmentItemData item)
    {
        if (target == null)
            return;

        Sprite icon = GetDisplayIcon(item);
        if (item != null && icon != null)
        {
            target.enabled = true;
            target.sprite = icon;
            target.preserveAspect = true;
        }
        else
        {
            target.enabled = false;
            target.sprite = null;
        }
    }

    private static Sprite GetDisplayIcon(EquipmentItemData item)
    {
        if (item == null)
            return null;

        if (item.icon != null)
            return item.icon;

        return item.sourceCraftedPart != null ? item.sourceCraftedPart.itemSprite : null;
    }

    private void ShowEmptyInfo()
    {
        if (textPartName != null) textPartName.text = "-";
        if (textType != null) textType.text = "-";
        if (textAttack != null) textAttack.text = "+0";
        if (textDefense != null) textDefense.text = "+0";
        if (textSpeed != null) textSpeed.text = "+0";
        if (textDescription != null) textDescription.text = "Select an item to view details";
    }

    private void ShowItemInfo(EquipmentItemData item)
    {
        if (item == null)
        {
            ShowEmptyInfo();
            return;
        }

        if (textPartName != null) textPartName.text = item.itemName;
        if (textType != null) textType.text = item.partType.ToString();
        if (textAttack != null) textAttack.text = item.attack >= 0 ? $"+{item.attack}" : item.attack.ToString();
        if (textDefense != null) textDefense.text = item.defense >= 0 ? $"+{item.defense}" : item.defense.ToString();
        if (textSpeed != null) textSpeed.text = item.speed >= 0 ? $"+{item.speed}" : item.speed.ToString();
        if (textDescription != null) textDescription.text = string.IsNullOrEmpty(item.description) ? "-" : item.description;
    }
}
