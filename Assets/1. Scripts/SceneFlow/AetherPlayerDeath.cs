using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어가 쓰러졌을 때를 맡는다.
///
/// 지금까지 체력이 0 이 되어도 아무 일도 일어나지 않았다. PlayerStatus.IsDead 를 보는 곳이
/// 가스 지대 하나뿐이어서, 죽은 채로 계속 걸어다니고 계속 맞았다.
/// 가까운 안전한 자리에서 다시 일으켜 주되, 세 번까지만 봐준다.
/// </summary>
public class AetherPlayerDeath : MonoBehaviour
{
    /// <summary>부활 가능 횟수. 다 쓰면 작전 실패 화면으로 간다.</summary>
    public const int MaxRespawns = 3;

    private const string StartSceneName = "01.스타트씬";

    /// <summary>플레이 화면이 아닌 씬. 여기서는 사망 처리를 하지 않는다.</summary>
    private static readonly string[] NonGameplayScenes =
    {
        "01.스타트씬",
        "02.플레이어선택",
        "03.LoadingScene",
        "Loading",
        "MainMenu",
        "LobbyScene",
    };

    private static AetherPlayerDeath instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_PlayerDeath");
        instance = host.AddComponent<AetherPlayerDeath>();
        DontDestroyOnLoad(host);
    }

    private PlayerStatus player;
    private Rigidbody playerBody;

    private int respawnsUsed;
    private bool busy;

    private Vector3 safePoint;
    private Quaternion safeRotation;
    private bool hasSafePoint;
    private float nextSafePointCheck;

    private AetherDeathScreens screens;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 구역이 바뀌면 안전 지점과 남은 횟수를 새로 센다.
        // (앞 구역에서 세 번 죽었다고 다음 구역을 한 번도 못 봐서는 안 된다.)
        respawnsUsed = 0;
        hasSafePoint = false;
        recentSafePoints.Clear();
        player = null;
        playerBody = null;
        busy = false;

        if (screens != null)
            screens.Hide();

        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (busy || IsNonGameplayScene(SceneManager.GetActiveScene().name))
            return;

        if (player == null && !FindPlayer())
            return;

        if (player.currentHP <= 0f)
        {
            StartCoroutine(HandleDeath());
            return;
        }

        RememberSafePoint();
    }

    private bool FindPlayer()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");

        foreach (PlayerStatus candidate in FindObjectsByType<PlayerStatus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            // 씬에 체력 칸이 여러 개 떠 있다. 태그가 붙은 쪽을 먼저 믿는다.
            if (tagged != null && candidate.transform.root != tagged.transform.root)
                continue;

            player = candidate;
            break;
        }

        if (player == null)
            return false;

        playerBody = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>(true);

        if (!hasSafePoint)
            StoreSafePoint(player.transform.root.position, player.transform.root.rotation);

        return true;
    }

    /// <summary>
    /// 되살아날 자리를 계속 갱신한다.
    ///
    /// 총에 맞고 있는 자리에서 되살리면 또 바로 죽는다.
    /// 한동안 안 맞았고 땅을 밟고 있을 때의 자리만 기억한다.
    /// </summary>
    private void RememberSafePoint()
    {
        if (Time.time < nextSafePointCheck)
            return;

        nextSafePointCheck = Time.time + 1f;

        if (Time.time - player.LastDamageTime < 4f)
            return;

        // 되살릴 때 옮기는 것도 뿌리이므로, 기억해 두는 자리도 뿌리 기준이어야 한다.
        Transform root = player.transform.root;
        Vector3 position = root.position;

        if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            return;

        StoreSafePoint(position, root.rotation);
    }

    private void StoreSafePoint(Vector3 position, Quaternion rotation)
    {
        safePoint = position;
        safeRotation = rotation;
        hasSafePoint = true;

        // 지나온 안전 지점을 1초마다 하나씩, 최근 60개까지 남긴다(되살릴 자리 고르기용).
        if (recentSafePoints.Count == 0 || (recentSafePoints[recentSafePoints.Count - 1].position - position).sqrMagnitude > 1f)
        {
            recentSafePoints.Add(new Pose(position, rotation));
            if (recentSafePoints.Count > 60)
                recentSafePoints.RemoveAt(0);
        }
    }

    private readonly System.Collections.Generic.List<Pose> recentSafePoints = new System.Collections.Generic.List<Pose>();

    /// <summary>되살아난 뒤 피해를 받지 않는 시간(초). 둘러싼 적 사이에서 바로 다시 쓰러지지 않게 한다.</summary>
    private const float ReviveProtectionSeconds = 6f;   // 3초는 05 처럼 적이 많은 곳에서 자리 잡기 전에 끝났다

    /// <summary>이 거리 안에 적이 있는 자리에는 되살리지 않는다.</summary>
    private const float ReviveEnemyClearance = 16f;

    /// <summary>
    /// 되살릴 자리를 고른다.
    /// 마지막 안전 지점(4초 동안 안 맞은 곳)에서만 되살리면, 그 뒤 적이 몰려와 둘러싼 자리로 돌아가
    /// 쓰러지자마자 또 쓰러졌다(05 모듈 줍는 곳에서 Shiri 가 2분 안에 기회를 다 씀).
    /// 최근 안전 지점 가운데 적이 가까이 없는 가장 최근 자리를 쓴다.
    /// 그런 자리가 없으면(05 시작 지점은 40m 안에 로봇 15기·드론·터렛) 주변 걸을 수 있는 땅을 훑어
    /// 적과 가장 먼 자리를 고른다. 로봇만 보던 것을 드론·터렛까지 본다.
    /// </summary>
    private Pose PickRevivePoint()
    {
        Pose fallback = new Pose(safePoint, safeRotation);
        System.Collections.Generic.List<Vector3> enemies = CollectEnemyPositions();
        if (enemies.Count == 0)
            return fallback;

        Pose best = fallback;
        float bestClearance = NearestEnemy(safePoint, enemies);
        if (bestClearance >= ReviveEnemyClearance)
            return fallback;

        for (int i = recentSafePoints.Count - 1; i >= 0; i--)
        {
            float clearance = NearestEnemy(recentSafePoints[i].position, enemies);
            if (clearance >= ReviveEnemyClearance)
                return recentSafePoints[i];
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                best = recentSafePoints[i];
            }
        }

        // 주변 고리(10~45m)를 훑는다. 길이 이어진(같은 내비메시) 자리만 쓴다.
        Vector3 origin = best.position;
        if (UnityEngine.AI.NavMesh.SamplePosition(origin, out UnityEngine.AI.NavMeshHit from, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            for (float radius = 10f; radius <= 45f; radius += 5f)
            {
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    Vector3 probe = origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                        continue;
                    float clearance = NearestEnemy(hit.position, enemies);
                    if (clearance <= bestClearance + 2f)
                        continue;
                    if (!UnityEngine.AI.NavMesh.CalculatePath(from.position, hit.position, UnityEngine.AI.NavMesh.AllAreas, path)
                        || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        continue;
                    bestClearance = clearance;
                    best = new Pose(hit.position + Vector3.up * 0.1f, best.rotation);
                }
                if (bestClearance >= ReviveEnemyClearance)
                    break;
            }
        }

        return best;
    }

    private static System.Collections.Generic.List<Vector3> CollectEnemyPositions()
    {
        var list = new System.Collections.Generic.List<Vector3>();
        foreach (Robots r in FindObjectsByType<Robots>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r != null && r.isActiveAndEnabled) list.Add(r.transform.position);
        foreach (DroneHealth d in FindObjectsByType<DroneHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (d != null && d.isActiveAndEnabled) { Vector3 p = d.transform.position; p.y = 0f; list.Add(p); }
        foreach (EnemyTurretController t in FindObjectsByType<EnemyTurretController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t != null && t.isActiveAndEnabled) list.Add(t.transform.position);
        return list;
    }

    private static float NearestEnemy(Vector3 position, System.Collections.Generic.List<Vector3> enemies)
    {
        float nearest = float.MaxValue;
        Vector3 flat = new Vector3(position.x, 0f, position.z);
        foreach (Vector3 e in enemies)
            nearest = Mathf.Min(nearest, Vector3.Distance(flat, new Vector3(e.x, 0f, e.z)));
        return nearest;
    }


    /// <summary>쓰러진 뒤 복귀까지 기다리는 시간(초). 화면의 '구조 대기' 막대가 이 시간만큼 줄어든다.</summary>
    private const float RescueWaitSeconds = 8f;

    private IEnumerator HandleDeath()
    {
        busy = true;

        LockPlayer(true);
        EnsureScreens();
        AetherAudio.Play2D("player_death", 0.85f, 1f, 0f); // 쓰러지는 쿵 + 귀울림

        bool canRespawn = respawnsUsed < MaxRespawns;

        if (canRespawn)
        {
            respawnsUsed++;
            int left = MaxRespawns - respawnsUsed;

            screens.ShowDowned(
                DescribeMate(),
                left > 0
                    ? $"대기 시간이 끝나면 가까운 지점에서 복귀합니다. 남은 기회 {left}회"
                    : "대기 시간이 끝나면 가까운 지점에서 복귀합니다. 마지막 기회입니다",
                canSpectate: false);

            float left_ = RescueWaitSeconds;
            while (left_ > 0f)
            {
                screens.UpdateDownedTimer(left_, RescueWaitSeconds);
                yield return null;
                left_ -= Time.unscaledDeltaTime;
            }

            screens.UpdateDownedTimer(0f, RescueWaitSeconds);

            Revive();
            AetherAudio.Play2D("respawn", 0.6f, 1f, 0f);
            screens.Hide();
            LockPlayer(false);
            busy = false;
            yield break;
        }

        screens.ShowEliminated();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // 버튼을 누를 때까지 여기서 멈춘다. busy 는 그대로 둔다.
    }

    /// <summary>
    /// 동료 칸에 적을 내용.
    /// 지금 빌드는 한 명만 조작하므로 살아 있는 두 번째 플레이어가 없다. 없는 것을 있는 척하지 않는다.
    /// </summary>
    private string DescribeMate()
    {
        foreach (PlayerStatus other in FindObjectsByType<PlayerStatus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (other == null || other == player || !other.gameObject.activeInHierarchy)
                continue;

            string state = other.currentHP <= 0f ? "전투 불능" : $"교전 중  HP {other.currentHP:F0} / {other.maxHP:F0}";
            return $"   {other.transform.root.name}        {state}";
        }

        return "   단독 작전 — 구조해 줄 동료가 없습니다";
    }

    private void EnsureScreens()
    {
        if (screens != null)
            return;

        screens = gameObject.AddComponent<AetherDeathScreens>();
        screens.LeaveMatch += BackToTitle;
        screens.Retry += Retry;
        screens.Lobby += BackToTitle;
    }

    private void Revive()
    {
        player.SetHP(player.maxHP);
        player.InvulnerableUntil = Time.time + ReviveProtectionSeconds;

        if (!hasSafePoint)
            return;

        Pose revive = PickRevivePoint();
        safePoint = revive.position;
        safeRotation = revive.rotation;

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }

        Transform root = player.transform.root;
        root.SetPositionAndRotation(safePoint, safeRotation);

        if (playerBody != null)
        {
            playerBody.position = safePoint;
            playerBody.rotation = safeRotation;
        }

        Physics.SyncTransforms();
    }

    /// <summary>쓰러진 동안 조작을 막는다. 엘리베이터에서 쓰는 방식과 같다.</summary>
    private void LockPlayer(bool locked)
    {
        if (player == null)
            return;

        Transform root = player.transform.root;

        foreach (Move_Hankyul move in root.GetComponentsInChildren<Move_Hankyul>(true))
        {
            if (move != null)
                move.enabled = !locked;
        }

        foreach (Hankyul_Fire fire in root.GetComponentsInChildren<Hankyul_Fire>(true))
        {
            if (fire != null)
                fire.enabled = !locked;
        }

        foreach (Move_Yunseo move in root.GetComponentsInChildren<Move_Yunseo>(true))
        {
            if (move == null)
                continue;

            move.SetMoveEnabled(!locked);
            move.SetLookEnabled(!locked);
        }

        // 윤서는 쓰러진 채로도 주먹을 휘둘렀다. 한결의 총처럼 같이 막는다.
        foreach (Yunseo_Battle battle in root.GetComponentsInChildren<Yunseo_Battle>(true))
        {
            if (battle != null)
                battle.enabled = !locked;
        }

        // 쓰러진 채로 숫자 키로 아이템을 쓰거나 실드·브레이커를 켤 수 있었다. 퀵슬롯은 HUD 에 따로 있어 장면 전체에서 찾는다.
        foreach (QuickSlotManager q in FindObjectsByType<QuickSlotManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (q != null) q.enabled = !locked;
        foreach (HangyeolPartAbilityController a in root.GetComponentsInChildren<HangyeolPartAbilityController>(true))
            if (a != null) a.enabled = !locked;
        foreach (YunseoPartAbilityController a in root.GetComponentsInChildren<YunseoPartAbilityController>(true))
            if (a != null) a.enabled = !locked;

        // 시점 조작도 같이 막는다. 안 막으면 작전 실패 화면에서 버튼을 누르는 동안
        // 마우스를 움직일 때마다 뒤에서 카메라가 같이 돌아간다.
        foreach (AetherCameraController camera in FindObjectsByType<AetherCameraController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (camera != null)
                camera.enabled = !locked;
        }

        if (playerBody != null && locked)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }
    }

    private static bool IsNonGameplayScene(string sceneName)
    {
        for (int i = 0; i < NonGameplayScenes.Length; i++)
        {
            if (string.Equals(sceneName, NonGameplayScenes[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    // ───────────────────────── 화면 전환 ─────────────────────────

    private void Retry()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (screens != null)
            screens.Hide();

        // 쓰러진 체력(0)이 넘어가 체력 1/4·갈증 0 으로 다시 시작해, 다시 하자마자 또 쓰러졌다.
        // 다시 시도는 체력·갈증을 가득 채워 시작한다(가방·파츠·레벨은 그대로).
        if (player != null)
        {
            player.SetHP(player.maxHP);
            player.SetThirst(player.maxThirst);
            AetherRunState.Capture();
        }

        AetherSceneFade.LoadScene(SceneManager.GetActiveScene().buildIndex); // 어두워졌다 다시 시작
    }

    private void BackToTitle()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (screens != null)
            screens.Hide();

        AetherSceneFade.LoadScene(StartSceneName);
    }
}
