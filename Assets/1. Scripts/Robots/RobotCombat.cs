using UnityEngine;

public struct RobotCombatConfig
{
    public Animator animator;
    public Transform owner;
    public Transform target;
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRange;
    public float fireInterval;
    public int burstShotCount;
    public float burstCooldown;
    public bool startWithBurstCooldown;
    public float bulletSpeed;
    public int bulletDamage;
    public float bulletLifeTime;
    public bool fireByAnimationEvent;
    public float fireEventFallbackDelay;
    public bool useAimingIdleDuringBurstCooldown;
}

public class RobotCombat : MonoBehaviour
{
    private static readonly int IsFiringHash = Animator.StringToHash("IsFiring");
    private static readonly int IsAimingIdleHash = Animator.StringToHash("IsAimingIdle");

    private Animator animator;
    private Transform owner;
    private Transform target;
    private GameObject bulletPrefab;
    private Transform firePoint;
    private float fireRange;
    private float fireInterval;
    private int burstShotCount;
    private float burstCooldown;
    private bool startWithBurstCooldown;
    private float bulletSpeed;
    private int bulletDamage;
    private float bulletLifeTime;
    private bool fireByAnimationEvent;
    private bool hasFiringState;
    private float fireEventFallbackDelay;
    private bool useAimingIdleDuringBurstCooldown;

    private bool fireEventPending;

    /// <summary>이 시각까지는 조준 자세 플래그를 내려 둔다. 사격 동작이 끊기지 않게.</summary>
    private float firingPoseHoldUntil;
    private bool inBurstCooldown;
    private float fireTimer;
    private float burstCooldownTimer;
    private float fireEventPendingTimer;
    private int burstShotsFired;

    /// <summary>쏠 차례인데 벽에 막혀 못 쏘기 시작한 시각. 막히지 않았으면 음수.</summary>
    private float blockedSince = -1f;


    /// <summary>이만큼 오래 막혀 있으면 자리를 옮겨야 한다.</summary>
    public bool LineOfFireBlocked => blockedSince >= 0f && Time.time - blockedSince >= AetherCombatBalance.RobotBlockedRepositionSeconds;

    public void Initialize(RobotCombatConfig config)
    {
        animator = config.animator;
        owner = config.owner;
        target = config.target;
        bulletPrefab = config.bulletPrefab;
        firePoint = config.firePoint;
        fireRange = config.fireRange;
        // 씬에 0 으로 저장된 로봇이 13기나 돼서, 3연발이 한 덩어리로 터져 나왔다.
        fireInterval = Mathf.Max(config.fireInterval, AetherCombatBalance.MinRobotFireInterval);
        burstShotCount = Mathf.Max(1, config.burstShotCount);
        burstCooldown = config.burstCooldown;
        startWithBurstCooldown = config.startWithBurstCooldown;
        bulletSpeed = config.bulletSpeed;
        bulletDamage = config.bulletDamage;
        bulletLifeTime = config.bulletLifeTime;
        // 씬에 이 값이 꺼진 채 저장된 로봇(Red_Assault_01, Camo_Heavy_01)은 사격 동작 없이
        // 조준 자세에서 총알만 나갔다. 사격 동작이 있는 로봇은 무조건 동작을 거쳐 쏜다.
        hasFiringState = HasFiringState(config.animator);
        fireByAnimationEvent = config.fireByAnimationEvent || hasFiringState;
        // 0.25초로는 발사 애니메이션 이벤트(1.0초 지점)가 올 틈이 없어,
        // 총을 들어 올리는 도중에 총알이 나갔다. 이벤트가 오면 그쪽이 쏘게 한다.
        fireEventFallbackDelay = Mathf.Max(config.fireEventFallbackDelay, AetherCombatBalance.MinFireEventFallback);
        useAimingIdleDuringBurstCooldown = config.useAimingIdleDuringBurstCooldown;

        StopCombatAnimation();
    }

