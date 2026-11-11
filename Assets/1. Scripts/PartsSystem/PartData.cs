using UnityEngine;

[CreateAssetMenu(menuName = "AETHER/Parts/Part Data", fileName = "PartData_")]
public class PartData : ItemData
{
    [Header("Part Data")]
    public string partId;
    public Sprite icon;
    public PartType partType = PartType.None;
    public PartSlotType slotType = PartSlotType.Utility;
    public float powerCost = 10f;
    public float cooldown = 1f;
    public float abilityValue = 1f;

    [Header("Crafting Preview Stats")]
    public int attackBonus;
    public int defenseBonus;
    public int speedBonus;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(partId))
            partId = id;
        else if (string.IsNullOrWhiteSpace(id))
            id = partId;

        if (icon == null)
            icon = itemSprite;
        else if (itemSprite == null)
            itemSprite = icon;
    }
}
