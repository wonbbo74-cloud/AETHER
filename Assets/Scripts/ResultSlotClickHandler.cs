using UnityEngine;
using UnityEngine.EventSystems;

public class ResultSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    public InventorySlot slot;
    public ItemDetailPanel detailPanel;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slot == null || detailPanel == null)
            return;

        if (slot.IsEmpty())
        {
            detailPanel.Hide();
            return;
        }

        detailPanel.Show(slot.itemData);
    }
}
