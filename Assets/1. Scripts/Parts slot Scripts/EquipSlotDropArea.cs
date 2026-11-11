using UnityEngine;
using UnityEngine.EventSystems;

public class EquipSlotDropArea : MonoBehaviour, IDropHandler
{
    [Header("이 슬롯이 어떤 장착 슬롯인지")]
    public EquipmentUIController.EquipSlot slot;

    [Header("컨트롤러 참조")]
    public EquipmentUIController controller;

    public void OnDrop(PointerEventData eventData)
    {
        if (controller == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        // ✅ Icon 같은 자식에서 드래그가 잡혀도 부모에서 InventoryItemButton을 찾아온다
        var sourceInvSlot = draggedGO.GetComponentInParent<InventoryItemButton>();
        if (sourceInvSlot == null) return;

        controller.TryEquipFromDrag(slot, sourceInvSlot);
    }
}
