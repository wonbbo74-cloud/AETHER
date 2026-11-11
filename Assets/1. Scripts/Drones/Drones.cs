using UnityEngine;
using UnityEngine.UI;

public class Drones : MonoBehaviour, IObserverAlertReceiver
{
    public enum State { Patrol, Investigate, Engage }
    public enum DroneRole { Observer, Suicider, Attacker }

    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform patrolCenterOverride;
    [HideInInspector, SerializeField] private Transform waypointParent;

    [Header("Move")]
    [SerializeField] private float flySpeed = 8f;
    [SerializeField] private float rotateSpeed = 8f;
    [SerializeField] private float arriveDistance = 1.2f;

    [Header("Patrol")]
    [SerializeField] private float patrolRadius = 25f;
    [SerializeField] private float minPatrolDistance = 6f;
    [SerializeField] private float maxPatrolDistance = 16f;
    [SerializeField] private float patrolPauseTime = 1.2f;
    [SerializeField] private int patrolRandomTryCount = 12;
    [SerializeField, Range(0f, 1f)] private float keepDirectionBias = 0.7f;

    [Header("Individual Variation")]
    [SerializeField] private bool randomizeIndividualBehavior = true;
    [SerializeField] private Vector2 initialPatrolDelayRange = new Vector2(0f, 2.5f);
    [SerializeField] private Vector2 flySpeedMultiplierRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private Vector2 turnSpeedMultiplierRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private Vector2 patrolTimeMultiplierRange = new Vector2(0.7f, 1.35f);
    [SerializeField] private Vector2 patrolRadiusMultiplierRange = new Vector2(0.85f, 1.2f);

    [Header("Vision")]
    [SerializeField] private float visionRange = 25f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Hearing / Investigate")]
    [SerializeField] private float investigateMemorySeconds = 5f;
    [SerializeField] private float investigateStopSeconds = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip idleLoop;
    [SerializeField] private AudioClip alertOneShot;
    [SerializeField] private AudioClip investigateOneShot;
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float combatVolume = 0.75f;
    [SerializeField] private float volumeLerpSpeed = 6f;

    [Header("Alert Loop")]
    [SerializeField] private AudioClip alertLoop;
    [SerializeField, Range(0f, 1f)] private float alertLoopVolume = 1f;
    [SerializeField, Range(0.5f, 2f)] private float alertLoopPitch = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugLogTarget = false;
    [SerializeField] private bool hideLegacyRedDotMarkerInGameplay = true;

    private Rigidbody rb;
    private AudioSource audioSource;
    private AudioSource alertSource;
    private DroneCombatBase combat;
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[16];

    private State state;
    private State prevState;
    private Vector3 patrolCenter;
    private float fixedHeight;
    private Vector3 patrolDirection;
    private Vector3 currentPatrolTarget;
    private Vector3 debugCurrentTarget = Vector3.positiveInfinity;
    private bool hasPatrolTarget;
    private float patrolWaitTimer;
    private float investigateArriveTime;
    private Vector3 lastInvestigatePosition;
    private float lastInvestigateTime = float.NegativeInfinity;
    private float initialPatrolDelay;

    public Transform Player => player;
    public State CurrentState => state;
    public float FixedHeight => fixedHeight;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        ApplyIndividualVariation();

        fixedHeight = transform.position.y;
        patrolCenter = patrolCenterOverride != null ? patrolCenterOverride.position : transform.position;
        patrolCenter.y = fixedHeight;
        patrolDirection = GetPlanarForward();

        SetupAudio();
        EnsureCombatComponent();
        combat = GetComponent<DroneCombatBase>();
        if (combat != null)
            combat.Initialize(this);
        HideLegacyRedDotMarker();

