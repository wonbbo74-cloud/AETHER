using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotDropArea : MonoBehaviour, IDropHandler
{
    [Header("컨트롤러")]
    public EquipmentUIController controller;

    [Header("이 인벤 칸이 가리키는 슬롯(InventoryItemButton)")]
    public InventoryItemButton targetSlot;

    public void OnDrop(PointerEventData eventData)
    {
        if (controller == null || targetSlot == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        // ✅ 장착 슬롯에서 끌고 온 경우
        var equippedDrag = draggedGO.GetComponentInParent<EquippedSlotDraggable>();
        if (equippedDrag != null)
        {
            controller.TryMoveEquippedToInventory(equippedDrag.slot, targetSlot);
            return;
        }

        // (선택) 인벤->인벤 이동/스왑도 하고 싶으면 여기 확장 가능
    }
}
