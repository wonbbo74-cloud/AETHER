using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartSlotUI : MonoBehaviour
{
    [SerializeField] private PartSlotType slotType;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    public PartSlotType SlotType => slotType;

    public void SetSlot(PartSlotType type)
    {
        slotType = type;
    }

    public void Refresh(PartData part)
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (icon == null)
            icon = FindIconImage();

        if (label != null)
        {
            string partName = part != null ? part.displayName : "Empty";
            label.text = $"{slotType}: {partName}";
        }

        if (icon != null)
        {
            icon.sprite = part != null ? (part.icon != null ? part.icon : part.itemSprite) : null;
            icon.enabled = icon.sprite != null;
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
}
