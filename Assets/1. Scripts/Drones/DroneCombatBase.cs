using UnityEngine;

public abstract class DroneCombatBase : MonoBehaviour
{
    protected Drones owner;

    public virtual void Initialize(Drones drone)
    {
        owner = drone;
    }

    public virtual void OnEnterCombat()
    {
    }

    public virtual void OnExitCombat()
    {
    }

    public virtual bool TryOverrideCombatDetection(out bool detected)
    {
        detected = false;
        return false;
    }

    public abstract void TickCombat();
}
