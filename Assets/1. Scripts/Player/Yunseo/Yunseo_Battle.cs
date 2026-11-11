using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yunseo_Battle
/// - Animator 전환도:
///   Locomotion <-> Battle
///   Locomotion -> SprintAttack -> Battle
///
/// - 좌클릭(일반/연속):
///   punch1~4 콤보 사용
///   - 클릭 즉시 다음 애니메이션으로 강제 전환하지 않음
///   - 현재 타격 애니메이션 중 다음 입력을 예약(queue)해두고
///   - 각 punch 상태의 Has Exit Time으로 자연스럽게 다음 타로 연결
///
/// - 달리는 중 좌클릭:
///   SprintAttack(DoSprintAttack Trigger) 실행
///
/// - 우클릭 홀드(1~3초):
///   1초 미만은 취소
///   - 1초: 파랑(티어1)
///   - 2초: 노랑(티어2)
///   - 3초 이상: 빨강(티어3)
///   - PowerTier(int)로 VFX/분기 표시, DoPowerPunch Trigger 실행
///
/// - 공격/행동 발생 시 InBattle=true 유지
///   마지막 행동 후 battleKeepTime 동안 유지 후 InBattle=false
///
/// - 데미지:
///   AE_PunchHit / AE_PowerPunchHit 시점에 OverlapSphere로 판정
///   양손(left/right) 히트 포인트를 모두 검사
///   맞은 대상이 IDamageable이면 TakeDamage(DamageInfo) 호출
/// </summary>
public class Yunseo_Battle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string inBattleBool = "InBattle";
    [SerializeField] private string comboStepInt = "ComboStep";          // 0~4
    [SerializeField] private string doPunchTrigger = "DoPunch";
    [SerializeField] private string doSprintAttackTrigger = "DoSprintAttack";
    [SerializeField] private string doPowerPunchTrigger = "DoPowerPunch";
    [SerializeField] private string powerTierInt = "PowerTier";          // 0~3
    [SerializeField] private string speedFloat = "Speed";                // Locomotion용 이동 속도 float

    [Header("Running 판정(둘 중 하나 사용)")]
    [Tooltip("true면 Animator의 Speed 파라미터로 달리기 판정")]
    [SerializeField] private bool useAnimatorSpeedForRunning = true;
    [Tooltip("Speed 값이 이 값 이상이면 달리는 중으로 판정")]
    [SerializeField] private float runSpeedThreshold = 0.6f;
    [Tooltip("false면 외부에서 SetRunning(true/false)로 직접 전달")]
    [SerializeField] private bool runningState = false;

    [Header("Punch Combo")]
    [SerializeField] private float firstPunchCooldown = 0.05f;
    [SerializeField] private float comboInputWindow = 1.0f;
    [SerializeField] private float afterPunch4Lock = 1.0f;
    [SerializeField] private float battleKeepTime = 3.0f;

    [Header("Punch Damage")]
    [SerializeField] private float punch1Damage = 10f;
    [SerializeField] private float punch2Damage = 12f;
    [SerializeField] private float punch3Damage = 15f;
    [SerializeField] private float punch4Damage = 20f;

    [Header("Power Punch Charge (Tier)")]
    [SerializeField] private float tier1Time = 1f;
    [SerializeField] private float tier2Time = 2f;
    [SerializeField] private float tier3Time = 3f;

    [SerializeField] private float damageTier1 = 50f;
    [SerializeField] private float damageTier2 = 90f;
    [SerializeField] private float damageTier3 = 140f;

    [Header("Hit Settings")]
    [SerializeField] private Transform leftPunchHitPoint;
    [SerializeField] private Transform rightPunchHitPoint;
    [SerializeField] private float punchHitRadius = 0.6f;
    [SerializeField] private LayerMask hitTargetLayers;

    [Header("Charge VFX (optional)")]
    [SerializeField] private GameObject vfxBlue;
    [SerializeField] private GameObject vfxYellow;
    [SerializeField] private GameObject vfxRed;

    [Header("Optional: Charge Pose Hold (advanced)")]
    [Tooltip("차징 중 특정 애니메이션 프레임에서 멈춰 자세 유지(해당 상태가 실제로 있어야 함)")]
    [SerializeField] private bool holdChargePose = false;
    [SerializeField] private string chargeStateName = "PowerCharge";
    [Range(0f, 1f)][SerializeField] private float chargeHoldNormalizedTime = 0.45f;
    [SerializeField] private int chargeLayer = 0;

    // ---------- 내부 상태 ----------
    private bool inputLocked = false;

    // 배틀그라운드식 잽(10/12 사용자 "펀치가 연속으로 안 된다"): 누를 때마다(누르고 있으면 계속) 일정 박자로
    // 오른손·왼손을 번갈아 곧게 뻗는다. 예전 콤보는 애니메이션 이벤트 창에 맞춰 눌러야만 이어져 자주 끊겼다.
    [Header("FPS Jab (PUBG style)")]
    [SerializeField] private bool fpsJabMode = true;
    [SerializeField] private float jabInterval = 0.36f;
    [SerializeField] private float jabHitDelay = 0.09f;
    [SerializeField] private float jabReach = 1.1f;
    public int JabCount { get; private set; }
    public float LastJabTime { get; private set; } = -10f;
    private float nextJabTime;

    // 펀치 콤보 상태
    private bool comboActive = false;
    private bool comboInputQueued = false;
    private bool canQueueNext = false;
    private int currentComboStep = 0;
    private float comboExpireTime = 0f;
    private float nextFirstPunchAllowedTime = 0f;
    private bool comboDecisionPending = false;

    // PowerPunch 차징 상태
    private bool isCharging = false;
    private float chargeTime = 0f;
    private int currentChargeTier = 0;

    private float queuedPowerDamage = 0f;
    private int queuedPowerTier = 0;

    private Coroutine lockRoutine;
    private Coroutine battleKeepRoutine;

    // 히트 판정용
    private readonly Collider[] hitResults = new Collider[16];
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

    // ---------- Animator hashes ----------
    private int hInBattle;
    private int hComboStep;
    private int hDoPunch;
    private int hDoSprint;
    private int hDoPower;
    private int hPowerTier;
    private int hSpeed;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        hInBattle = Animator.StringToHash(inBattleBool);
        hComboStep = Animator.StringToHash(comboStepInt);
        hDoPunch = Animator.StringToHash(doPunchTrigger);
        hDoSprint = Animator.StringToHash(doSprintAttackTrigger);
        hDoPower = Animator.StringToHash(doPowerPunchTrigger);
        hPowerTier = Animator.StringToHash(powerTierInt);
        hSpeed = Animator.StringToHash(speedFloat);

        SetChargeVfx(0);
        SafeSetPowerTier(0);

        if (animator != null)
            animator.SetInteger(hComboStep, 0);

        // 05 씬의 윤서는 맞힐 레이어가 0(아무것도 안 맞음), 프리팹은 'Player' 레이어만이었다.
        // 적은 씬이 열리면 'Enemy' 레이어로 옮겨지므로(EnemyRuntimeLayerNormalizer) 한 대도 못 쳤다.
        // 자기 몸은 HitFromPoint 에서 따로 거른다.
        hitTargetLayers |= LayerMask.GetMask("Default", "Player", "Enemy");
    }

    private void Update()
    {
        if (animator == null) return;

        if (holdChargePose && isCharging)
        {
            animator.Play(chargeStateName, chargeLayer, chargeHoldNormalizedTime);
            animator.Update(0f);
        }

        // 메뉴(엘리베이터 층 목록 등)를 고르는 클릭으로 펀치가 나가지 않게 한다.
        bool menuCursor = Cursor.lockState != CursorLockMode.Locked;

        if (!menuCursor && Input.GetMouseButtonDown(1))
            StartCharge();

        if (isCharging)
            TickCharge();

        if (Input.GetMouseButtonUp(1))
            ReleaseCharge();

        if (!menuCursor && (Input.GetMouseButtonDown(0) || (fpsJabMode && Input.GetMouseButton(0) && Time.time >= nextJabTime)))
        {
            if (!isCharging)
                TryLeftClickAttack();
        }

        if (comboActive && Time.time > comboExpireTime)
            comboInputQueued = false;

        // 콤보는 애니메이션 이벤트(AE_ComboDecision)로만 끝난다. 맞거나 다른 동작에 끊겨 이벤트가 안 오면
        // comboActive 가 영영 켜진 채로 남아 그 뒤로 펀치가 한 대도 안 나갔다(10/12 최종 점검, 05 에서 35초간 먹통).
        // 입력 창이 닫히고 1초가 지나도 소식이 없으면 콤보를 끝내 다시 칠 수 있게 한다.
        if (comboActive && !comboDecisionPending && Time.time > comboExpireTime + 1f)
            EndCombo(false);
    }

    private void LateUpdate()
    {
        if (!comboDecisionPending) return;

        comboDecisionPending = false;

        if (!comboActive) return;

        if (comboInputQueued)
            AdvanceCombo();
        else
            EndCombo(false);
    }

    public void SetRunning(bool isRunning)
    {
        runningState = isRunning;
    }

    public void BeginDoorBreachCharge()
    {
        if (animator == null) return;
        if (isCharging) return;

        StartCharge();
    }

    public void CancelDoorBreachCharge()
    {
        if (animator == null) return;

        isCharging = false;
        inputLocked = false;
        chargeTime = 0f;
        StopChargeSound();

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    public void ExecuteDoorBreachPowerPunch()
    {
        if (animator == null) return;

        isCharging = false;
        inputLocked = false;
        StopChargeSound();

        queuedPowerTier = 3;
        queuedPowerDamage = damageTier3;

        SafeSetPowerTier(queuedPowerTier);
        EnterBattle();

        animator.ResetTrigger(hDoPower);
        animator.SetTrigger(hDoPower);
        SchedulePowerShockwave();

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    private bool IsRunningNow()
    {
        if (!useAnimatorSpeedForRunning)
            return runningState;

        float s = animator.GetFloat(hSpeed);
        return s >= runSpeedThreshold;
    }

    // -------------------------
    // Left Click: SprintAttack or Punch Combo
    // -------------------------
    private void TryLeftClickAttack()
    {
        if (inputLocked) return;

        if (IsRunningNow())
        {
            EnterBattle();
            animator.ResetTrigger(hDoSprint);
            animator.SetTrigger(hDoSprint);

            // 달리기 공격 애니메이션에는 타격 이벤트가 없어 피해가 0 이었다. 맞는 순간에 직접 판정한다.
            StartCoroutine(DelayedHit(SprintAttackHitDelay, punch2Damage));
            return;
        }

        TryPunchCombo();
    }

    private void Jab()
    {
        if (Time.time < nextJabTime)
            return;

        nextJabTime = Time.time + jabInterval;
        JabCount++;
        LastJabTime = Time.time;

        // 3인칭(남에게 보이는 몸)은 원래 펀치 1·2 동작을 번갈아 쓴다.
        currentComboStep = JabCount % 2 == 1 ? 1 : 2;
        animator.SetInteger(hComboStep, currentComboStep);
        EnterBattle();
        animator.ResetTrigger(hDoPunch);
        animator.SetTrigger(hDoPunch);
        AetherAudio.Play2D("punch_swing", 0.5f, 1f + (JabCount % 2) * 0.05f, 0.06f);

        StartCoroutine(JabHit(currentComboStep == 1 ? punch1Damage : punch2Damage));
    }

    /// <summary>1인칭이라 판정도 시선 앞에서 한다(몸 애니메이션의 주먹 자리는 화면과 맞지 않는다).</summary>
    private IEnumerator JabHit(float damage)
    {
        yield return new WaitForSeconds(jabHitDelay);

        Transform aim = transform;
        Move_Yunseo move = GetComponent<Move_Yunseo>();
        if (move != null && move.camTransform != null)
            aim = move.camTransform;

        GameObject probe = new GameObject("AETHER_JabProbe");
        probe.transform.position = aim.position + aim.forward * (jabReach * 0.65f) - Vector3.up * 0.15f;
        damagedTargets.Clear();
        HitFromPoint(probe.transform, damage);
        bool hitDoor = TryPunchDoor(probe.transform.position, 1);
        if (damagedTargets.Count > 0 && !hitDoor)
            PlayHeavyHitSound();
        Destroy(probe);
    }

    // 맞았을 때 '퍽' 하는 묵직한 타격음(10/12 사용자 "좀 더 때리는 소리"): 기존 타격음을 크게 + 낮은 충격음을 겹친다.
    private void PlayHeavyHitSound()
    {
        // '퍽'만 두 겹으로 크게. 예전에 겹치던 충전 펀치 소리는 금속 울림(댕동)이 들려 뺐다(10/13 사용자).
        AetherAudio.Play2D("punch_thud", 1f, 1f, 0.07f);
        AetherAudio.Play2D("punch_thud", 1f, 0.92f, 0.07f);
    }

    // 연구소 입구 문(Lab_Door)을 주먹으로 부순다. 잽 5번 또는 충전 펀치 한 방이면 열린다.
    private const int DoorPunchesNeeded = 5;
    private int doorPunches;

    private bool TryPunchDoor(Vector3 center, int strength)
    {
        int count = Physics.OverlapSphereNonAlloc(center, 1.0f, hitResults, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = hitResults[i];
            if (col == null) continue;
            Lab_Door door = col.GetComponentInParent<Lab_Door>();
            if (door == null || door.IsDisabled) continue;

            doorPunches += strength;
            AetherAudio.PlayAt("punch_hit", center, 1f, 3f, 40f, 0.08f, 0.75f, 80);
            AetherAudio.PlayAt("target_hit", center, 0.25f, 3f, 40f, 0.05f, 0.7f + 0.06f * Mathf.Min(doorPunches, DoorPunchesNeeded), 70);   // 댕 소리는 작게
            if (doorPunches >= DoorPunchesNeeded)
            {
                AetherAudio.PlayAt("debris", center, 1f, 4f, 60f, 0.05f, 1f, 80);
                door.PunchOpen();
            }
            return true;
        }
        return false;
    }

    private void TryPunchCombo()
    {
        if (fpsJabMode)
        {
            Jab();
            return;
        }

        float now = Time.time;

        if (!comboActive)
        {
            if (now < nextFirstPunchAllowedTime)
                return;

            StartPunchCombo();
            return;
        }

        if (canQueueNext)
        {
            comboInputQueued = true;
            comboExpireTime = now + comboInputWindow;
            EnterBattle();
        }
    }

    private void StartPunchCombo()
    {
        comboActive = true;
        comboInputQueued = false;
        canQueueNext = false;
        currentComboStep = 1;

        animator.SetInteger(hComboStep, currentComboStep);

        EnterBattle();
        animator.ResetTrigger(hDoPunch);
        animator.SetTrigger(hDoPunch);
        AetherAudio.Play2D("punch_swing", 0.5f, 1f, 0.06f); // 주먹이 바람을 가르는 소리

        nextFirstPunchAllowedTime = Time.time + firstPunchCooldown;
        comboExpireTime = Time.time + comboInputWindow;
    }

    private void AdvanceCombo()
    {
        currentComboStep++;

        if (currentComboStep > 4)
        {
            EndCombo(true);
            return;
        }

        animator.SetInteger(hComboStep, currentComboStep);
        comboInputQueued = false;
        canQueueNext = false;
        comboExpireTime = Time.time + comboInputWindow;

        EnterBattle();
        AetherAudio.Play2D("punch_swing", 0.5f, 1f + currentComboStep * 0.03f, 0.05f);
    }

    private void EndCombo(bool applyFinalLock)
    {
        comboActive = false;
        comboInputQueued = false;
        canQueueNext = false;
        currentComboStep = 0;
        comboExpireTime = 0f;
        comboDecisionPending = false;

        animator.SetInteger(hComboStep, 0);

        if (applyFinalLock)
            ApplyInputLock(afterPunch4Lock);
    }

    // -------------------------
    // Right Click: Power Punch Charge (Tier)
    // -------------------------
    private void StartCharge()
    {
        if (inputLocked) return;
        if (comboActive) return;

        isCharging = true;
        chargeTime = 0f;
        UpdateChargeTier(0);

        EnterBattle();
        inputLocked = true;
        StartChargeSound();
    }

    // 힘을 모으는 동안 웅웅거리는 소리. 단계가 오를수록 높아진다.
    private AetherAudioLoop chargeLoop;

    private void StartChargeSound()
    {
        StopChargeSound();
        chargeLoop = AetherAudio.StartLoop("punch_charge", transform, 0.4f, 1f, 10f, false);
    }

    private void OnDisable()
    {
        // 충전 중에 꺼지면(채팅·일시정지·쓰러짐) 웅웅거림이 남지 않게
        StopChargeSound();
    }

    private void StopChargeSound()
    {
        if (chargeLoop != null) chargeLoop.Stop();
        chargeLoop = null;
    }

    private void TickCharge()
    {
        chargeTime += Time.deltaTime;

        int tier = GetChargeTier(chargeTime);
        UpdateChargeTier(tier);
        if (chargeLoop != null)
            chargeLoop.Pitch = Mathf.Lerp(0.85f, 1.6f, Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, tier3Time)));

        if (chargeTime > tier3Time)
            chargeTime = tier3Time;
    }

    private void ReleaseCharge()
    {
        if (!isCharging) return;

        isCharging = false;
        inputLocked = false;
        StopChargeSound();

        int tier = GetChargeTier(chargeTime);

        if (tier == 0)
        {
            UpdateChargeTier(0);
            SetChargeVfx(0);
            SafeSetPowerTier(0);
            return;
        }

        queuedPowerTier = tier;
        queuedPowerDamage = tier switch
        {
            1 => damageTier1,
            2 => damageTier2,
            _ => damageTier3
        };

        SafeSetPowerTier(queuedPowerTier);
        EnterBattle();

        animator.ResetTrigger(hDoPower);
        animator.SetTrigger(hDoPower);
        SchedulePowerShockwave();

        UpdateChargeTier(0);
        SetChargeVfx(0);
        SafeSetPowerTier(0);
    }

    private int GetChargeTier(float t)
    {
        if (t < tier1Time) return 0;
        if (t < tier2Time) return 1;
        if (t < tier3Time) return 2;
        return 3;
    }

    private void UpdateChargeTier(int tier)
    {
        if (currentChargeTier == tier) return;

        currentChargeTier = tier;
        SetChargeVfx(tier);
        SafeSetPowerTier(tier);
    }

    private void SetChargeVfx(int tier)
    {
        if (vfxBlue) vfxBlue.SetActive(tier == 1);
        if (vfxYellow) vfxYellow.SetActive(tier == 2);
        if (vfxRed) vfxRed.SetActive(tier == 3);
    }

    private void SafeSetPowerTier(int tier)
    {
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == hPowerTier && p.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(hPowerTier, tier);
                return;
            }
        }
    }

    // -------------------------
    // Damage / Hit Detection
    // -------------------------
    private float GetCurrentPunchDamage()
    {
        switch (currentComboStep)
        {
            case 1: return punch1Damage;
            case 2: return punch2Damage;
            case 3: return punch3Damage;
            case 4: return punch4Damage;
            default: return punch1Damage;
        }
    }

    private void PerformPunchHit(float damage)
    {
        damagedTargets.Clear();

        HitFromPoint(leftPunchHitPoint, damage);
        HitFromPoint(rightPunchHitPoint, damage);

        // 맞혔을 때만 둔탁한 타격음(헛손질은 휘두르는 소리만)
        if (damagedTargets.Count > 0)
        {
            Transform p = rightPunchHitPoint != null ? rightPunchHitPoint : transform;
            AetherAudio.PlayAt("punch_hit", p.position, 0.9f, 1f, 20f, 0.06f, 1f, 80);
        }
    }

    private void HitFromPoint(Transform hitPointTransform, float damage)
    {
        if (hitPointTransform == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            hitPointTransform.position,
            punchHitRadius,
            hitResults,
            hitTargetLayers
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = hitResults[i];
            if (col == null) continue;

            // 'Player' 레이어도 맞히므로 자기 몸(PlayerStatus 도 IDamageable)은 거른다.
            if (col.transform.IsChildOf(transform.root)) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            if (damagedTargets.Contains(damageable))
                continue;

            damagedTargets.Add(damageable);

            Vector3 hitPoint = col.ClosestPointSafe(hitPointTransform.position);

            DamageInfo damageInfo = new DamageInfo(
                damage,
                DamageType.punch,
                gameObject,
                gameObject,
                hitPoint
            );

            damageable.TakeDamage(damageInfo);
        }

        // 공장 출입구(Lab_Door)의 표적은 총알에만 반응해서 윤서는 05 를 빠져나갈 수 없었다.
        // 주먹이 닿은 표적도 맞은 것으로 친다. 표적은 레이어가 제각각이라 레이어를 가리지 않는다.
        int pointerCount = Physics.OverlapSphereNonAlloc(hitPointTransform.position, punchHitRadius, hitResults, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < pointerCount; i++)
        {
            Lab_DoorPointer pointer = hitResults[i] != null ? hitResults[i].GetComponentInParent<Lab_DoorPointer>() : null;
            if (pointer != null)
                pointer.Hit();
        }
    }

    // -------------------------
    // 충전 펀치 충격파 / 달리기 공격
    // -------------------------
    // 충전 펀치 애니메이션에는 타격 이벤트가 없어 피해가 0 이었다. 그리고 주먹(반경 0.6m)으로는
    // 2.5m 위에 떠 있는 드론과 높이 달린 출입구 표적에 닿지 않는다. 08·09 는 적을 다 잡아야 넘어가므로
    // 윤서는 진행할 수 없었다. 충전 펀치를 시선 방향의 충격파로 만들어 멀리·위로도 닿게 한다.
    private const float SprintAttackHitDelay = 0.25f;
    private const float PowerShockwaveDelay = 0.35f;
    private const float PowerShockwaveHalfAngle = 45f;

    private Coroutine powerShockwaveRoutine;

    private void SchedulePowerShockwave()
    {
        if (powerShockwaveRoutine != null)
            StopCoroutine(powerShockwaveRoutine);

        powerShockwaveRoutine = StartCoroutine(PowerShockwaveRoutine(queuedPowerTier, queuedPowerDamage));
    }

    private IEnumerator PowerShockwaveRoutine(int tier, float damage)
    {
        yield return new WaitForSeconds(PowerShockwaveDelay);
        powerShockwaveRoutine = null;
        PowerShockwave(tier, damage);
    }

    private IEnumerator DelayedHit(float delay, float damage)
    {
        yield return new WaitForSeconds(delay);
        PerformPunchHit(damage);
    }

    private static float ShockwaveRange(int tier)
    {
        return tier >= 3 ? 8f : tier == 2 ? 6f : 4f;
    }

    private void PowerShockwave(int tier, float damage)
    {
        if (tier <= 0 || damage <= 0f)
            return;

        // 1인칭이므로 시선(카메라) 방향으로 쏜다. 위를 보면 위로 나간다.
        Transform aim = transform;
        Move_Yunseo move = GetComponent<Move_Yunseo>();
        if (move != null && move.camTransform != null)
            aim = move.camTransform;

        Vector3 origin = aim == transform ? transform.position + Vector3.up * 1.4f : aim.position;
        Vector3 forward = aim.forward;
        float range = ShockwaveRange(tier);

        // 충전 펀치: 단계가 높을수록 크고 낮게
        AetherAudio.Play2D("power_punch", Mathf.Lerp(0.6f, 1f, tier / 3f), Mathf.Lerp(1.1f, 0.9f, tier / 3f), 0.03f);

        damagedTargets.Clear();

        Collider[] found = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < found.Length; i++)
        {
            Collider col = found[i];
            if (col == null || col.transform.IsChildOf(transform.root))
                continue;

            Vector3 closest = col.ClosestPointSafe(origin);
            Vector3 toTarget = closest - origin;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toTarget) > PowerShockwaveHalfAngle)
                continue;

            // 충전 펀치는 연구소 입구 문을 한 방에 부순다(표적 순서와 상관없이).
            Lab_Door labDoor = col.GetComponentInParent<Lab_Door>();
            if (labDoor != null && !labDoor.IsDisabled && Vector3.Distance(origin, closest) <= 3.5f)
            {
                AetherAudio.PlayAt("debris", closest, 1f, 4f, 60f, 0.05f, 1f, 80);
                labDoor.PunchOpen();
                continue;
            }

            Lab_DoorPointer pointer = col.GetComponentInParent<Lab_DoorPointer>();
            if (pointer != null)
            {
                pointer.Hit();
                continue;
            }

            if (col.isTrigger)
                continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null || damagedTargets.Contains(damageable))
                continue;

            damagedTargets.Add(damageable);
            damageable.TakeDamage(new DamageInfo(damage, DamageType.punch, gameObject, gameObject, closest));
        }

        AetherGunFX.Flash(origin + forward * 0.8f, forward, 0.35f + tier * 0.15f);
    }

    // -------------------------
    // InBattle 유지 / Input Lock
    // -------------------------
    private void EnterBattle()
    {
        animator.SetBool(hInBattle, true);

        if (battleKeepRoutine != null)
            StopCoroutine(battleKeepRoutine);

        battleKeepRoutine = StartCoroutine(BattleKeepCoroutine());
    }

    private IEnumerator BattleKeepCoroutine()
    {
        float end = Time.time + battleKeepTime;

        while (Time.time < end)
            yield return null;

        animator.SetBool(hInBattle, false);
        battleKeepRoutine = null;
    }

    private void ApplyInputLock(float seconds)
    {
        if (lockRoutine != null)
            StopCoroutine(lockRoutine);

        lockRoutine = StartCoroutine(InputLockCoroutine(seconds));
    }

    private IEnumerator InputLockCoroutine(float seconds)
    {
        inputLocked = true;
        yield return new WaitForSeconds(seconds);
        inputLocked = false;
        lockRoutine = null;
    }

    // -------------------------
    // Animation Events
    // -------------------------
    public void AE_OpenComboWindow()
    {
        if (!comboActive) return;
        canQueueNext = true;
    }

    public void AE_ComboDecision()
    {
        if (!comboActive) return;
        comboDecisionPending = true;
    }

    public void AE_EndComboFinal()
    {
        if (fpsJabMode) return;   // 잽은 콤보 끝 잠금(1초)을 쓰지 않는다
        EndCombo(true);
    }

    public void AE_PunchHit()
    {
        if (fpsJabMode) return;   // 잽 판정은 JabHit 이 한다(두 번 때리지 않게)

        float damage = GetCurrentPunchDamage();
        PerformPunchHit(damage);
    }

    public void AE_PowerPunchHit()
    {
        // 충격파가 같은 펀치를 처리한다. 이벤트가 붙은 애니메이션이 생겨도 두 번 때리지 않게 한다.
        if (powerShockwaveRoutine != null)
            return;

        PerformPunchHit(queuedPowerDamage);
    }

    // -------------------------
    // Debug Gizmo
    // -------------------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (leftPunchHitPoint != null)
            Gizmos.DrawWireSphere(leftPunchHitPoint.position, punchHitRadius);

        if (rightPunchHitPoint != null)
            Gizmos.DrawWireSphere(rightPunchHitPoint.position, punchHitRadius);
    }
}