    public void SetTarget(Transform nextTarget)
    {
        target = nextTarget;
    }

    public void EnterAttack()
    {
        fireTimer = 0f;
        burstCooldownTimer = 0f;
        fireEventPendingTimer = 0f;
        fireEventPending = false;
        inBurstCooldown = startWithBurstCooldown;
        burstShotsFired = 0;
        blockedSince = -1f;

        StopCombatAnimation();
    }

    public bool TickAttack(float distanceToTarget)
    {
        if (target == null) return true;
        if (distanceToTarget > fireRange) return true;

        // 사거리 안이면 총이 플레이어를 겨누게 한다.
        // 이게 없으면 총알만 플레이어 쪽으로 날아가고 총은 엉뚱한 데를 보고 있다.
        AimWeaponAtTarget();

        if (inBurstCooldown)
        {
            // 연발이 끝나는 순간은 아직 사격 동작이 재생 중이다.
            // 여기서 조준 자세를 켜 버리면 그 동작이 잘린다.
            if (useAimingIdleDuringBurstCooldown && Time.time >= firingPoseHoldUntil)
                SetAimingIdle(true);

            burstCooldownTimer += Time.deltaTime;
            if (burstCooldownTimer >= burstCooldown)
            {
                inBurstCooldown = false;
                burstCooldownTimer = 0f;
                fireTimer = 0f;
            }

            return false;
        }

        if (fireEventPending)
        {
            fireEventPendingTimer += Time.deltaTime;

            if (!hasFiringState)
            {
                if (fireEventPendingTimer >= fireEventFallbackDelay)
                {
                    FireBullet();
                    AfterShotFired();
                }

                return false;
            }

            // 시간이 아니라 애니메이터를 보고 쏜다. 총을 다 든 사격 구간에서만 나간다.
            if (TryGetFiringPhase(out float phase) &&
                phase >= AetherCombatBalance.RobotFirePhase &&
                phase <= AetherCombatBalance.RobotFirePhaseLimit)
            {
                FireBullet();
                AfterShotFired();
            }
            else if (fireEventPendingTimer >= AetherCombatBalance.RobotFirePoseTimeout)
            {
                // 사격 동작에 끝내 못 들어갔다(맞아서 끊겼거나 다른 동작에 잘렸다).
                // 엉뚱한 자세로 쏘느니 이 탄은 버리고 곧 다시 시도한다.
                CancelPendingShot();
            }

            return false;
        }

        // 쏘는 중이 아니면 '총 든 자세'를 유지한다.
        //
        // 애니메이터는 Aiming Idle ──IsFiring──▶ Firing Rifle ──▶ Aiming Idle 로 돌고,
        // Firing Rifle 로 들어가는 길은 Aiming Idle 에서 나오는 것 하나뿐이다.
        // 자세를 풀어 두면 총을 내린 Rifle Idle 로 가 버려서, IsFiring 을 켜도
        // 사격 동작이 재생되지 않는다. (총을 내린 채 총알만 나오던 이유다.)
        //
        // 다만 사격 동작이 재생되는 동안에는 내려 두어야 한다.
        // 'Any State → Aiming Idle' 전환이 IsAimingIdle 만 보고 아무 때나 끼어들어
        // 사격 동작을 잘라 버리기 때문이다.
        if (Time.time >= firingPoseHoldUntil)
            SetAimingIdle(true);

        fireTimer += Time.deltaTime;
        if (fireTimer < fireInterval) return false;

        // 총구가 벽에 파묻혔거나 사이에 벽이 있으면 쏘지 않는다.
        // 공격 중에는 제자리에 서서 쏘기만 해서, 건물 모서리에 멈춘 로봇은 총알을 전부
        // 벽에 박았다(쏘자마자 0.2m 에서 사라져 '총알이 안 나오는 적'으로 보였다).
        // 오래 막혀 있으면 Robots 가 자리를 옮긴다(LineOfFireBlocked).
        if (!HasClearShot())
        {
            if (blockedSince < 0f)
                blockedSince = Time.time;

            fireTimer = fireInterval - 0.12f;
            return false;
        }

        blockedSince = -1f;

        // 적 전체가 한꺼번에 쏘지 않도록 차례를 기다린다.
        // 총알만 거르면 빈 총을 쏘는 꼴이 되므로, 발사 동작에 들어가기 전에 막는다.
        if (!AetherFireDirector.TryShoot(AetherFireDirector.Kind.Robot, this))
        {
            fireTimer = fireInterval - 0.12f;   // 조금 뒤에 다시 물어본다
            return false;
        }

        if (fireByAnimationEvent)
        {
            fireEventPending = true;
            fireEventPendingTimer = 0f;

            // 조준 자세 플래그를 먼저 내려야 'Any State → Aiming Idle' 이 사격 동작을 끊지 않는다.
            SetAimingIdle(false);
            firingPoseHoldUntil = Time.time + AetherCombatBalance.FiringAnimationSeconds;

            if (hasFiringState)
            {
                // IsFiring 전환은 Aiming Idle 에서만 출발한다. 연발 중에는 이미 Firing Rifle 이라
                // 동작이 다시 시작되지 않고 반복 재생되던 자리에서 총알이 나갔다.
                // 매 탄마다 사격 동작을 직접 다시 튼다. 연발 중이면 총을 든 지점부터.
                // IsFiring 은 꺼 둔 채라 동작이 끝나면(78.6%) 스스로 Aiming Idle 로 돌아간다.
                PlayFiringAnimation();
                return false;
            }

            SetFiring(true);
            return false;
        }

        FireBullet();
        AfterShotFired();
        return false;
    }

