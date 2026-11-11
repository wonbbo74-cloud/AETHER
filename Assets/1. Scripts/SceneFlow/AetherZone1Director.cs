using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 1구역(08.1구역_2)의 한 판을 처음부터 끝까지 굴러가게 만든다.
///
/// 이 씬은 바이너리로 저장돼 있어 파일을 직접 고칠 수 없다. 그래서 씬은 건드리지 않고
/// 실행할 때 필요한 것만 세운다. 되돌리려면 이 스크립트만 지우면 된다.
///
/// === 씬 실측 (AETHER/진단/1구역 경로 측정) ===
///   엘리베이터 (1322.6, 121.8, 1414) — 공장 옥상 Factory2Roof01 위
///   옥상은 전투구역 방향으로 48m까지만 이어지고, 그 끝 (1277.5, 121.8, 1394.7)에서 끊긴다
///   옥상 아래 지면은 y 84m — 낙차 37.7m
///   연못 물은 y=103.5의 229 x 257m짜리 거대한 수면. 골짜기 바닥(84~103m)이 전부 잠겨 있다
///   배수 콘솔 (1199.8, 109.5, 1299) — 수면 위로 솟은 계량기함 위
///   적 247체는 z 972~1297, 즉 연못 남쪽 마른 땅에 깔려 있다
///
/// === 그래서 한 판의 흐름은 이렇게 된다 ===
///   1. 엘리베이터에서 내리면 공장 옥상이다. 앞은 절벽, 아래는 물에 잠긴 골짜기.
///   2. 옥상 끝에서 배수 콘솔까지 123m를 슈퍼점프 발판으로 건넌다.
///      발판은 높낮이가 오르내려 충전량을 가늠해야 하고, 두 개는 밟으면 무너진다.
///      떨어지면 저 아래 물이므로, 마지막으로 밟은 발판에서 다시 시작한다.
///   3. 콘솔을 작동시키면 물이 빠지고 골짜기 바닥과 적들의 구역이 열린다.
///   4. 적 웨이브 3개와 방패 모듈 4개가 기다린다. 식량·물은 길 내내 놓여 있다.
///   5. 모듈 4개를 다 모으면 타고 온 엘리베이터로 파츠 제작실에 돌아갈 수 있다.
/// </summary>
public class AetherZone1Director : MonoBehaviour
{
    private const string ScenePrefix = "08.";
    private const string EncounterRootName = "AETHER_Combat_Encounter_08";
    private const string GeneratedRootName = "AETHER_Zone1_Runtime";

    // ------------------------------------------------------------- 실측 기준점

    private static readonly Vector3 ElevatorPos = new Vector3(1322.57f, 121.76f, 1413.97f);
    private static readonly Vector3 RoofEdge = new Vector3(1277.5f, 121.8f, 1394.7f);
    private static readonly Vector3 DrainConsolePos = new Vector3(1199.80f, 109.50f, 1298.94f);

    /// <summary>연못 수면 높이. 이보다 낮은 땅은 배수 전까지 물에 잠겨 있다.</summary>
    private const float WaterSurfaceY = 103.5f;

    /// <summary>
    /// 이 씬의 배치물은 원본 프리팹보다 약 2배로 놓여 있다(문 4.63m, 단독주택 15.8m).
    /// 그렇다고 플레이어를 키우면 씬을 넘나들 때마다 몸집이 달라져서,
    /// 타고 온 엘리베이터 칸이 눈앞에서 커졌다 작아졌다 한다.
    ///
    /// 그래서 <b>플레이어는 어느 씬에서나 그대로 둔다.</b> 대신 씬 쪽에서 튀는 것을 잡는다.
    ///   · 엘리베이터 배율 → ElevatorInteractionController가 모든 씬을 x1.5로 맞춘다
    ///   · 점프 코스·아이템 → 이 배치 크기를 전제로 설계한다
    /// </summary>
    private const float SceneBuildScale = 2f;

    // ------------------------------------------------------------- 점프 코스

    /// <summary>
    /// 슈퍼점프 도달 높이(m). 플레이어는 키 1.55m, 달리기 6m/s이고 공중에서도 그대로 움직인다.
    /// 그래서 건널 수 있는 거리는 (도달높이로 정해지는 체공시간) x 6m/s 다.
    ///   2m  → 체공 1.3초 → 약 8m 건넘
    ///   14m → 체공 3.4초 → 약 20m 건넘
    /// 아래 코스는 간격 14m·오르막 4m 안쪽이라 충전을 가늠하면 넘어가고, 대충 누르면 떨어진다.
    /// </summary>
    private const float SuperJumpMinApex = 2f;
    private const float SuperJumpMaxApex = 14f;
    private const float SuperJumpChargeTime = 1.2f;

    /// <summary>배수 콘솔에 손이 닿는 거리. 기본 3.2m면 이 씬의 큰 계량기함에는 짧다.</summary>
    private const float ConsoleReachDistance = 4.5f;

    // ------------------------------------------------------------- 전투

    private struct Wave
    {
        public string name;
        public Vector3 anchor;
        public float triggerRadius;
        public int robots;
        public int drones;
        public int suiciders;

        public Wave(string name, Vector3 anchor, float triggerRadius, int robots, int drones, int suiciders)
        {
            this.name = name;
            this.anchor = anchor;
            this.triggerRadius = triggerRadius;
            this.robots = robots;
            this.drones = drones;
            this.suiciders = suiciders;
        }
    }

    /// <summary>물이 빠진 뒤 나아가는 길을 따라 배치한다. 코스를 건너는 동안에는 시작되지 않는다.</summary>
    private static readonly Wave[] Waves =
    {
        new Wave("콘솔", new Vector3(1199.8f, 105f, 1290f), 45f, 4, 2, 0),
        new Wave("골짜기", new Vector3(1165f, 102f, 1215f), 60f, 5, 3, 1),
        new Wave("본대", new Vector3(1130f, 105f, 1130f), 70f, 7, 3, 2),
    };

    // ------------------------------------------------------------- 아이템

    /// <summary>1구역에서 모으는 방패 재료. 네 개를 다 모으면 구역이 끝난다.</summary>
    private static readonly string[] ShieldModules =
    {
        "shield_core_module",
        "power_cell_module",
        "stabilizer_module",
        "frame_core_module",
    };

    /// <summary>모듈을 놓을 자리. 배수 후 걸어갈 수 있는 곳이라야 한다.</summary>
    private static readonly Vector3[] ModuleAnchors =
    {
        new Vector3(1214f, 106f, 1286f),   // 콘솔 바로 옆 — 첫 번째는 쉽게 눈에 띄게
        new Vector3(1160f, 103f, 1232f),   // 골짜기
        new Vector3(1108f, 104f, 1168f),   // 적 구역 서쪽
        new Vector3(1186f, 104f, 1124f),   // 적 구역 남쪽
    };

