using UnityEngine;

public class DroneSuiciderCombat : DroneCombatBase
{
    [Header("Explosion")]
    [SerializeField] private float explodeDamage = 30f;
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private AudioClip explosionSfx;
    [SerializeField] private float explosionVfxLifeTime = 2f;
    [SerializeField] private bool useExplosionRadius = true;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private LayerMask playerMask;

    [Header("Dive Charge")]
    [SerializeField] private float chargeSpeedMultiplier = 1.6f;
    [SerializeField] private float diveSpeedMultiplier = 2.4f;
    [SerializeField] private float preDiveHeightOffset = 1.5f;
    [SerializeField] private float diveStartDistance = 2.5f;
    [SerializeField] private float finalArriveThreshold = 0.25f;

    private bool exploded;
    private bool hasLockedTarget;
    private bool isDiving;

    private Vector3 lockedGroundPosition;
    private Vector3 lockedApproachPosition;

    public bool HasLockedTarget => hasLockedTarget;
    public bool IsExploded => exploded;
    public bool IsChargeCommitted => hasLockedTarget && !exploded;

    public void ConfigureLegacy(
        float damage,
        GameObject vfxPrefab,
        AudioClip sfx,
        float vfxLife,
        bool useRadius,
        float radius,
        LayerMask mask)
    {
        explodeDamage = damage;
        explosionVfxPrefab = vfxPrefab;
        explosionSfx = sfx;
        explosionVfxLifeTime = vfxLife;
        useExplosionRadius = useRadius;
        explosionRadius = radius;
        playerMask = mask;
    }

    public override void OnEnterCombat()
    {
        exploded = false;
        hasLockedTarget = false;
        isDiving = false;

        if (owner == null || owner.Player == null)
            return;

        // 플레이어를 처음 인식한 위치(바닥 기준)
        lockedGroundPosition = owner.Player.position;

        // 드론이 먼저 향할 접근 지점: 플레이어 위치 바로 위
        lockedApproachPosition = lockedGroundPosition + Vector3.up * preDiveHeightOffset;

        hasLockedTarget = true;
    }

    public override void OnExitCombat()
    {
        // 자폭 드론은 돌진 시작 후 취소하지 않음
    }

    public override void TickCombat()
    {
        if (owner == null || exploded || !hasLockedTarget)
            return;

        if (!isDiving)
        {
            bool arrivedAtApproach = MoveToDiveApproach();

            float distToGroundPoint = Vector3.Distance(owner.transform.position, lockedGroundPosition);
            if (arrivedAtApproach || distToGroundPoint <= diveStartDistance)
            {
                isDiving = true;
            }
        }
        else
        {
            bool arrivedAtGroundPoint = MoveToDiveGroundPoint();

            if (arrivedAtGroundPoint)
            {
                Explode();
            }
        }
    }

    private bool MoveToDiveApproach()
    {
        Vector3 current = owner.transform.position;
        Vector3 target = lockedApproachPosition;

        Vector3 next = Vector3.MoveTowards(
            current,
            target,
            owner.GetFlySpeed() * chargeSpeedMultiplier * Time.deltaTime
        );

        owner.transform.position = next;
        owner.FaceDirect3D(target);

        return Vector3.Distance(next, target) <= owner.GetArriveDistance();
    }

    private bool MoveToDiveGroundPoint()
    {
        Vector3 current = owner.transform.position;
        Vector3 target = lockedGroundPosition;

        Vector3 next = Vector3.MoveTowards(
            current,
            target,
            owner.GetFlySpeed() * diveSpeedMultiplier * Time.deltaTime
        );

        owner.transform.position = next;
        owner.FaceDirect3D(target);

        return Vector3.Distance(next, target) <= finalArriveThreshold;
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, owner.transform.position, Quaternion.identity);
            Destroy(vfx, Mathf.Max(0.1f, explosionVfxLifeTime));
        }

        if (explosionSfx != null)
            AudioSource.PlayClipAtPoint(explosionSfx, owner.transform.position, AetherAudio.Sfx);
        else
            AetherAudio.PlayAt("explosion", owner.transform.position, 1f, 4f, 70f, 0.06f, 1f, 30); // 프리팹에 비어 있던 자폭음

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.15f, 0.22f, 28f);

        if (!useExplosionRadius)
        {
            if (owner.Player != null)
            {
                IDamageable damageable = owner.Player.GetComponent<IDamageable>();
                if (damageable == null)
                    damageable = owner.Player.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(new DamageInfo(
                        explodeDamage,
                        DamageType.Drone_Explosion,
                        gameObject,
                        gameObject,
                        owner.transform.position));
                }
            }
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(owner.transform.position, explosionRadius, playerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(new DamageInfo(
                    explodeDamage,
                    DamageType.Drone_Explosion,
                    gameObject,
                    gameObject,
                    hits[i].ClosestPointSafe(owner.transform.position)));
            }
        }

        DroneHealth droneHealth = owner.GetComponent<DroneHealth>();
        if (droneHealth == null)
            droneHealth = owner.GetComponentInParent<DroneHealth>();

        if (droneHealth != null)
        {
            droneHealth.DieBySelfDestruct();
        }
        else
        {
            Destroy(owner.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform drawTransform = owner != null ? owner.transform : transform;

        if (useExplosionRadius)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(drawTransform.position, explosionRadius);
        }

        if (hasLockedTarget)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lockedGroundPosition, 0.25f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lockedApproachPosition, 0.25f);

            Gizmos.color = Color.white;
            Gizmos.DrawLine(drawTransform.position, lockedApproachPosition);
            Gizmos.DrawLine(lockedApproachPosition, lockedGroundPosition);
        }
    }
#endif
}