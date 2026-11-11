using UnityEngine;

public class ShieldController : MonoBehaviour
{
    [SerializeField] private GameObject shieldVisual;
    [SerializeField] private Collider shieldCollider;
    private Renderer ownRenderer;

    private void Awake()
    {
        if (shieldCollider == null)
            shieldCollider = GetComponentInChildren<Collider>(true);

        if (shieldCollider != null)
            shieldCollider.isTrigger = true;

        ownRenderer = GetComponent<Renderer>();

        if (GetComponentInChildren<ShieldHitbox>(true) == null)
            gameObject.AddComponent<ShieldHitbox>();

        SetActive(false);
    }

    public void SetActive(bool active)
    {
        if (shieldVisual != null)
            shieldVisual.SetActive(active);
        else
        {
            if (ownRenderer != null)
                ownRenderer.enabled = active;

            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(active);
        }

        if (shieldCollider != null)
            shieldCollider.enabled = active;
    }
}