    /// <summary>
    /// 식량·물·자원을 뿌릴 거점.
    ///
    /// 적은 x 927~1360, z 972~1297에 깔려 있다(실측). 길목에만 두면 전투 구역이 텅 비므로,
    /// 적 구역 전체를 격자처럼 덮고 출발 지점인 옥상에도 몇 개 둔다.
    /// </summary>
    private static readonly Vector3[] SupplyAnchors =
    {
        // 출발 지점(옥상)
        ElevatorPos,
        RoofEdge,

        // 콘솔 착지 주변
        new Vector3(1205f, 106f, 1292f),
        new Vector3(1240f, 104f, 1280f),

        // 적 구역 — 북쪽 줄
        new Vector3(1300f, 105f, 1250f),
        new Vector3(1220f, 103f, 1240f),
        new Vector3(1140f, 103f, 1250f),
        new Vector3(1050f, 105f, 1240f),
        new Vector3( 975f, 108f, 1230f),

        // 적 구역 — 가운데 줄
        new Vector3(1320f, 106f, 1150f),
        new Vector3(1240f, 104f, 1140f),
        new Vector3(1150f, 104f, 1150f),
        new Vector3(1060f, 106f, 1140f),
        new Vector3( 980f, 109f, 1150f),

        // 적 구역 — 남쪽 줄
        new Vector3(1300f, 107f, 1040f),
        new Vector3(1210f, 106f, 1030f),
        new Vector3(1120f, 107f, 1040f),
        new Vector3(1030f, 109f, 1030f),
        new Vector3( 960f, 111f, 1000f),
    };

    /// <summary>보급 프리팩이 들어 있는 Resources 경로.</summary>
    private const string SupplyPrefabRoot = "AETHERParts/ModulePrefabs/";

    /// <summary>
    /// (프리팩 이름, 개수) — 거점이 20곳이므로 각각 20개면
    /// 어느 방향으로 가든 간간이 눈에 띄다.
    /// 식량 깡통과 물병은 04.Tutorial1에서 쓰는 그 프리팩 그대로다.
    /// </summary>
    private static readonly (string id, int count)[] Supplies =
    {
        ("Food", 20),
        ("Water", 20),
    };

    // ------------------------------------------------------------- 상태

    private static bool subscribed;
    private static bool builtThisScene;

    private readonly List<Wave> pendingWaves = new List<Wave>();
    private readonly List<List<GameObject>> waveMembers = new List<List<GameObject>>();
    /// <summary>코스에서 발을 디딜 수 있는 자리. 씬 구조물 위와 끼워 넣은 판이 섞여 있다.</summary>
    private readonly List<AetherCoursePoint> coursePoints = new List<AetherCoursePoint>();

    /// <summary>씨에서 찾은 복제원본. 같은 이름을 여러 번 찾지 않도록 기억해 둔다.</summary>
    private static readonly Dictionary<string, GameObject> sceneProps = new Dictionary<string, GameObject>();

