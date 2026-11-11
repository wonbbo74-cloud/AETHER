using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 구역별 강화파츠 재료(모듈 4개 + 제작설계서 1개)를 맵 안에 숨겨 배치한다.
///
/// 예전 구현은 모든 모듈을 플레이어 바로 앞에 큐브로 쏟아놨다. 그러면
/// "맵을 뒤져서 찾는다"는 설계가 통째로 사라지고, 제작실에서도 다 주울 수 있었다.
/// 지금은 다음 규칙을 따른다.
///  - 씬에 손으로 배치한 모듈이 있으면 그것을 그대로 쓰고 자동 생성하지 않는다.
///  - 제작실(07)에는 재료를 뿌리지 않는다. 재료는 필드에서 구해 와야 한다.
///  - 자동 배치가 필요할 때는 플레이어 시야 밖 / 일정 거리 밖의 지면에 흩어 놓는다.
///  - 이미 주웠거나 제작에 쓴 재료는 다시 생성하지 않는다.
/// </summary>
public class PartSceneModuleBootstrap : MonoBehaviour
{
    private const string RuntimeRootName = "Runtime Parts Modules";

    /// <summary>한 구역에서 모을 수 있는 파츠 1종의 재료 묶음.</summary>
    private struct ModuleSet
    {
        public string scenePrefix;
        public string[] moduleIds;   // 맵에 숨겨둘 모듈 4개
        public string paperItemId;   // 인벤토리에 들어가는 제작설계서 (없으면 빈 문자열)

        public ModuleSet(string scenePrefix, string paperItemId, params string[] moduleIds)
        {
            this.scenePrefix = scenePrefix;
            this.paperItemId = paperItemId;
            this.moduleIds = moduleIds;
        }
    }

    // 구역별로 어떤 파츠 재료가 나오는지. 진행 순서(빌드 순서)와 맞춘다.
    //   05 폐허된 도시 → 슈퍼점프 (1구역 진입에 필요)
    //   08 1구역       → 방패
    //   09 2구역       → 브레이커
    //   11 3구역       → 추가 배치 없음(최종 구역)
    private static readonly ModuleSet[] SceneModuleSets =
    {
        // The 05 blueprint (BookPageWriting4_paper) is not left on the floor. It pops out of the lying robot
        // only after the circuit puzzle is solved (the robot's RewardGunSpawner), so it is not auto-placed here.
        new ModuleSet("05.", string.Empty,
            "BatteryAA_module", "Cable_Black_module", "Crank_module", "SmallKey_module"),
        // 08.1구역은 AetherZone1Director가 배수 후 걸어갈 수 있는 자리에 직접 놓는다.
        // 여기서 무작위로도 뿌리면 물속이나 옥상에 겹쳐 생기므로 넣지 않는다.
        new ModuleSet("09.", string.Empty,
            "breaker_core_module", "power_cell_module", "stabilizer_module", "frame_core_module"),
    };