    public void OnFireAnimationEvent(float distanceToTarget)
    {
        if (!fireByAnimationEvent) return;
        // 사격 동작이 있는 로봇은 애니메이터 진행도를 보고 쏜다. 이 이벤트는 클립 끝(1.0)에
        // 찍혀 있어, 반복 재생 중에 오면 총을 내리는 순간에 쏘게 된다.
        if (hasFiringState) return;
        if (!fireEventPending) return;
        if (distanceToTarget > fireRange) return;

        FireBullet();
        AfterShotFired();
    }

    public void StopCombatAnimation()
    {
        fireEventPending = false;
        SetFiring(false);
        SetAimingIdle(false);
    }

    private AetherWeaponAim weaponAim;
    private bool weaponAimResolved;

    /// <summary>총이 플레이어를 보도록 한다. 총을 쥔 뼈(발사 지점의 부모)를 돌린다.</summary>
    private void AimWeaponAtTarget()
    {
        if (target == null || firePoint == null || firePoint.parent == null)
            return;

        if (!weaponAimResolved)
        {
            weaponAimResolved = true;

            Transform gun = firePoint.parent;
            weaponAim = gun.GetComponent<AetherWeaponAim>();
            if (weaponAim == null)
                weaponAim = gun.gameObject.AddComponent<AetherWeaponAim>();

            weaponAim.Configure(firePoint);
        }

        if (weaponAim != null)
            weaponAim.AimAt(target.position + Vector3.up * 1.0f);
    }

    private void FireBullet()
    {
        if (bulletPrefab == null || target == null) return;

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : transform.position + transform.forward * 1.1f + Vector3.up * 1.2f;

        Vector3 targetPos = target.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - spawnPos).normalized;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        // 손에 든 총은 레이저 무기로 보이게 한다. 화염은 작게, 반동은 그대로 남긴다.
        float halfBolt = AetherGunFX.ShapeLaserBolt(bulletObj, dir);

        // 탄이 길어서 총구에 그대로 두면 절반이 총열 뒤로 삐져나온다. 꼬리를 총구에 맞춘다.
        bulletObj.transform.position = spawnPos + dir * halfBolt;

