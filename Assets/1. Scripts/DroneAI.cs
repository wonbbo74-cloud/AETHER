using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(DroneHearing))]
public class DroneAI : MonoBehaviour
{
    public enum State { Patrol, Investigate, Kamikaze } // 기존 Chase/Attack 대신 Kamikaze로 단순화

    [Header("Refs")]
    [Tooltip("없으면 Tag=Player 로 자동 탐색")]
    [SerializeField] private Transform player;
    private float nextPlayerLookup;

    [Tooltip("WP_01~ 을 자식으로 가진 부모(DroneWaypoints) 오브젝트를 넣으세요.")]
    [SerializeField] private Transform waypointParent;

    private Transform[] waypoints;

    [Header("Move")]
    [SerializeField] private float flySpeed = 8f;
    [SerializeField] private float rotateSpeed = 8f;
    [SerializeField] private float hoverHeight = 2.5f;
    [SerializeField] private float arriveDistance = 1.2f;

    [Header("Vision (거리 + 장애물 기반)")]
    [SerializeField] private float visionRange = 25f;
    [Tooltip("벽/건물/장애물 레이어만 넣기 (플레이어 레이어 넣으면 안됨!)")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("Hearing / Investigate")]
    [SerializeField] private float investigateMemorySeconds = 5f;
    [SerializeField] private float investigateStopSeconds = 1.0f;
    private float investigateArriveTime;

    // =======================
    // Kamikaze
    // =======================
    [Header("Kamikaze (돌진 폭발)")]
    [Tooltip("플레이어를 발견하면 이 거리 안으로 들어올 때 폭발")]
    [SerializeField] private float explodeRange = 2.2f;

    [Tooltip("폭발 데미지 (플레이어 HP에서 이만큼 감소)")]
    [SerializeField] private float explodeDamage = 30f;

    [Tooltip("폭발 이펙트 프리팹(ParticleSystem 프리팹)")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("폭발 사운드(1회)")]
    [SerializeField] private AudioClip explosionSfx;

    [Tooltip("폭발 이펙트가 씬에 남아있을 시간(초)")]
    [SerializeField] private float explosionVfxLifeTime = 2.0f;

    [Tooltip("폭발 시 플레이어 피해를 '반경'으로 줄지(근접 판정)")]
    [SerializeField] private bool useExplosionRadius = false;

    [Tooltip("useExplosionRadius=true일 때 폭발 반경")]
    [SerializeField] private float explosionRadius = 3.0f;

    [Tooltip("반경 데미지를 줄 때 플레이어 레이어(또는 Player만 포함된 레이어)")]
    [SerializeField] private LayerMask playerMask;

    private bool exploded = false;

    // =======================
    // Audio
    // =======================
    [Header("Audio")]
    [Tooltip("드론 기본 비행/프로펠러 루프")]
    [SerializeField] private AudioClip idleLoop;

    [Tooltip("플레이어 인식/돌진 시작 경고음(1회)")]
    [SerializeField] private AudioClip alertOneShot;

    [Tooltip("소리 듣고 조사 시작(1회, 선택)")]
    [SerializeField] private AudioClip investigateOneShot;

    [Tooltip("Idle일 때 볼륨")]
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.35f;

    [Tooltip("돌진 중 볼륨")]
    [SerializeField, Range(0f, 1f)] private float combatVolume = 0.75f;

    [Tooltip("볼륨 변화 부드럽게(초당)")]
    [SerializeField] private float volumeLerpSpeed = 6f;

    // =======================
    // Alert Loop (NEW)
    // =======================
    [Header("Alert Loop (NEW)")]
    [Tooltip("플레이어 발견 후 반복 경고음(짧은 경고 비프/사이렌 클립 추천)")]
    [SerializeField] private AudioClip alertLoop;

    [Tooltip("경고음 반복 볼륨")]
    [SerializeField, Range(0f, 1f)] private float alertLoopVolume = 1.0f;

    [Tooltip("경고음 반복 피치")]
    [SerializeField, Range(0.5f, 2f)] private float alertLoopPitch = 1.0f;

    private AudioSource audioSource;   // idleLoop용
    private AudioSource alertSource;   // alertLoop용(분리)
    private State prevState;

    [Header("Debug")]
    [SerializeField] private bool debugLogTarget = false;
    [SerializeField] private bool hideLegacyRedDotMarkerInGameplay = true;