    private Transform player;
    private float checkTimer;
    private bool zoneCleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        EnsureForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        builtThisScene = false;
        sceneProps.Clear();
        pondBoundsResolved = false;
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith(ScenePrefix, System.StringComparison.Ordinal))
            return;

        if (builtThisScene)
            return;

        // 플레이어를 만드는 AetherZoneBootstrap보다 먼저 시작 지점을 세워 둔다.
        EnsureSpawnMarker();

        GameObject host = new GameObject("AetherZone1Director");
        host.AddComponent<AetherZone1Director>();
    }

    /// <summary>엘리베이터 앞(옥상)을 시작 지점으로 표시한다.</summary>
    private static Transform EnsureSpawnMarker()
    {
        GameObject existing = GameObject.Find("PlayerSpawn");
        if (existing != null)
            return existing.transform;

        // 엘리베이터에서 옥상 끝 쪽으로 조금 나온 자리
        Vector3 toEdge = (RoofEdge - ElevatorPos);
        toEdge.y = 0f;
        Vector3 spawn = ElevatorPos + toEdge.normalized * 5f;

        if (Physics.Raycast(spawn + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            spawn = hit.point;

        GameObject marker = new GameObject("PlayerSpawn");
        marker.transform.position = spawn + Vector3.up * 1f;
        // 옥상 끝(= 건너야 할 방향)을 바라보게 한다.
        marker.transform.rotation = Quaternion.LookRotation(toEdge.normalized, Vector3.up);
        return marker.transform;
    }

    private void Start()
    {
        if (builtThisScene)
        {
            Destroy(gameObject);
            return;
        }

        builtThisScene = true;

        Transform root = GetGeneratedRoot();

        StartCoroutine(SetUpPlayerWhenReady());
        BuildJumpCourse(root);
        PlaceShieldModules();
        ScatterSupplies();
        RebuildEncounter();
        PrepareDrainConsole();

        Debug.Log("[AetherZone1] 1구역 구성 완료 — 옥상 출발, 점프 코스 → 배수 콘솔 → 전투·모듈 → 엘리베이터 귀환.");
    }

    private IEnumerator SetUpPlayerWhenReady()
    {
        Transform found = null;
        for (int frame = 0; frame < 300 && found == null; frame++)
        {
            found = FindPlayer();
            if (found == null)
                yield return null;
        }

        if (found == null)
        {
            Debug.LogWarning("[AetherZone1] 플레이어를 찾지 못해 시작 위치·크기 설정을 건너뜁니다.");
            yield break;
        }

        player = found;
        MovePlayerToStart(found);
        AttachFallRecovery(found);

        yield return null;
        AetherZoneBootstrap.MatchExistingEnemyScales(found);

        // HUD가 자리 잡은 뒤에 안내를 띄운다.
        yield return new WaitForSecondsRealtime(1.2f);
        AetherObjectiveToast.Show("옥상 끝으로 가라.\n슈퍼점프로 발판을 건너 물에 잠긴 골짜기를 지나야 한다.", 14f);
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f)
            return;

        checkTimer = 0.4f;

        if (player == null)
        {
            player = FindPlayer();
            if (player == null)
                return;
        }

        CheckWaves();
        CheckZoneClear();
    }

    private void CheckWaves()
    {
        for (int i = pendingWaves.Count - 1; i >= 0; i--)
        {
            Wave wave = pendingWaves[i];
            if (Vector3.Distance(player.position, wave.anchor) > wave.triggerRadius)
                continue;

            ActivateWave(i);
            pendingWaves.RemoveAt(i);
        }
    }

    /// <summary>모듈 4개를 다 모으면 구역을 끝내고 엘리베이터 귀환을 연다.</summary>
    private void CheckZoneClear()
    {
        if (zoneCleared)
            return;

        string sceneName = SceneManager.GetActiveScene().name;

        for (int i = 0; i < ShieldModules.Length; i++)
        {
            if (!PartSceneModuleBootstrap.IsModuleCollected(sceneName, ShieldModules[i]))
                return;
        }

        zoneCleared = true;
        ElevatorZoneProgress.MarkZoneCleared(1);

        Debug.Log("[AetherZone1] 방패 재료 4개를 모두 모았습니다 — 엘리베이터로 파츠 제작실에 돌아갈 수 있습니다.");
        AetherObjectiveToast.Show("방패 재료를 모두 모았다. 엘리베이터로 돌아가 파츠 제작실로 이동하자.");
    }

    // ------------------------------------------------------------------ 플레이어

    private void MovePlayerToStart(Transform found)
    {
        if (found == null)
            return;

        Transform spawn = EnsureSpawnMarker();
        if (spawn == null)
            return;

        // 위치가 이미 맞더라도 바라보는 방향은 맞춰 준다.
        // 엉뚱한 쪽을 보고 시작하면 건너야 할 옥상 끝이 어디인지 알 수가 없다.
        found.rotation = spawn.rotation;
        SyncPlayerLook(found, spawn.rotation);

        if (Vector3.Distance(found.position, spawn.position) < 25f)
            return;

        Rigidbody body = found.GetComponent<Rigidbody>();
        if (body != null)
            body.linearVelocity = Vector3.zero;

        found.position = spawn.position;

        Debug.Log($"[AetherZone1] 플레이어를 엘리베이터 앞 옥상({spawn.position.x:0}, {spawn.position.y:0}, {spawn.position.z:0})에서 시작하도록 옮겼습니다.");
    }

    /// <summary>
    /// 트랜스폼만 돌리면 Move_Hankyul이 갖고 있는 목표 각도가 그대로라 다음 프레임에 되돌아간다.
    /// 시선 상태까지 같이 맞춰야 실제로 그 방향을 보고 시작한다.
    /// </summary>
    private static void SyncPlayerLook(Transform found, Quaternion rotation)
    {
        AetherPlayerCharacter.SnapLook(found.root.gameObject, rotation);
    }

    private void AttachFallRecovery(Transform found)
    {
        AetherJumpCourseRecovery recovery = found.GetComponent<AetherJumpCourseRecovery>();
        if (recovery == null)
            recovery = found.gameObject.AddComponent<AetherJumpCourseRecovery>();

        recovery.Setup(coursePoints, RoofEdge + Vector3.up * 1.5f, WaterSurfaceY);
    }

    private static Transform FindPlayer()
    {
        // 지금 조작 중인 캐릭터(한결·윤서).
        return AetherPlayerCharacter.FindActiveTransform();
    }

    private static Transform GetGeneratedRoot()
    {
        GameObject root = GameObject.Find(GeneratedRootName);
        if (root == null)
            root = new GameObject(GeneratedRootName);

        return root.transform;
    }

    // ------------------------------------------------------------------ 점프 코스

    /// <summary>
    /// 옥상 끝에서 배수 콘솔까지, 씬에 원래 있던 구조물을 밟고 건넌다.
    ///
    /// 경로 위에서 수면(103.5m) 위로 나와 있는 구조물을 실측해서 골랐다
    /// (AETHER/진단/1구역 경로 측정). 이것들이 코스의 뼈대다.
    /// 다만 구조물 사이가 30~44m씩 벌어져 있어 슈퍼점프로는 한 번에 못 넘기 때문에,
    /// 그 사이에만 콘크리트 판을 몇 장 놓아 징검다리를 만든다.
    /// </summary>
    private struct RouteAnchor
    {
        public string name;    // 씬에서 찾을 이름
        public float along;    // 옥상 끝에서 콘솔 쪽으로 나아간 거리(실측)
        public float side;     // 경로 기준 좌우 어긋남(실측)

        public RouteAnchor(string name, float along, float side)
        {
            this.name = name;
            this.along = along;
            this.side = side;
        }
    }

    private static readonly RouteAnchor[] RouteStructures =
    {
        new RouteAnchor("GroundIsland",        42f, -12f),   // 윗면 105.2  10 x 12
        new RouteAnchor("Hangar_Rounded_Tile", 52f,  25f),   // 윗면 107.1  29 x 16
        new RouteAnchor("Barrel",              79f,   7f),   // 윗면 111.6   9 x  9
        new RouteAnchor("Duct Straight",       97f,  31f),   // 윗면 120.7  30 x  7
        new RouteAnchor("Tank_Body",          127f,   3f),   // 윗면 110.5  11 x 10 — 콘솔 바로 앞
    };

    /// <summary>
    /// 슈퍼점프로 안정적으로 건널 수 있는 거리. 이보다 벌어지면 판을 끼워 넣는다.
    /// 완충했을 때 한계가 약 16.4m이므로, 여유를 두고 13m로 잡는다.
    /// </summary>
    private const float MaxHop = 13f;

    /// <summary>건널 수 있는 거리를 계산할 때 쓰는 달리기 속도(Hankyul_Player 기본값).</summary>
    private const float RunSpeedForReach = 6f;

    /// <summary>사이를 메울 때 쓰는 씬 프리팹.</summary>
    private const string FillerPropName = "BetonPlate1";
    private const string CollapsePropName = "Pallet";

    /// <summary>무너지는 발판으로 만들 징검다리 번호(0부터). 나무 팔레트가 된다.</summary>
    private static readonly int[] CollapsingFillerIndices = { 2, 5 };

    private void BuildJumpCourse(Transform root)
    {
        Vector3 forward = DrainConsolePos - RoofEdge;
        forward.y = 0f;
        forward.Normalize();

        // 좌우 방향은 측정 도구(AETHER/진단/1구역 경로 측정)가 찍는 부호와 같아야 한다.
        // 도구는 Cross(forward, 대상).y 로 좌우를 재는데, 그 값은 (-forward.z, 0, forward.x)
        // 방향의 계수와 부호가 반대다. 이걸 맞추지 않으면 표의 좌표를 그대로 썼는데도
        // 반대편 50~60m를 뒤지게 된다(실제로 Hangar와 Duct를 못 찾고 있었다).
        Vector3 side = new Vector3(forward.z, 0f, -forward.x);

        coursePoints.Clear();

        Vector3 previous = RoofEdge;
        int fillerIndex = 0;
        int structuresUsed = 0;
        float longestGap = 0f;

        for (int i = 0; i < RouteStructures.Length; i++)
        {
            RouteAnchor anchor = RouteStructures[i];

            Vector3 expected = RoofEdge + forward * anchor.along + side * anchor.side;
            GameObject structure = FindRouteStructure(anchor.name, expected);

            if (structure == null)
            {
                Debug.Log($"[AetherZone1]   {i + 1}. '{anchor.name}' — 씨에서 찾지 못했습니다 (기대 지점 {expected.x:0}, {expected.z:0}).");
                continue;
            }

            if (!TryGetLandingPoint(structure, previous, out Vector3 landing, out float landingRadius))
            {
                Debug.Log($"[AetherZone1]   {i + 1}. '{anchor.name}' — 윈면에 설 자리를 찾지 못했습니다.");
                continue;
            }

            // 구조물까지 한 번에 못 가면 사이에 판을 깐다.
            float gap = FlatDistance(previous, landing);
            int hops = Mathf.Max(1, Mathf.CeilToInt(gap / MaxHop));

            for (int step = 1; step < hops; step++)
            {
                float t = step / (float)hops;
                Vector3 position = Vector3.Lerp(previous, landing, t);

                // 일직선 경사로가 되지 않도록 가운데를 살짝 들어 올린다.
                position.y += Mathf.Sin(t * Mathf.PI) * 2.5f;

                bool collapses = System.Array.IndexOf(CollapsingFillerIndices, fillerIndex) >= 0;
                AddFillerPlatform(root, position, collapses, fillerIndex);
                fillerIndex++;
            }

            RegisterCoursePoint(structure.transform, landing, landingRadius);
            structuresUsed++;

            Debug.Log($"[AetherZone1]   {i + 1}. '{anchor.name}' → {structure.name} " +
                      $"착지 ({landing.x:0}, {landing.y:0.0}, {landing.z:0}), 반경 {landingRadius:0.#}m, " +
                      $"직전에서 {gap:0.#}m를 {hops}걸음으로.");

            longestGap = Mathf.Max(longestGap, gap / hops);
            previous = landing;
        }

        CreateCourseGate(root, RoofEdge);

        // 완충했을 때 건널 수 있는 거리 = 체공시간 x 달리기 속도.
        // 체공시간은 2 * sqrt(2h/g)다. 도달 높이와 직접 비교하면 안 된다.
        float gravity = Mathf.Abs(Physics.gravity.y);
        float airTime = 2f * Mathf.Sqrt(2f * SuperJumpMaxApex / Mathf.Max(0.01f, gravity));
        float reach = airTime * RunSpeedForReach;

        Debug.Log($"[AetherZone1] 점프 코스 완성 — 씬 구조물 {structuresUsed}개를 밟고, " +
                  $"사이에 판 {fillerIndex}장(그중 나무 팔레트 {Mathf.Min(CollapsingFillerIndices.Length, fillerIndex)}장)을 끼웠습니다. " +
                  $"가장 먼 한 걸음 {longestGap:0.#}m / 완충 시 건널 수 있는 거리 {reach:0.#}m.");

        if (longestGap > reach - 2f)
            Debug.Log($"[AetherZone1] ※ 가장 먼 한 걸음 {longestGap:0.#}m가 한계 {reach:0.#}m에 너무 가깝습니다. MaxHop을 줄이세요.");
    }

    /// <summary>실측 좌표에 가장 가까운 같은 이름의 구조물을 고른다.</summary>
    private static GameObject FindRouteStructure(string prefabName, Vector3 expected)
    {
        GameObject best = null;
        float bestDistance = float.MaxValue;

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (!NameMatches(t.name, prefabName))
                    continue;

                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;

                // 수면 아래로 잠긴 것은 밟을 수 없다.
                if (renderer.bounds.max.y < WaterSurfaceY + 1f)
                    continue;

                float distance = FlatDistance(renderer.bounds.center, expected);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = t.gameObject;
            }
        }

        return bestDistance < 45f ? best : null;
    }

    /// <summary>"Barrel"이 "Barrel (2)"와는 맞고 "BarrelStack"과는 안 맞게 한다.</summary>
    private static bool NameMatches(string objectName, string prefabName)
    {
        if (!objectName.StartsWith(prefabName, System.StringComparison.Ordinal))
            return false;

        if (objectName.Length == prefabName.Length)
            return true;

        char next = objectName[prefabName.Length];
        return next == ' ' || next == '(' || next == '_';
    }

    /// <summary>
    /// 구조물 윗면에서 실제로 설 수 있는 지점을 찾는다.
    /// 한가운데가 아니라 '직전 발판에서 가장 가까운 가장자리'를 골라야 건너뛸 거리가 짧아진다.
    /// </summary>
    private static bool TryGetLandingPoint(GameObject structure, Vector3 from, out Vector3 landing, out float radius)
    {
        landing = Vector3.zero;
        radius = 2f;

        if (!TryGetWorldBounds(structure, out Bounds bounds))
            return false;

        // 씬에 놓인 장식물 중에는 콜라이더가 아예 없는 것이 있다
        // (Barrel·Duct Straight·Tank_Body가 그랬다). 그대로면 레이가 통과할 뿐 아니라
        // 애초에 그 위에 올라설 수가 없다. 발판으로 쓰려면 딛을 것을 붙여 줘야 한다.
        EnsureStandableCollider(structure);

        // 가장자리에서 조금 안쪽으로 들어온 범위
        const float inset = 2f;
        const int samples = 7;

        float halfX = Mathf.Max(0.5f, bounds.extents.x - inset);
        float halfZ = Mathf.Max(0.5f, bounds.extents.z - inset);

        // 윗면을 격자로 훑어서 '물 위에 있으면서 직전 발판에 가장 가까운' 자리를 고른다.
        //
        // 그냥 가장 가까운 가장자리를 잡으면 안 된다. 격납고처럼 지붕이 둥근 구조물은
        // 가장자리가 수면 아래로 내려가서, 착지하자마자 물에 빠진다(실제로 y=101.1이 나왔다).
        bool found = false;
        float bestDistance = float.MaxValue;

        Bounds expandedBounds = bounds;
        expandedBounds.Expand(1f);

        for (int ix = 0; ix < samples; ix++)
        {
            for (int iz = 0; iz < samples; iz++)
            {
                float u = samples == 1 ? 0.5f : ix / (float)(samples - 1);
                float v = samples == 1 ? 0.5f : iz / (float)(samples - 1);

                Vector3 candidate = new Vector3(
                    Mathf.Lerp(bounds.center.x - halfX, bounds.center.x + halfX, u),
                    0f,
                    Mathf.Lerp(bounds.center.z - halfZ, bounds.center.z + halfZ, v));

                Vector3 rayStart = new Vector3(candidate.x, bounds.max.y + 5f, candidate.z);
                if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                // 맞은 것이 이 구조물인지 확인한다.
                // 계층으로 따지면 안 된다 — LOD 묶음처럼 콜라이더가 형제나 부모에 달린 경우가 있어서,
                // IsChildOf로 보면 멀쩡한 구조물도 전부 걸러진다(실제로 5개 중 4개가 걸렸다).
                // 경계 안에 맞았는지로 보는 편이 구조에 상관없이 잘 맞는다.
                if (!expandedBounds.Contains(hit.point))
                    continue;

                // 물에 잠기는 자리에는 내려설 수 없다.
                if (hit.point.y < WaterSurfaceY + 1f)
                    continue;

                // 너무 가파르면 미끄러져 떨어진다.
                if (Vector3.Angle(hit.normal, Vector3.up) > 45f)
                    continue;

                float distance = FlatDistance(hit.point, from);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                landing = hit.point;
                found = true;
            }
        }

        if (!found)
            return false;

        // 고른 자리 주변에서 얼마나 여유가 있는지 — 발판으로서의 반경.
        radius = Mathf.Max(1f, Mathf.Min(halfX, halfZ) * 0.6f);
        return true;
    }

    /// <summary>구조물 사이를 메우는 판 한 장. 씬에 있는 프리팹을 복제해서 쓴다.</summary>
    private void AddFillerPlatform(Transform root, Vector3 position, bool collapses, int index)
    {
        string propName = collapses ? CollapsePropName : FillerPropName;
        GameObject platform = ClonePropFromScene(propName);

        if (platform == null)
        {
            Debug.LogWarning($"[AetherZone1] 씬에서 '{propName}'을(를) 찾지 못해 임시 판을 놓습니다.");
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.transform.localScale = new Vector3(4f, 0.4f, 4f);
        }

        platform.name = $"JumpFiller_{index:00}_{propName}" + (collapses ? "_무너짐" : string.Empty);
        platform.transform.SetParent(root, true);
        platform.transform.rotation = Quaternion.identity;
        platform.transform.position = position;

        float radius = 2f;

        // 프리팹마다 원점이 제각각이라, 윗면이 설계한 높이에 오도록 내려 맞춘다.
        if (TryGetWorldBounds(platform, out Bounds bounds))
        {
            platform.transform.position = position - Vector3.up * (bounds.max.y - platform.transform.position.y);
            radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        }

        EnsureStandableCollider(platform);

        if (collapses)
            platform.AddComponent<AetherCollapsingPlatform>();

        RegisterCoursePoint(platform.transform, position, radius);
    }

    private void RegisterCoursePoint(Transform owner, Vector3 point, float radius)
    {
        coursePoints.Add(new AetherCoursePoint
        {
            owner = owner,
            point = point,
            radius = radius,
        });
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    /// <summary>
    /// 씬에 배치돼 있는 같은 이름의 물건을 찾아 복제한다.
    /// "BetonPlate1 (12)"처럼 뒤에 번호가 붙어 있어도 찾는다.
    /// </summary>
    private static GameObject ClonePropFromScene(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;

        if (!sceneProps.TryGetValue(prefabName, out GameObject source))
        {
            source = FindPropInScene(prefabName);
            sceneProps[prefabName] = source;
        }

        if (source == null)
            return null;

        GameObject clone = Instantiate(source);
        clone.SetActive(true);

        // 원본이 정적으로 잡혀 있으면 복제본을 움직일 수 없어 무너지는 발판이 떨어지지 않는다.
        foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = false;

        return clone;
    }

    private static GameObject FindPropInScene(string prefabName)
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (!NameMatches(t.name, prefabName))
                    continue;

                if (t.GetComponentInChildren<MeshRenderer>(true) != null)
                    return t.gameObject;
            }
        }

        return null;
    }

    /// <summary>발판 위에 올라설 수 있어야 한다. 콜라이더가 없거나 트리거뿐이면 붙여 준다.</summary>
    private static void EnsureStandableCollider(GameObject platform)
    {
        Collider[] colliders = platform.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
                return;
        }

        if (!TryGetWorldBounds(platform, out Bounds bounds))
            return;

        BoxCollider box = platform.AddComponent<BoxCollider>();
        box.center = platform.transform.InverseTransformPoint(bounds.center);

        // size는 로컬 단위다. 월드 크기를 그대로 넣으면 배율만큼 부풀어 버린다.
        // (10배로 배치된 Barrel에서 콜라이더가 10배가 되어 레이가 엉뚱한 높이에 맞았다.)
        Vector3 scale = platform.transform.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        bool started = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
                continue;

            if (!started)
            {
                bounds = renderers[i].bounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return started;
    }

    /// <summary>옥상 끝 안내. 슈퍼점프가 없으면 건널 수 없다는 것을 알려 준다.</summary>
    private static void CreateCourseGate(Transform root, Vector3 edge)
    {
        GameObject gate = new GameObject("JumpCourse Gate");
        gate.transform.SetParent(root, true);
        gate.transform.position = edge;

        gate.AddComponent<AetherSuperJumpGate>().Setup(18f);
    }

    // ------------------------------------------------------------------ 모듈

    /// <summary>
    /// 방패 재료 4개를 적 구역에 놓는다.
    ///
    /// 새로 만들지 않고 <b>이 씬에 이미 놓여 있는 모듈 프롭</b>
    /// (Key_module / Chakram_module / Dynamite_module / Valve_module)을 그대로 쓰고,
    /// 담긴 아이템만 방패 재료로 바꾼 뒤 적 구역으로 옮긴다.
    /// 05.폐허된 도시의 모듈(BatteryAA·Cable·Crank·SmallKey)과 똑같은 방식이다.
    /// </summary>
    private void PlaceShieldModules()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        List<ModulePickupInteractable> authored = new List<ModulePickupInteractable>();
        foreach (ModulePickupInteractable pickup in
                 FindObjectsByType<ModulePickupInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup != null && pickup.transform.root.name != GeneratedRootName)
                authored.Add(pickup);
        }

        authored.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        int reused = 0;
        int placed = 0;

        for (int i = 0; i < ShieldModules.Length; i++)
        {
            string id = ShieldModules[i];

            ItemData item = PartResourceResolver.FindItemData(id);
            if (item == null)
            {
                Debug.LogWarning($"[AetherZone1] 모듈 데이터를 찾지 못했습니다: {id}");
                continue;
            }

            string key = PartSceneModuleBootstrap.MakeCollectedKey(sceneName, id);
            if (PartSystemRuntimeState.HasCollectedSceneItem(key))
                continue;

            Vector3 anchor = ModuleAnchors[i % ModuleAnchors.Length];
            if (!TryFindDryGround(anchor, 45f, out Vector3 position))
            {
                Debug.LogWarning($"[AetherZone1] {id}를 놓을 마른 땅을 찾지 못했습니다: {anchor}");
                continue;
            }

            if (i < authored.Count)
            {
                // 씬에 있던 프롭을 재사용한다.
                ModulePickupInteractable pickup = authored[i];
                pickup.gameObject.SetActive(true);
                pickup.transform.position = position;
                pickup.Configure(item, 1, key);
                pickup.gameObject.name = item.id;

                ModuleWorldMarker marker = pickup.GetComponent<ModuleWorldMarker>();
                if (marker == null)
                    marker = pickup.gameObject.AddComponent<ModuleWorldMarker>();
                marker.Configure(item);

                reused++;
            }
            else if (PartSceneModuleBootstrap.SpawnModuleAt(item, position, key, i) == null)
            {
                continue;
            }

            placed++;
        }

        Debug.Log($"[AetherZone1] 방패 재료 {placed}개를 적 구역에 배치했습니다 (씬 모듈 프롭 {reused}개 재사용).");
    }

    /// <summary>
    /// 식량과 물을 적 구역 전체에 뿌린다.
    ///
    /// 프로젝트에 이미 있는 픽업 프리팹(식량 깡통 / 물병)을 그대로 쓴다.
    /// 04.Tutorial1에서 쓰는 것과 같은 물건이라, 다른 씬과 보이는 것이 달라지지 않는다.
    /// </summary>
    private void ScatterSupplies()
    {
        int created = 0;
        int anchorIndex = 0;

        for (int s = 0; s < Supplies.Length; s++)
        {
            GameObject prefab = Resources.Load<GameObject>(SupplyPrefabRoot + Supplies[s].id);
            if (prefab == null)
            {
                Debug.LogWarning($"[AetherZone1] 보급 프리팹을 찾지 못했습니다: Resources/{SupplyPrefabRoot}{Supplies[s].id}");
                continue;
            }

            for (int n = 0; n < Supplies[s].count; n++)
            {
                Vector3 anchor = SupplyAnchors[anchorIndex % SupplyAnchors.Length];
                anchorIndex++;

                // 거점 하나가 담당하는 반경. 거점 간격이 80~90m이므로 40m면 사이가 비지 않는다.
                if (!TryFindDryGround(anchor, 40f, out Vector3 position))
                    continue;

                SpawnSupply(prefab, position);
                created++;
            }
        }

        // 점프 코스 위에도 몇 개 올려 둔다 — 건너는 중에 회복할 수 있도록.
        for (int i = 1; i < coursePoints.Count; i += 3)
        {
            string id = (i / 3) % 2 == 0 ? "Water" : "Food";
            GameObject prefab = Resources.Load<GameObject>(SupplyPrefabRoot + id);
            if (prefab == null)
                continue;

            SpawnSupply(prefab, coursePoints[i].point + Vector3.up * 0.4f);
            created++;
        }

        Debug.Log($"[AetherZone1] 식량·물 {created}개를 적 구역 전체와 점프 코스에 뿌렸습니다.");
    }

    private void SpawnSupply(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.value * 360f, 0f));
        instance.name = prefab.name;
        instance.transform.SetParent(GetGeneratedRoot(), true);
    }

    /// <summary>
    /// 연못이 실제로 덮고 있는 범위. 씬에서 재서 채운다.
    ///
    /// 처음에는 '수면(103.5m)보다 낮으면 물속'이라고 단순하게 봤는데, 그러면
    /// 연못 바깥의 마른 땅(적 구역은 z 972~1297로 연못 밖이고 높이가 95~100m다)까지
    /// 전부 물속으로 쳐서 아이템을 거의 못 놓았다. 연못 범위 안에서만 높이를 따져야 한다.
    /// </summary>
    private static Bounds pondBounds;
    private static bool pondBoundsResolved;

    private static Bounds GetPondBounds()
    {
        if (pondBoundsResolved)
            return pondBounds;

        pondBoundsResolved = true;
        pondBounds = new Bounds(Vector3.zero, Vector3.zero);

        GameObject pond = GameObject.Find("Pond Water");
        Renderer renderer = pond != null ? pond.GetComponent<Renderer>() : null;

        if (renderer != null)
        {
            pondBounds = renderer.bounds;
            Debug.Log($"[AetherZone1] 연못 범위: x {pondBounds.min.x:0}~{pondBounds.max.x:0}, " +
                      $"z {pondBounds.min.z:0}~{pondBounds.max.z:0}, 수면 y={pondBounds.max.y:0.0}");
        }
        else
        {
            Debug.LogWarning("[AetherZone1] 'Pond Water'를 찾지 못했습니다. 물 판정 없이 배치합니다.");
        }

        return pondBounds;
    }

    /// <summary>이 지점이 배수 전 물에 잠겨 있는가.</summary>
    private static bool IsUnderWater(Vector3 point)
    {
        Bounds pond = GetPondBounds();
        if (pond.size.sqrMagnitude < 0.01f)
            return false;

        bool insideFootprint = point.x >= pond.min.x && point.x <= pond.max.x
                            && point.z >= pond.min.z && point.z <= pond.max.z;

        return insideFootprint && point.y < WaterSurfaceY + 0.5f;
    }

    /// <summary>물에 잠기지 않고, 무언가에 파묻히지도 않은 지면을 찾는다.</summary>
    private static bool TryFindDryGround(Vector3 center, float radius, out Vector3 result)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            // 처음 몇 번은 앵커 그대로, 그 뒤로는 점점 넓게 훑는다.
            float spread = attempt < 3 ? 0f : radius * (attempt / 80f);
            Vector2 offset = attempt < 3 ? Vector2.zero : Random.insideUnitCircle * spread;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

            if (!Physics.Raycast(candidate + Vector3.up * 150f, Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.collider != null && hit.collider.CompareTag("Water"))
                continue;

            if (IsUnderWater(hit.point))
                continue;

            // 너무 가파른 비탈에는 놓지 않는다 — 굴러떨어지거나 박혀 보인다.
            if (Vector3.Angle(hit.normal, Vector3.up) > 35f)
                continue;

            // 벽이나 물체 속이면 제외
            if (Physics.CheckSphere(hit.point + Vector3.up * 0.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            result = hit.point + Vector3.up * 0.1f;
            return true;
        }

        result = center;
        return false;
    }

    // ------------------------------------------------------------------ 배수 콘솔

    /// <summary>
    /// 콘솔을 이 씬의 크기에 맞춘다. 기본 상호작용 거리 3.2m는 2.6배 세계에서 너무 짧다.
    /// </summary>
    private static void PrepareDrainConsole()
    {
        WaterDrainConsole[] consoles = FindObjectsByType<WaterDrainConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < consoles.Length; i++)
        {
            consoles[i].SetMaxActivationDistance(ConsoleReachDistance);
        }

        if (consoles.Length == 0)
            Debug.LogWarning("[AetherZone1] 배수 콘솔을 찾지 못했습니다. 물이 빠지지 않습니다.");
        else
            Debug.Log($"[AetherZone1] 배수 콘솔 {consoles.Length}개를 준비했습니다 (상호작용 거리 {ConsoleReachDistance:0.#}m).");
    }

    // ------------------------------------------------------------------ 전투

    private void RebuildEncounter()
    {
        GameObject encounterRoot = GameObject.Find(EncounterRootName);
        if (encounterRoot == null)
        {
            Debug.LogWarning("[AetherZone1] 전투 그룹을 찾지 못했습니다: " + EncounterRootName);
            return;
        }

        List<GameObject> robots = new List<GameObject>();
        List<GameObject> drones = new List<GameObject>();
        List<GameObject> suiciders = new List<GameObject>();

        CollectEnemies(encounterRoot.transform, robots, drones, suiciders);

        int total = robots.Count + drones.Count + suiciders.Count;
        for (int i = 0; i < robots.Count; i++) robots[i].SetActive(false);
        for (int i = 0; i < drones.Count; i++) drones[i].SetActive(false);
        for (int i = 0; i < suiciders.Count; i++) suiciders[i].SetActive(false);

        pendingWaves.Clear();
        waveMembers.Clear();

        int used = 0;
        for (int w = 0; w < Waves.Length; w++)
        {
            Wave wave = Waves[w];
            List<GameObject> members = new List<GameObject>();

            TakeNearest(robots, wave.anchor, wave.robots, members);
            TakeNearest(drones, wave.anchor, wave.drones, members);
            TakeNearest(suiciders, wave.anchor, wave.suiciders, members);

            waveMembers.Add(members);
            pendingWaves.Add(wave);
            used += members.Count;
        }

        Debug.Log($"[AetherZone1] 적 {total}체 중 {used}체만 3웨이브로 편성했습니다. (나머지는 비활성)");
    }

    private static void CollectEnemies(Transform root, List<GameObject> robots, List<GameObject> drones, List<GameObject> suiciders)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            bool isEnemy = false;
            if (child.GetComponent<RobotHealth>() != null)
            {
                robots.Add(child.gameObject);
                isEnemy = true;
            }
            else if (child.GetComponent<DroneSuiciderCombat>() != null)
            {
                suiciders.Add(child.gameObject);
                isEnemy = true;
            }
            else if (child.GetComponent<DroneHealth>() != null || child.GetComponent<Drones>() != null)
            {
                drones.Add(child.gameObject);
                isEnemy = true;
            }

            if (!isEnemy && child.childCount > 0)
                CollectEnemies(child, robots, drones, suiciders);
        }
    }

    private static void TakeNearest(List<GameObject> pool, Vector3 anchor, int count, List<GameObject> members)
    {
        for (int n = 0; n < count && pool.Count > 0; n++)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                float d = (pool[i].transform.position - anchor).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            members.Add(pool[best]);
            pool.RemoveAt(best);
        }
    }

    private void ActivateWave(int index)
    {
        if (index < 0 || index >= waveMembers.Count)
            return;

        Wave wave = pendingWaves[index];
        List<GameObject> members = waveMembers[index];

        int placed = 0;
        for (int i = 0; i < members.Count; i++)
        {
            GameObject enemy = members[i];
            if (enemy == null)
                continue;

            bool airborne = enemy.GetComponent<Drones>() != null || enemy.GetComponent<DroneHealth>() != null
                            || enemy.GetComponent<DroneSuiciderCombat>() != null;

            if (TryPlaceEnemy(enemy, wave.anchor, i, airborne))
                placed++;

            enemy.SetActive(true);
            BindPlayer(enemy, player);
        }

        Debug.Log($"[AetherZone1] '{wave.name}' 웨이브 시작 — 적 {placed}체.");
        AetherObjectiveToast.Show($"적 감지 — {wave.name} 구역");

        AlertLevelManager alert = AlertLevelManager.Instance;
        if (alert != null)
            alert.ApplyAlertEvent(AlertEventType.FailureSmall);
    }

    private static bool TryPlaceEnemy(GameObject enemy, Vector3 anchor, int index, bool airborne)
    {
        float angle = index * 137.5f * Mathf.Deg2Rad;   // 황금각 — 고르게 흩어진다
        float radius = 10f + index * 3.2f;
        Vector3 candidate = anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

        if (airborne)
        {
            if (Physics.Raycast(candidate + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                candidate = hit.point;

            candidate += Vector3.up * Random.Range(5f, 10f);
            enemy.transform.position = candidate;

            NavMeshAgent flyAgent = enemy.GetComponentInChildren<NavMeshAgent>(true);
            if (flyAgent != null)
                flyAgent.enabled = false;

            return true;
        }

        if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
            return false;

        enemy.transform.position = navHit.position;

        NavMeshAgent agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.enabled)
            agent.Warp(navHit.position);

        return true;
    }

    private static void BindPlayer(GameObject enemy, Transform target)
    {
        if (target == null)
            return;

        Robots[] robots = enemy.GetComponentsInChildren<Robots>(true);
        for (int i = 0; i < robots.Length; i++)
            SetPrivateField(robots[i], "player", target);

        Drones[] drones = enemy.GetComponentsInChildren<Drones>(true);
        for (int i = 0; i < drones.Length; i++)
            SetPrivateField(drones[i], "player", target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null || value == null)
            return;

        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        if (field != null && field.FieldType.IsInstanceOfType(value))
            field.SetValue(target, value);
    }
}

