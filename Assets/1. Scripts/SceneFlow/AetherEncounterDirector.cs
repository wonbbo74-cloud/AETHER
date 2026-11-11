using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 05 폐허된 도시의 교전 흐름.
///
/// 시작 지점 10~40m 안에 로봇 14·드론 7·터렛 4 가 몰려 있어서, 들어서자마자 모두가 한꺼번에 덤볐다.
/// 이제 시작 지점 근처(45m) 적들은 처음엔 '대기'(감지·사격 안 함, 로봇은 순찰만) 상태로 두고,
/// 웨이브로 하나씩 깨운다.
///   - 가까운 일반 로봇부터 → 드론·중장비·터렛은 뒤 웨이브. 웨이브 크기 2 → 3 → 4 → 5 …
///   - 앞 웨이브가 거의 정리되면(깨어 있는 적 1기 이하) 다음 웨이브. 너무 오래 끌면 35초 뒤에도 온다.
///   - 웨이브가 진행될수록 적 공격이 조금씩 세진다(받는 피해 ×0.8 → 최대 ×1.2).
///   - 대기 중인 적도 플레이어가 쏘거나 7m 안으로 다가오면 그 자리에서 깨어난다(몰래 지나가기는 허용).
///   - 정찰 드론의 경보는 대기 중인 적을 깨우지 않는다(예전엔 30m 안 모두를 불러 모았다).
/// 시작 지점에서 먼 적(공장 가는 길의 로봇)은 손대지 않는다 — 걸어가며 차례로 만나게 된다.
/// </summary>
public class AetherEncounterDirector : MonoBehaviour
{
    private const float GateRadius = 45f;
    private const float FirstWaveDelay = 4f;
    private const float WaveTimeout = 35f;
    private const float WakeDistance = 7f;
    private static readonly int[] WaveBudget = { 2, 3, 4, 5, 6, 8, 99 };

    private static readonly HashSet<GameObject> dormant = new HashSet<GameObject>();

    // 로봇 회로 퍼즐(Puzzle2)에 다녀오면 05 장면이 새로 열려, 쓰러뜨린 적이 모두 되살아나고 웨이브도 처음부터 다시 왔다.
    // 이번 판에 쓰러뜨린 적(처음 자리로 구분)과 이미 지나간 웨이브 수를 기억해 둔다.
    private static readonly HashSet<string> killedEnemies = new HashSet<string>();
    private static int releasedWaves;

    public static void Export(AetherSaveData data)
    {
        data.killed05 = new List<string>(killedEnemies);
        data.wavesReleased05 = releasedWaves;
    }

    public static void Import(AetherSaveData data)
    {
        killedEnemies.Clear();
        foreach (string id in data.killed05) killedEnemies.Add(id);
        releasedWaves = data.wavesReleased05;
    }
    private static float damageScale = 1f;
    private static AetherEncounterDirector instance;

    private readonly List<List<GameObject>> waves = new List<List<GameObject>>();
    private readonly List<GameObject> awake = new List<GameObject>();
    private int nextWave;
    private float lastWaveTime;
    private float nextHuntPing;

    /// <summary>대기 중(아직 깨지 않은)인 적인지. 적 AI 가 감지·사격 전에 확인한다.</summary>
    public static bool IsDormant(Component enemy)
    {
        return dormant.Count > 0 && enemy != null && dormant.Contains(enemy.gameObject);
    }

    /// <summary>웨이브에 따라 오르는 적 피해 배율(05 에서만, 그 밖에선 1).</summary>
    public static float DamageScale => instance != null ? damageScale : 1f;