        // 총구에 붙은 빛줄기가 '총에서 나왔다'는 걸 보여 준다. 총알만으로는 안 읽힌다.
        AetherGunFX.LaserMuzzleBeam(spawnPos, dir);
        AetherGunFX.FlashLaser(spawnPos, dir);
        AetherShotAudio.PlayLaser(spawnPos);

        // 발사 지점만 밀면 눈에 보이는 게 없다. 총 자체(발사 지점의 부모)를 민다.
        Transform recoilTarget = firePoint != null && firePoint.parent != null ? firePoint.parent : firePoint;
        AetherGunFX.Kick(recoilTarget, dir, 0.045f, 5f);

        EnemyBullet bullet = bulletObj.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(dir, bulletSpeed, AetherCombatBalance.ScaleRobotDamage(bulletDamage), bulletLifeTime, owner != null ? owner : transform);
            return;
        }

        RobotBullet robotBullet = bulletObj.GetComponent<RobotBullet>();
        if (robotBullet != null)
        {
            robotBullet.Initialize(dir, bulletSpeed, AetherCombatBalance.ScaleRobotDamage(bulletDamage), bulletLifeTime, owner != null ? owner : transform);
        }
    }

    private void AfterShotFired()
    {
        fireEventPending = false;
        fireEventPendingTimer = 0f;
        fireTimer = 0f;
        SetFiring(false);

        burstShotsFired++;
        if (burstShotsFired >= burstShotCount)
        {
            burstShotsFired = 0;
            inBurstCooldown = true;
            burstCooldownTimer = 0f;
        }
    }

    private static readonly int FiringStateHash = Animator.StringToHash("Base Layer.Firing Rifle");

    private static bool HasFiringState(Animator target)
    {
        return target != null && target.runtimeAnimatorController != null && target.HasState(0, FiringStateHash);
    }

    private void PlayFiringAnimation()
    {
        if (animator == null)
            return;

        bool alreadyFiring = TryGetFiringPhase(out _);
        animator.CrossFadeInFixedTime(FiringStateHash,
            alreadyFiring ? 0.1f : 0.25f,
            0,
            alreadyFiring ? AetherCombatBalance.RobotFireRestartSeconds : 0f);
    }

    /// <summary>지금(넘어가는 중이면 넘어갈 쪽) 사격 동작이면 그 진행도(0~1)를 준다.</summary>
    private bool TryGetFiringPhase(out float phase)
    {
        phase = 0f;
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        if (info.fullPathHash != FiringStateHash)
            return false;

        // 클립이 반복 재생이라 1 을 넘어간다.
        phase = info.normalizedTime - Mathf.Floor(info.normalizedTime);
        return true;
    }

    /// <summary>지금 쏘면 총알이 플레이어까지 갈 수 있는지. (AetherLineOfFire)</summary>
    public bool HasClearShot()
    {
        if (target == null)
            return false;

        Transform self = owner != null ? owner : transform;
        Vector3 muzzle = firePoint != null ? firePoint.position : self.position + Vector3.up * 1.4f;
        Vector3 body = self.position + Vector3.up * (muzzle.y - self.position.y);

        return AetherLineOfFire.IsClear(body, muzzle, target.position + Vector3.up * 1.0f, self, target);
    }

    private void CancelPendingShot()
    {
        fireEventPending = false;
        fireEventPendingTimer = 0f;
        fireTimer = fireInterval - 0.12f;
        firingPoseHoldUntil = Time.time;
        SetFiring(false);
    }

    private void SetFiring(bool firing)
    {
        if (animator != null)
            animator.SetBool(IsFiringHash, firing);
    }

    private void SetAimingIdle(bool aimingIdle)
    {
        if (animator != null)
            animator.SetBool(IsAimingIdleHash, aimingIdle);
    }
}
