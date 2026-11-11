using UnityEngine;

public class CraftSlotInitializer : MonoBehaviour
{
    private InventorySlot slot;

    private void Awake()
    {
        slot = GetComponent<InventorySlot>();
        if (slot == null) return;

        // 🔥 게임 시작 시 무조건 비우기
        slot.Clear();
    }
}