/// <summary>
/// 슈퍼점프 파츠를 장착했는지 알려 주는 옥상 끝 안내판.
/// </summary>
public class AetherSuperJumpGate : MonoBehaviour
{
    private float radius = 18f;
    private float timer;
    private bool notified;

    public void Setup(float triggerRadius)
    {
        radius = triggerRadius;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = 0.5f;

        GameObject player = AetherPlayerCharacter.FindActive();
        if (player == null)
            return;

        if (Vector3.Distance(player.transform.position, transform.position) > radius)
        {
            notified = false;
            return;
        }

        if (notified)
            return;

        notified = true;

        EquipmentManager equipment = player.GetComponentInChildren<EquipmentManager>(true);
        bool hasSuperJump = equipment != null && equipment.HasPart(PartType.SuperJump);

        if (hasSuperJump)
        {
            Debug.Log("[AetherZone1] 슈퍼점프 장착 확인 — 스페이스를 길게 눌러 발판을 건너세요.");
            AetherObjectiveToast.Show("스페이스를 길게 눌러 충전하고 발판을 건너라. 붉은 발판은 무너진다.");
        }
        else
        {
            Debug.Log("[AetherZone1] 슈퍼점프 파츠가 없습니다.");
            AetherObjectiveToast.Show("슈퍼점프 파츠가 없다. 파츠 제작실에서 제작·장착한 뒤 다시 오자.");
        }
    }
}

