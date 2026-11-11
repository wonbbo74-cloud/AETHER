using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EMPGrenade : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider grenadeCollider;
    [SerializeField] private GameObject explosionFxPrefab;

    [Header("EMP")]
    [SerializeField] private float empRadius = 8f;
    [SerializeField] private float empDuration = 4f;
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("Detonation")]
    [SerializeField] private float fuseTime = 1.5f;
    [SerializeField] private bool explodeOnCollision = true;
    [SerializeField] private float collisionArmDelay = 0.15f;

    private bool collisionArmed;
    private bool detonated;
    private GameObject ownerObject;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        grenadeCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (grenadeCollider == null) grenadeCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        Invoke(nameof(ArmCollision), collisionArmDelay);
        Invoke(nameof(Detonate), fuseTime);
    }

    public void Initialize(Vector3 launchVelocity, GameObject owner)
    {
        ownerObject = owner;

        if (rb != null)
            rb.linearVelocity = launchVelocity;

        IgnoreOwnerCollision(true);
    }

    private void ArmCollision()
    {
        collisionArmed = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!explodeOnCollision) return;
        if (!collisionArmed) return;
        if (detonated) return;

        if (ownerObject != null && collision.transform.root.gameObject == ownerObject.transform.root.gameObject)
            return;

        Detonate();
    }

    public void Detonate()
    {
        if (detonated) return;
        detonated = true;

        CancelInvoke();

        Vector3 center = transform.position;

        if (explosionFxPrefab != null)
            Instantiate(explosionFxPrefab, center, Quaternion.identity);

        // 전자기 방전: 터지는 소리 + 지직거리는 아크
        AetherAudio.PlayAt("emp_blast", center, 1f, 3f, 50f, 0.04f, 1f, 50);

        Collider[] hits = Physics.OverlapSphere(center, empRadius, targetMask, QueryTriggerInteraction.Collide);
        HashSet<int> processedRoots = new HashSet<int>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            IEMPStunnable stunnable = FindStunnable(hit, out Component holder);
            if (stunnable == null || holder == null) continue;

            int rootId = holder.transform.root.gameObject.GetInstanceID();
            if (!processedRoots.Add(rootId)) continue;

            stunnable.ApplyEMP(empDuration);
        }

        Destroy(gameObject);
    }

    private IEMPStunnable FindStunnable(Collider hit, out Component holder)
    {
        holder = null;

        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IEMPStunnable stunnable)
            {
                holder = behaviour;
                return stunnable;
            }
        }

        return null;
    }

    private void IgnoreOwnerCollision(bool ignore)
    {
        if (ownerObject == null || grenadeCollider == null) return;

        Collider[] ownerColliders = ownerObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in ownerColliders)
        {
            if (col == null) continue;
            Physics.IgnoreCollision(grenadeCollider, col, ignore);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, empRadius);
    }
}