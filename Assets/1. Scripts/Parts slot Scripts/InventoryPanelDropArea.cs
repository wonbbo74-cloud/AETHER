using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryPanelDropArea : MonoBehaviour, IDropHandler
{
    public EquipmentUIController controller;

    public void OnDrop(PointerEventData eventData)
    {
        if (controller == null) return;

        var draggedGO = eventData.pointerDrag;
        if (draggedGO == null) return;

        var equippedDrag = draggedGO.GetComponentInParent<EquippedSlotDraggable>();
        if (equippedDrag != null)
        {
            controller.TryMoveEquippedToFirstEmpty(equippedDrag.slot);
        }
    }
}