/// <summary>
/// 밟으면 잠시 뒤 무너지는 발판. 잠깐 뒤 제자리로 돌아온다.
/// 코스에 '망설일 수 없는 구간'을 만들어 준다.
/// </summary>
public class AetherCollapsingPlatform : MonoBehaviour
{
    [SerializeField] private float delayBeforeFall = 1.1f;
    [SerializeField] private float respawnDelay = 5f;

    private Vector3 homePosition;
    private Renderer platformRenderer;
    private Color baseColor;
    private bool falling;

    private void Awake()
    {
        homePosition = transform.position;
        platformRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>(true);

        if (platformRenderer != null && platformRenderer.material.HasProperty("_Color"))
            baseColor = platformRenderer.material.color;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (falling || !AetherPlayerCharacter.IsPlayer(collision.transform))
            return;

        StartCoroutine(FallAndReturn());
    }

    private IEnumerator FallAndReturn()
    {
        falling = true;

        // 무너지기 전에 깜빡여서 알려 준다.
        float elapsed = 0f;
        while (elapsed < delayBeforeFall)
        {
            elapsed += Time.deltaTime;

            if (platformRenderer != null)
            {
                float blink = Mathf.PingPong(elapsed * 8f, 1f);
                platformRenderer.material.color = Color.Lerp(baseColor, Color.white, blink);
            }

            yield return null;
        }

        // 아래로 떨어뜨린다.
        float fallTime = 0f;
        while (fallTime < 1.2f)
        {
            fallTime += Time.deltaTime;
            transform.position += Vector3.down * (18f * Time.deltaTime);
            yield return null;
        }

        gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnDelay);