        state = State.Patrol;
        prevState = state;
        patrolWaitTimer = -initialPatrolDelay;
    }

    private void ApplyIndividualVariation()
    {
        if (!randomizeIndividualBehavior)
            return;

        initialPatrolDelay = Random.Range(
            Mathf.Min(initialPatrolDelayRange.x, initialPatrolDelayRange.y),
            Mathf.Max(initialPatrolDelayRange.x, initialPatrolDelayRange.y));

        flySpeed *= Random.Range(
            Mathf.Min(flySpeedMultiplierRange.x, flySpeedMultiplierRange.y),
            Mathf.Max(flySpeedMultiplierRange.x, flySpeedMultiplierRange.y));
        rotateSpeed *= Random.Range(
            Mathf.Min(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y),
            Mathf.Max(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y));
        patrolPauseTime *= Random.Range(
            Mathf.Min(patrolTimeMultiplierRange.x, patrolTimeMultiplierRange.y),
            Mathf.Max(patrolTimeMultiplierRange.x, patrolTimeMultiplierRange.y));

        float radiusMultiplier = Random.Range(
            Mathf.Min(patrolRadiusMultiplierRange.x, patrolRadiusMultiplierRange.y),
            Mathf.Max(patrolRadiusMultiplierRange.x, patrolRadiusMultiplierRange.y));
        patrolRadius *= radiusMultiplier;
        minPatrolDistance *= radiusMultiplier;
        maxPatrolDistance *= radiusMultiplier;
        investigateStopSeconds *= Random.Range(0.8f, 1.2f);
        alertLoopPitch = Mathf.Clamp(alertLoopPitch * Random.Range(0.94f, 1.06f), 0.5f, 2f);
    }

    private float nextPlayerLookup;

    private void Update()
    {
        AetherPlayerCharacter.RefreshTarget(ref player, ref nextPlayerLookup);
        if (player == null) return;

        // 05 웨이브: 아직 깨지 않은 드론은 제자리에 떠 있기만 한다(AetherEncounterDirector).
        if (AetherEncounterDirector.IsDormant(this)) return;

        ResolveState();
        HandleStateTransition();
        HandleAudioByState();

        switch (state)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Investigate:
                UpdateInvestigate();
                break;
            case State.Engage:
                UpdateEngage();
                break;
        }

        prevState = state;
    }

    private void LateUpdate()
    {
        HideLegacyRedDotMarker();
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

    protected virtual void EnsureCombatComponent()
    {
    }

    private void ResolveState()
    {
        bool canSee;
        if (combat != null && combat.TryOverrideCombatDetection(out bool overrideDetection))
            canSee = overrideDetection;
        else
            canSee = CanSeePlayer();

        bool heardRecently = Time.time - lastInvestigateTime <= investigateMemorySeconds;

        // 자폭 드론이 한번 락온 후 돌진 중이면 계속 Engage 유지
        DroneSuiciderCombat suiciderCombat = combat as DroneSuiciderCombat;
        if (suiciderCombat != null && suiciderCombat.IsChargeCommitted)
        {
            state = State.Engage;
            return;
        }

        if (canSee)
            state = State.Engage;
        else if (heardRecently)
            state = State.Investigate;
        else
            state = State.Patrol;
    }

    private void HandleStateTransition()
    {
        if (state == prevState) return;

        if (state == State.Investigate)
        {
            investigateArriveTime = 0f;
            hasPatrolTarget = false;
            patrolWaitTimer = 0f;
            PlayOneShot(investigateOneShot, 0.8f);
        }

        if (state == State.Engage && prevState != State.Engage)
        {
            combat?.OnEnterCombat();
            PlayOneShot(alertOneShot, 1f);
        }
        else if (prevState == State.Engage)
        {
            combat?.OnExitCombat();
        }
    }

    private void UpdatePatrol()
    {
        if (!hasPatrolTarget)
        {
            patrolWaitTimer += Time.deltaTime;
            if (patrolWaitTimer < patrolPauseTime) return;

            if (!TryGetRandomPatrolPoint(out currentPatrolTarget))
                return;

            hasPatrolTarget = true;
            patrolWaitTimer = 0f;
            debugCurrentTarget = currentPatrolTarget;

            if (debugLogTarget)
                Debug.Log($"[Drones] Patrol -> {currentPatrolTarget}", this);
        }

        if (MoveToPoint(currentPatrolTarget, currentPatrolTarget))
        {
            Vector3 dir = currentPatrolTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                patrolDirection = dir.normalized;

            hasPatrolTarget = false;
            patrolWaitTimer = 0f;
        }
    }

    private void UpdateInvestigate()
    {
        Vector3 investigatePos = ClampHeight(lastInvestigatePosition);
        debugCurrentTarget = investigatePos;

        bool arrived = MoveToPoint(investigatePos, lastInvestigatePosition);
        if (!arrived)
        {
            investigateArriveTime = 0f;
            return;
        }

        if (investigateArriveTime <= 0f)
            investigateArriveTime = Time.time;

        if (Time.time - investigateArriveTime >= investigateStopSeconds)
        {
            state = State.Patrol;
            hasPatrolTarget = false;
            patrolWaitTimer = 0f;
        }
    }

    private void UpdateEngage()
    {
        if (combat != null)
            combat.TickCombat();
        else
            FaceTo(player.position);
    }

    public bool MoveToPoint(Vector3 worldPoint, Vector3 lookAt, float speedMultiplier = 1f)
    {
        Vector3 target = ClampHeight(worldPoint);
        Vector3 current = ClampHeight(transform.position);
        Vector3 next = Vector3.MoveTowards(current, target, flySpeed * Mathf.Max(0.1f, speedMultiplier) * Time.deltaTime);
        transform.position = next;
        FaceTo(lookAt);
        return Vector3.Distance(next, target) <= arriveDistance;
    }

    public void FaceTo(Vector3 lookPos)
    {
        Vector3 flatLookPos = ClampHeight(lookPos);
        Vector3 dir = flatLookPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    public float DistanceToPlayerXZ()
    {
        if (player == null) return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = player.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public Vector3 DirectionToPlayerXZ()
    {
        if (player == null) return Vector3.zero;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.zero;
    }

    public Vector3 ClampHeight(Vector3 position)
    {
        position.y = fixedHeight;
        return position;
    }

    public Vector3 ClampInsidePatrolRadius(Vector3 position)
    {
        Vector3 center = patrolCenterOverride != null ? patrolCenterOverride.position : patrolCenter;
        center.y = fixedHeight;

        Vector3 offset = position - center;
        offset.y = 0f;

        if (offset.magnitude > patrolRadius)
            offset = offset.normalized * patrolRadius;

        return center + offset;
    }

    private bool TryGetRandomPatrolPoint(out Vector3 result)
    {
        Vector3 center = patrolCenterOverride != null ? patrolCenterOverride.position : patrolCenter;
        center.y = fixedHeight;
        Vector3 forward = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : GetPlanarForward();

        for (int i = 0; i < patrolRandomTryCount; i++)
        {
            Vector2 random2D = Random.insideUnitCircle;
            if (random2D.sqrMagnitude < 0.01f) continue;

            Vector3 randomDir = new Vector3(random2D.x, 0f, random2D.y).normalized;
            Vector3 blendedDir = Vector3.Slerp(randomDir, forward, keepDirectionBias).normalized;
            float distance = Random.Range(minPatrolDistance, maxPatrolDistance);

            Vector3 candidate = transform.position + blendedDir * distance;
            candidate = ClampInsidePatrolRadius(candidate);
            candidate.y = fixedHeight;

            Vector3 delta = candidate - transform.position;
            delta.y = 0f;
            if (delta.magnitude < minPatrolDistance * 0.6f)
                continue;

            result = candidate;
            return true;
        }

        result = ClampHeight(transform.position);
        return false;
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 origin = transform.position;
        Vector3 target = player.position;
        Vector3 toPlayer = target - origin;
        float dist = toPlayer.magnitude;
        if (dist > visionRange) return false;

        return HasClearLine(origin, target);
    }

    public bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f) return true;

        return HasClearLine(origin, target);
    }

    private bool HasClearLine(Vector3 origin, Vector3 target)
    {
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f) return true;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            toTarget / distance,
            lineOfSightHits,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = lineOfSightHits[i].collider;
            if (hitCollider == null)
                continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
                continue;

            return false;
        }

        return true;
    }

    public void OnObserverAlert(Vector3 alertPosition, float searchDuration)
    {
        if (!isActiveAndEnabled) return;
        if (AetherEncounterDirector.IsDormant(this)) return;
        if (CurrentState == State.Engage && CanSeePlayer()) return;

        lastInvestigatePosition = alertPosition;
        lastInvestigateTime = Time.time;
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private void SetupAudio()
    {
        // 프리팹에 소리가 비어 있어(fileID 0) 드론이 무음으로 날아다녔다. 비어 있으면 AETHER 효과음으로 채운다.
        if (idleLoop == null) idleLoop = AetherAudio.Clip("drone_hum");
        if (alertOneShot == null) alertOneShot = AetherAudio.Clip("drone_alert");
        if (investigateOneShot == null) investigateOneShot = AetherAudio.Clip("drone_alert");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 35f;
        audioSource.dopplerLevel = 0.3f;

        alertSource = gameObject.AddComponent<AudioSource>();
        alertSource.playOnAwake = false;
        alertSource.spatialBlend = 1f;
        alertSource.rolloffMode = AudioRolloffMode.Logarithmic;
        alertSource.loop = true;
        alertSource.volume = 0f;

        StartIdleLoop();
    }

    private void StartIdleLoop()
    {
        if (idleLoop == null) return;

        audioSource.clip = idleLoop;
        audioSource.loop = true;
        audioSource.volume = idleVolume * AetherAudio.Sfx;
        if (!audioSource.isPlaying) audioSource.Play();
    }

    private void HandleAudioByState()
    {
        float targetVolume = (state == State.Engage ? combatVolume : idleVolume) * AetherAudio.Sfx;
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * volumeLerpSpeed);

        if (idleLoop != null && (!audioSource.isPlaying || audioSource.clip != idleLoop))
        {
            audioSource.clip = idleLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        bool shouldPlayAlertLoop = state == State.Engage;
        if (shouldPlayAlertLoop && alertLoop != null)
        {
            if (alertSource.clip != alertLoop) alertSource.clip = alertLoop;
            alertSource.pitch = alertLoopPitch;

            if (!alertSource.isPlaying)
                alertSource.Play();

            alertSource.volume = Mathf.Lerp(alertSource.volume, alertLoopVolume * AetherAudio.Sfx, Time.deltaTime * volumeLerpSpeed);
        }
        else if (alertSource.isPlaying)
        {
            alertSource.volume = Mathf.Lerp(alertSource.volume, 0f, Time.deltaTime * volumeLerpSpeed);
            if (alertSource.volume <= 0.01f)
            {
                alertSource.Stop();
                alertSource.clip = null;
            }
        }
    }

    private void PlayOneShot(AudioClip clip, float volumeScale)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale)); // 소스 음량에 이미 효과음 음량이 곱해져 있다
    }

    public float GetFlySpeed()
    {
        return flySpeed;
    }

    public float GetArriveDistance()
    {
        return arriveDistance;
    }

    public void FaceDirect3D(Vector3 lookPos)
    {
        Vector3 dir = lookPos - transform.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }   

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 currentPos = transform.position;

        // 인식 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(currentPos, visionRange);

        // 순찰 중심점 / 순찰 범위
        Vector3 center = patrolCenterOverride != null ? patrolCenterOverride.position : transform.position;
        center.y = transform.position.y;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(center, 0.2f);

        // 현재 목표점
        if (!float.IsInfinity(debugCurrentTarget.x))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(debugCurrentTarget, 0.4f);
            Gizmos.DrawLine(transform.position, debugCurrentTarget);
        }
    }
#endif
}
