using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 1·2·3구역처럼 맵 지오메트리만 있고 플레이어/적이 배치되지 않은 씬을 실제로 플레이 가능한
/// 상태로 만든다.
///
/// - 플레이어(한결)가 없으면 Resources/AETHERRuntime/Hankyul_찐 을 스폰하고 태그·컴포넌트를 보장한다.
/// - 카메라와 오디오 리스너가 없으면 만들어 화면이 검게 나오는 것을 막는다.
/// - 구역별 편성표에 따라 적을 배치한다. 좌표를 하드코딩하지 않고 NavMesh/지면을 검사해
///   실제로 서 있을 수 있는 자리에만 놓는다.
/// - 경계도(AlertLevel)가 높으면 적 수를 늘린다(설계 문서의 DDA 규칙).
/// </summary>
public class AetherZoneBootstrap : MonoBehaviour
{
    private const string GeneratedRootName = "AETHER_Zone_RuntimeCombat";
    // 모든 씬이 같은 플레이어를 쓰도록 파츠 제작실(07)의 프리팹을 기준으로 삼는다.
    private const string PlayerResourcePath = "AETHERRuntime/Hankyul_Player";

    private struct ZoneComposition
    {
        public string scenePrefix;
        public int robots;
        public int drones;
        public int turrets;

        public ZoneComposition(string scenePrefix, int robots, int drones, int turrets)
        {
            this.scenePrefix = scenePrefix;
            this.robots = robots;
            this.drones = drones;
            this.turrets = turrets;
        }
    }

    // 구역별 기본 적 편성. 진행할수록 빡세진다.
    private static readonly ZoneComposition[] Compositions =
    {
        new ZoneComposition("08.1구역_2", 8, 3, 2),
        new ZoneComposition("08.1구역", 6, 2, 2),
        new ZoneComposition("09.", 10, 4, 3),
        new ZoneComposition("11.", 12, 6, 4),
    };

    // 플레이어만 보장하고 전투는 만들지 않는 씬
    private static readonly string[] PlayerOnlyScenePrefixes = { "06.", "07." };

    private static readonly HashSet<string> combatBuiltScenes = new HashSet<string>();
    private static bool subscribed;

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
        combatBuiltScenes.Remove(scene.name);
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            return;

        bool needsCombat = TryGetComposition(sceneName, out ZoneComposition composition);
        bool playerOnly = IsPlayerOnlyScene(sceneName);

        if (!needsCombat && !playerOnly)
            return;