        transform.position = homePosition;
        if (platformRenderer != null)
            platformRenderer.material.color = baseColor;

        gameObject.SetActive(true);
        falling = false;
    }
}

/// <summary>
/// 코스에서 발을 디딜 수 있는 자리 하나.
///
/// 씬에 원래 있던 구조물 위(옥상 한가운데가 아니라 가장자리)와,
/// 사이에 끼워 넣은 판이 섞여 있어서 위치와 크기를 따로 들고 다녀야 한다.
/// </summary>
public struct AetherCoursePoint
{
    /// <summary>이 자리를 갖고 있는 물건. 무너지는 판은 사라지므로 살아 있는지 확인하는 데 쓴다.</summary>
    public Transform owner;

    /// <summary>실제로 발이 닿는 지점.</summary>
    public Vector3 point;

    /// <summary>이 지점 주변 어디까지를 같은 발판으로 볼지.</summary>
    public float radius;
}

/// <summary>
/// 점프 코스에서 떨어졌을 때 마지막으로 밟은 자리로 되돌린다.
///
/// 물이 차 있을 때 떨어지면 골짜기 바닥에 갇히고, 물을 뺀 뒤에 떨어지면 37m 아래로 추락한다.
/// 어느 쪽이든 판이 막히므로, 죽이는 대신 직전 자리에서 다시 시작하게 한다.
/// </summary>
public class AetherJumpCourseRecovery : MonoBehaviour
{
    private readonly List<AetherCoursePoint> points = new List<AetherCoursePoint>();