    /// <summary>플레이어에게 맞은 적은 그 자리에서 깨어난다.</summary>
    public static void WakeByDamage(Component hit)
    {
        if (instance == null || hit == null || dormant.Count == 0)
            return;

        GameObject ai = FindAiObject(hit);
        if (ai != null && dormant.Contains(ai))
            instance.Wake(ai, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        dormant.Clear();
        damageScale = 1f;

        // 새로 시작하면 쓰러뜨린 적 기록을 지운다.
        if (scene.name.StartsWith("01.") || scene.name.StartsWith("02."))
        {
            killedEnemies.Clear();
            releasedWaves = 0;
        }

        if (!scene.name.StartsWith("05.") || FindFirstObjectByType<AetherEncounterDirector>() != null)
            return;

        GameObject go = new GameObject("AETHER_EncounterDirector");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherEncounterDirector>();
    }

    private void OnEnable() => instance = this;

    private void Awake()
    {
        // 장면이 막 열린 순간(적이 아직 처음 자리에 있을 때) 적마다 이름표를 붙인다.
        foreach (RobotHealth robot in FindObjectsByType<RobotHealth>(FindObjectsSortMode.None))
            Track(AiRoot(robot), () => robot.Died += _ => killedEnemies.Add(EnemyId(AiRoot(robot), robot.transform.position)));

        foreach (DroneHealth drone in FindObjectsByType<DroneHealth>(FindObjectsSortMode.None))
            Track(AiRoot(drone), () => drone.Died += _ => killedEnemies.Add(EnemyId(AiRoot(drone), drone.transform.position)));
    }

    private static GameObject AiRoot(Component health)
    {
        GameObject ai = FindAiObject(health);
        return ai != null ? ai : health.gameObject;
    }

    private readonly Dictionary<GameObject, string> enemyIds = new Dictionary<GameObject, string>();

    private void Track(GameObject enemy, System.Action subscribe)
    {
        string id = EnemyId(enemy, enemy.transform.position);
        enemyIds[enemy] = id;

        // 이번 판에 이미 쓰러뜨린 적이면 다시 나오지 않게 한다.
        if (killedEnemies.Contains(id))
        {
            enemy.SetActive(false);
            return;
        }

        subscribe();
    }

    private string EnemyId(GameObject enemy, Vector3 fallbackPosition)
    {
        // 죽을 땐 처음 자리에서 움직여 있으므로, 장면이 열릴 때 매긴 이름표를 쓴다.
        if (enemyIds.TryGetValue(enemy, out string id))
            return id;

        Vector3 p = fallbackPosition;
        return $"{enemy.name}@{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.z)}";
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            dormant.Clear();
            damageScale = 1f;
        }
    }

    private IEnumerator Start()
    {
        Transform player = null;
        for (int i = 0; i < 120 && player == null; i++)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            if (player == null) yield return null;
        }

        if (player == null)
            yield break;

        BuildWaves(player.position);
        foreach (List<GameObject> wave in waves)
            foreach (GameObject enemy in wave)
                dormant.Add(enemy);
        foreach (GameObject turret in turrets)
            dormant.Add(turret);

        damageScale = 0.8f;
        yield return new WaitForSeconds(FirstWaveDelay);
        bool firstVisit = releasedWaves == 0;
        ReleaseNextWave(false);

        // 04 벽 그림엔 이동·줍기만 있고 싸우는 법이 없다. 첫 교전 직전에 한 번 알려 준다
        // (Meka 는 1 키로 총을 꺼내야 쏠 수 있는데, 모른 채 맞기만 했다).
        if (firstVisit)
            AetherObjectiveToast.Show(AetherCharacterChoice.Selected == AetherCharacter.Yunseo
            ? "적 접근! 좌클릭 펀치\n우클릭 길게 → 충전 펀치"
            : "적 접근! [1] 총 꺼내기\n좌클릭 사격 · [R] 재장전", 9f);

