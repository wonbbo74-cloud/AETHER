using UnityEngine;

public class RobotBullet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Vector3 modelRotationOffset = new Vector3(0f, 0f, -90f);

    private Vector3 direction;
    private float speed;
    private int damage;
    private Transform ownerRoot;
    private float whizDistance = float.MaxValue;
    private bool whizzed;
    private Rigidbody cachedRigidbody;

    public void Initialize(Vector3 dir, float moveSpeed, int hitDamage, float lifeTime, Transform owner)
    {
        direction = dir.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0, hitDamage);
        ownerRoot = owner;

        // Correct the model rotation if the prefab forward axis does not match +Z.
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(modelRotationOffset);

        cachedRigidbody = GetComponent<Rigidbody>();
        // 고정(kinematic) 탄은 Update 에서 직접 움직인다. 속도를 넣으면 '지원 안 함' 경고가 탄마다 떴다.
        if (cachedRigidbody != null && !cachedRigidbody.isKinematic)
        {
            cachedRigidbody.linearVelocity = direction * speed;
        }

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));
    }

    private void Update()
    {
        AetherHitFX.TrackWhiz(transform.position, ref whizDistance, ref whizzed);

        float step = speed * Time.deltaTime;
        if (SweepHit(step))
            return;

        if (cachedRigidbody != null && !cachedRigidbody.isKinematic)
            return;

        transform.position += direction * step;
    }

    // 트리거 탄은 빠르면 얇은 벽을 건너뛰고, 위치를 직접 옮길 땐 움직이지 않는 벽과 닿아도 아무 일이 없었다.
    // 이번 프레임에 갈 구간을 먼저 훑어 벽·플레이어에 맞으면 그 자리에서 처리한다.
    private bool SweepHit(float step)
    {
        if (step <= 0f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, step, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (ownerRoot != null && hit.collider.transform.IsChildOf(ownerRoot)) continue;
            if (BelongsToEnemy(hit.collider.transform)) continue;

            transform.position = hit.point;
            OnTriggerEnter(hit.collider);
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot)) return;

        if (BelongsToEnemy(other.transform)) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            var damageInfo = new DamageInfo(
                damage,
                DamageType.Enemy_Bullet,
                gameObject,
                ownerRoot != null ? ownerRoot.gameObject : null,
                transform.position
            );
            damageable.TakeDamage(damageInfo);
            AetherHitFX.BulletHit(transform.position, direction, null, true);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            AetherHitFX.BulletHit(transform.position, direction, other, true);
            Destroy(gameObject);
        }
    }

    private static bool BelongsToEnemy(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.CompareTag("Enemy"))
                return true;
        }

        return false;
    }
}