    private Vector3 courseStart;
    private float failY;
    private Vector3 lastSafePoint;
    private Rigidbody body;
    private float checkTimer;

    public void Setup(List<AetherCoursePoint> coursePoints, Vector3 start, float waterSurfaceY)
    {
        points.Clear();
        if (coursePoints != null)
            points.AddRange(coursePoints);

        courseStart = start;
        lastSafePoint = start;

        // 수면보다 조금 위에서 이미 실패로 본다. 물에 빠지고 나서 되돌리면 늦다.
        failY = waterSurfaceY + 1.5f;

        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f)
            return;

        checkTimer = 0.2f;

        UpdateLastSafePoint();

        if (transform.position.y > failY)
            return;

        // 코스를 다 건너 골짜기로 내려온 뒤라면 되돌리지 않는다.
        if (!IsOverCourse())
            return;

        Recover();
    }

    /// <summary>발판 위에 서 있으면 그 자리를 기억해 둔다.</summary>
    private void UpdateLastSafePoint()
    {
        for (int i = 0; i < points.Count; i++)
        {
            AetherCoursePoint entry = points[i];

            // 무너져서 사라진 판은 돌아갈 자리가 될 수 없다.
            if (entry.owner == null || !entry.owner.gameObject.activeInHierarchy)
                continue;

            Vector3 delta = transform.position - entry.point;
            if (Mathf.Abs(delta.y) > 4f)
                continue;

            if (new Vector2(delta.x, delta.z).magnitude > entry.radius + 1f)
                continue;

            lastSafePoint = entry.point + Vector3.up * 1.5f;
            return;
        }
    }

    /// <summary>플레이어가 아직 코스 구간 안에 있는가.</summary>
    private bool IsOverCourse()
    {
        if (points.Count == 0)
            return false;

        float nearest = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 delta = transform.position - points[i].point;
            nearest = Mathf.Min(nearest, new Vector2(delta.x, delta.z).magnitude);
        }

        return nearest < 35f;
    }

    private void Recover()
    {
        Vector3 target = lastSafePoint.sqrMagnitude > 0.01f ? lastSafePoint : courseStart;

        if (body != null)
            body.linearVelocity = Vector3.zero;

        transform.position = target;

        Debug.Log("[AetherZone1] 떨어져서 직전 발판으로 되돌렸습니다.");
        AetherObjectiveToast.Show("떨어졌다 — 직전 발판에서 다시 시작");
    }
}
