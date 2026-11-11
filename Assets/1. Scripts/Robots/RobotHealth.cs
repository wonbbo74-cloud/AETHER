using System;
using UnityEngine;
using UnityEngine.UI;

public class RobotHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP;
    [SerializeField] private float deathDespawnDelay = 2f;

    [Header("HP UI")]
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFillImage;

    private bool isDead;

    /// <summary>마지막으로 때린 쪽이 플레이어였는지. 처치 수를 셀 때 쓴다.</summary>
    private bool killedByPlayer;

    public event Action<RobotHealth> Died;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsDead => isDead;
    public float DeathDespawnDelay => deathDespawnDelay;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = maxHP;
        deathDespawnDelay = Mathf.Max(0f, deathDespawnDelay);
        isDead = false;

        SetHpBarVisible(hpBarRoot != null);
        UpdateUI();
    }

    public void Configure(int maxHpValue, float despawnDelay)
    {
        maxHP = Mathf.Max(1, maxHpValue);
        deathDespawnDelay = Mathf.Max(0f, despawnDelay);
        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 0, maxHP);
        UpdateUI();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (isDead) return;

        // 플레이어 공격은 에테르 레벨만큼 세진다.
        bool fromPlayer = IsFromPlayer(damageInfo.type);
        float amount = fromPlayer ? damageInfo.amount * AetherLevel.DamageScale : damageInfo.amount;
        int damage = Mathf.Max(0, Mathf.RoundToInt(amount));
        if (damage <= 0) return;

        // 작전 실패 화면에 쓸 전적. 플레이어가 때린 것만 센다.
        killedByPlayer = fromPlayer;
        if (fromPlayer)
            AetherEncounterDirector.WakeByDamage(this);   // 05 웨이브: 맞은 대기 적은 깨어난다
        if (killedByPlayer)
            AetherRunStats.ReportDamageDealt(Mathf.Min(damage, currentHP));

        currentHP = Mathf.Max(0, currentHP - damage);
        UpdateUI();

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHP = 0;
        UpdateUI();
        SetHpBarVisible(false);

        if (killedByPlayer)
        {
            AetherRunStats.ReportKill();

            // 터렛도 RobotHealth 를 쓴다. 터렛은 더 많이 준다.
            bool turret = GetComponentInParent<EnemyTurretController>() != null || GetComponentInChildren<EnemyTurretController>() != null;
            AetherLevel.ReportKill(this, turret ? AetherLevel.TurretKillExp : AetherLevel.RobotKillExp, turret ? "터렛 파괴" : "로봇 처치");
        }

        EnemyReward reward = GetComponent<EnemyReward>();
        if (reward != null)
        {
            reward.GiveReward();
        }

        Died?.Invoke(this);
    }

    /// <summary>플레이어가 낸 피해인지. 적 총알(Enemy_Bullet)만 아니면 플레이어 쪽으로 본다.</summary>
    private static bool IsFromPlayer(DamageType type)
    {
        return type == DamageType.Bullet || type == DamageType.punch || type == DamageType.Generic;
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }

        if (hpFillImage != null)
        {
            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
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