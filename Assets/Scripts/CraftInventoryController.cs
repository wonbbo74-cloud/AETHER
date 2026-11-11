using UnityEngine;
using System.Collections.Generic;

public class CraftInventoryController : MonoBehaviour
{
    [Header("All Item 슬롯들")]
    public List<InventorySlot> allItemSlots = new List<InventorySlot>();

    [Header("모든 아이템 데이터")]
    public List<CraftItemDataSO> allItems = new List<CraftItemDataSO>();

    void Start()
    {
        PopulateAllItems();
    }

    void PopulateAllItems()
    {
        // 슬롯 초기화
        foreach (var slot in allItemSlots)
        {
            slot.Clear();
        }

        // 아이템을 슬롯에 순서대로 배치
        for (int i = 0; i < allItems.Count; i++)
        {
            if (i >= allItemSlots.Count)
                break;

            allItemSlots[i].SetItem(allItems[i]);
        }
    }
}
