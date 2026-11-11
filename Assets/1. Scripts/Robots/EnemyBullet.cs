using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Vector3 modelRotationOffset = new Vector3(0f, 0f, -90f);

    private Vector3 direction;
    private float speed;
    private int damage;
    private Transform ownerRoot;
    private float whizDistance = float.MaxValue;
    private bool whizzed;

    public void Initialize(Vector3 dir, float moveSpeed, int hitDamage, float lifeTime, Transform owner)
    {
        direction = dir.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0, hitDamage);
        ownerRoot = owner;

        // Correct the model rotation if the prefab forward axis does not match +Z.
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(modelRotationOffset);

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));
    }

    private void Update()
    {
        AetherHitFX.TrackWhiz(transform.position, ref whizDistance, ref whizzed);

        float step = speed * Time.deltaTime;
        if (SweepHit(step))
            return;

        transform.position += direction * step;
    }

    // 이 탄은 위치를 직접 옮기는 트리거라, 움직이지 않는 벽(리지드바디 없는 충돌체)과는 닿아도 아무 일이 없었다.
    // 쏜 뒤 사이에 벽이 끼면 탄이 벽을 뚫고 지나갔다. 이번 프레임에 갈 구간을 먼저 훑는다.
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
            AetherHitFX.BulletHit(transform.position, direction, null, false);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            AetherHitFX.BulletHit(transform.position, direction, other, false);
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
