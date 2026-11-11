using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

    private Vector3 direction;
    private float speed;
    private float damage;
    private Transform ownerRoot;
    private GameObject hitImpactPrefab;
    private bool hasImpacted;
    private Rigidbody cachedRigidbody;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody != null)
        {
            cachedRigidbody.useGravity = false;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    public void Initialize(
        Vector3 dir,
        float moveSpeed,
        float hitDamage,
        float lifeTime,
        Transform owner,
        GameObject impactPrefab = null)
    {
        direction = dir.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0f, hitDamage);
        ownerRoot = owner;
        hitImpactPrefab = impactPrefab;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(modelRotationOffset);

        if (cachedRigidbody != null)
            cachedRigidbody.linearVelocity = direction * speed;

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));
    }

    private void FixedUpdate()
    {
        if (hasImpacted)
            return;

        // 총알은 트리거라 연속 충돌 검사가 되지 않는다. 초속 80m 면 물리 한 번에 1.6m 를 건너뛰어
        // 로봇 몸을 그대로 통과했다(06 연구소 2.3m 정면 37발 → 0발 명중, 궤적으로 확인).
        // 이번에 날아갈 구간을 먼저 훑어서 맞는 것이 있으면 그 자리에서 맞힌다.
        float step = speed * Time.fixedDeltaTime;
        Vector3 from = cachedRigidbody != null ? cachedRigidbody.position : transform.position;
        if (step > 0f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(from, SweepRadius, direction, step, SweepMask(), QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (ownerRoot != null && hit.collider.transform.IsChildOf(ownerRoot)) continue;

                Vector3 point = hit.point == Vector3.zero && hit.distance == 0f ? from : hit.point;
                TryApplyDamage(hit.collider, point);
                return;
            }
        }

        if (cachedRigidbody == null)
            transform.position += direction * step;
    }

    private const float SweepRadius = 0.04f;
    private static int sweepMask;

    private static int SweepMask()
    {
        if (sweepMask == 0)
        {
            int ignore = 1 << 2;
            foreach (string layer in new[] { "Player", "ViewModel", "UI", "Minimap" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0) ignore |= 1 << index;
            }
            sweepMask = ~ignore;
        }
        return sweepMask;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasImpacted) return;
        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot)) return;

        // 상점·아이템 줍기·구역 감지 같은 '보이지 않는 영역'(트리거)에 닿아도 총알이 사라졌다.
        // 상점 앞에 서서 쏘면 총구에서 바로 사라져 적이 한 대도 안 맞았다.
        // 맞을 수 있는 것(적·문 표적)이 아닌 트리거는 그냥 지나간다.
        bool hittable = other.GetComponentInParent<IDamageable>() != null
                        || other.GetComponentInParent<Lab_DoorPointer>() != null;
        if (!hittable) return;

        TryApplyDamage(other, other.ClosestPointSafe(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;
        if (ownerRoot != null && collision.transform.IsChildOf(ownerRoot)) return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        TryApplyDamage(collision.collider, hitPoint);
    }

    private void TryApplyDamage(Collider other, Vector3 hitPoint)
    {
        hasImpacted = true;

        Lab_DoorPointer doorPointer = other.GetComponent<Lab_DoorPointer>();
        if (doorPointer == null)
            doorPointer = other.GetComponentInParent<Lab_DoorPointer>();

        if (doorPointer != null)
            doorPointer.Hit();

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            var damageInfo = new DamageInfo(
                damage,
                DamageType.Bullet,
                gameObject,
                ownerRoot != null ? ownerRoot.gameObject : null,
                hitPoint
            );
            damageable.TakeDamage(damageInfo);
        }

        // Impact sound: armour clang on enemies, otherwise a surface hit (concrete crack / metal ping).
        if (damageable != null)
            AetherAudio.PlayAt("robot_hit", hitPoint, 0.7f, 2f, 40f, 0.06f, 1f, 110);
        else
            AetherAudio.PlayAt(AetherSurface.Classify(other) == "metal" ? "bullet_impact_metal" : "bullet_impact_concrete",
                hitPoint, 0.5f, 1.5f, 30f, 0.08f, 1f, 150);

        SpawnImpact(hitPoint);
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 hitPoint)
    {
        if (hitImpactPrefab == null) return;

        GameObject impact = Instantiate(hitImpactPrefab, hitPoint, Quaternion.LookRotation(-direction, Vector3.up));
        Destroy(impact, 2f);
    }
}