        WaitForSeconds tick = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return tick;
            Tick();
        }
    }

    // ------------------------------------------------------------------ 웨이브 만들기

    private readonly List<GameObject> turrets = new List<GameObject>();

    private void BuildWaves(Vector3 start)
    {
        List<(GameObject go, float score)> candidates = new List<(GameObject, float)>();

        void Consider(Component c, float extra)
        {
            if (c == null || !c.gameObject.activeInHierarchy) return;
            float d = Vector3.Distance(Flat(c.transform.position), Flat(start));
            if (d > GateRadius) return;

            string n = c.name.ToLowerInvariant();
            if (n.Contains("heavy") || n.Contains("assault") || n.Contains("variety"))
                extra += 20f;   // 중장비·특수 개체는 뒤로
            candidates.Add((c.gameObject, d + extra));
        }

        foreach (Robots r in FindObjectsByType<Robots>(FindObjectsSortMode.None))
        {
            RobotHealth h = r.GetComponent<RobotHealth>();
            if (h == null || !h.IsDead) Consider(r, 0f);
        }
        foreach (Drones d in FindObjectsByType<Drones>(FindObjectsSortMode.None))
            Consider(d, 18f);
        // 터렛은 웨이브에 넣지 않는다. 마지막 웨이브에 들어가 있어 그 웨이브가 풀릴 때까지 잠들어 있었고,
        // 움직이지 못하니 그 앞을 지나가도 한 발도 쏘지 않았다(사용자 지적). 첫 웨이브와 함께 모두 깨운다.
        foreach (EnemyTurretController t in FindObjectsByType<EnemyTurretController>(FindObjectsSortMode.None))
            if (t != null && t.gameObject.activeInHierarchy)
                turrets.Add(t.gameObject);

        candidates.Sort((a, b) => a.score.CompareTo(b.score));

        int index = 0;
        for (int w = 0; index < candidates.Count; w++)
        {
            int budget = WaveBudget[Mathf.Min(w, WaveBudget.Length - 1)];
            List<GameObject> wave = new List<GameObject>();
            for (int k = 0; k < budget && index < candidates.Count; k++)
                wave.Add(candidates[index++].go);
            waves.Add(wave);
        }
    }

    // ------------------------------------------------------------------ 진행

    private void Tick()
    {
        awake.RemoveAll(e => !IsAlive(e));

        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player != null && dormant.Count > 0)
        {
            // 가까이 다가오면 그 적만 깨어난다.
            List<GameObject> near = null;
            foreach (GameObject e in dormant)
            {
                if (e == null) continue;
                if (Vector3.Distance(e.transform.position, player.position) <= WakeDistance)
                    (near ??= new List<GameObject>()).Add(e);
            }
            if (near != null)
                foreach (GameObject e in near) Wake(e, true);
        }

        // 깨어난 적이 플레이어를 놓치고 순찰로 돌아가지 않게, 4초마다 플레이어가 있는 쪽을 다시 알려 준다
        // (플레이어가 보이면 알아서 교전한다). 처음 시험 때 첫 웨이브가 다른 길로 새서 40초 동안 아무 일도 없었다.
        if (player != null && Time.time >= nextHuntPing)
        {
            nextHuntPing = Time.time + 4f;
            foreach (GameObject e in awake)
            {
                if (e == null) continue;
                Robots robot = e.GetComponent<Robots>();
                if (robot != null) robot.OnObserverAlert(player.position, 8f);
                Drones drone = e.GetComponent<Drones>();
                if (drone != null) drone.OnObserverAlert(player.position, 8f);
            }
        }

        if (nextWave >= waves.Count)
            return;

        bool cleared = awake.Count <= 1;
        bool overdue = Time.time - lastWaveTime >= WaveTimeout && awake.Count <= 3;
        if (cleared || overdue)
            ReleaseNextWave(true);
    }

    private void ReleaseNextWave(bool announce)
    {
        if (nextWave >= waves.Count)
            return;

        List<GameObject> wave = waves[nextWave];
        nextWave++;
        releasedWaves++;
        lastWaveTime = Time.time;
        damageScale = Mathf.Min(1.2f, 0.8f + 0.08f * (nextWave - 1));

        foreach (GameObject e in wave)
            if (dormant.Contains(e)) Wake(e, true);

        // 첫 교전이 시작되면 터렛도 모두 깨어난다(사거리 안에 보이면 쏜다).
        // (웨이브 진행 판정의 '깨어 있는 적' 수에는 넣지 않는다 — 터렛이 남아 있다고 다음 웨이브가 막히면 안 된다.)
        foreach (GameObject t in turrets)
            dormant.Remove(t);

        if (announce)
            AetherObjectiveToast.Show(nextWave >= waves.Count
                ? "마지막 적 무리가 다가온다"
                : $"적 증원 접근 중  ({nextWave} / {waves.Count})", 4f);
    }

    private void Wake(GameObject enemy, bool hunt)
    {
        if (enemy == null || !dormant.Remove(enemy))
            return;

        if (IsAlive(enemy))
            awake.Add(enemy);

        if (!hunt)
            return;

        // 깨어난 적은 플레이어가 있던 쪽을 수색하며 다가온다(보이면 바로 교전).
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null)
            return;

        Robots robot = enemy.GetComponent<Robots>();
        if (robot != null) robot.OnObserverAlert(player.position, 12f);

        Drones drone = enemy.GetComponent<Drones>();
        if (drone != null) drone.OnObserverAlert(player.position, 10f);
    }

    private static bool IsAlive(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy)
            return false;

        RobotHealth rh = enemy.GetComponentInChildren<RobotHealth>();
        if (rh != null) return !rh.IsDead;

        DroneHealth dh = enemy.GetComponentInChildren<DroneHealth>();
        if (dh != null) return !dh.IsDead;

        return true;
    }

    private static GameObject FindAiObject(Component hit)
    {
        Robots r = hit.GetComponentInParent<Robots>();
        if (r != null) return r.gameObject;
        Drones d = hit.GetComponentInParent<Drones>();
        if (d != null) return d.gameObject;
        EnemyTurretController t = hit.GetComponentInParent<EnemyTurretController>();
        if (t == null) t = hit.GetComponentInChildren<EnemyTurretController>();
        return t != null ? t.gameObject : null;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
