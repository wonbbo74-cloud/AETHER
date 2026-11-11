using UnityEngine;

public class ShieldHitbox : MonoBehaviour
{
    [SerializeField] private string[] projectileNameTokens = { "bullet", "projectile", "missile", "laser" };

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.GetComponentInParent<PlayerStatus>() != null)
            return;

        if (LooksLikeProjectile(other))
            Destroy(other.gameObject);
    }

    private bool LooksLikeProjectile(Collider other)
    {
        if (other.attachedRigidbody != null)
            return true;

        string targetName = other.gameObject.name.ToLowerInvariant();
        for (int i = 0; i < projectileNameTokens.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(projectileNameTokens[i]) && targetName.Contains(projectileNameTokens[i]))
                return true;
        }

        return false;
    }
}
