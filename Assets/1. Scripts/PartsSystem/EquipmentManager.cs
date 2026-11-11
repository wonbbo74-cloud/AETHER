using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    private readonly Dictionary<PartSlotType, PartData> equipped = new Dictionary<PartSlotType, PartData>();
    private readonly Dictionary<PartSlotType, PartType> externalEquippedTypes = new Dictionary<PartSlotType, PartType>();

    public event Action<PartSlotType, PartData> OnEquipmentChanged;
    public event Action<PartSlotType, PartData> OnPartEquipped;
    public event Action<PartSlotType, PartData> OnPartUnequipped;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
    }

    public void SetInventory(PlayerInventory sourceInventory)
    {
        if (sourceInventory != null)
            inventory = sourceInventory;
    }

    public PartData GetEquipped(PartSlotType slot)
    {
        slot = PartSlotTypeUtility.Normalize(slot);
        return equipped.TryGetValue(slot, out PartData part) ? part : null;
    }

    public bool HasPart(PartType type)
    {
        foreach (PartData part in equipped.Values)
        {
            if (part != null && part.partType == type)
                return true;
        }

        foreach (PartType externalType in externalEquippedTypes.Values)
        {
            if (externalType == type)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 파츠 장착은 한결만 한다. 장착 화면이 여러 개(새 장착 화면·예전 슬롯 UI·파츠슬롯2)라
    /// 화면마다 막으면 빠지는 곳이 생기므로, 모든 장착이 거치는 여기서 막는다.
    /// </summary>
    private bool RefuseIfCannotEquip()
    {
        if (AetherPlayerCharacter.CanEquipParts(gameObject))
            return false;

        AetherObjectiveToast.Show(AetherPlayerCharacter.PartsEquipOnlyHankyulMessage, 3f);
        return true;
    }

    public void SetExternalEquipped(PartSlotType slot, PartType type)
    {
        if (type != PartType.None && RefuseIfCannotEquip())
            return;

        slot = PartSlotTypeUtility.Normalize(slot);
        if (type == PartType.None)
            externalEquippedTypes.Remove(slot);
        else
            externalEquippedTypes[slot] = type;

        OnEquipmentChanged?.Invoke(slot, null);
    }

    public PartType GetExternalEquipped(PartSlotType slot)
    {
        slot = PartSlotTypeUtility.Normalize(slot);
        return externalEquippedTypes.TryGetValue(slot, out PartType type) ? type : PartType.None;
    }

    public void RestoreEquippedPart(PartSlotType slot, PartData part)
    {
        // 씬을 넘어올 때 복원하는 경로. 윤서에게는 안내 없이 조용히 건너뛴다.
        if (part != null && !AetherPlayerCharacter.CanEquipParts(gameObject))
            return;

        slot = PartSlotTypeUtility.Normalize(slot);
        if (part == null)
            equipped.Remove(slot);
        else
            equipped[slot] = part;

        OnEquipmentChanged?.Invoke(slot, part);
    }

    public bool EquipPart(PartData part)
    {
        if (part == null)
            return false;

        if (RefuseIfCannotEquip())
            return false;

        PartSlotType slot = PartSlotTypeUtility.Normalize(part.slotType);

        foreach (KeyValuePair<PartSlotType, PartData> pair in equipped)
        {
            if (pair.Key != slot && pair.Value == part)
                return false;
        }

        if (equipped.TryGetValue(slot, out PartData current) && current == part)
            return true;

        // 강화파츠는 인벤토리가 아니라 파츠 보관함(PartSystemRuntimeState)에 있다.
        // 예전처럼 인벤토리에 남아 있던 파츠는 보관함으로 옮긴 뒤 장착한다.
        if (inventory != null && inventory.HasItem(part, 1))
        {
            inventory.ConsumeItem(part, 1);
            PartSystemRuntimeState.RegisterCraftedEquipmentPart(part);
        }

        if (!PartSystemRuntimeState.IsPartOwned(part))
            return false;

        // 원래 끼워져 있던 파츠는 보관함으로 돌아간다(보관함 목록은 '가진 것 - 장착한 것'이라 따로 넣을 필요가 없다).
        if (equipped.TryGetValue(slot, out PartData previous) && previous != null)
            OnPartUnequipped?.Invoke(slot, previous);

        equipped[slot] = part;
        OnEquipmentChanged?.Invoke(slot, part);
        OnPartEquipped?.Invoke(slot, part);
        return true;
    }

    public void Unequip(PartSlotType slot)
    {
        slot = PartSlotTypeUtility.Normalize(slot);
        if (!equipped.TryGetValue(slot, out PartData part) || part == null)
            return;

        // 빼낸 파츠는 인벤토리가 아니라 파츠 보관함으로 돌아간다.
        equipped.Remove(slot);
        PartSystemRuntimeState.RegisterCraftedEquipmentPart(part);

        OnEquipmentChanged?.Invoke(slot, null);
        OnPartUnequipped?.Invoke(slot, part);
    }
}
