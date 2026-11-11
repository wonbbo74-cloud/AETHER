using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PartSystemRuntimeState
{
    private struct InventoryEntry
    {
        public ItemData item;
        public int count;
    }

    private static readonly List<InventoryEntry> inventorySnapshot = new List<InventoryEntry>();
    private static readonly Dictionary<PartSlotType, PartData> equippedSnapshot = new Dictionary<PartSlotType, PartData>();
    private static readonly Dictionary<PartSlotType, PartType> externalEquippedSnapshot = new Dictionary<PartSlotType, PartType>();
    private static readonly List<PartData> craftedEquipmentParts = new List<PartData>();
    private static readonly HashSet<string> collectedSceneItemKeys = new HashSet<string>();

    private static PlayerInventory boundInventory;
    private static EquipmentManager boundEquipment;
    private static bool hasInventorySnapshot;
    private static bool hasEquipmentSnapshot;
    private static bool restoringInventory;
    private static bool restoringEquipment;
    private static bool subscribedToScenes;
    private static int lastSceneLoadFrame = -1000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeSceneBinding()
    {
        SubscribeSceneEvents();
        GetOrBindBestInventory();
    }

    private static void SubscribeSceneEvents()
    {
        if (subscribedToScenes)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribedToScenes = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastSceneLoadFrame = Time.frameCount;
        GetOrBindBestInventory();
    }

    public static PlayerInventory GetOrBindBestInventory()
    {
        PlayerInventory inventory = FindBestInventory();
        if (inventory != null)
            BindInventory(inventory);

        return inventory != null ? inventory : boundInventory;
    }

    public static PlayerInventory FindBestInventory()
    {
        PlayerInventory[] inventories = UnityEngine.Object.FindObjectsOfType<PlayerInventory>(true);
        if (inventories == null || inventories.Length == 0)
            return null;

        PlayerInventory best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < inventories.Length; i++)
        {
            PlayerInventory inventory = inventories[i];
            if (inventory == null)
                continue;

            int score = ScoreInventory(inventory);
            if (score > bestScore)
            {
                best = inventory;
                bestScore = score;
            }
        }

        return best;
    }

    private static int ScoreInventory(PlayerInventory inventory)
    {
        int score = 0;
        GameObject owner = inventory.gameObject;

        if (owner.activeInHierarchy)
            score += 1000;

        // 지금 조작 중인 캐릭터의 인벤토리. (예전에는 한결만 쳐 줘서 윤서 인벤토리가 밀렸다.)
        GameObject active = AetherPlayerCharacter.FindActive();
        if (active != null && inventory.transform.IsChildOf(active.transform))
            score += 500;
        else if (inventory.GetComponentInParent<Move_Hankyul>() != null || inventory.GetComponentInParent<Move_Yunseo>() != null)
            score += 100;

        string lowerName = owner.name.ToLowerInvariant();
        if (lowerName.Contains("hankyul") || lowerName.Contains("한결"))
            score += 250;

        if (owner.CompareTag("Player"))
            score += 100;

        score += CountOccupiedSlots(inventory);
        return score;
    }

    public static void BindInventory(PlayerInventory inventory)
    {
        if (boundInventory == inventory)
            return;

        if (boundInventory != null)
            boundInventory.OnInventoryChanged -= CaptureBoundInventory;

        boundInventory = inventory;

        if (boundInventory == null)
            return;

        if (hasInventorySnapshot)
            RestoreInventory(boundInventory);
        else if (CountOccupiedSlots(boundInventory) > 0)
            CaptureInventory(boundInventory);

        // 강화파츠는 인벤토리가 아니라 파츠 보관함에 둔다.
        MovePartsOutOfInventory(boundInventory);

        boundInventory.OnInventoryChanged += CaptureBoundInventory;
    }

    public static void BindEquipment(EquipmentManager equipment)
    {
        if (boundEquipment == equipment)
            return;

        UnsubscribeEquipment();
        boundEquipment = equipment;

        if (boundEquipment == null)
            return;

        if (hasEquipmentSnapshot)
            RestoreEquipment(boundEquipment);
        else
            CaptureEquipment(boundEquipment);

        SubscribeEquipment(boundEquipment);
    }

    /// <summary>
    /// 새 판을 시작할 때 이전 판의 인벤토리/장비/수집 기록을 모두 버린다.
    /// static 필드는 플레이 세션 내내 살아있으므로 명시적으로 지워주지 않으면
    /// 메인 메뉴로 나갔다 다시 시작해도 지난 판 아이템이 그대로 남는다.
    /// </summary>
    public static void ResetForNewRun()
    {
        inventorySnapshot.Clear();
        equippedSnapshot.Clear();
        externalEquippedSnapshot.Clear();
        craftedEquipmentParts.Clear();
        collectedSceneItemKeys.Clear();

        hasInventorySnapshot = false;
        hasEquipmentSnapshot = false;

        UnsubscribeEquipment();

        if (boundInventory != null)
            boundInventory.OnInventoryChanged -= CaptureBoundInventory;

        boundInventory = null;
        boundEquipment = null;
    }

    public static void RegisterCraftedEquipmentPart(PartData part)
    {
        if (part == null)
            return;

        if (!craftedEquipmentParts.Contains(part))
            craftedEquipmentParts.Add(part);

        AetherSave.MarkDirty();
    }

    // ------------------------------------------------------------------ 저장 / 이어하기

    /// <summary>저장할 값을 꺼낸다. 아이템은 Resources/AETHERParts 의 에셋 이름으로 적는다.</summary>
    public static void Export(AetherSaveData data)
    {
        if (boundInventory != null && !restoringInventory)
            CaptureInventory(boundInventory);
        if (boundEquipment != null && !restoringEquipment)
            CaptureEquipment(boundEquipment);

        data.inventory.Clear();
        foreach (InventoryEntry entry in inventorySnapshot)
            if (entry.item != null && entry.count > 0)
                data.inventory.Add(new AetherSaveData.ItemCount { name = entry.item.name, count = entry.count });

        data.craftedParts.Clear();
        foreach (PartData part in craftedEquipmentParts)
            if (part != null) data.craftedParts.Add(part.name);

        data.equipped.Clear();
        foreach (KeyValuePair<PartSlotType, PartData> pair in equippedSnapshot)
            if (pair.Value != null)
                data.equipped.Add(new AetherSaveData.SlotPart { slot = pair.Key.ToString(), name = pair.Value.name });

        data.collectedSceneItems = new List<string>(collectedSceneItemKeys);
    }

    /// <summary>저장된 값을 되살린다. 다음 장면에서 플레이어가 생기면 인벤토리·장비에 들어간다.</summary>
    public static void Import(AetherSaveData data)
    {
        ResetForNewRun();
        ItemData[] all = Resources.LoadAll<ItemData>("AETHERParts");

        foreach (AetherSaveData.ItemCount entry in data.inventory)
        {
            ItemData item = FindByName(all, entry.name);
            if (item != null && entry.count > 0)
                inventorySnapshot.Add(new InventoryEntry { item = item, count = entry.count });
        }
        hasInventorySnapshot = inventorySnapshot.Count > 0;

        foreach (string name in data.craftedParts)
            if (FindByName(all, name) is PartData part && !craftedEquipmentParts.Contains(part))
                craftedEquipmentParts.Add(part);

        foreach (AetherSaveData.SlotPart entry in data.equipped)
            if (Enum.TryParse(entry.slot, out PartSlotType slot) && FindByName(all, entry.name) is PartData part)
                equippedSnapshot[slot] = part;
        hasEquipmentSnapshot = equippedSnapshot.Count > 0;

        foreach (string key in data.collectedSceneItems)
            collectedSceneItemKeys.Add(key);
    }

    private static ItemData FindByName(ItemData[] all, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (ItemData item in all)
            if (item != null && item.name == name)
                return item;
        return null;
    }

    public static IReadOnlyList<PartData> GetCraftedEquipmentParts()
    {
        return craftedEquipmentParts;
    }

    // ------------------------------------------------------------------ 강화파츠 보관함
    //
    // 강화파츠는 인벤토리에 들어가지 않는다. 만든 파츠는 이 보관함(제작 기록)에 들어가고, 파츠 장착 화면(P)만
    // 이것을 다룬다. 장착 중인 파츠는 장비에, 나머지는 보관함에 있는 것으로 본다.

    /// <summary>이 파츠를 가지고 있는지(만들었는지).</summary>
    public static bool IsPartOwned(PartData part)
    {
        return part != null && craftedEquipmentParts.Contains(part);
    }

    /// <summary>장착하지 않고 보관 중인 강화파츠.</summary>
    public static List<PartData> GetStoredParts(EquipmentManager equipment)
    {
        List<PartData> result = new List<PartData>();
        foreach (PartData part in craftedEquipmentParts)
        {
            if (part == null || result.Contains(part))
                continue;
            if (equipment != null && equipment.GetEquipped(PartSlotTypeUtility.Normalize(part.slotType)) == part)
                continue;
            result.Add(part);
        }
        return result;
    }

    /// <summary>
    /// 인벤토리에 들어 있는 강화파츠를 보관함으로 옮긴다. 예전 방식(인벤토리에 넣던 것)이나
    /// 다른 경로로 들어온 파츠가 인벤토리에 남지 않게 한다.
    /// </summary>
    public static bool MovePartsOutOfInventory(PlayerInventory inventory)
    {
        if (inventory == null || inventory.slots == null)
            return false;

        bool moved = false;
        foreach (PlayerInventory.Slot slot in inventory.slots)
        {
            if (slot == null || slot.count <= 0 || !(slot.item is PartData part))
                continue;

            RegisterCraftedEquipmentPart(part);
            slot.item = null;
            slot.count = 0;
            moved = true;
        }

        if (moved)
            inventory.NotifyInventoryChanged();
        return moved;
    }

    public static bool HasCollectedSceneItem(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && collectedSceneItemKeys.Contains(key);
    }

    public static void RegisterCollectedSceneItem(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            collectedSceneItemKeys.Add(key);
        AetherSave.MarkDirty();
    }

    private static void CaptureBoundInventory()
    {
        if (!restoringInventory)
            CaptureInventory(boundInventory);
    }

    private static void CaptureBoundEquipment(PartSlotType slot, PartData part)
    {
        if (!restoringEquipment)
            CaptureEquipment(boundEquipment);
    }

    private static void CaptureInventory(PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        int occupiedSlots = CountOccupiedSlots(inventory);
        bool hasStoredItems = inventorySnapshot.Count > 0;
        if (occupiedSlots == 0 && hasStoredItems)
            return;

        inventorySnapshot.Clear();
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            inventorySnapshot.Add(new InventoryEntry
            {
                item = slot.item,
                count = slot.count
            });
        }

        hasInventorySnapshot = true;
        AetherSave.MarkDirty();
    }

    private static void RestoreInventory(PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        restoringInventory = true;

        int neededSlots = Mathf.Max(inventory.slotCount, inventorySnapshot.Count);
        while (inventory.slots.Count < neededSlots)
            inventory.slots.Add(new PlayerInventory.Slot());

        inventory.slotCount = Mathf.Max(inventory.slotCount, inventory.slots.Count);

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot == null)
            {
                slot = new PlayerInventory.Slot();
                inventory.slots[i] = slot;
            }

            slot.item = null;
            slot.count = 0;
        }

        for (int i = 0; i < inventorySnapshot.Count; i++)
        {
            InventoryEntry entry = inventorySnapshot[i];
            inventory.slots[i].item = entry.item;
            inventory.slots[i].count = entry.count;
        }

        restoringInventory = false;
        inventory.NotifyInventoryChanged();
    }

    private static int CountOccupiedSlots(PlayerInventory inventory)
    {
        if (inventory == null || inventory.slots == null)
            return 0;

        int count = 0;
        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot != null && slot.item != null && slot.count > 0)
                count++;
        }

        return count;
    }

    private static void CaptureEquipment(EquipmentManager equipment)
    {
        if (equipment == null)
            return;

        equippedSnapshot.Clear();
        externalEquippedSnapshot.Clear();
        HashSet<PartSlotType> processedSlots = new HashSet<PartSlotType>();

        foreach (PartSlotType slot in Enum.GetValues(typeof(PartSlotType)))
        {
            PartSlotType logicalSlot = PartSlotTypeUtility.Normalize(slot);
            if (!processedSlots.Add(logicalSlot))
                continue;

            PartData part = equipment.GetEquipped(logicalSlot);
            if (part != null)
                equippedSnapshot[logicalSlot] = part;

            PartType externalType = equipment.GetExternalEquipped(logicalSlot);
            if (externalType != PartType.None)
                externalEquippedSnapshot[logicalSlot] = externalType;
        }

        hasEquipmentSnapshot = true;
        AetherSave.MarkDirty();
    }

    private static void RestoreEquipment(EquipmentManager equipment)
    {
        if (equipment == null)
            return;

        restoringEquipment = true;
        HashSet<PartSlotType> processedSlots = new HashSet<PartSlotType>();

        foreach (PartSlotType slot in Enum.GetValues(typeof(PartSlotType)))
        {
            PartSlotType logicalSlot = PartSlotTypeUtility.Normalize(slot);
            if (!processedSlots.Add(logicalSlot))
                continue;

            equippedSnapshot.TryGetValue(logicalSlot, out PartData part);
            equipment.RestoreEquippedPart(logicalSlot, part);

            PartType externalType = externalEquippedSnapshot.TryGetValue(logicalSlot, out PartType type)
                ? type
                : PartType.None;
            equipment.SetExternalEquipped(logicalSlot, externalType);
        }

        restoringEquipment = false;
    }

    private static void SubscribeEquipment(EquipmentManager equipment)
    {
        equipment.OnEquipmentChanged += CaptureBoundEquipment;
        equipment.OnPartEquipped += CaptureBoundEquipment;
        equipment.OnPartUnequipped += CaptureBoundEquipment;
    }

    private static void UnsubscribeEquipment()
    {
        if (boundEquipment == null)
            return;

        boundEquipment.OnEquipmentChanged -= CaptureBoundEquipment;
        boundEquipment.OnPartEquipped -= CaptureBoundEquipment;
        boundEquipment.OnPartUnequipped -= CaptureBoundEquipment;
    }
}
