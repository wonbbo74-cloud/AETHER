using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PartInventoryEntryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    private PartCraftingUI owner;
    private ItemData item;

    public ItemData Item => item;
    public static ItemData DraggedItem { get; private set; }

    public void Setup(PartCraftingUI newOwner, ItemData newItem, int count)
    {
        owner = newOwner;
        item = newItem;

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (icon == null)
            icon = FindIconImage();

        if (label != null)
        {
            string itemName = item != null && !string.IsNullOrWhiteSpace(item.displayName)
                ? item.displayName
                : ItemNameRuleUtility.GetBestItemId(item);
            label.text = count > 1 ? $"{itemName} x{count}" : itemName;
        }

        if (icon != null && item != null)
        {
            icon.sprite = item.itemSprite;
            icon.enabled = item.itemSprite != null;
        }
    }

    private Image FindIconImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate != null && candidate.transform != transform && candidate.name.ToLowerInvariant().Contains("icon"))
                return candidate;
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate != null && candidate.transform != transform)
                return candidate;
        }

        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.SelectModule(item);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        DraggedItem = item;
        if (owner != null)
            owner.SelectModule(item);
    }

    // Unity 는 IDragHandler 가 있어야 끌기를 시작한다. 이게 없어 OnBeginDrag·OnDrop 이 한 번도 불리지 않았다.
    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DraggedItem = null;
    }
}
