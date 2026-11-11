using UnityEngine;
using UnityEngine.UI;

public class PlayerStatus : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("Thirst")]
    public float maxThirst = 100f;
    public float currentThirst = 100f;
    [SerializeField] private bool drainThirstOverTime = true;
    [SerializeField, Min(0f)] private float thirstDrainPerSecond = 0.2f;

    [Header("자연 회복")]
    [Tooltip("일정 시간 안 맞으면 체력이 스스로 차오른다. 엄폐 → 회복 → 반격의 흐름을 만든다.")]
    [SerializeField] private bool regenerateHP = true;
    [Tooltip("마지막으로 맞은 뒤 이만큼 지나야 회복이 시작된다(초).")]
    [SerializeField, Min(0f)] private float regenDelay = 5f;
    [SerializeField, Min(0f)] private float regenPerSecond = 8f;

    /// <summary>마지막으로 피해를 입은 시각. 자연 회복과 사망 처리에서 쓴다.</summary>
    public float LastDamageTime { get; private set; } = -999f;

    /// <summary>체력이 0 이 되는 순간 한 번 울린다.</summary>
    public event System.Action<PlayerStatus> Died;

    private bool deathAnnounced;

    [Header("Shield")]
    public bool useShield = false;          // 윤서만 true
    public float maxShield = 100f;
    public float currentShield = 100f;

    [Header("HP UI")]
    public Slider hpSlider;
    public Image hpFillImage;

    [Header("Shield UI")]
    public Slider shieldSlider;
    public Image shieldFillImage;

    private void Awake()
    {
        // 씬·프리팹마다 100 으로 저장되어 있어 한 곳(AetherCombatBalance)에서 맞춘다.
        // 에테르 레벨이 올라 있으면 그만큼 더한다.
        ApplyAetherLevel(false);
    }

    /// <summary>
    /// 최대 체력을 기본값 + 에테르 레벨 보너스로 맞춘다. 체력은 비율을 유지한다.
    /// refill 이면(레벨이 오른 순간) 체력·실드를 가득 채운다.
    /// </summary>
    public void ApplyAetherLevel(bool refill)
    {
        float target = AetherCombatBalance.PlayerMaxHP + AetherLevel.MaxHPBonus;
        if (maxHP < target)
        {
            float ratio = maxHP > 0f ? currentHP / maxHP : 1f;
            maxHP = target;
            currentHP = maxHP * ratio;
        }

        if (refill)
        {
            currentHP = maxHP;
            if (useShield) currentShield = maxShield;
            UpdateUI();
        }
    }

    private void Start()
    {
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        currentThirst = Mathf.Clamp(currentThirst, 0f, maxThirst);

        if (useShield)
            currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        else
            currentShield = 0f;

        UpdateUI();
    }

    private void Update()
    {
        // 퍼즐 화면에서 에테르 레벨이 올랐으면, 돌아와서 체력 복원(첫 몇 프레임)이 끝난 뒤 가득 채운다.
        // 장면 이동 뒤 체력 되돌리기(AetherRunState)가 0.6초보다 늦게 붙으면, 먼저 가득 채운 체력을
        // 퍼즐 전 체력으로 다시 덮어썼다(퍼즐2에서 레벨이 올라도 돌아오면 HP 16). 되돌리기가 끝난 뒤에 채운다.
        if (Time.timeSinceLevelLoad > 0.6f && isActiveAndEnabled && AetherRunState.IsBoundTo(this) && AetherLevel.ConsumePendingRefill())
            ApplyAetherLevel(true);

        CheckDeath();
        RegenerateHP();
        AnnounceThirst();

        // 줄어드는 속도는 AetherCombatBalance 한 곳에서 정한다(프리팹 6개에 각각 저장된 값은 쓰지 않음).
        if (!drainThirstOverTime || currentThirst <= 0f)
            return;

        currentThirst = Mathf.Max(0f, currentThirst - AetherCombatBalance.ThirstDrainPerSecond * Time.deltaTime);
    }

    private bool thirstAnnounced;

    /// <summary>갈증이 바닥나는 순간 한 번, 체력이 회복되지 않는 이유를 알려 준다.</summary>
    private void AnnounceThirst()
    {
        if (!IsThirsty)
        {
            thirstAnnounced = false;
            return;
        }

        if (thirstAnnounced || !isActiveAndEnabled)
            return;

        thirstAnnounced = true;
        AetherObjectiveToast.Show("목이 마르다!\n물을 마셔야 체력이 다시 회복된다.", 6f);
    }

    /// <summary>
    /// 체력이 0 이 되는 순간을 알린다.
    ///
    /// 지금까지 IsDead 를 보는 곳이 가스 지대 하나뿐이라, 체력이 0 이 되어도
    /// 플레이어는 멀쩡히 걸어다녔다. 한 번만 울리고, 다시 살아나면 초기화된다.
    /// </summary>
    private void CheckDeath()
    {
        if (currentHP > 0f)
        {
            deathAnnounced = false;
            return;
        }

        if (deathAnnounced)
            return;

        deathAnnounced = true;
        Died?.Invoke(this);
    }

    /// <summary>
    /// 한동안 안 맞으면 체력이 차오른다.
    ///
    /// 회복 수단이 아이템뿐이라, 한 번 깎인 체력은 사실상 돌아오지 않았다.
    /// 쓰러진 뒤에는 차지 않는다 — 부활은 사망 처리 쪽에서 맡는다.
    /// </summary>
    private void RegenerateHP()
    {
        if (!regenerateHP || regenPerSecond <= 0f)
            return;

        if (currentHP <= 0f || currentHP >= maxHP)
            return;

        // 에테르 레벨 3 부터 회복이 더 빨리, 더 많이 된다.
        if (Time.time - LastDamageTime < Mathf.Max(0f, regenDelay - AetherLevel.RegenDelayCut))
            return;

        // 목이 마르면(갈증 0) 체력이 스스로 차오르지 않는다. 물을 마셔야 다시 회복된다.
        if (IsThirsty)
            return;

        // 프리팹 값 8/초는 맞은 만큼 금방 차서 HP 가 안 닳는 것처럼 보였다(10/12 사용자). 5/초로 묶었다가
        // 10/13 사용자 "회복 속도 좀만 천천히" → 3.5/초.
        currentHP = Mathf.Min(maxHP, currentHP + (Mathf.Min(regenPerSecond, 3.5f) + AetherLevel.RegenBonusPerSecond) * Time.deltaTime);
        UpdateUI();
    }

    public bool IsDead => currentHP <= 0f;

    /// <summary>갈증이 바닥나 자연 회복이 멈춘 상태.</summary>
    public bool IsThirsty => maxThirst > 0f && currentThirst <= 0f;

    public void ChangeHP(float amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        UpdateUI();
    }

    public void ChangeShield(float amount)
    {
        if (!useShield) return;

        currentShield += amount;
        currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        UpdateUI();
    }

    public void ChangeThirst(float amount)
    {
        currentThirst = Mathf.Clamp(currentThirst + amount, 0f, maxThirst);
        UpdateUI();
    }

    public void SetThirst(float value)
    {
        currentThirst = Mathf.Clamp(value, 0f, maxThirst);
        UpdateUI();
    }

    /// <summary>이 시각까지는 피해를 받지 않는다(되살아난 직후 몸을 피할 틈).</summary>
    public float InvulnerableUntil { get; set; } = -1f;

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f) return;
        if (Time.time < InvulnerableUntil) return;

        LastDamageTime = Time.time;
        float incoming = damage;

        if (useShield && currentShield > 0f)
        {
            if (currentShield >= damage)
            {
                currentShield -= damage;
                damage = 0f;
            }
            else
            {
                damage -= currentShield;
                currentShield = 0f;
            }
        }

        if (damage > 0f)
        {
            currentHP -= damage;
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        }

        // 맞은 게 화면·소리로 읽히게 한다. 꺼져 있는 다른 캐릭터는 제외.
        if (isActiveAndEnabled)
            AetherHitFX.PlayerDamaged(incoming, damage <= 0f);

        UpdateUI();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        float amount = damageInfo.amount;

        // 적 총알·드론 자폭은 전체 배율로 한 번 더 줄인다(AetherCombatBalance.IncomingEnemyDamageScale).
        if (damageInfo.type == DamageType.Enemy_Bullet || damageInfo.type == DamageType.Drone_Explosion)
            amount = AetherCombatBalance.ScaleIncomingEnemyDamage(amount);

        ApplyDamage(amount);
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(damage);
    }

    public void SetHP(float value)
    {
        currentHP = Mathf.Clamp(value, 0f, maxHP);
        UpdateUI();
    }

    public void SetShield(float value)
    {
        if (!useShield) return;

        currentShield = Mathf.Clamp(value, 0f, maxShield);
        UpdateUI();
    }

    public bool RestoreShield(float amount)
    {
        if (!useShield) return false;
        if (amount <= 0f) return false;
        if (currentShield >= maxShield) return false;

        currentShield += amount;
        currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool TryBuyShieldCharge(out string message)
    {
        message = "";

        if (!useShield)
        {
            message = "실드팩은 " + AetherCharacterChoice.YunseoDisplayName + "만 쓸 수 있습니다.";
            return false;
        }

        if (currentShield >= 100f)
        {
            message = "실드가 가득 찼습니다.";
            return false;
        }

        currentShield = Mathf.Clamp(100f, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool UseShieldPack(out string message)
    {
        message = "";

        if (!useShield)
        {
            message = "실드팩은 " + AetherCharacterChoice.YunseoDisplayName + "만 쓸 수 있습니다.";
            return false;
        }

        // 가득 찬 상태에서 쓰면 팩만 사라졌다.
        if (currentShield >= maxShield - 0.01f)
        {
            message = "실드가 가득 찼습니다.";
            return false;
        }

        currentShield = Mathf.Clamp(100f, 0f, maxShield);
        UpdateUI();
        return true;
    }

    public bool ApplyItem(ItemData item, out string message)
    {
        message = "";

        if (item == null || item.effects == null || item.effects.Length == 0)
        {
            message = "사용할 수 없는 아이템입니다.";
            return false;
        }

        // 먹어도 아무 득이 없으면(체력이 가득한데 식량을 먹는 등) 쓰지 않는다.
        // 예전엔 체력이 가득해도 식량이 사라지고 수분만 10 줄었다.
        bool onlyVitals = true;
        bool useful = false;
        foreach (var effect in item.effects)
        {
            if (effect.type == EffectType.Health) { if (effect.value > 0 && currentHP < maxHP) useful = true; }
            else if (effect.type == EffectType.Thirst) { if (effect.value > 0 && currentThirst < maxThirst) useful = true; }
            else if (effect.type != EffectType.CustomText) onlyVitals = false;
        }
        if (onlyVitals && !useful)
        {
            message = "체력·수분이 가득 차\n지금은 먹어도 효과가 없습니다.";
            return false;
        }

        bool applied = false;

        foreach (var effect in item.effects)
        {
            switch (effect.type)
            {
                case EffectType.Health:
                    ChangeHP(effect.value);
                    applied = true;
                    break;

                case EffectType.Thirst:
                    ChangeThirst(effect.value);
                    applied = true;
                    break;

                case EffectType.CustomText:
                    // 필요 시 customText 출력 처리
                    applied = true;
                    break;

                case EffectType.ShieldPack:
                {
                    string shieldMessage;
                    if (UseShieldPack(out shieldMessage))
                    {
                        AetherAudio.Play2D("shield_on", 0.55f, 1f, 0f);
                        applied = true;
                    }
                    else if (string.IsNullOrEmpty(message))
                    {
                        message = shieldMessage;
                    }
                    break;
                }

                case EffectType.EnergyPack:
                    // EnergyPack은 Hankyul_Fire에서 처리
                    break;
            }
        }

        if (!applied && string.IsNullOrEmpty(message))
            message = "아이템을 사용할 수 없습니다.";

        if (applied)
            PlayConsumeSound(item);

        return applied;
    }

    /// <summary>
    /// 먹고 마시는 소리는 아이템마다 한 번. 음식·물 모두 체력과 갈증을 같이 채워서 효과마다 내면 씹기와 꿀꺽이 겹쳤다.
    /// 갈증을 더 많이 채우면 마시는 소리, 아니면 씹는 소리(실드팩은 실드 소리를 따로 낸다).
    /// </summary>
    private static void PlayConsumeSound(ItemData item)
    {
        float hp = 0f, thirst = 0f;
        foreach (var e in item.effects)
        {
            if (e.type == EffectType.Health) hp += e.value;
            else if (e.type == EffectType.Thirst) thirst += e.value;
        }
        if (hp <= 0f && thirst <= 0f) return;
        AetherAudio.Play2D(thirst > hp ? "drink" : "eat", 0.55f, 1f, 0.04f);
    }

    public void ApplyItem(ItemData item)
    {
        string _;
        ApplyItem(item, out _);
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
            float hpRatio = maxHP > 0f ? currentHP / maxHP : 0f;
            hpFillImage.fillAmount = hpRatio;
        }

        if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(useShield);
            shieldSlider.maxValue = maxShield;
            shieldSlider.value = currentShield;
        }

        if (shieldFillImage != null)
        {
            shieldFillImage.gameObject.SetActive(useShield);
            float shieldRatio = maxShield > 0f ? currentShield / maxShield : 0f;
            shieldFillImage.fillAmount = shieldRatio;
        }
    }
}
