using UnityEngine;

public enum DamageType
{
    Generic,
    Bullet,
    Enemy_Bullet,
    Drone_Explosion,
    punch
}

[System.Serializable]
public struct DamageInfo
{
    public float amount;
    public DamageType type;
    public GameObject source;
    public GameObject instigator;
    public Vector3 hitPoint;

    public DamageInfo(float amount, DamageType type, GameObject source = null, GameObject instigator = null, Vector3 hitPoint = default)
    {
        this.amount = amount;
        this.type = type;
        this.source = source;
        this.instigator = instigator;
        this.hitPoint = hitPoint;
    }
}

public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}
