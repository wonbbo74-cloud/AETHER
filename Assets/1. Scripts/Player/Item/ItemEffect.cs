using UnityEngine;

public enum EffectType
{
    Health,
    Thirst,
    CustomText,
    EnergyPack,
    ShieldPack
}

[System.Serializable]
public class ItemEffect
{
    public EffectType type;
    public int value;
    public string customText;
    public Sprite icon;
}