    private DroneHearing hearing;
    private Rigidbody rb;
    private int wpIndex;
    private State state;
    private readonly RaycastHit[] visionHits = new RaycastHit[16];

    private Vector3 debugCurrentTarget = Vector3.positiveInfinity;

    private void Awake()
    {
        hearing = GetComponent<DroneHearing>();
        rb = GetComponent<Rigidbody>();

        // 물리 영향 제거(튕김/이탈 방지)
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // AudioSource 준비 (idleLoop)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;     // 3D 사운드
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;

        // ✅ 경고음 전용 AudioSource (반복 경고음)
        alertSource = gameObject.AddComponent<AudioSource>();
        alertSource.playOnAwake = false;
        alertSource.spatialBlend = 1f;
        alertSource.rolloffMode = AudioRolloffMode.Logarithmic;
        alertSource.loop = true;
        alertSource.volume = 0f;

        // 기본 루프 시작
        StartIdleLoop();

        // Player 자동 찾기
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        BuildWaypointsFromParent();
        prevState = state;
        HideLegacyRedDotMarker();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        BuildWaypointsFromParent();
    }

    private void BuildWaypointsFromParent()
    {
        if (waypointParent == null || waypointParent.childCount == 0)
        {
            waypoints = null;
            return;
        }

        int count = waypointParent.childCount;
        waypoints = new Transform[count];

        for (int i = 0; i < count; i++)
            waypoints[i] = waypointParent.GetChild(i);

        // 이름 기준 정렬로 순서 꼬임 방지 (WP_01, WP_02...)
        Array.Sort(waypoints, (a, b) => string.CompareOrdinal(a.name, b.name));

        if (wpIndex >= count) wpIndex = 0;
    }

    private void Update()
    {
        if (exploded) return;

        AetherPlayerCharacter.RefreshTarget(ref player, ref nextPlayerLookup);
        if (player == null)
        {
            Debug.LogWarning("[DroneAI] Player reference missing. Player 태그 확인!", this);
            return;
        }

        bool hasPatrol = (waypoints != null && waypoints.Length > 0);

        bool canSee = CanSeePlayer();

        // 상태 우선순위: Kamikaze > Investigate > Patrol
        if (canSee) state = State.Kamikaze;
        else if (hearing != null && hearing.HeardRecently(investigateMemorySeconds)) state = State.Investigate;
        else state = hasPatrol ? State.Patrol : State.Investigate;

        // 상태 변화에 따른 사운드/볼륨
        HandleAudioByState();

        switch (state)
        {
            case State.Patrol:
                Patrol();
                break;

            case State.Investigate:
                if (hearing != null && hearing.HeardRecently(investigateMemorySeconds))
                    Investigate();
                break;

            case State.Kamikaze:
                Kamikaze();
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

    // -------------------- Audio --------------------

    private void StartIdleLoop()
    {
        if (idleLoop == null) return;

        audioSource.clip = idleLoop;
        audioSource.loop = true;
        audioSource.volume = idleVolume;
        if (!audioSource.isPlaying) audioSource.Play();
    }

    private void HandleAudioByState()
    {
        // 1) 상태 전이 1회성 효과음
        if (state != prevState)
        {
            if (state == State.Investigate && prevState != State.Investigate)
            {
                PlayOneShot(investigateOneShot, 0.8f);
            }

            // Kamikaze 진입 시 1회 경고음(선택)
            if (state == State.Kamikaze && prevState != State.Kamikaze)
            {
                PlayOneShot(alertOneShot, 1f);
            }
        }

        // 2) 루프 볼륨(Idle vs Combat)
        float targetVol = (state == State.Kamikaze) ? combatVolume : idleVolume;
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVol, Time.deltaTime * volumeLerpSpeed);

        // 3) idle 루프 유지
        if (idleLoop != null && (!audioSource.isPlaying || audioSource.clip != idleLoop))
        {
            audioSource.clip = idleLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 4) ✅ 경고음 반복 루프(플레이어 발견 시 반복 + 더 크게)
        bool shouldAlertLoop = (state == State.Kamikaze);

        if (shouldAlertLoop && alertLoop != null)
        {
            if (alertSource.clip != alertLoop) alertSource.clip = alertLoop;
            alertSource.pitch = alertLoopPitch;

            if (!alertSource.isPlaying)
                alertSource.Play();

            alertSource.volume = Mathf.Lerp(alertSource.volume, alertLoopVolume, Time.deltaTime * volumeLerpSpeed);
        }
        else
        {
            if (alertSource.isPlaying)
            {
                alertSource.volume = Mathf.Lerp(alertSource.volume, 0f, Time.deltaTime * volumeLerpSpeed);
                if (alertSource.volume <= 0.01f)
                {
                    alertSource.Stop();
                    alertSource.clip = null;
                }
            }
        }
    }

    private void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    // -------------------- STATES --------------------

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform wp = waypoints[wpIndex];

        Vector3 target = wp.position + Vector3.up * hoverHeight;
        debugCurrentTarget = target;

        if (debugLogTarget)
            Debug.Log($"[DroneAI] Patrol -> {wp.name}, target={target}", this);

        MoveToTarget(target, lookAt: wp.position);

        if (Vector3.Distance(transform.position, target) <= arriveDistance)
            wpIndex = (wpIndex + 1) % waypoints.Length;
    }