        GameObject host = new GameObject("AetherZoneBootstrap_Runtime");
        AetherZoneBootstrap runner = host.AddComponent<AetherZoneBootstrap>();
        runner.sceneName = sceneName;
        runner.composition = composition;
        runner.buildCombat = needsCombat;
    }

    private static bool IsPlayerOnlyScene(string sceneName)
    {
        for (int i = 0; i < PlayerOnlyScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(PlayerOnlyScenePrefixes[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TryGetComposition(string sceneName, out ZoneComposition composition)
    {
        composition = default;
        for (int i = 0; i < Compositions.Length; i++)
        {
            if (sceneName.StartsWith(Compositions[i].scenePrefix, System.StringComparison.Ordinal))
            {
                composition = Compositions[i];
                return true;
            }
        }

        return false;
    }

    private string sceneName;
    private ZoneComposition composition;
    private bool buildCombat;

    private void Start()
    {
        Transform player = EnsurePlayer();
        if (player == null)
        {
            Debug.LogError($"[AetherZoneBootstrap] '{sceneName}'에 플레이어를 만들 수 없습니다. " +
                           "Resources/AETHERRuntime/Hankyul_찐 프리팹을 확인하세요.");
            Destroy(gameObject);
            return;
        }

        EnsureCamera(player);

        if (buildCombat && combatBuiltScenes.Add(sceneName))
            BuildCombat(player);

        // 씬에 원래 놓여 있던 적도 플레이어 기준으로 크기를 맞춘다.
        MatchExistingEnemyScales(player);

        // 플레이어를 방금 만들었다면, 플레이어가 없어서 건너뛰었던 UI/파츠 바인딩을 다시 돌린다.
        RefreshPlayerDependentSystems();

        Destroy(gameObject);
    }

    /// <summary>
    /// 파츠 장착 UI(P키)와 모듈 픽업 바인딩은 씬 로드 시점에 플레이어가 없으면 그냥 건너뛴다.
    /// 구역 씬은 플레이어를 우리가 나중에 만들기 때문에, 만든 뒤 한 번 더 돌려줘야
    /// 05씬과 똑같이 인벤토리·파츠 UI가 붙는다.
    /// </summary>
    private static void RefreshPlayerDependentSystems()
    {
        PartsSlot2GlobalBootstrap.EnsureScenePlayerUI();

        PartSceneAutoBinder binder = FindFirstObjectByType<PartSceneAutoBinder>(FindObjectsInactive.Include);
        if (binder != null)
        {
            binder.BindSceneObjects();
            return;
        }

        GameObject helper = new GameObject("PartSceneAutoBinder_ZoneRefresh");
        PartSceneAutoBinder runtimeBinder = helper.AddComponent<PartSceneAutoBinder>();
        runtimeBinder.BindSceneObjects();
        Destroy(helper);
    }

    // ------------------------------------------------------------------ 플레이어

    private Transform EnsurePlayer()
    {
        // 플레이어 선택 화면에서 고른 캐릭터를 세운다(한결·윤서). 씬에 있으면 그것을, 없으면 만든다.
        GameObject selected = AetherCharacterSpawner.EnsureSelectedPlayer(FindPlayerSpawnPoint(), Quaternion.identity);
        if (selected != null)
            return selected.transform;

        // 고른 캐릭터를 못 만들었을 때만 예전처럼 한결로 진행한다.
        Move_Hankyul existing = FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Include);
        if (existing != null)
        {
            PreparePlayer(existing.gameObject);
            return existing.transform;
        }

        GameObject prefab = Resources.Load<GameObject>(PlayerResourcePath);
        if (prefab == null)
        {
            // 이름이 바뀌었을 수 있으니 폴더 전체에서 한결을 찾아본다.
            GameObject[] candidates = Resources.LoadAll<GameObject>("AETHERRuntime");
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].GetComponentInChildren<Move_Hankyul>(true) != null)
                {
                    prefab = candidates[i];
                    break;
                }
            }
        }

        if (prefab == null)
            return null;

        Vector3 spawn = FindPlayerSpawnPoint();
        GameObject player = Instantiate(prefab, spawn, Quaternion.identity);
        player.name = "Hankyul (Runtime Spawn)";
        PreparePlayer(player);
        return player.transform;
    }

    /// <summary>
    /// 씬에 스폰 지점 표식이 있으면 그것을, 없으면 NavMesh 중심이나 지면을 찾아 세운다.
    /// 아무 것도 못 찾으면 원점 위에 두되 바닥을 뚫고 떨어지지 않게 살짝 띄운다.
    /// </summary>
    private Vector3 FindPlayerSpawnPoint()
    {
        // 1) 관례적인 이름의 스폰 포인트
        string[] spawnNames = { "PlayerSpawn", "SpawnPoint", "Player Spawn", "StartPoint", "Start" };
        for (int i = 0; i < spawnNames.Length; i++)
        {
            GameObject marker = GameObject.Find(spawnNames[i]);
            if (marker != null)
                return marker.transform.position + Vector3.up * 0.5f;
        }

        // 2) 씬에 이미 놓인 리스폰 컨트롤러
        PlayerRespawnController respawn = FindFirstObjectByType<PlayerRespawnController>(FindObjectsInactive.Include);
        if (respawn != null)
            return respawn.transform.position + Vector3.up * 0.5f;

        // 3) NavMesh 위 임의 지점
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices != null && triangulation.vertices.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < triangulation.vertices.Length; i++)
                sum += triangulation.vertices[i];

            Vector3 center = sum / triangulation.vertices.Length;
            if (NavMesh.SamplePosition(center, out NavMeshHit hit, 200f, NavMesh.AllAreas))
                return hit.position + Vector3.up * 0.6f;
        }

        // 4) 지면 레이캐스트
        if (Physics.Raycast(new Vector3(0f, 500f, 0f), Vector3.down, out RaycastHit groundHit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            return groundHit.point + Vector3.up * 0.6f;

        return new Vector3(0f, 2f, 0f);
    }

    private void PreparePlayer(GameObject player)
    {
        // 파츠·인벤토리·생존 수치 연결은 두 캐릭터 공용 규칙을 쓴다.
        AetherPlayerCharacter.Prepare(player);
    }

    private void EnsureCamera(Transform player)
    {
        if (Camera.main != null)
        {
            EnsureAudioListener(Camera.main.gameObject);
            return;
        }

        Camera anyCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        if (anyCamera != null)
        {
            EnsureAudioListener(anyCamera.gameObject);
            return;
        }

        // 플레이어 프리팹 안에 카메라가 있으면 그것을 쓴다.
        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            EnsureAudioListener(playerCamera.gameObject);
            return;
        }

        GameObject cameraObject = new GameObject("Runtime Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.SetParent(player, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        camera.nearClipPlane = 0.05f;
        EnsureAudioListener(cameraObject);

        Debug.LogWarning($"[AetherZoneBootstrap] '{sceneName}'에 카메라가 없어 임시 카메라를 만들었습니다.");
    }

    private static void EnsureAudioListener(GameObject target)
    {
        if (FindFirstObjectByType<AudioListener>(FindObjectsInactive.Exclude) == null)
            target.AddComponent<AudioListener>();
    }

    // ------------------------------------------------------------------ 전투

    /// <summary>
    /// 씬에 이미 적이 배치돼 있으면 새로 스폰하지 않는다.
    /// (1구역처럼 손으로 깔아 둔 인카운터가 있는 씬에 또 쏟아부으면 겹친다.)
    /// </summary>
    private static bool SceneAlreadyHasEnemies()
    {
        int count = FindObjectsByType<RobotHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
                    + FindObjectsByType<DroneHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        return count >= 8;
    }

    private void BuildCombat(Transform player)
    {
        if (SceneAlreadyHasEnemies())
        {
            Debug.Log("[AetherZoneBootstrap] 씬에 이미 적이 배치돼 있어 추가 스폰을 건너뜁니다.");
            return;
        }

        GameObject oldRoot = GameObject.Find(GeneratedRootName);
        if (oldRoot != null)
            Destroy(oldRoot);

        Transform root = new GameObject(GeneratedRootName).transform;

        GameObject robotPrefab = Resources.Load<GameObject>("AETHERRuntime/Robot");
        GameObject droneAttackerPrefab = Resources.Load<GameObject>("AETHERRuntime/Drone_Attacker");
        GameObject droneObserverPrefab = Resources.Load<GameObject>("AETHERRuntime/Drone_Observer");
        GameObject turretPrefab = Resources.Load<GameObject>("AETHERRuntime/turret_1_1");
        GameObject robotBullet = Resources.Load<GameObject>("AETHERRuntime/RobotBullet");
        GameObject enemyBullet = Resources.Load<GameObject>("AETHERRuntime/EnemyBullet");

        float scale = GetAlertScale();
        int robotCount = Mathf.RoundToInt(composition.robots * scale);
        int droneCount = Mathf.RoundToInt(composition.drones * scale);
        int turretCount = Mathf.RoundToInt(composition.turrets * scale);

        List<Vector3> used = new List<Vector3>();
        int spawned = 0;

        spawned += SpawnGroup(robotPrefab, robotCount, root, player, used, "Zone_Robot", robotBullet, false);
        spawned += SpawnDrones(droneAttackerPrefab, droneObserverPrefab, droneCount, root, player, used, enemyBullet);
        spawned += SpawnGroup(turretPrefab, turretCount, root, player, used, "Zone_Turret", enemyBullet, false);

        if (spawned == 0)
            Debug.LogWarning($"[AetherZoneBootstrap] '{sceneName}'에 적을 배치하지 못했습니다. " +
                             "NavMesh가 구워져 있는지, Resources/AETHERRuntime 프리팹이 있는지 확인하세요.");
        else
            Debug.Log($"[AetherZoneBootstrap] '{sceneName}' 전투 구성 완료: 적 {spawned}체 (경계도 배율 {scale:0.00}).");
    }

    /// <summary>경계도가 높을수록 적이 많아진다. 매니저가 없으면 1배.</summary>
    private static float GetAlertScale()
    {
        AlertLevelManager alert = AlertLevelManager.Instance;
        if (alert == null)
            return 1f;

        switch (alert.CurrentDifficulty)
        {
            case AlertDifficulty.Hard:
                return 1.4f;
            case AlertDifficulty.Easy:
                return 0.7f;
            default:
                return 1f;
        }
    }

    private static int SpawnGroup(GameObject prefab, int count, Transform parent, Transform player,
        List<Vector3> used, string namePrefix, GameObject bulletPrefab, bool airborne)
    {
        if (prefab == null || count <= 0)
            return 0;

        int created = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryFindEnemySpot(player, used, airborne, out Vector3 position))
                continue;

            used.Add(position);
            SpawnOne(prefab, position, parent, player, $"{namePrefix}_{created + 1:00}", bulletPrefab, airborne, created);
            created++;
        }

        return created;
    }

    private static int SpawnDrones(GameObject attackerPrefab, GameObject observerPrefab, int count,
        Transform parent, Transform player, List<Vector3> used, GameObject bulletPrefab)
    {
        List<GameObject> variants = new List<GameObject>();
        if (attackerPrefab != null) variants.Add(attackerPrefab);
        if (observerPrefab != null) variants.Add(observerPrefab);

        // 자폭 드론도 05(폐허된 도시)와 같은 구성이 되도록 섞는다.
        GameObject suiciderPrefab = Resources.Load<GameObject>("AETHERRuntime/Drone_Suicider");
        if (suiciderPrefab != null) variants.Add(suiciderPrefab);

        if (variants.Count == 0 || count <= 0)
            return 0;

        int created = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryFindEnemySpot(player, used, true, out Vector3 position))
                continue;

            used.Add(position);
            SpawnOne(variants[i % variants.Count], position, parent, player,
                $"Zone_Drone_{created + 1:00}", bulletPrefab, true, created);
            created++;
        }

        return created;
    }

    /// <summary>
    /// 적을 놓을 자리. 플레이어에게서 최소 거리 이상 떨어져 있어야 하고(스폰 즉시 피격 방지),
    /// 지상 적은 NavMesh 위에 있어야 추적이 동작한다.
    /// </summary>
    private static bool TryFindEnemySpot(Transform player, List<Vector3> used, bool airborne, out Vector3 result)
    {
        const float minDistance = 12f;
        const float maxDistance = 70f;
        const float minSeparation = 4f;
        const int attempts = 160;

        Vector3 origin = player.position;
        result = Vector3.zero;

        for (int i = 0; i < attempts; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Mathf.Lerp(minDistance, maxDistance, Random.value);
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            Vector3 placed;
            if (airborne)
            {
                if (!TryProjectToGround(candidate, out placed))
                    continue;

                placed += Vector3.up * Random.Range(3f, 7f);

                // 공중 적이 천장이나 구조물 안에 끼지 않도록 확인
                if (Physics.CheckSphere(placed, 1f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
            }
            else
            {
                // 지상 적은 반드시 NavMesh 위에 있어야 한다.
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
                    continue;

                placed = navHit.position;

                if (Physics.CheckSphere(placed + Vector3.up * 1f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
            }

            bool tooClose = false;
            for (int j = 0; j < used.Count; j++)
            {
                if ((used[j] - placed).sqrMagnitude < minSeparation * minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            result = placed;
            return true;
        }

        return false;
    }

    private static bool TryProjectToGround(Vector3 position, out Vector3 grounded)
    {
        grounded = position;

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 12f, NavMesh.AllAreas))
        {
            grounded = navHit.position;
            return true;
        }

        if (Physics.Raycast(position + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            grounded = hit.point;
            return true;
        }

        return false;
    }

    internal static GameObject SpawnOne(GameObject prefab, Vector3 position, Transform parent, Transform player,
        string objectName, GameObject bulletPrefab, bool isDrone, int index)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, parent);
        instance.name = objectName;

        Vector3 direction = player.position - instance.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            instance.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        EnsureEnemyTagAndLayer(instance);
        EnsurePlayerReferences(instance, player);
        ConfigureProjectile(instance, bulletPrefab);
        EnsureReward(instance);
        SettleAgent(instance, isDrone, index);
        return instance;
    }

    // ------------------------------------------------------------------ 크기 보정

    // 플레이어 키를 1로 봤을 때 각 적이 가져야 할 높이 비율.
    private const float RobotHeightRatio = 1.05f;
    private const float DroneHeightRatio = 0.5f;
    private const float TurretHeightRatio = 0.85f;

    /// <summary>
    /// 맵마다 스케일 기준이 달라서 적이 플레이어에 비해 터무니없이 작게(또는 크게) 보이는 문제를
    /// 실행 중에 바로잡는다. 렌더러 경계로 실제 높이를 재고 플레이어 키에 맞춰 비율을 맞춘다.
    /// </summary>
    internal static void MatchEnemyScale(GameObject enemy, float playerHeight, float ratio)
    {
        if (enemy == null || playerHeight <= 0.01f)
            return;

        float currentHeight = MeasureHeight(enemy);
        if (currentHeight <= 0.01f)
            return;

        float target = playerHeight * ratio;
        float factor = target / currentHeight;

        // 측정 오차로 인한 과도한 보정을 막는다.
        if (factor > 0.9f && factor < 1.1f)
            return;

        factor = Mathf.Clamp(factor, 0.25f, 6f);
        enemy.transform.localScale *= factor;

        // NavMeshAgent도 몸집에 맞춰 늘려야 벽에 끼거나 붕 뜨지 않는다.
        NavMeshAgent agent = enemy.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null)
        {
            agent.radius *= factor;
            agent.height *= factor;
        }
    }

    /// <summary>렌더러 전체를 감싸는 월드 높이.</summary>
    private static float MeasureHeight(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return has ? bounds.size.y : 0f;
    }

    internal static float MeasurePlayerHeight(Transform player)
    {
        CharacterController controller = player.GetComponentInChildren<CharacterController>(true);
        if (controller != null && controller.height > 0.01f)
            return controller.height * Mathf.Abs(player.lossyScale.y);

        CapsuleCollider capsule = player.GetComponentInChildren<CapsuleCollider>(true);
        if (capsule != null && capsule.height > 0.01f)
            return capsule.height * Mathf.Abs(player.lossyScale.y);

        float measured = MeasureHeight(player.gameObject);
        return measured > 0.01f ? measured : 1.8f;
    }

    /// <summary>
    /// 캐릭터 하나의 최상위 오브젝트를 찾는다.
    /// transform.root를 쓰면 "AETHER_Zone_RuntimeCombat" 같은 정리용 부모나 씬의 그룹 오브젝트까지
    /// 통째로 잡혀서, 크기를 바꿀 때 맵 전체가 늘어나 버린다.
    /// </summary>
    private static Transform GetCharacterRoot(Transform start)
    {
        Transform current = start;

        while (current.parent != null && BelongsToSameCharacter(current.parent))
            current = current.parent;

        return current;
    }

    private static bool BelongsToSameCharacter(Transform candidate)
    {
        return candidate.GetComponent<Robots>() != null
            || candidate.GetComponent<Drones>() != null
            || candidate.GetComponent<RobotHealth>() != null
            || candidate.GetComponent<DroneHealth>() != null
            || candidate.GetComponent<LegacyDroneHealth>() != null
            || candidate.GetComponent<EnemyTurretController>() != null
            || candidate.GetComponent<NavMeshAgent>() != null
            || candidate.GetComponent<Animator>() != null;
    }

    private static int MatchScaleFor<T>(float playerHeight, float ratio) where T : Component
    {
        int adjusted = 0;
        HashSet<Transform> handled = new HashSet<Transform>();

        T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null)
                continue;

            Transform root = GetCharacterRoot(found[i].transform);
            if (!handled.Add(root))
                continue;

            Vector3 before = root.localScale;
            MatchEnemyScale(root.gameObject, playerHeight, ratio);
            if (root.localScale != before)
                adjusted++;
        }

        return adjusted;
    }

    /// <summary>씬에 미리 배치돼 있던 적과 방금 스폰한 적 모두 플레이어 기준으로 크기를 맞춘다.</summary>
    public static void MatchExistingEnemyScales(Transform player)
    {
        float playerHeight = MeasurePlayerHeight(player);
        if (playerHeight <= 0.01f)
            return;

        int adjusted = 0;
        adjusted += MatchScaleFor<RobotHealth>(playerHeight, RobotHeightRatio);
        adjusted += MatchScaleFor<DroneHealth>(playerHeight, DroneHeightRatio);
        adjusted += MatchScaleFor<LegacyDroneHealth>(playerHeight, DroneHeightRatio);
        adjusted += MatchScaleFor<EnemyTurretController>(playerHeight, TurretHeightRatio);

        if (adjusted > 0)
            Debug.Log($"[AetherZoneBootstrap] 적 {adjusted}체의 크기를 플레이어 키({playerHeight:0.00}m) 기준으로 맞췄습니다.");
    }

    /// <summary>적을 처치했을 때 AP가 들어오도록 보상 컴포넌트를 보장한다.</summary>
    private static void EnsureReward(GameObject enemyRoot)
    {
        if (enemyRoot.GetComponentInChildren<EnemyReward>(true) == null)
            enemyRoot.AddComponent<EnemyReward>();
    }

    private static void EnsurePlayerReferences(GameObject enemyRoot, Transform player)
    {
        Robots[] robots = enemyRoot.GetComponentsInChildren<Robots>(true);
        for (int i = 0; i < robots.Length; i++)
            SetPrivateField(robots[i], "player", player);

        Drones[] drones = enemyRoot.GetComponentsInChildren<Drones>(true);
        for (int i = 0; i < drones.Length; i++)
            SetPrivateField(drones[i], "player", player);
    }

    private static void ConfigureProjectile(GameObject enemyRoot, GameObject bulletPrefab)
    {
        if (bulletPrefab == null)
            return;

        Robots[] robots = enemyRoot.GetComponentsInChildren<Robots>(true);
        for (int i = 0; i < robots.Length; i++)
            SetPrivateField(robots[i], "bulletPrefab", bulletPrefab);

        DroneAttackerCombat[] droneAttackers = enemyRoot.GetComponentsInChildren<DroneAttackerCombat>(true);
        for (int i = 0; i < droneAttackers.Length; i++)
            droneAttackers[i].ConfigureLegacy(bulletPrefab, null, 18f, 10f, 0.8f, 18f, 10, 4f);

        EnemyTurretController[] turrets = enemyRoot.GetComponentsInChildren<EnemyTurretController>(true);
        for (int i = 0; i < turrets.Length; i++)
            turrets[i].Configure(bulletPrefab);
    }

    private static void SettleAgent(GameObject enemyRoot, bool isDrone, int index)
    {
        Animator animator = enemyRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.speed = Mathf.Clamp(0.9f + (index % 5) * 0.05f, 0.9f, 1.12f);

        NavMeshAgent agent = enemyRoot.GetComponentInChildren<NavMeshAgent>(true);
        if (agent == null)
            return;

        if (isDrone)
        {
            // 공중 유닛은 NavMesh 위에 없으므로 에이전트를 꺼서 경고 스팸을 막는다.
            agent.enabled = false;
            return;
        }

        agent.avoidancePriority = Mathf.Clamp(40 + index * 4, 0, 99);

        if (agent.enabled && NavMesh.SamplePosition(enemyRoot.transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
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

    private static void EnsureEnemyTagAndLayer(GameObject enemy)
    {
        try
        {
            enemy.tag = "Enemy";
        }
        catch (UnityException)
        {
        }

        int layer = LayerMask.NameToLayer("Enemy");
        if (layer >= 0)
            SetLayerRecursively(enemy, layer);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
