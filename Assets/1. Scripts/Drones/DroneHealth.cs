using System;
using UnityEngine;
using UnityEngine.UI;

public class DroneHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    [SerializeField] private int customMaxHP = 3;
    [SerializeField] private int currentHP;

    [Header("HP UI")]
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFillImage;

    [Header("World Space HPBar")]
    [SerializeField] private Transform hpBarCanvas;
    [SerializeField] private bool billboardHpBar = true;

    [Header("Death FX/SFX")]
    [SerializeField] private GameObject deathFxPrefab;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private float deathVolume = 3f;
    [SerializeField] private float fxLifeTime = 2f;

    private bool isDead;
    private Camera mainCam;

    public event Action<DroneHealth> Died;

    public int CurrentHP => currentHP;
    public int MaxHP => customMaxHP;
    public bool IsDead => isDead;

    private void Awake()
    {
        customMaxHP = Mathf.Max(1, customMaxHP);
        currentHP = customMaxHP;
        isDead = false;

        mainCam = Camera.main;

        SetHpBarVisible(hpBarRoot != null);
        UpdateUI();
    }

    private void LateUpdate()
    {
        if (!billboardHpBar) return;
        if (hpBarCanvas == null || mainCam == null) return;

        hpBarCanvas.rotation = Quaternion.LookRotation(
            hpBarCanvas.position - mainCam.transform.position
        );
    }

    public void Configure(int maxHpValue)
    {
        customMaxHP = Mathf.Max(1, maxHpValue);
        currentHP = Mathf.Clamp(currentHP <= 0 ? customMaxHP : currentHP, 0, customMaxHP);
        UpdateUI();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (isDead) return;

        int damage = Mathf.Max(0, Mathf.RoundToInt(damageInfo.amount));
        if (damage <= 0) return;

        currentHP = Mathf.Max(0, currentHP - damage);
        UpdateUI();

        // 적 총알·다른 드론의 폭발에 떨어진 것은 플레이어 처치가 아니다.
        bool byPlayer = damageInfo.type != DamageType.Enemy_Bullet && damageInfo.type != DamageType.Drone_Explosion;
        if (byPlayer)
            AetherEncounterDirector.WakeByDamage(this);

        if (currentHP <= 0)
            Die(byPlayer);   // 플레이어에게 처치됨 -> 보상 지급
    }

    public void DieBySelfDestruct()
    {
        Die(false);      // 자폭 사망 -> 보상 미지급
    }

    private void Die(bool grantReward)
    {
        if (isDead) return;

        isDead = true;
        currentHP = 0;
        UpdateUI();
        SetHpBarVisible(false);

        if (grantReward)
        {
            EnemyReward reward = GetComponent<EnemyReward>();
            if (reward != null)
                reward.GiveReward();

            // 드론 처치도 전적과 에테르에 넣는다(예전엔 세지 않았다).
            AetherRunStats.ReportKill();
            AetherLevel.ReportKill(this, AetherLevel.DroneKillExp, "드론 격추");
        }

        Vector3 pos = transform.position;

        if (deathFxPrefab != null)
        {
            GameObject fx = Instantiate(deathFxPrefab, pos, Quaternion.identity);

            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
                ps.Play();

            Destroy(fx, fxLifeTime);
        }

        if (deathClip == null)
        {
            // 프리팹에 비어 있던 사망음: 터지는 소리 + 떨어지는 금속음(3D)
            AetherAudio.PlayAt("drone_death", pos, 1f, 3f, 45f, 0.05f, 1f, 60);
        }
        else
        {
            GameObject soundGO = new GameObject("DroneDeathSound");
            soundGO.transform.position = pos;

            AudioSource sfx = soundGO.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;
            sfx.volume = deathVolume * AetherAudio.Sfx;
            sfx.PlayOneShot(deathClip);

            Destroy(soundGO, deathClip.length + 0.2f);
        }

        Died?.Invoke(this);
        Destroy(gameObject);
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = customMaxHP;
            hpSlider.value = currentHP;
        }

        if (hpFillImage != null)
        {
            float ratio = customMaxHP > 0 ? (float)currentHP / customMaxHP : 0f;
            hpFillImage.fillAmount = ratio;
            hpFillImage.color = Color.Lerp(
                new Color(0.85f, 0.15f, 0.15f),
                new Color(0.2f, 0.9f, 0.2f),
                ratio
            );
        }
    }

    private void SetHpBarVisible(bool visible)
    {
        if (hpBarRoot != null)
            hpBarRoot.SetActive(visible);
    }
}