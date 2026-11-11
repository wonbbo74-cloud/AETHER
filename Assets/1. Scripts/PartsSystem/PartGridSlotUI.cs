using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PartGridSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image background;

    private PartCraftingGridManager manager;
    private Vector2Int position;
    private ItemData item;

    public Vector2Int Position => position;
    public ItemData Item => item;

    public void Setup(PartCraftingGridManager newManager, Vector2Int newPosition)
    {
        manager = newManager;
        position = newPosition;

        if (icon == null)
            icon = transform.Find("Icon") != null ? transform.Find("Icon").GetComponent<Image>() : null;

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (background == null)
            background = GetComponent<Image>();

        SetItem(null);
    }

    public void SetItem(ItemData newItem)
    {
        item = newItem;

        if (icon != null)
        {
            icon.sprite = item != null ? item.itemSprite : null;
            icon.enabled = item != null && item.itemSprite != null;
        }

        if (label != null)
        {
            label.text = item != null ? ShortName(item) : string.Empty;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (manager != null && PartInventoryEntryUI.DraggedItem != null)
            manager.PlaceItem(position, PartInventoryEntryUI.DraggedItem);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null)
            manager.PlaceSelectedItem(position);
    }

    private static string ShortName(ItemData data)
    {
        string id = ItemNameRuleUtility.GetBestItemId(data);
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        id = id.Replace("_module", string.Empty);
        return id.Length <= 8 ? id : id.Substring(0, 8);
    }
}
