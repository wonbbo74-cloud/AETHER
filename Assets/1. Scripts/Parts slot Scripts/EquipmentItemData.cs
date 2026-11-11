using UnityEngine;

public enum EquipmentPartType
{
    Arm,
    Leg,
    Support,
    Head,
    Core
}

[CreateAssetMenu(menuName = "AETHER/Equipment Item Data", fileName = "NewEquipmentItem")]
public class EquipmentItemData : ScriptableObject
{
    [Header("�⺻ ����")]
    public string itemName;
    public EquipmentPartType partType;

    [Header("����")]
    public int attack;
    public int defense;
    public int speed;

    [Header("����")]
    [TextArea(3, 8)]
    public string description;

    [Header("UI ������(������ null ����)")]
    public Sprite icon;

    [Header("Crafted Part Link")]
    public PartData sourceCraftedPart;
    public PartType abilityType = PartType.None;
    public bool enableSuperJump;
    public float jumpForceAdd = 0f;
    public float jumpForceMultiplier = 1f;

    private void OnValidate()
    {
        if (sourceCraftedPart == null)
            return;

        if (string.IsNullOrWhiteSpace(itemName))
            itemName = string.IsNullOrWhiteSpace(sourceCraftedPart.displayName)
                ? sourceCraftedPart.name
                : sourceCraftedPart.displayName;

        if (icon == null)
            icon = sourceCraftedPart.itemSprite != null ? sourceCraftedPart.itemSprite : sourceCraftedPart.icon;

        abilityType = sourceCraftedPart.partType;
        partType = ToEquipmentPartType(sourceCraftedPart.slotType);

        if (attack == 0)
            attack = sourceCraftedPart.attackBonus;
        if (defense == 0)
            defense = sourceCraftedPart.defenseBonus;
        if (speed == 0)
            speed = sourceCraftedPart.speedBonus;
    }

    public static EquipmentPartType ToEquipmentPartType(PartSlotType slotType)
    {
        switch (PartSlotTypeUtility.Normalize(slotType))
        {
            case PartSlotType.Head:
                return EquipmentPartType.Head;
            case PartSlotType.Leg:
                return EquipmentPartType.Leg;
            case PartSlotType.Support:
                return EquipmentPartType.Support;
            case PartSlotType.Core:
                return EquipmentPartType.Core;
            default:
                return EquipmentPartType.Arm;
        }
    }
}
