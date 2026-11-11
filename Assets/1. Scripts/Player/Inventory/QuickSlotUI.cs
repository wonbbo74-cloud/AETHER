using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class QuickSlotUI : MonoBehaviour, IDropHandler
{
    [Header("퀵슬롯 인덱스 (0~4)")]
    public int quickSlotIndex;

    [Header("UI 참조")]
    public Image iconImage;
    public TextMeshProUGUI countText;

    // 외부에서 주입
    private QuickSlotManager manager;

    private void Awake()
    {
        SetEmpty();
    }

    // PlayerUIManager가 연결해줌
    public void Bind(QuickSlotManager m)
    {
        manager = m;
    }

    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (countText != null)
            countText.text = "";
    }

    public void SetItem(ItemData item, int count)
    {
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.itemSprite : null;
            iconImage.enabled = (iconImage.sprite != null);
        }

        if (countText != null)
            countText.text = (count > 1) ? count.ToString() : "";
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (manager == null) return;

        var slotUI = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
        if (slotUI == null) return;

        int invIndex = slotUI.SlotIndex;
        manager.AssignSlot(quickSlotIndex, invIndex);
    }
}
