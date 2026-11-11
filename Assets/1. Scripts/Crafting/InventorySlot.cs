
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [Header("UI")]
    public RawImage iconRawImage;

    [Header("Data")]
    public CraftItemDataSO itemData;

    private void Start()
    {
        if (iconRawImage == null)
        {
            Debug.LogError($"[InventorySlot] iconRawImage 미연결: {name}");
            return;
        }

        // 아이콘은 Raycast 차단
        iconRawImage.raycastTarget = false;
        Refresh();
    }

    public bool IsEmpty()
    {
        return itemData == null;
    }

    public void SetItem(CraftItemDataSO data)
    {
        itemData = data;
        Refresh();
    }

    public void Clear()
    {
        itemData = null;
        iconRawImage.texture = null;
        iconRawImage.enabled = false;
    }

    private void Refresh()
    {
        if (itemData == null)
        {
            iconRawImage.texture = null;
            iconRawImage.enabled = false;
            return;
        }

        iconRawImage.texture = itemData.iconTexture;
        iconRawImage.enabled = true;
        iconRawImage.color = Color.white;
    }
}
