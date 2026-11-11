using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class Robots : MonoBehaviour, IObserverAlertReceiver
{
    private enum MoveState { Idle = 0, Walk = 1, WalkToStop = 2, Run = 3 }
    private enum AIState { Patrol, AlertPause, ChaseToRange, Attack, ChaseWide, ObserverInvestigate, Dead }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform player;
    [SerializeField] private RobotCombat combat;
    [SerializeField] private RobotHealth health;

    [Header("Alert Mark (WorldSpace Canvas or GameObject)")]
    [SerializeField] private GameObject alertMark;
    [SerializeField] private float alertMarkDuration = 1f;
    [SerializeField] private bool showAlertMarkInGameplay = false;
    [SerializeField] private bool hideLegacyRedDotMarkerInGameplay = true;

    [Header("Patrol (3s walk -> walkToStop -> 1s idle)")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float runSpeed = 3.8f;
    [SerializeField] private float walkDuration = 3f;
    [SerializeField] private float walkToStopDuration = 0.8f;
    [SerializeField] private float stopDuration = 1f;

    [Header("Random Patrol Nav")]
    [SerializeField] private int patrolRandomTryCount = 12;
    [SerializeField] private float minPatrolDistance = 2.5f;
    [SerializeField] private float maxPatrolDistance = 7f;
    [Tooltip("순찰은 처음 배치된 자리에서 이 거리 안에서만 한다. 쫓아가다 벗어나면 돌아온다.")]
    [SerializeField] private float patrolLeashRadius = 8f;
    [Tooltip("NavMesh.SamplePosition search radius")]
    [SerializeField] private float navSampleRadius = 2f;
    [Tooltip("Distance to consider patrol point reached")]
    [SerializeField] private float patrolReachThreshold = 0.8f;
    [Tooltip("Avoid choosing directions too far behind the robot")]
    [SerializeField] private bool avoidBackwardTooMuch = true;
    [SerializeField, Range(-1f, 1f)] private float backwardDotLimit = -0.2f;
    [Tooltip("Fallback straight-line patrol distance")]
    [SerializeField] private float patrolStepDistance = 6f;
    [Tooltip("Layers used as real floor when no NavMesh is available. Enemy and Player layers are intentionally ignored.")]
    [SerializeField] private LayerMask groundMask;

    [Header("Individual Variation")]
    [SerializeField] private bool randomizeIndividualBehavior = true;
    [SerializeField] private Vector2 initialPatrolDelayRange = new Vector2(0f, 2.5f);
    [SerializeField] private Vector2 moveSpeedMultiplierRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private Vector2 behaviorTimeMultiplierRange = new Vector2(0.75f, 1.3f);
    [SerializeField] private Vector2 turnSpeedMultiplierRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private Vector2 animationSpeedRange = new Vector2(0.92f, 1.08f);

    [Header("Detection (Sight)")]
    [SerializeField] private float sightRange = 18f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float sightCheckInterval = 0.1f;
    [SerializeField] private LayerMask obstacleMask;

    [Tooltip("이 거리 안으로 들어오면 장애물에 가려도 알아챈다. 건물에 붙어 선 로봇이 영영 못 보는 것을 막는다.")]
    [SerializeField] private float closeAwarenessRange = 12f;

    [Header("Combat")]
    [SerializeField] private float fireRange = 12f;
    [SerializeField] private float wideChaseRange = 30f;
    [SerializeField] private float giveUpRange = 35f;
    [SerializeField] private float alertPauseTime = 1f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private int burstShotCount = 3;
    [SerializeField] private float burstCooldown = 2f;
    [SerializeField] private bool startWithBurstCooldown = true;
    [SerializeField] private float bulletSpeed = 18f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletLifeTime = 4f;
    [SerializeField] private bool fireByAnimationEvent = true;
    [SerializeField] private float fireEventFallbackDelay = 0.25f;
    [SerializeField] private bool useAimingIdleDuringBurstCooldown = true;

    [Header("Observer Alert")]
    [SerializeField] private float observerSearchRadius = 3.5f;
    [SerializeField] private float observerArriveThreshold = 1.1f;
    [SerializeField] private float observerSearchPointInterval = 1.25f;

    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 540f;

    [Header("HP")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private float deathDespawnDelay = 2f;

    private bool isDead;
    private bool deathTriggered;

    private AIState aiState = AIState.Patrol;
    private MoveState moveState = MoveState.Idle;

    private float patrolTimer;
    private float stateTimer;
    private float initialPatrolDelay;
    private float initialAnimationPhase;

    private Vector3 patrolDirection;
    private Vector3 currentPatrolTarget;
    private bool hasPatrolTarget;
    private Vector3 homePosition;
    private Vector3 attackLockPos;
    private float repositionUntil;
    private float nextPlayerLookup;

    private Coroutine alertCo;
    private Coroutine sightCo;
    private Vector3 observerAlertCenter;
    private Vector3 observerInvestigateTarget;
    private float observerSearchEndTime;
    private float observerSearchPointTimer;
    private bool hasObserverInvestigateTarget;

    private static readonly int MoveStateHash = Animator.StringToHash("MoveState");
    private static readonly int DieTriggerHash = Animator.StringToHash("Die");

    private bool HasUsableAgent => agent != null && agent.enabled && agent.isOnNavMesh;
    private float fallbackGroundY;

    // 내비메시는 평평한 바닥 기준인데 그 위에 잔해 모델이 덮인 곳이 많아서,
    // 로봇이 잔해 위를 걸으면 발이 0.4~0.6m 씩 잔해 속에 묻혔다.
    // 발밑의 실제 표면 높이를 재서 에이전트를 그만큼 띄운다(최대 GroundLiftMax).
    private const float GroundLiftMax = 1.0f;
    private const float GroundLiftInterval = 0.15f;
    private const float GroundLiftSpeed = 2f;
    private static readonly RaycastHit[] GroundLiftHits = new RaycastHit[12];
    private float baseOffsetOrigin;
    private bool baseOffsetCaptured;
    private float groundLiftTarget;
    private float groundLiftTimer;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<RobotCombat>();
        health = GetComponent<RobotHealth>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (combat == null) combat = GetComponent<RobotCombat>() ?? gameObject.AddComponent<RobotCombat>();
        if (health == null) health = GetComponent<RobotHealth>() ?? gameObject.AddComponent<RobotHealth>();

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (sightRange <= fireRange)
            sightRange = fireRange + 3f;

        ApplyIndividualVariation();

        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.acceleration = 999f;
            agent.angularSpeed = 999f;
            agent.stoppingDistance = 0f;
            agent.updateRotation = false;
        }

        if (alertMark != null)
            alertMark.SetActive(false);
        HideLegacyRedDotMarker();

        patrolDirection = transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
        patrolStepDistance = Mathf.Max(patrolStepDistance, walkSpeed * walkDuration);
        groundMask = ResolveGroundMask(groundMask);
        fallbackGroundY = transform.position.y;

        // 총이 스킨메시로 붙어 있는 병사형은 발사 지점이 몸통에 고정되어 있어서,
        // 조준해도 총알이 가슴 앞 허공에서 나왔다. 총 메시의 총구로 옮긴다.
        if (firePoint != null && !FollowsArm(firePoint))
            AetherMuzzleFit.AttachToSkinnedWeapon(transform, firePoint);
        else if (firePoint != null)
            AetherMuzzleFit.SnapToBarrelTip(firePoint);

        // 순찰할 때 돌아올 자리. 없으면 무작위로 걸으며 배치한 곳에서 점점 멀어진다.
        homePosition = transform.position;

        combat.Initialize(new RobotCombatConfig
        {
            animator = animator,
            owner = transform,
            target = player,
            bulletPrefab = bulletPrefab,
            firePoint = firePoint,
            fireRange = fireRange,
            fireInterval = fireInterval,
            burstShotCount = burstShotCount,
            burstCooldown = burstCooldown,
            startWithBurstCooldown = startWithBurstCooldown,
            bulletSpeed = bulletSpeed,
            bulletDamage = bulletDamage,
            bulletLifeTime = bulletLifeTime,
            fireByAnimationEvent = fireByAnimationEvent,
            fireEventFallbackDelay = fireEventFallbackDelay,
            useAimingIdleDuringBurstCooldown = useAimingIdleDuringBurstCooldown
        });

        health.Configure(maxHP, deathDespawnDelay);
        health.Died += HandleDeath;

        sightCo = StartCoroutine(SightLoop());
        EnterPatrol();
        patrolTimer = -initialPatrolDelay;

        if (animator != null && randomizeIndividualBehavior)
            animator.Play(0, 0, initialAnimationPhase);
    }

    /// <summary>발사 지점이 손·무기 뼈 아래에 있는지. 아니면 팔 동작을 안 따라간다.</summary>
    private static bool FollowsArm(Transform point)
    {
        for (Transform t = point; t != null; t = t.parent)
        {
            string lower = t.name.ToLowerInvariant();
            if (lower.Contains("hand") || lower.Contains("weapon") || lower.Contains("lowerarm"))
                return true;
        }

        return false;
    }

    private void ApplyIndividualVariation()
    {
        if (!randomizeIndividualBehavior)
            return;

        initialPatrolDelay = Random.Range(
            Mathf.Min(initialPatrolDelayRange.x, initialPatrolDelayRange.y),
            Mathf.Max(initialPatrolDelayRange.x, initialPatrolDelayRange.y));
        initialAnimationPhase = Random.value;

        float moveMultiplier = Random.Range(
            Mathf.Min(moveSpeedMultiplierRange.x, moveSpeedMultiplierRange.y),
            Mathf.Max(moveSpeedMultiplierRange.x, moveSpeedMultiplierRange.y));
        float timeMultiplier = Random.Range(
            Mathf.Min(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y),
            Mathf.Max(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y));

        walkSpeed *= moveMultiplier;
        runSpeed *= Mathf.Clamp(moveMultiplier + Random.Range(-0.08f, 0.08f), 0.75f, 1.25f);
        walkDuration *= timeMultiplier;
        walkToStopDuration *= Random.Range(0.85f, 1.15f);
        stopDuration *= Random.Range(
            Mathf.Min(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y),
            Mathf.Max(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y));
        alertPauseTime *= Random.Range(0.8f, 1.2f);
        fireInterval *= Random.Range(0.85f, 1.15f);
        burstCooldown *= Random.Range(0.8f, 1.25f);
        rotateSpeed *= Random.Range(
            Mathf.Min(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y),
            Mathf.Max(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y));

        if (animator != null)
        {
            animator.speed = Random.Range(
                Mathf.Min(animationSpeedRange.x, animationSpeedRange.y),
                Mathf.Max(animationSpeedRange.x, animationSpeedRange.y));
        }

        if (agent != null)
            agent.avoidancePriority = Random.Range(30, 71);
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;

        if (sightCo != null) StopCoroutine(sightCo);
        if (alertCo != null) StopCoroutine(alertCo);

        if (alertMark != null)
            alertMark.SetActive(false);
    }

    private void Update()
    {
        if (isDead) return;

        UpdateGroundLift();
        UpdateStuckRecovery();
        AetherPlayerCharacter.RefreshTarget(ref player, ref nextPlayerLookup);
        combat.SetTarget(player);

        // 05 웨이브: 아직 깨지 않은 로봇은 순찰만 한다(AetherEncounterDirector).
        if (player == null || AetherEncounterDirector.IsDormant(this))
        {
            UpdatePatrolOnly();
            return;
        }

        switch (aiState)
        {
            case AIState.Patrol:
                UpdatePatrolOnly();
                break;
            case AIState.AlertPause:
                UpdateAlertPause();
                break;
            case AIState.ChaseToRange:
                UpdateChaseToRange();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
            case AIState.ChaseWide:
                UpdateChaseWide();
                break;
            case AIState.ObserverInvestigate:
                UpdateObserverInvestigate();
                break;
        }
    }

    private void LateUpdate()
    {
        HideLegacyRedDotMarker();
    }

    private IEnumerator SightLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(sightCheckInterval);

        while (true)
        {
            if (!isDead && player != null && aiState == AIState.Patrol && !AetherEncounterDirector.IsDormant(this))
            {
                if (CanSeePlayerSight(sightRange))
                    EnterAlertPause();
            }

            yield return wait;
        }
    }

    private void EnterPatrol()
    {
        aiState = AIState.Patrol;
        combat.StopCombatAnimation();

        SetAgentStopped(false);
        SetAgentSpeed(walkSpeed);

        patrolTimer = 0f;
        hasPatrolTarget = false;
        SetMoveState(MoveState.Idle);
    }

    private void UpdatePatrolOnly()
    {
        patrolTimer += Time.deltaTime;

        switch (moveState)
        {
            case MoveState.Idle:
                if (patrolTimer >= stopDuration)
                    BeginPatrolWalk();
                break;
            case MoveState.Walk:
                UpdatePatrolWalk();
                if (patrolTimer >= walkDuration)
                    BeginWalkToStop();
                break;
            case MoveState.WalkToStop:
                SetAgentStopped(true);
                if (patrolTimer >= walkToStopDuration)
                    BeginIdle();
                break;
            case MoveState.Run:
                BeginIdle();
                break;
        }
    }

    private void BeginPatrolWalk()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.Walk);

        SetAgentStopped(false);
        SetAgentSpeed(walkSpeed);

        if (TryGetRandomPatrolPoint(out Vector3 patrolPos))
        {
            currentPatrolTarget = patrolPos;
            hasPatrolTarget = true;

            Vector3 dir = currentPatrolTarget - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                patrolDirection = dir.normalized;
                RotateTowards(patrolDirection, rotateSpeed);
            }

            SetMoveDestination(currentPatrolTarget, walkSpeed, patrolReachThreshold);
        }
        else
        {
            hasPatrolTarget = false;
            ResetAgentPath();
            SetAgentStopped(true);
            BeginIdle();
        }
    }

    private void UpdatePatrolWalk()
    {
        if (!hasPatrolTarget) return;

        Vector3 toTarget = currentPatrolTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.001f)
            RotateTowards(toTarget.normalized, rotateSpeed);

        MoveWithoutNavMeshIfNeeded(currentPatrolTarget, walkSpeed, patrolReachThreshold);

        if (toTarget.magnitude <= patrolReachThreshold)
        {
            hasPatrolTarget = false;
            BeginWalkToStop();
            return;
        }

        if (HasUsableAgent && !agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete))
        {
            hasPatrolTarget = false;
            BeginIdle();
        }
    }

    private void BeginWalkToStop()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.WalkToStop);

        ResetAgentPath();
        SetAgentStopped(true);
    }

    private void BeginIdle()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.Idle);

        ResetAgentPath();
        SetAgentStopped(true);
    }

    private bool TryGetRandomPatrolPoint(out Vector3 result)
    {
        Vector3 origin = transform.position;

        // 플레이어를 쫓다가 배치 자리에서 멀어졌으면 무작위로 걷지 말고 돌아간다.
        if (!WithinLeash(origin) && TryGetNavPosition(homePosition, navSampleRadius, out Vector3 home))
        {
            if (!HasUsableAgent)
            {
                result = home;
                return true;
            }

            NavMeshPath homePath = new NavMeshPath();
            if (agent.CalculatePath(home, homePath) && homePath.status == NavMeshPathStatus.PathComplete)
            {
                result = home;
                return true;
            }
        }

        for (int i = 0; i < patrolRandomTryCount; i++)
        {
            Vector2 random2D = Random.insideUnitCircle.normalized;
            if (random2D.sqrMagnitude < 0.001f) continue;

            Vector3 dir = new Vector3(random2D.x, 0f, random2D.y);

            if (avoidBackwardTooMuch)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;

                if (forward.sqrMagnitude > 0.001f)
                {
                    forward.Normalize();
                    if (Vector3.Dot(forward, dir) < backwardDotLimit)
                        continue;
                }
            }

            float distance = Random.Range(minPatrolDistance, maxPatrolDistance);
            Vector3 desired = origin + dir * distance;

            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 flat = hit.position - origin;
            flat.y = 0f;
            if (flat.magnitude < minPatrolDistance * 0.7f)
                continue;

            if (!WithinLeash(hit.position))
                continue;

            if (IsBuriedSpot(hit.position))
                continue;

            if (HasUsableAgent)
            {
                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path))
                    continue;

                if (path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            result = hit.position;
            return true;
        }

        for (int i = 0; i < 5; i++)
        {
            Vector3 dir = GetNonZeroRandomDirectionXZ();
            Vector3 desired = origin + dir * patrolStepDistance;

            if (TryGetNavPosition(desired, navSampleRadius, out Vector3 navPos))
            {
                if (!WithinLeash(navPos))
                    continue;

                if (IsBuriedSpot(navPos))
                    continue;

                if (!HasUsableAgent)
                {
                    result = navPos;
                    return true;
                }

                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(navPos, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    result = navPos;
                    return true;
                }
            }
        }

        result = origin;
        return false;
    }

    private void UpdateGroundLift()
    {
        if (!HasUsableAgent)
            return;

        if (!baseOffsetCaptured)
        {
            baseOffsetCaptured = true;
            baseOffsetOrigin = agent.baseOffset;
        }

        groundLiftTimer -= Time.deltaTime;
        if (groundLiftTimer <= 0f)
        {
            groundLiftTimer = GroundLiftInterval;
            groundLiftTarget = MeasureGroundLift();
        }

        float target = baseOffsetOrigin + groundLiftTarget;
        if (!Mathf.Approximately(agent.baseOffset, target))
            agent.baseOffset = Mathf.MoveTowards(agent.baseOffset, target, GroundLiftSpeed * Time.deltaTime);
    }

    /// <summary>내비메시 위치보다 발밑의 실제 표면(잔해 등)이 얼마나 높은지. 0 ~ GroundLiftMax.</summary>
    private float MeasureGroundLift()
    {
        Vector3 nav = agent.nextPosition;
        Vector3 origin = nav + Vector3.up * (GroundLiftMax + 1.5f);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundLiftHits, GroundLiftMax + 2.5f, ~0, QueryTriggerInteraction.Ignore);

        float highest = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = GroundLiftHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            // 다른 적이나 플레이어 위로 올라서지 않는다.
            if (hit.CompareTag("Enemy") || hit.CompareTag("Player") || hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic)
                continue;

            // 머리 위를 가리는 것(천장·처마)은 발밑이 아니다.
            if (GroundLiftHits[i].point.y > nav.y + GroundLiftMax + 0.05f)
                continue;

            highest = Mathf.Max(highest, GroundLiftHits[i].point.y);
        }

        if (float.IsNegativeInfinity(highest))
            return 0f;

        return Mathf.Clamp(highest - nav.y, 0f, GroundLiftMax);
    }

    private const float StuckCheckSeconds = 0.8f;
    private const float StuckMinProgress = 0.25f;
    private Vector3 stuckCheckPos;
    private float stuckCheckUntil;
    private int stuckStrikes;

    /// <summary>
    /// 길찾기는 "가라"고 하는데 몸이 1.5초 동안 20cm 도 못 가면 낀 것으로 본다.
    /// 잔해 가장자리 같은 길찾기 경계에 몸이 붙으면 달리는 동작만 하며 제자리에 서 있었다(05 에서 실측).
    /// 한 번은 길을 다시 찾고, 그래도 끼어 있으면 다음 경유지로 살짝 옮겨 빼낸다.
    /// </summary>
    private void UpdateStuckRecovery()
    {
        if (!HasUsableAgent)
            return;

        bool wantsToMove = !agent.isStopped && agent.hasPath && !agent.pathPending
                           && agent.desiredVelocity.sqrMagnitude > 0.25f;
        if (!wantsToMove)
        {
            stuckStrikes = 0;
            stuckCheckPos = transform.position;
            stuckCheckUntil = Time.time + StuckCheckSeconds;
            return;
        }

        if (Time.time < stuckCheckUntil)
            return;

        Vector3 moved = transform.position - stuckCheckPos;
        moved.y = 0f;
        stuckCheckPos = transform.position;
        stuckCheckUntil = Time.time + StuckCheckSeconds;

        if (moved.magnitude >= StuckMinProgress)
        {
            stuckStrikes = 0;
            return;
        }

        stuckStrikes++;
        Vector3 destination = agent.destination;

        if (stuckStrikes >= 2)
        {
            agent.ResetPath();
            agent.SetDestination(destination);
            stuckStrikes = 0;
            return;
        }

        // 길찾기 경계에 붙어 모퉁이를 못 넘는 경우가 대부분이라, 모퉁이 너머 쪽으로 살짝 옮긴다.
        // (0.8m 는 달리는 속도로 0.2초 거리라 순간이동처럼 튀어 보이지 않는다)
        Vector3[] corners = agent.path.corners;
        Vector3 aim = corners.Length > 2 ? corners[2] : corners.Length > 1 ? corners[1] : destination;
        Vector3 step = aim - agent.nextPosition;
        step.y = 0f;
        Vector3 nudge = agent.nextPosition + (step.sqrMagnitude > 0.0001f ? step.normalized * Mathf.Min(0.8f, step.magnitude) : Vector3.zero);

        if (NavMesh.SamplePosition(nudge, out NavMeshHit hit, 0.6f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            agent.SetDestination(destination);
        }
    }

    private static readonly RaycastHit[] BuriedSpotHits = new RaycastHit[8];

    // 내비메시가 잔해 더미 밑까지 깔려 있어서, 그 위 자리를 고르면 하반신이 잔해에 묻힌 채 서 있었다.
    // 발 중심 바로 위(0.5~2.2m)에 위를 향한 표면이 있으면 묻힌 자리로 본다. 옆의 벽은 해당 없다.
    private bool IsBuriedSpot(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 2.2f, Vector3.down, BuriedSpotHits, 1.7f,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = BuriedSpotHits[i];
            Collider c = hit.collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.CompareTag("Enemy") || c.CompareTag("Player")) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            if (hit.normal.y < 0.3f) continue;
            return true;
        }
        return false;
    }

    private bool WithinLeash(Vector3 point)
    {
        Vector3 fromHome = point - homePosition;
        fromHome.y = 0f;
        return fromHome.magnitude <= patrolLeashRadius;
    }

    private float lastAlertSoundTime = -99f;

    private void EnterAlertPause()
    {
        aiState = AIState.AlertPause;
        stateTimer = 0f;

        ResetAgentPath();
        SetAgentStopped(true);

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Idle);
        ShowAlertMarkOnce();

        // 발견 신호음 + 몸을 돌리는 관절음. 플레이어에게 '들켰다'를 소리로 알려 준다.
        // 시야 끝에서 순찰↔경계를 오가면 1초마다 울려서 로봇마다 5초에 한 번만.
        if (Time.time - lastAlertSoundTime > 5f)
        {
            lastAlertSoundTime = Time.time;
            AetherAudio.PlayAt("robot_alert", transform.position + Vector3.up * 1.6f, 0.75f, 3f, 40f, 0.03f, 1f, 90);
            AetherAudio.PlayAt("robot_servo", transform.position + Vector3.up * 1.3f, 0.45f, 1.5f, 18f, 0.08f);
        }
    }

    private void UpdateAlertPause()
    {
        stateTimer += Time.deltaTime;
        FacePlayer(rotateSpeed);

        if (stateTimer >= alertPauseTime)
        {
            if (CanSeePlayerSight(sightRange))
                EnterChaseToRange();
            else
                EnterPatrol();
        }
    }

    private void ShowAlertMarkOnce()
    {
        if (alertMark == null) return;
        if (!showAlertMarkInGameplay)
        {
            alertMark.SetActive(false);
            return;
        }

        if (alertCo != null) StopCoroutine(alertCo);
        alertCo = StartCoroutine(AlertRoutine());
    }

    private void HideLegacyRedDotMarker()
    {
        if (!hideLegacyRedDotMarkerInGameplay || !Application.isPlaying)
            return;

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
            {
                Image image = images[imageIndex];
                if (image == null || image.sprite == null)
                    continue;

                if (!image.sprite.name.Contains("RedDot"))
                    continue;

                canvas.gameObject.SetActive(false);
                return;
            }
        }
    }

    private IEnumerator AlertRoutine()
    {
        alertMark.SetActive(true);
        yield return new WaitForSeconds(alertMarkDuration);
        alertMark.SetActive(false);
        alertCo = null;
    }

    private void EnterChaseToRange()
    {
        aiState = AIState.ChaseToRange;

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);
    }

    private void UpdateChaseToRange()
    {
        float dist = DistanceToPlayerXZ();
        bool inRubble = dist > 3f && IsBuriedSpot(transform.position);

        if (dist <= fireRange && !inRubble)
        {
            EnterAttack();
            return;
        }

        if (!CanSeePlayerSight(sightRange))
        {
            EnterChaseWide();
            return;
        }

        SetMoveDestination(player.position, runSpeed, inRubble ? 2f : fireRange);
        FacePlayer(rotateSpeed);
    }

    private void EnterAttack()
    {
        aiState = AIState.Attack;

        ResetAgentPath();
        SetAgentStopped(true);
        if (HasUsableAgent)
            agent.velocity = Vector3.zero;

        attackLockPos = transform.position;
        SetMoveState(MoveState.Idle);
        combat.EnterAttack();
    }

    private void UpdateAttack()
    {
        // 제자리에 고정하되 높이는 발밑 표면을 따른다(잔해 위에서 발이 묻히지 않게).
        if (HasUsableAgent)
            attackLockPos.y = agent.nextPosition.y + agent.baseOffset;

        transform.position = attackLockPos;
        FacePlayer(rotateSpeed);

        // 공격 중에는 제자리에 박혀 있다. 건물 모서리나 터렛에 총구가 묻힌 채로 서면
        // 영영 못 쏜다. 그럴 땐 플레이어 쪽으로 달려 쏠 수 있는 자리를 찾는다.
        if (combat.LineOfFireBlocked)
        {
            repositionUntil = Time.time + AetherCombatBalance.RobotRepositionSeconds;
            EnterChaseWide();
            return;
        }

        if (combat.TickAttack(DistanceToPlayerXZ()))
            EnterChaseWide();
    }

    private void EnterChaseWide()
    {
        aiState = AIState.ChaseWide;

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);
    }

    private void EnterObserverInvestigate(Vector3 alertPosition, float searchDuration)
    {
        if (isDead) return;

        aiState = AIState.ObserverInvestigate;

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);

        observerAlertCenter = alertPosition;
        observerInvestigateTarget = alertPosition;
        observerSearchEndTime = Time.time + Mathf.Max(0.5f, searchDuration);
        observerSearchPointTimer = 0f;
        hasObserverInvestigateTarget = true;

        SetMoveDestination(observerInvestigateTarget, runSpeed, observerArriveThreshold);
        ShowAlertMarkOnce();
    }

    private void UpdateChaseWide()
    {
        float dist = DistanceToPlayerXZ();

        if (dist > giveUpRange)
        {
            EnterPatrol();
            return;
        }

        // 자리를 옮기는 중이면 사거리 안이어도 트일 때까지(또는 시간이 다 될 때까지) 더 다가간다.
        // 달릴 때는 총을 겨누지 않아 총구가 벽에서 빠져 보이므로, 처음 얼마간은 검사하지 않고
        // 실제로 몸을 옮긴다. (안 그러면 그 자리에서 공격↔이동을 반복하며 한 발도 못 쏜다.)
        bool repositioning = Time.time < repositionUntil;
        bool movedEnough = Time.time >= repositionUntil - AetherCombatBalance.RobotRepositionSeconds + AetherCombatBalance.RobotRepositionMinSeconds;

        // 잔해 더미 속에서 멈춰 서면 하반신이 묻힌 채 쏘게 되므로, 빠져나올 때까지 더 다가간다.
        bool inRubble = dist > 3f && IsBuriedSpot(transform.position);

        if (dist <= fireRange && !inRubble && (!repositioning || (movedEnough && combat.HasClearShot())))
        {
            repositionUntil = 0f;
            EnterAttack();
            return;
        }

        SetMoveDestination(player.position, runSpeed, repositioning || inRubble ? 2f : fireRange);
        FacePlayer(rotateSpeed);
    }

    private void UpdateObserverInvestigate()
    {
        if (CanSeePlayerSight(sightRange))
        {
            EnterAlertPause();
            return;
        }

        if (Time.time >= observerSearchEndTime)
        {
            EnterPatrol();
            return;
        }

        if (hasObserverInvestigateTarget)
        {
            Vector3 toTarget = observerInvestigateTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                RotateTowards(toTarget.normalized, rotateSpeed);

            if (HasReachedDestination(observerArriveThreshold))
            {
                hasObserverInvestigateTarget = false;
                observerSearchPointTimer = 0f;
                ResetAgentPath();
                SetAgentStopped(true);
                SetMoveState(MoveState.Idle);
            }

            return;
        }

        observerSearchPointTimer += Time.deltaTime;
        if (observerSearchPointTimer < observerSearchPointInterval)
        {
            Vector3 lookCenter = observerAlertCenter - transform.position;
            lookCenter.y = 0f;
            if (lookCenter.sqrMagnitude > 0.001f)
                RotateTowards(lookCenter.normalized, rotateSpeed);

            return;
        }

        observerSearchPointTimer = 0f;

        if (TryGetObserverSearchPoint(out observerInvestigateTarget))
        {
            hasObserverInvestigateTarget = true;
            SetAgentStopped(false);
            SetMoveDestination(observerInvestigateTarget, runSpeed, observerArriveThreshold);
            SetMoveState(MoveState.Run);
        }
    }

    private bool CanSeePlayerSight(float maxDist)
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist > maxDist) return false;

        // 바짝 붙으면 등 뒤에 서 있어도 알아챈다.
        //
        // 시야각 검사가 먼저 돌아서, 플레이어가 옆이나 뒤로 돌아 들어오면
        // 코앞에 있어도 영영 못 봤다. 그래서 "가까이 갈수록 오히려 안 쏜다"가 됐다.
        // 발소리와 기척은 보는 방향과 상관없으므로 각도보다 먼저 통과시킨다.
        if (dist <= closeAwarenessRange)
            return true;

        Vector3 dir = toTarget / Mathf.Max(dist, 0.0001f);
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(transform.forward, flatDir.normalized);
            if (angle > viewAngle * 0.5f) return false;
        }

        // 제 사거리 안까지 들어왔으면 장애물 검사를 건너뛴다.
        //
        // 건물 콜라이더에 바짝 붙어 선 로봇이 많아서, 눈높이 광선이 0.3~0.5m 앞에서 막혀
        // 플레이어가 코앞에 와도 영영 못 보고 순찰만 돌았다.
        // "내 총이 닿는 거리면 알아챈다"로 두면 그런 로봇도 제 몫을 한다.
        if (dist <= fireRange)
            return true;

        return HasLineOfSight(target, maxDist);
    }

    /// <summary>
    /// 눈높이 한 줄기만 쏘면 턱·난간·벽 모서리에 걸려 영영 못 본다.
    /// 가슴·눈·머리 위 세 높이에서 쏴 보고 하나라도 뚫리면 보이는 것으로 친다.
    /// </summary>
    private bool HasLineOfSight(Vector3 target, float maxDist)
    {
        for (int i = 0; i < SightHeights.Length; i++)
        {
            Vector3 origin = transform.position + Vector3.up * SightHeights[i];
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;

            if (distance > maxDist)
                continue;

            if (distance < 0.05f)
                return true;

            Vector3 direction = toTarget / distance;

            // 제 몸이나 바로 옆 벽에 걸리지 않게 조금 앞에서 출발한다.
            const float skip = 0.45f;
            if (distance <= skip)
                return true;

            if (!Physics.Raycast(origin + direction * skip, direction, distance - skip, obstacleMask, QueryTriggerInteraction.Ignore))
                return true;
        }

        return false;
    }

    private static readonly float[] SightHeights = { 1.5f, 1.0f, 2.0f };

    private float DistanceToPlayerXZ()
    {
        if (player == null) return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = player.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void FacePlayer(float rotSpeed)
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        RotateTowards(toPlayer.normalized, rotSpeed);
    }

    private void RotateTowards(Vector3 dir, float rotSpeed)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
    }

    private Vector3 GetNonZeroRandomDirectionXZ()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.2f)
                return dir.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private bool TryGetNavPosition(Vector3 desired, float radius, out Vector3 navPos)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            navPos = hit.position;
            return true;
        }

        navPos = desired;
        return false;
    }

    private void SetAgentSpeed(float speed)
    {
        if (agent != null && agent.enabled)
            agent.speed = speed;
    }

    private void SetAgentStopped(bool stopped)
    {
        if (HasUsableAgent)
            agent.isStopped = stopped;
    }

    private void ResetAgentPath()
    {
        if (HasUsableAgent)
            agent.ResetPath();
    }

    private void SetMoveDestination(Vector3 destination, float speed, float stoppingDistance)
    {
        if (HasUsableAgent)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(destination);
            return;
        }

        MoveDirectlyToward(destination, speed, stoppingDistance);
    }

    private void MoveWithoutNavMeshIfNeeded(Vector3 destination, float speed, float stoppingDistance)
    {
        if (HasUsableAgent)
            return;

        MoveDirectlyToward(destination, speed, stoppingDistance);
    }

    private void MoveDirectlyToward(Vector3 destination, float speed, float stoppingDistance)
    {
        Vector3 current = transform.position;
        Vector3 target = destination;
        current.y = 0f;
        target.y = 0f;

        Vector3 toTarget = target - current;
        float distance = toTarget.magnitude;
        if (distance <= stoppingDistance || distance <= 0.001f)
            return;

        Vector3 direction = toTarget / distance;
        RotateTowards(direction, rotateSpeed);

        Vector3 nextPosition = transform.position + direction * (speed * Time.deltaTime);
        transform.position = SnapRuntimePositionToGround(nextPosition);
    }

    private Vector3 SnapRuntimePositionToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 4f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.point.y - fallbackGroundY) <= 1.5f)
            {
                position.y = hit.point.y;
                fallbackGroundY = hit.point.y;
                return position;
            }
        }

        position.y = fallbackGroundY;

        return position;
    }

    private static LayerMask ResolveGroundMask(LayerMask configuredMask)
    {
        if (configuredMask.value != 0)
            return configuredMask;

        int mask = LayerMask.GetMask("floor", "Default", "ElevatorInterior");
        if (mask == 0)
            mask = ~LayerMask.GetMask("Player", "Enemy", "UI", "Ignore Raycast", "Water");

        return mask;
    }

    private bool TryGetObserverSearchPoint(out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset2D = Random.insideUnitCircle * observerSearchRadius;
            Vector3 desired = observerAlertCenter + new Vector3(offset2D.x, 0f, offset2D.y);

            if (!TryGetNavPosition(desired, navSampleRadius, out Vector3 navPos))
                continue;

            if (HasUsableAgent)
            {
                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(navPos, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            result = navPos;
            return true;
        }

        result = observerAlertCenter;
        return false;
    }

    private bool HasReachedDestination(float threshold)
    {
        if (!HasUsableAgent)
            return Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(observerInvestigateTarget.x, 0f, observerInvestigateTarget.z)) <= threshold;

        if (agent.pathPending) return false;
        if (!agent.hasPath) return true;
        if (agent.remainingDistance > threshold) return false;

        return agent.velocity.sqrMagnitude <= 0.01f;
    }

    public void OnObserverAlert(Vector3 alertPosition, float searchDuration)
    {
        if (!isActiveAndEnabled || isDead) return;
        if (AetherEncounterDirector.IsDormant(this)) return;   // 대기 중인 로봇은 정찰 드론 경보에 모이지 않는다
        if (player != null && CanSeePlayerSight(sightRange)) return;

        if (!TryGetNavPosition(alertPosition, navSampleRadius + observerSearchRadius, out Vector3 navPos))
            return;

        EnterObserverInvestigate(navPos, searchDuration);
    }

    public void OnFireAnimationEvent()
    {
        if (!isDead)
            combat.OnFireAnimationEvent(DistanceToPlayerXZ());
    }

    private void HandleDeath(RobotHealth robotHealth)
    {
        if (deathTriggered) return;

        deathTriggered = true;
        isDead = true;
        aiState = AIState.Dead;

        // 전원이 꺼지며 쓰러지는 금속음
        AetherAudio.PlayAt("robot_death", transform.position + Vector3.up, 1f, 3f, 45f, 0.05f, 1f, 60);

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Idle);

        if (agent != null)
        {
            ResetAgentPath();
            SetAgentStopped(true);
            agent.enabled = false;
        }

        if (alertMark != null)
            alertMark.SetActive(false);

        if (alertCo != null) StopCoroutine(alertCo);
        if (sightCo != null) StopCoroutine(sightCo);

        if (animator != null)
        {
            animator.ResetTrigger(DieTriggerHash);
            animator.SetTrigger(DieTriggerHash);
        }

        StartCoroutine(DespawnAfter(robotHealth.DeathDespawnDelay));
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private void SetMoveState(MoveState state)
    {
        moveState = state;
        if (animator != null)
            animator.SetInteger(MoveStateHash, (int)state);
    }
}
