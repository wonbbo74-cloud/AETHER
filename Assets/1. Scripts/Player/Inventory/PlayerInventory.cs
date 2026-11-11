using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 슬롯 설정")]
    public int slotCount = 30;

    [Serializable]
    public class Slot
    {
        public ItemData item;
        public int count;
    }

    public List<Slot> slots = new List<Slot>();

    public event Action OnInventoryChanged;
    private readonly List<ItemData> recentItems = new List<ItemData>();

    private void Awake()
    {
        // 슬롯리스트 크기 정렬
        if (slots.Count < slotCount)
        {
            int add = slotCount - slots.Count;
            for (int i = 0; i < add; i++)
            {
                slots.Add(new Slot());
            }
        }
        else if (slots.Count > slotCount)
        {
            slots.RemoveRange(slotCount, slots.Count - slotCount);
        }

        RaiseChanged();
    }

    public int SlotCount => slots.Count;

    public Slot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public bool AddItem(ItemData data, int amount)
    {
        if (data == null || amount <= 0) return false;

        // 1) 같은 아이템 → 스택
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.item == data && s.count > 0)
            {
                s.count += amount;
                MarkItemRecentlyAdded(data);
                RaiseChanged();
                return true;
            }
        }

        // 2) 빈 슬롯
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.item == null || s.count <= 0)
            {
                s.item = data;
                s.count = amount;
                MarkItemRecentlyAdded(data);
                RaiseChanged();
                return true;
            }
        }

        Debug.LogWarning("[Inventory] 빈 슬롯이 없습니다.");
        return false;
    }


    public bool HasItem(ItemData data, int amount = 1)
    {
        if (data == null || amount <= 0) return false;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.item == data && slot.count > 0)
            {
                total += slot.count;
                if (total >= amount)
                    return true;
            }
        }

        return false;
    }

    public bool ConsumeItem(ItemData data, int amount = 1)
    {
        if (!HasItem(data, amount)) return false;

        int remaining = amount;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.item != data || slot.count <= 0)
                continue;

            int consume = Mathf.Min(slot.count, remaining);
            slot.count -= consume;
            remaining -= consume;

            if (slot.count <= 0)
            {
                slot.item = null;
                slot.count = 0;
            }
        }

        RaiseChanged();
        return true;
    }

    public void NotifyInventoryChanged()
    {
        PruneRecentItems();
        RaiseChanged();
    }
    // index 슬롯에서 amount만큼 사용/버리기
    public bool ConsumeAt(int index, int amount)
    {
        var slot = GetSlot(index);
        if (slot == null || slot.item == null || slot.count <= 0) return false;
        if (amount <= 0) return false;

        slot.count -= amount;
        if (slot.count <= 0)
        {
            slot.item = null;
            slot.count = 0;
        }

        PruneRecentItems();
        RaiseChanged();
        return true;
    }

    public void DebugLogInventory()
    {
        Debug.Log("=== Inventory Debug ===");

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            if (slot.item == null || slot.count <= 0)
            {
                Debug.Log($"Slot {i}: (빈 슬롯)");
            }
            else
            {
                Debug.Log($"Slot {i}: {slot.item.displayName} x {slot.count}");
            }
        }

        Debug.Log("========================");
    }

    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= slots.Count) return;
        if (indexB < 0 || indexB >= slots.Count) return;
        if (indexA == indexB) return;

        var temp = slots[indexA];
        slots[indexA] = slots[indexB];
        slots[indexB] = temp;

        RaiseChanged();
    }

    public List<int> GetRecentOccupiedSlotIndices(int maxCount)
    {
        PruneRecentItems();

        List<int> indices = new List<int>();
        if (maxCount <= 0)
            return indices;

        for (int r = 0; r < recentItems.Count && indices.Count < maxCount; r++)
        {
            ItemData recentItem = recentItems[r];
            int index = FindFirstSlotIndex(recentItem);
            if (index >= 0 && !indices.Contains(index))
                indices.Add(index);
        }

        for (int i = 0; i < slots.Count && indices.Count < maxCount; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.item != null && slot.count > 0 && !indices.Contains(i))
                indices.Add(i);
        }

        return indices;
    }

    private void MarkItemRecentlyAdded(ItemData data)
    {
        if (data == null)
            return;

        recentItems.Remove(data);
        recentItems.Insert(0, data);
    }

    private void PruneRecentItems()
    {
        for (int i = recentItems.Count - 1; i >= 0; i--)
        {
            if (FindFirstSlotIndex(recentItems[i]) < 0)
                recentItems.RemoveAt(i);
        }
    }

    private int FindFirstSlotIndex(ItemData data)
    {
        if (data == null)
            return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.item == data && slot.count > 0)
                return i;
        }

        return -1;
    }

    private void RaiseChanged()
    {
        OnInventoryChanged?.Invoke();
    }


}
