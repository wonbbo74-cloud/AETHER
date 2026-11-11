using UnityEngine;
using UnityEngine.UI;

public class LegacyDroneHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    [SerializeField] private int maxHP = 3;
    [SerializeField] private int currentHP;

    [Header("UI")]
    [SerializeField] private Slider hpSlider;

    [Header("Death FX/SFX")]
    [SerializeField] private GameObject deathFxPrefab;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private float deathVolume = 3f;
    [SerializeField] private float fxLifeTime = 2f;

    private bool isDead;

    private void Awake()
    {
        currentHP = Mathf.Max(1, maxHP);
        UpdateUI();
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
            return;

        currentHP = Mathf.Max(0, currentHP - Mathf.RoundToInt(amount));
        UpdateUI();

        if (currentHP <= 0)
            Die();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        TakeDamage(damageInfo.amount);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Vector3 pos = transform.position;

        if (deathFxPrefab != null)
        {
            GameObject fx = Instantiate(deathFxPrefab, pos, Quaternion.identity);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
                ps.Play();
            Destroy(fx, fxLifeTime);
        }

        if (deathClip != null)
        {
            GameObject soundGO = new GameObject("DroneDeathSound");
            soundGO.transform.position = pos;

            AudioSource sfx = soundGO.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;
            sfx.volume = deathVolume;
            sfx.PlayOneShot(deathClip);

            Destroy(soundGO, deathClip.length + 0.2f);
        }

        Destroy(gameObject);
    }

    private void UpdateUI()
    {
        if (hpSlider == null)
            return;

        hpSlider.maxValue = Mathf.Max(1, maxHP);
        hpSlider.value = currentHP;
    }
}