    private void Investigate()
    {
        Vector3 investigatePos = hearing.LastHeardPosition;

        Vector3 target = investigatePos + Vector3.up * hoverHeight;
        debugCurrentTarget = target;

        MoveToTarget(target, lookAt: investigatePos);

        if (Vector3.Distance(transform.position, target) <= arriveDistance)
        {
            if (investigateArriveTime <= 0f) investigateArriveTime = Time.time;

            if (Time.time - investigateArriveTime >= investigateStopSeconds)
            {
                // 다음 프레임에서 소리 기억이 끝나면 자동으로 Patrol로 복귀
            }
        }
        else
        {
            investigateArriveTime = 0f;
        }
    }

    private void Kamikaze()
    {
        Vector3 target = player.position; // 직선 돌진
        debugCurrentTarget = target;

        MoveToTarget(target, lookAt: player.position);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= explodeRange)
        {
            Explode();
        }
    }

    // -------------------- EXPLOSION --------------------

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        // 1) 폭발 VFX
        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, Mathf.Max(0.1f, explosionVfxLifeTime));
        }

        // 2) 폭발 SFX
        if (explosionSfx != null)
        {
            AudioSource.PlayClipAtPoint(explosionSfx, transform.position, 1f);
        }

        // 2.5) 카메라 흔들림
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.15f, 0.22f, 28f);
        }

        // 3) 플레이어 데미지
        if (!useExplosionRadius)
        {
            var ps = player.GetComponent<PlayerStatus>();
            if (ps != null)
                ps.TakeDamage(AetherCombatBalance.ScaleIncomingEnemyDamage(explodeDamage));
        }
        else
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, playerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var ps = hits[i].GetComponentInParent<PlayerStatus>();
                if (ps != null)
                    ps.TakeDamage(AetherCombatBalance.ScaleIncomingEnemyDamage(explodeDamage));
            }
        }

        // 4) 드론 제거
        Destroy(gameObject);
    }

    // -------------------- MOVE / VISION --------------------

    private void MoveToTarget(Vector3 target, Vector3 lookAt)
    {
        Vector3 to = target - transform.position;
        if (to.sqrMagnitude < 0.0001f) return;

        transform.position += to.normalized * flySpeed * Time.deltaTime;
        FaceTo(lookAt);
    }

    private void FaceTo(Vector3 lookPos)
    {
        Vector3 dir = (lookPos - transform.position);
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotateSpeed * Time.deltaTime);
    }

    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        if (dist > visionRange) return false;

        int hitCount = Physics.RaycastNonAlloc(
            transform.position,
            toPlayer.normalized,
            visionHits,
            dist,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = visionHits[i].collider;
            if (hitCollider == null)
                continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
                continue;

            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explodeRange);

        if (useExplosionRadius)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        if (!float.IsInfinity(debugCurrentTarget.x))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(debugCurrentTarget, 0.4f);
            Gizmos.DrawLine(transform.position, debugCurrentTarget);
        }

        if (waypointParent != null && waypointParent.childCount > 1)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = waypointParent.GetChild(0).position;
            for (int i = 1; i < waypointParent.childCount; i++)
            {
                Vector3 cur = waypointParent.GetChild(i).position;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }
#endif
}