    // 재료를 절대 자동 생성하지 않는 씬(제작만 하는 공간, 튜토리얼, 메뉴 등)
    private static readonly string[] NeverSpawnScenePrefixes = { "01.", "02.", "03.", "04.", "06.", "07.", "MainMenu", "Tutorial", "Loading", "Puzzle", "puzzle" };

    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableModules)
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
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!TryGetModuleSet(scene.name, out ModuleSet set))
            return;

        // 플레이어가 아직 생성되지 않았을 수 있으므로 러너를 통해 잠시 기다렸다 배치한다.
        PartSceneModuleSpawnRunner.Schedule(scene.name, set.moduleIds, set.paperItemId);
    }

    private static bool TryGetModuleSet(string sceneName, out ModuleSet set)
    {
        set = default;
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < NeverSpawnScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(NeverSpawnScenePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                return false;
        }

        for (int i = 0; i < SceneModuleSets.Length; i++)
        {
            if (sceneName.StartsWith(SceneModuleSets[i].scenePrefix, System.StringComparison.Ordinal))
            {
                set = SceneModuleSets[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>실제 배치. 러너가 플레이어를 찾은 뒤 한 번 호출한다.</summary>
    public static int SpawnMissingModules(string sceneName, string[] moduleIds, string paperItemId, Transform player)
    {
        if (moduleIds == null || player == null)
            return 0;

        List<string> wanted = new List<string>();
        for (int i = 0; i < moduleIds.Length; i++)
            wanted.Add(ItemNameRuleUtility.NormalizeModuleAlias(moduleIds[i]));

        if (!string.IsNullOrWhiteSpace(paperItemId))
            wanted.Add(ItemNameRuleUtility.NormalizeModuleAlias(paperItemId));

        HashSet<string> alreadyInScene = CollectExistingModuleIds();
        Transform root = GetOrCreateRoot();

        List<Vector3> used = new List<Vector3>();
        int created = 0;

        for (int i = 0; i < wanted.Count; i++)
        {
            string id = wanted[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // 씬에 손으로 배치해 둔 것이 있으면 건드리지 않는다.
            if (alreadyInScene.Contains(id))
                continue;

            // 이미 주웠거나 제작에 쓴 재료는 다시 만들지 않는다.
            string key = BuildCollectedKey(sceneName, id);
            if (PartSystemRuntimeState.HasCollectedSceneItem(key))
                continue;

            ItemData item = PartResourceResolver.FindItemData(id);
            if (item == null)
            {
                Debug.LogWarning($"[PartSceneModuleBootstrap] 모듈 데이터를 찾지 못했습니다: {id}");
                continue;
            }

            if (!TryFindHiddenSpot(player, used, out Vector3 position))
                continue;

            used.Add(position);
            CreateRuntimeModule(root, item, position, key, created);
            created++;
        }

        return created;
    }

    private static string BuildCollectedKey(string sceneName, string id)
    {
        return "auto:" + sceneName + ":" + id;
    }

    /// <summary>
    /// 정해진 자리에 모듈 하나를 만든다. 구역 연출 스크립트가 직접 배치할 때 쓴다.
    /// 무작위로 숨기는 SpawnMissingModules와 달리 위치를 지정하지만,
    /// 생김새·상호작용·표식은 05.폐허된 도시와 완전히 같은 경로를 탄다.
    /// </summary>
    /// <returns>이미 주웠으면 null.</returns>
    public static GameObject SpawnModuleAt(ItemData item, Vector3 position, string collectedKey, int visualIndex)
    {
        if (item == null)
            return null;

        if (!string.IsNullOrEmpty(collectedKey) && PartSystemRuntimeState.HasCollectedSceneItem(collectedKey))
            return null;

        Transform root = GetOrCreateRoot();
        return CreateRuntimeModule(root, item, position, collectedKey, visualIndex);
    }

    /// <summary>해당 씬에서 이 모듈을 이미 주웠는지.</summary>
    public static bool IsModuleCollected(string sceneName, string moduleId)
    {
        string id = ItemNameRuleUtility.NormalizeModuleAlias(moduleId);
        return PartSystemRuntimeState.HasCollectedSceneItem(BuildCollectedKey(sceneName, id));
    }

    /// <summary>SpawnModuleAt에 넘길 수집 키를 만든다(씬 이름 기준).</summary>
    public static string MakeCollectedKey(string sceneName, string moduleId)
    {
        return BuildCollectedKey(sceneName, ItemNameRuleUtility.NormalizeModuleAlias(moduleId));
    }

    /// <summary>
    /// 플레이어에게서 일정 거리 떨어져 있고, 시야가 막혀 있는 지면 지점을 찾는다.
    /// NavMesh가 구워져 있으면 그것을, 없으면 위에서 아래로 쏜 레이를 기준으로 삼는다.
    /// </summary>
    private static bool TryFindHiddenSpot(Transform player, List<Vector3> used, out Vector3 result)
    {
        const float minDist = 14f;
        const float maxDist = 55f;
        const float minSep = 6f;
        const int samples = 120;

        Vector3 origin = player.position;
        Vector3 eye = origin + Vector3.up * 1.5f;

        Vector3 bestFallback = Vector3.zero;
        bool hasFallback = false;

        for (int attempt = 0; attempt < samples; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Mathf.Lerp(minDist, maxDist, Random.value);
            Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            if (!TryProjectToGround(candidate, out Vector3 grounded))
                continue;

            if (IsTooClose(grounded, used, minSep))
                continue;

            // 놓을 자리가 벽이나 오브젝트 속이면 제외
            if (Physics.CheckSphere(grounded + Vector3.up * 0.35f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            if (!hasFallback)
            {
                bestFallback = grounded;
                hasFallback = true;
            }

            // 플레이어 시작 지점에서 바로 보이면 '숨겨진' 것이 아니다.
            bool visibleFromStart = !Physics.Linecast(eye, grounded + Vector3.up * 0.35f, ~0, QueryTriggerInteraction.Ignore);
            if (visibleFromStart)
                continue;

            result = grounded;
            return true;
        }

        // 시야가 막힌 곳을 못 찾으면 최소한 거리 조건이라도 만족하는 곳에 둔다.
        result = bestFallback;
        return hasFallback;
    }

    private static bool IsTooClose(Vector3 candidate, List<Vector3> used, float minSeparation)
    {
        for (int i = 0; i < used.Count; i++)
        {
            if ((used[i] - candidate).sqrMagnitude < minSeparation * minSeparation)
                return true;
        }

        return false;
    }

    private static bool TryProjectToGround(Vector3 position, out Vector3 grounded)
    {
        grounded = position;

        // 1순위: NavMesh 위 (플레이어가 실제로 걸어갈 수 있는 곳)
        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 12f, NavMesh.AllAreas))
        {
            grounded = navHit.position + Vector3.up * 0.25f;
            return true;
        }

        // 2순위: 위에서 아래로 레이캐스트
        Vector3 rayStart = position + Vector3.up * 60f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            grounded = hit.point + Vector3.up * 0.25f;
            return true;
        }

        return false;
    }

    private static HashSet<string> CollectExistingModuleIds()
    {
        HashSet<string> ids = new HashSet<string>();

        GameObject[] sceneObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null)
                continue;

            string objectId = ItemNameRuleUtility.NormalizeModuleAlias(sceneObject.name);
            if (ItemNameRuleUtility.IsModuleItem(objectId) || ItemNameRuleUtility.IsPaperObject(objectId))
                ids.Add(objectId);
        }

        ModulePickupInteractable[] modules = FindObjectsByType<ModulePickupInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < modules.Length; i++)
        {
            ModulePickupInteractable module = modules[i];
            if (module == null)
                continue;

            ItemData data = PartResourceResolver.FindItemData(module.name);
            string id = data != null
                ? ItemNameRuleUtility.GetBestItemId(data)
                : ItemNameRuleUtility.NormalizeModuleAlias(module.name);

            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        PaperInteractable[] papers = FindObjectsByType<PaperInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < papers.Length; i++)
        {
            PaperInteractable paper = papers[i];
            if (paper == null)
                continue;

            string id = ItemNameRuleUtility.NormalizeModuleAlias(paper.name);
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        return ids;
    }

    private static Transform GetOrCreateRoot()
    {
        GameObject root = GameObject.Find(RuntimeRootName);
        if (root == null)
            root = new GameObject(RuntimeRootName);

        return root.transform;
    }

    /// <summary>
    /// 모듈 하나를 실제로 만든다.
    /// Resources/AETHERParts/ModulePrefabs/&lt;모듈id&gt; 에 프리팹을 넣어 두면 그 모양을 쓰고,
    /// 없으면 아이템 아이콘을 띄운 픽업 오브젝트를 만든다(예전의 맨 큐브 대체).
    /// </summary>
    private static GameObject CreateRuntimeModule(Transform root, ItemData item, Vector3 position, string collectedKey, int index)
    {
        string id = ItemNameRuleUtility.GetBestItemId(item);
        bool isPaper = ItemNameRuleUtility.IsPaperObject(id);

        GameObject go = InstantiateModuleVisual(id, isPaper, index, out float radius);
        go.name = item.id;
        go.transform.SetParent(root, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);

        // 상호작용용 트리거 콜라이더는 반드시 '루트'에 있어야 한다.
        // ModulePickupInteractable이 RequireComponent(Collider)라서, 루트에 없으면 붙지 않는다.
        //
        // 자식까지 뒤지면 안 된다. 아래에서 Destroy한 아이콘/받침대 콜라이더는 이 프레임 끝까지
        // 살아 있어서 '있다'고 잘못 판단하게 되고, 그러면 루트는 콜라이더 없이 남는다.
        // (그 탓에 1구역 모듈 생성이 통째로 실패하고 있었다.)
        Collider collider = go.GetComponent<Collider>();
        if (collider == null)
        {
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius;
            sphere.center = new Vector3(0f, radius, 0f);
            collider = sphere;
        }

        // 오목한 메시 콜라이더는 트리거가 될 수 없다 — 경고만 쏟아지므로 건드리지 않는다.
        if (!(collider is MeshCollider mesh) || mesh.convex)
            collider.isTrigger = true;

        if (isPaper)
        {
            PaperInteractable paper = go.AddComponent<PaperInteractable>();
            paper.Configure(item);
        }
        else
        {
            ModulePickupInteractable pickup = go.AddComponent<ModulePickupInteractable>();
            pickup.Configure(item, 1, collectedKey);
        }

        ModuleWorldMarker marker = go.AddComponent<ModuleWorldMarker>();
        marker.Configure(item);

        // 천천히 돌고 위아래로 떠서 주울 수 있는 물건이라는 걸 알린다.
        go.AddComponent<ModulePickupSpin>();

        return go;
    }

    /// <summary>모듈 외형. 전용 프리팹이 있으면 그것을, 없으면 아이콘 픽업을 만든다.</summary>
    private static GameObject InstantiateModuleVisual(string id, bool isPaper, int index, out float radius)
    {
        radius = 0.35f;

        GameObject prefab = Resources.Load<GameObject>("AETHERParts/ModulePrefabs/" + id);
        if (prefab != null)
            return Object.Instantiate(prefab);

        Color tint = GetVisualColor(index);

        GameObject rootObject = new GameObject("Module Pickup");

        // 바닥의 빛 웅덩이 — 어두운 맵에서 멀리서도 보인다.
        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(rootObject.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        glow.type = LightType.Point;
        glow.color = tint;
        glow.range = 4.5f;
        glow.intensity = 2.2f;
        glow.shadows = LightShadows.None;

        // 아이템 아이콘을 세워 놓은 판
        GameObject iconObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconObject.name = "Icon";
        iconObject.transform.SetParent(rootObject.transform, false);
        iconObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        iconObject.transform.localScale = isPaper ? new Vector3(0.55f, 0.7f, 1f) : new Vector3(0.5f, 0.5f, 1f);

        Collider iconCollider = iconObject.GetComponent<Collider>();
        if (iconCollider != null)
            Object.Destroy(iconCollider);

        Renderer iconRenderer = iconObject.GetComponent<Renderer>();
        if (iconRenderer != null)
            iconRenderer.material = CreateIconMaterial(id, tint);

        // 항상 카메라를 보도록
        iconObject.AddComponent<Billboard>();

        // 받침대
        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = "Base";
        baseObject.transform.SetParent(rootObject.transform, false);
        baseObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        baseObject.transform.localScale = new Vector3(0.34f, 0.03f, 0.34f);

        Collider baseCollider = baseObject.GetComponent<Collider>();
        if (baseCollider != null)
            Object.Destroy(baseCollider);

        Renderer baseRenderer = baseObject.GetComponent<Renderer>();
        if (baseRenderer != null)
            baseRenderer.material = CreateEmissiveMaterial(tint * 0.6f);

        return rootObject;
    }

    private static Material CreateIconMaterial(string id, Color tint)
    {
        ItemData item = PartResourceResolver.FindItemData(id);
        Sprite sprite = item != null ? item.itemSprite : null;
        return AetherShaders.CreateIcon(sprite != null ? sprite.texture : null, tint);
    }

    private static Material CreateEmissiveMaterial(Color color)
    {
        return AetherShaders.CreateSolid(color);
    }

    private static Color GetVisualColor(int index)
    {
        Color[] colors =
        {
            new Color(0.25f, 0.85f, 1f),
            new Color(1f, 0.78f, 0.25f),
            new Color(0.45f, 1f, 0.55f),
            new Color(1f, 0.45f, 0.35f),
            new Color(0.88f, 0.88f, 0.78f)
        };

        return colors[index % colors.Length];
    }
}

/// <summary>주울 수 있는 물건이라는 걸 알리는 회전 + 부유 연출.</summary>
public class ModulePickupSpin : MonoBehaviour
{
    [SerializeField] private float spinDegreesPerSecond = 45f;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 1.6f;

    private Vector3 basePosition;
    private float phase;

    private void Start()
    {
        basePosition = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);

        float offset = Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        transform.position = basePosition + Vector3.up * offset;
    }
}

/// <summary>
/// 플레이어가 씬에 생성될 때까지 기다렸다가 모듈 배치를 한 번만 실행한다.
/// (플레이어는 AetherZoneBootstrap이 나중에 만들어 줄 수도 있다.)
/// </summary>
public class PartSceneModuleSpawnRunner : MonoBehaviour
{
    private const float WaitSeconds = 6f;

    private static readonly HashSet<string> spawnedScenes = new HashSet<string>();

    private string sceneName;
    private string[] moduleIds;
    private string paperItemId;
    private float timer;

    public static void Schedule(string sceneName, string[] moduleIds, string paperItemId)
    {
        if (spawnedScenes.Contains(sceneName))
            return;

        GameObject host = new GameObject("PartSceneModuleSpawnRunner");
        PartSceneModuleSpawnRunner runner = host.AddComponent<PartSceneModuleSpawnRunner>();
        runner.sceneName = sceneName;
        runner.moduleIds = moduleIds;
        runner.paperItemId = paperItemId;
        runner.timer = WaitSeconds;
    }

    /// <summary>씬을 다시 들어갔을 때 재배치가 필요하면 호출한다.</summary>
    public static void ClearSpawnedFlag(string sceneName)
    {
        spawnedScenes.Remove(sceneName);
    }

    private void Update()
    {
        Transform player = FindPlayerTransform();
        if (player == null)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                Debug.LogWarning($"[PartSceneModuleBootstrap] '{sceneName}'에서 플레이어를 찾지 못해 모듈 배치를 건너뜁니다.");
                Destroy(gameObject);
            }

            return;
        }

        if (spawnedScenes.Add(sceneName))
        {
            int created = PartSceneModuleBootstrap.SpawnMissingModules(sceneName, moduleIds, paperItemId, player);
            if (created > 0)
                Debug.Log($"[PartSceneModuleBootstrap] '{sceneName}'에 모듈/설계도 {created}개를 숨겨 배치했습니다.");
        }

        Destroy(gameObject);
    }

    private static Transform FindPlayerTransform()
    {
        Move_Hankyul hankyul = FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Exclude);
        if (hankyul != null)
            return hankyul.transform;

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            return tagged.transform;

        Move_Yunseo yunseo = FindFirstObjectByType<Move_Yunseo>(FindObjectsInactive.Exclude);
        return yunseo != null ? yunseo.transform : null;
    }
}
