using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 06 연구소 메인 장소: 시작 복도에서 엘리베이터까지 가는 길에 볼거리를 놓는다.
///
/// - 연구 기록 단말 5개: F 로 짧은 기록을 읽는다. 처음 읽을 때 에테르 경험치.
/// - 표본 탱크: 큰 홀 왼쪽 벽. 가까이 가면 안의 것이 돌아보고 유리를 두드린다.
/// - 정전 구역: 큰 홀에 들어서면 불이 깜빡이다 꺼지고, 붉은 경광등이 돌고, 천장 탐조등이 플레이어를 쫓는다.
///   선반 너머에서 비상 전원이 돌아온다.
/// - 보안 레이저: 위아래로 움직이는 붉은 감지선 3줄. 닿으면 아주 약한 피해와 함께 경보가 울리고 경비 로봇이 온다.
/// - 보안 게이트: 엘리베이터 앞을 막는 레이저 벽. 옆 단말기에서 숫자 해독(퍼즐3, 숫자야구)을 풀면 열린다.
///
/// 씬 파일은 건드리지 않고 실행 중에 만든다. 퍼즐에서 돌아오면 씬이 다시 열리므로, 이미 본 연출은 정적 값으로 기억한다.
/// </summary>
public class AetherLabCorridor : MonoBehaviour
{
    public const string GatePuzzleId = "06-security-gate";
    private const string PuzzleSceneName = "Puzzle3";

    // ---- 자리(06 실측) ----
    // 시작 복도: x -17.4 ~ -5.4, z -84 ~ -37 / 큰 홀: x -20.4 ~ -2.4, z -35 ~ 36, 천장 9.5 / 엘리베이터 (-11.5, 33.5)
    private static readonly Vector3[] LogSpots =
    {
        new Vector3(-16.7f, 0f, -66f),
        new Vector3(-6.1f, 0f, -50f),
        new Vector3(-19.7f, 0f, -29f),
        new Vector3(-3.1f, 0f, -17f),
        new Vector3(-19.4f, 0f, 19f),
    };

    private static readonly string[] LogTitles =
    {
        "연구 일지 #014",
        "시설 관리 메모",
        "표본 관리 기록",
        "보안 점검표",
        "누군가의 메모",
    };

    private static readonly string[] LogBodies =
    {
        "에테르 공명 실험 3차.\n출력이 예상치의 네 배를 넘었다. 경비 로봇들이 공명 신호에 반응해 순찰을 멈추지 않는다.\n실험은 중단한다. …중단할 수 있다면.",
        "정전 시 비상 전원은 엘리베이터 쪽 회로에만 공급된다.\n큰 홀 조명은 포기할 것. 탐조등은 움직이는 것을 끝까지 따라간다.\n선반 구역까지만 버티면 불이 돌아온다.",
        "7번 탱크 개체가 또 유리를 두드렸다.\n진정제를 두 배로 늘렸지만 사람이 다가가면 눈을 뜬다.\n탱크 유리는 아직 버틴다. 아직은.",
        "복도 보안 레이저: 감지선 세 줄이 위아래로 움직인다.\n선에 닿으면 경보와 함께 경비 로봇이 출동한다.\n선이 머리 위로 올라갔을 때 지나가면 된다.",
        "엘리베이터 앞 보안 게이트 해독 코드.\n네 자리, 같은 숫자는 두 번 쓰지 않는다.\nS 는 자리까지 맞은 숫자, B 는 숫자만 맞은 것.\n기회는 여섯 번, 2분 안에 풀 것.",
    };

    private const int LogExp = 50;

    private static readonly Vector3 TankSpot = new Vector3(-18.4f, 0f, -22f);
    private const float TankWakeDistance = 7f;

    private const float BlackoutStartZ = -33f;
    private const float BlackoutEndZ = 14f;

    private static readonly float[] LaserZ = { -13f, -10f, -7f };
    private const float HallLeftX = -20.3f;
    private const float HallRightX = -2.5f;
    private const float LaserCooldown = 12f;
    private const int LaserMaxSpawns = 3;

    private const float GateZ = 25f;
    private static readonly Vector3 GateTerminalSpot = new Vector3(-3.2f, 0f, 21f);

    // ---- 씬을 다시 열어도 남는 상태 ----
    private static readonly HashSet<int> logsRead = new HashSet<int>();
    private static bool tankWoken;
    private static bool blackoutDone;
    private static int laserSpawns;
    private static bool gateAnnounced;

    private Transform player;
    private float playerHeight = 1.8f;
    private static Material additive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("06.") || FindFirstObjectByType<AetherLabCorridor>() != null)
            return;

        GameObject go = new GameObject("AETHER_LabCorridor");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherLabCorridor>();
    }

    private IEnumerator Start()
    {
        // 분위기(AetherAtmosphere)·적 배치가 끝난 뒤에 손댄다.
        yield return null;
        yield return null;

        for (int i = 0; i < LogSpots.Length; i++)
            BuildLogTerminal(i);
        BuildTank();
        BuildLasers();
        BuildGate();
        if (!blackoutDone)
            gameObject.AddComponent<LabBlackout>().Init(this);
    }

    private void Update()
    {
        if (player == null)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            if (player != null)
                playerHeight = AetherZoneBootstrap.MeasurePlayerHeight(player);
        }
    }

    public Transform Player => player;
    public float PlayerHeight => playerHeight;

    // ------------------------------------------------------------------ 공용

    internal static Material Additive(Color tint)
    {
        if (additive == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            additive = new Material(shader) { name = "AETHER_LabAdditive" };
        }
        Material m = new Material(additive);
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
        m.color = tint;
        return m;
    }

    internal static Material Solid(Color albedo, Color emission)
    {
        Shader shader = Shader.Find("Standard");
        Material m = new Material(shader) { name = "AETHER_LabSolid" };
        m.color = albedo;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.55f);
        if (emission.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
        return m;
    }

    internal static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        if (!keepCollider) Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    internal static Light PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
    {
        GameObject g = new GameObject("Light");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    internal static LineRenderer Beam(Transform parent, Material mat, float width)
    {
        GameObject g = new GameObject("Beam");
        g.transform.SetParent(parent, false);
        LineRenderer line = g.AddComponent<LineRenderer>();
        line.sharedMaterial = mat;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    /// <summary>바닥 높이. 위에서 아래로 쏴서 처음 닿는 곳.</summary>
    internal static float FloorAt(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return p.y;
    }

    /// <summary>홀 가운데(x -11.5)를 바라보는 회전.</summary>
    private static Quaternion FaceHall(Vector3 p)
    {
        return Quaternion.LookRotation(new Vector3(-11.5f - p.x, 0f, 0f).normalized, Vector3.up);
    }

    // ------------------------------------------------------------------ 연구 기록

    private void BuildLogTerminal(int index)
    {
        Vector3 p = LogSpots[index];
        p.y = FloorAt(p);

        GameObject root = new GameObject("ResearchLog_" + (index + 1));
        root.transform.SetParent(transform, false);
        root.transform.SetPositionAndRotation(p, FaceHall(p));

        Material body = Solid(new Color(0.12f, 0.14f, 0.16f), Color.black);
        bool read = logsRead.Contains(index);
        Material screen = Solid(Color.black, read ? new Color(0.15f, 0.45f, 0.55f) : new Color(0.25f, 1.1f, 1.4f));

        Primitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.55f, 1f, 0.4f), body);
        GameObject head = Primitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.15f, 0.05f), new Vector3(0.65f, 0.42f, 0.08f), body);
        head.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        // 쿼드는 -z 쪽에서 보이므로 뒤집은 뒤, 머리 부분과 같이 위로 20° 젖힌다.
        GameObject glass = Primitive(PrimitiveType.Quad, root.transform, new Vector3(0f, 1.165f, 0.093f), new Vector3(0.56f, 0.34f, 1f), screen);
        glass.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);

        Light glow = PointLight(root.transform, new Vector3(0f, 1.3f, 0.5f), new Color(0.3f, 0.85f, 1f), 3f, read ? 0.6f : 1.6f);

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.75f, 0f);
        box.size = new Vector3(0.9f, 1.6f, 0.8f);

        AetherResearchLog log = root.AddComponent<AetherResearchLog>();
        log.Init(index, LogTitles[index], LogBodies[index], screen, glow, logsRead.Contains(index));
    }

    internal static void MarkLogRead(int index)
    {
        if (!logsRead.Add(index))
            return;
        AetherLevel.ReportPuzzle("06-log-" + (index + 1), LogExp, "연구 기록 해독");
    }

    // ------------------------------------------------------------------ 표본 탱크

    private void BuildTank()
    {
        Vector3 p = TankSpot;
        p.y = FloorAt(p);
        GameObject root = new GameObject("SpecimenTank");
        root.transform.SetParent(transform, false);
        root.transform.position = p;

        Material metal = Solid(new Color(0.1f, 0.11f, 0.12f), Color.black);
        Material trim = Solid(new Color(0.08f, 0.1f, 0.08f), new Color(0.15f, 0.9f, 0.35f));
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 0.2f, 2.4f), metal, true);
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(2.25f, 0.02f, 2.25f), trim);
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3.75f, 0f), new Vector3(2.4f, 0.15f, 2.4f), metal);
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3.58f, 0f), new Vector3(2.25f, 0.02f, 2.25f), trim);
        // 천장으로 이어지는 관.
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 6.5f, 0f), new Vector3(0.35f, 2.7f, 0.35f), metal);

        // 액체(초록 발광)와 유리(옅은 청록).
        GameObject liquid = Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(2.05f, 1.55f, 2.05f), Additive(new Color(0.05f, 0.45f, 0.18f, 0.5f)));
        Primitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(2.2f, 1.6f, 2.2f), Additive(new Color(0.1f, 0.25f, 0.3f, 0.35f)));

        // 유리는 통과하지 못하게.
        CapsuleCollider wall = root.AddComponent<CapsuleCollider>();
        wall.center = new Vector3(0f, 2f, 0f);
        wall.radius = 1.15f;
        wall.height = 4f;

        Light inner = PointLight(root.transform, new Vector3(0f, 2.2f, 0f), new Color(0.25f, 1f, 0.45f), 7f, 2.2f);
        Light eye = PointLight(root.transform, new Vector3(0f, 2.6f, 0.35f), new Color(1f, 0.1f, 0.05f), 2.5f, 0f);

        Transform specimen = MakeSpecimen(root.transform, new Vector3(0f, 0.45f, 0f), 2.4f);

        AetherSpecimenTank tank = root.AddComponent<AetherSpecimenTank>();
        tank.Init(this, specimen, inner, eye, liquid.transform, tankWoken);
    }

    internal static void MarkTankWoken() => tankWoken = true;

    /// <summary>경비 로봇 모양만 빌려 온다. 움직임·전투·충돌은 모두 떼어 낸다.</summary>
    private static Transform MakeSpecimen(Transform parent, Vector3 localPos, float height)
    {
        GameObject prefab = Resources.Load<GameObject>("AETHERRuntime/Robot");
        if (prefab == null)
            return null;

        GameObject holder = new GameObject("SpecimenHolder");
        holder.SetActive(false);
        GameObject g = Instantiate(prefab, holder.transform);
        g.name = "Specimen";

        // 체력바 같은 UI 는 통째로 뗀다.
        foreach (Canvas canvas in g.GetComponentsInChildren<Canvas>(true))
            if (canvas != null) DestroyImmediate(canvas.gameObject);

        // 모양(메시·뼈·애니메이터)만 남긴다. 서로 기대는 부품이 있어 몇 번 돈다.
        for (int pass = 0; pass < 4; pass++)
        {
            foreach (Component c in g.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is Animator)
                    continue;
                if (pass < 2 && !(c is MonoBehaviour))
                    continue;   // 스크립트를 먼저 떼야 RequireComponent 로 묶인 부품이 풀린다
                DestroyImmediate(c);
            }
        }
        foreach (Animator a in g.GetComponentsInChildren<Animator>(true))
            a.applyRootMotion = false;

        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Destroy(holder);

        // 높이를 탱크에 맞춘다.
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any && b.size.y > 0.01f)
            g.transform.localScale *= height / b.size.y;

        // 초록 액체 속이라 어둡고 푸르스름하게.
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material[] mats = r.materials;
            foreach (Material m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty("_Color")) m.color = m.color * new Color(0.45f, 0.7f, 0.55f, 1f);
            }
            r.materials = mats;
        }
        return g.transform;
    }

    // ------------------------------------------------------------------ 보안 레이저

    private void BuildLasers()
    {
        GameObject root = new GameObject("SecurityLasers");
        root.transform.SetParent(transform, false);
        root.AddComponent<AetherSecurityLasers>().Init(this, LaserZ, HallLeftX, HallRightX, FloorAt(new Vector3(-11.5f, 0f, -10f)));
    }

    internal bool CanSpawnResponders => laserSpawns < LaserMaxSpawns;
    internal float Cooldown => LaserCooldown;

    /// <summary>경보: 경비 로봇 두 대가 앞·뒤에서 달려온다.</summary>
    internal void SpawnResponders()
    {
        if (player == null || laserSpawns >= LaserMaxSpawns)
            return;
        laserSpawns++;

        GameObject robotPrefab = Resources.Load<GameObject>("AETHERRuntime/Robot");
        GameObject robotBullet = Resources.Load<GameObject>("AETHERRuntime/RobotBullet");
        if (robotPrefab == null)
            return;

        Vector3[] spots = { new Vector3(-11.5f, 0f, 9f), new Vector3(-11.5f, 0f, -22f) };
        for (int i = 0; i < spots.Length; i++)
        {
            if (!NavMesh.SamplePosition(spots[i], out NavMeshHit hit, 5f, NavMesh.AllAreas))
                continue;
            GameObject robot = AetherZoneBootstrap.SpawnOne(robotPrefab, hit.position, transform, player,
                "Lab_AlarmRobot_" + laserSpawns + "_" + i, robotBullet, false, 20 + laserSpawns * 2 + i);
            if (robot == null) continue;
            AetherZoneBootstrap.MatchEnemyScale(robot, playerHeight, 1.05f);
            Robots ai = robot.GetComponentInChildren<Robots>();
            if (ai != null) ai.OnObserverAlert(player.position, 12f);
        }
    }

    // ------------------------------------------------------------------ 보안 게이트

    private void BuildGate()
    {
        bool cleared = PuzzleSceneFlowState.IsPuzzleCleared(GatePuzzleId);
        if (cleared && gateAnnounced)
        {
            BuildGateTerminal(true);
            return;
        }

        GameObject root = new GameObject("SecurityGate");
        root.transform.SetParent(transform, false);
        float floor = FloorAt(new Vector3(-11.5f, 0f, GateZ - 1f));
        root.transform.position = new Vector3(0f, floor, GateZ);

        BoxCollider block = root.AddComponent<BoxCollider>();
        block.center = new Vector3((HallLeftX + HallRightX) * 0.5f, 5f, 0f);
        block.size = new Vector3(HallRightX - HallLeftX + 0.4f, 10f, 0.3f);

        AetherSecurityGate gate = root.AddComponent<AetherSecurityGate>();
        gate.Init(HallLeftX, HallRightX, floor);

        BuildGateTerminal(cleared);

        if (cleared)
        {
            gateAnnounced = true;
            StartCoroutine(OpenGateAfterReturn(gate));
        }
    }

    private IEnumerator OpenGateAfterReturn(AetherSecurityGate gate)
    {
        yield return new WaitForSeconds(1.2f);
        AetherLevel.ReportPuzzle(GatePuzzleId, AetherLevel.CircuitPuzzleExp, "보안 코드 해독");
        AetherObjectiveToast.Show("보안 게이트 해제 — 엘리베이터로 이동하라", 5f);
        gate.Open();
    }

    private void BuildGateTerminal(bool cleared)
    {
        Vector3 p = GateTerminalSpot;
        p.y = FloorAt(p);
        GameObject root = new GameObject("GateTerminal");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        root.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.left));

        Material body = Solid(new Color(0.12f, 0.12f, 0.13f), Color.black);
        Color screenColor = cleared ? new Color(0.2f, 1.2f, 0.5f) : new Color(1.4f, 0.15f, 0.1f);
        Material screen = Solid(Color.black, screenColor);
        Primitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.45f), body, true);
        GameObject glass = Primitive(PrimitiveType.Quad, root.transform, new Vector3(0f, 1.3f, 0.065f), new Vector3(0.62f, 0.4f, 1f), screen);
        glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Primitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.48f, 0.12f), body);
        PointLight(root.transform, new Vector3(0f, 1.4f, 0.6f), cleared ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.2f, 0.15f), 3.5f, 1.8f);

        if (!cleared)
        {
            ProximityPuzzleOpener opener = root.AddComponent<ProximityPuzzleOpener>();
            Set(opener, "openPuzzleScene", true);
            Set(opener, "puzzleSceneName", PuzzleSceneName);
            Set(opener, "puzzleId", GatePuzzleId);
            Set(opener, "useDistanceFallback", true);
            Set(opener, "interactDistance", 2.6f);
            Set(opener, "promptTitle", "보안 단말기");
            Set(opener, "promptActionText", "보안 코드 해독");
        }
        root.SetActive(true);
    }

    private static void Set(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(target, value);
    }

    internal static void MarkBlackoutDone() => blackoutDone = true;
    internal static float BlackoutStart => BlackoutStartZ;
    internal static float BlackoutEnd => BlackoutEndZ;
    internal static float LeftX => HallLeftX;
    internal static float RightX => HallRightX;
}

// ====================================================================== 연구 기록 단말

/// <summary>F 로 읽는 연구 기록. 읽는 동안 게임을 멈추고, F·ESC 로 닫는다.</summary>
public class AetherResearchLog : MonoBehaviour, IInteractable, IPickupInteractable
{
    private int index;
    private string title;
    private string body;
    private Material screen;
    private Light glow;
    private bool read;

    public void Init(int index, string title, string body, Material screen, Light glow, bool alreadyRead)
    {
        this.index = index;
        this.title = title;
        this.body = body;
        this.screen = screen;
        this.glow = glow;
        read = alreadyRead;
    }

    public string GetDisplayName() => read ? "연구 기록 다시 읽기" : "연구 기록 읽기";
    public Sprite GetIcon() => null;

    public void Interact(PlayerInteractor interactor)
    {
        // 닫는 F 가 같은 프레임에 다시 여는 F 로 읽히지 않게.
        if (AetherResearchLogView.IsOpen || AetherUIState.ConsumedCloseThisFrame)
            return;

        AetherAudio.PlayAt("ui_open", transform.position + Vector3.up, 0.7f);
        AetherResearchLogView.Open(title, body, () =>
        {
            if (!read)
            {
                read = true;
                AetherLabCorridor.MarkLogRead(index);
                if (screen != null) screen.SetColor("_EmissionColor", new Color(0.15f, 0.45f, 0.55f));
                if (glow != null) glow.intensity = 0.6f;
            }
        });
    }

    private void Update()
    {
        // 안 읽은 단말은 화면이 숨 쉬듯 깜빡여 눈에 띈다.
        if (read || glow == null) return;
        glow.intensity = 1.2f + Mathf.Sin(Time.time * 3f + index) * 0.6f;
    }
}

/// <summary>연구 기록 읽기 화면(한 번에 하나).</summary>
public class AetherResearchLogView : MonoBehaviour
{
    private const string FontPath = "UI/Fonts/NanumGothicCoding-Bold SDF";
    private static AetherResearchLogView current;
    public static bool IsOpen => current != null;

    private System.Action onClose;
    private static bool previousHangulRule;
    private int openFrame;

    public static void Open(string title, string body, System.Action onClose)
    {
        if (current != null) return;

        // 한글이 낱자 사이("신 / 호")가 아니라 띄어쓰기에서 줄바꿈되게. 닫을 때 되돌린다.
        previousHangulRule = TMPro.TMP_Settings.useModernHangulLineBreakingRules;
        TMPro.TMP_Settings.useModernHangulLineBreakingRules = true;

        GameObject root = new GameObject("AETHER_ResearchLogView");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        UnityEngine.UI.CanvasScaler scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TMPro.TMP_FontAsset font = Resources.Load<TMPro.TMP_FontAsset>(FontPath);

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        dim.transform.SetParent(root.transform, false);
        RectTransform dr = (RectTransform)dim.transform;
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        dim.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0.01f, 0.02f, 0.7f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(root.transform, false);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.03f, 0.08f, 0.1f, 0.96f);
        UnityEngine.UI.Outline o = panel.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = new Color(0.25f, 0.85f, 1f, 0.8f);
        o.effectDistance = new Vector2(3f, -3f);

        // 글 길이에 맞춰 판 높이를 정하고 모두 가운데 정렬한다(예전엔 고정 높이라 아래가 비었다).
        const float PanelWidth = 860f;
        const float TextWidth = 760f;
        var center = TMPro.TextAlignmentOptions.Center;
        TMPro.TextMeshProUGUI label = Text(panel.transform, font, "RESEARCH LOG", 20f, new Color(0.35f, 0.8f, 0.95f, 0.8f), TextWidth, center);
        TMPro.TextMeshProUGUI head = Text(panel.transform, font, title, 42f, new Color(0.8f, 0.97f, 1f), TextWidth, center);
        TMPro.TextMeshProUGUI text = Text(panel.transform, font, body, 28f, new Color(0.82f, 0.9f, 0.92f), TextWidth, center);
        TMPro.TextMeshProUGUI close = Text(panel.transform, font, "<color=#7FE8FF>[F]</color> 닫기", 24f, new Color(0.7f, 0.85f, 0.9f), TextWidth, center);

        GameObject line = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        line.transform.SetParent(panel.transform, false);
        line.GetComponent<UnityEngine.UI.Image>().color = new Color(0.25f, 0.85f, 1f, 0.35f);

        float bodyHeight = text.GetPreferredValues(body, TextWidth, 0f).y;
        float[] heights = { 28f, 54f, 2f, bodyHeight, 32f };
        float[] gaps = { 6f, 18f, 26f, 30f };
        const float PadTop = 38f, PadBottom = 30f;
        float total = PadTop + PadBottom;
        foreach (float h in heights) total += h;
        foreach (float g in gaps) total += g;
        ((RectTransform)panel.transform).sizeDelta = new Vector2(PanelWidth, total);

        RectTransform[] rows = { label.rectTransform, head.rectTransform, (RectTransform)line.transform, text.rectTransform, close.rectTransform };
        float y = total * 0.5f - PadTop;
        for (int i = 0; i < rows.Length; i++)
        {
            float w = i == 2 ? 520f : TextWidth;
            rows[i].sizeDelta = new Vector2(w, heights[i]);
            rows[i].anchoredPosition = new Vector2(0f, y - heights[i] * 0.5f);
            y -= heights[i] + (i < gaps.Length ? gaps[i] : 0f);
        }

        current = root.AddComponent<AetherResearchLogView>();
        current.onClose = onClose;
        current.openFrame = Time.frameCount;
        AetherUIState.BeginScreen(true);
        AetherAudio.Play2D("paper_open", 0.6f);
    }

    private static TMPro.TextMeshProUGUI Text(Transform parent, TMPro.TMP_FontAsset font, string s, float size, Color c, float width, TMPro.TextAlignmentOptions align)
    {
        GameObject g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TMPro.TextMeshProUGUI t = g.AddComponent<TMPro.TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.lineSpacing = 12f;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(width, 40f);
        return t;
    }

    private void Update()
    {
        if (Time.frameCount <= openFrame + 1)
            return;

        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            Close();
    }

    private void Close()
    {
        if (current != this) return;
        current = null;
        AetherAudio.Play2D("paper_close", 0.6f);
        TMPro.TMP_Settings.useModernHangulLineBreakingRules = previousHangulRule;
        AetherUIState.EndScreen();
        System.Action cb = onClose;
        Destroy(gameObject);
        cb?.Invoke();
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
            AetherUIState.EndScreen();
        }
    }
}

// ====================================================================== 표본 탱크

/// <summary>가까이 가면 안의 것이 돌아보고 붉은 눈이 켜지며 유리를 두드린다. 그 뒤로도 플레이어를 눈으로 쫓는다.</summary>
public class AetherSpecimenTank : MonoBehaviour
{
    private AetherLabCorridor owner;
    private Transform specimen;
    private Light inner;
    private Light eye;
    private Transform liquid;
    private bool woken;
    private Vector3 specimenBase;
    private readonly List<Transform> bubbles = new List<Transform>();

    public void Init(AetherLabCorridor owner, Transform specimen, Light inner, Light eye, Transform liquid, bool alreadyWoken)
    {
        this.owner = owner;
        this.specimen = specimen;
        this.inner = inner;
        this.eye = eye;
        this.liquid = liquid;
        woken = alreadyWoken;
        if (specimen != null) specimenBase = specimen.localPosition;

        Material bubbleMat = AetherLabCorridor.Additive(new Color(0.4f, 1f, 0.6f, 0.6f));
        for (int i = 0; i < 14; i++)
        {
            float s = Random.Range(0.04f, 0.1f);
            GameObject b = AetherLabCorridor.Primitive(PrimitiveType.Sphere, transform,
                new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.5f, 3.4f), Random.Range(-0.8f, 0.8f)), Vector3.one * s, bubbleMat);
            bubbles.Add(b.transform);
        }

        AetherAudio.StartLoop("hold_loop", transform, 0.18f, 1.5f, 10f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (Transform b in bubbles)
        {
            Vector3 p = b.localPosition;
            p.y += dt * (0.5f + b.localScale.x * 6f);
            p.x += Mathf.Sin(Time.time * 3f + p.y * 4f) * dt * 0.1f;
            if (p.y > 3.45f) p = new Vector3(Random.Range(-0.8f, 0.8f), 0.5f, Random.Range(-0.8f, 0.8f));
            b.localPosition = p;
        }

        if (inner != null)
            inner.intensity = 2.2f + Mathf.Sin(Time.time * 1.3f) * 0.3f;

        Transform player = owner != null ? owner.Player : null;
        if (player == null || specimen == null)
            return;

        float dist = Vector3.Distance(new Vector3(player.position.x, 0f, player.position.z), new Vector3(transform.position.x, 0f, transform.position.z));

        // 둥둥 떠 있다.
        specimen.localPosition = specimenBase + Vector3.up * Mathf.Sin(Time.time * 0.7f) * 0.06f;

        if (!woken)
        {
            if (dist < TankWake)
            {
                woken = true;
                AetherLabCorridor.MarkTankWoken();
                StartCoroutine(Wake());
            }
            return;
        }

        if (dist < 14f)
        {
            Vector3 to = player.position - specimen.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                specimen.rotation = Quaternion.Slerp(specimen.rotation, Quaternion.LookRotation(to), 1.5f * dt);
            if (eye != null && !waking)
                eye.intensity = Mathf.Lerp(eye.intensity, 2.5f + Mathf.Sin(Time.time * 6f) * 0.8f, 4f * dt);
        }
        else if (eye != null && !waking)
        {
            eye.intensity = Mathf.Lerp(eye.intensity, 0f, 2f * dt);
        }
    }

    private const float TankWake = 9.5f;   // 홀 가운데(탱크에서 옆으로 7m)로 지나가도 깨도록
    private bool waking;

    private IEnumerator Wake()
    {
        waking = true;
        Transform player = owner.Player;

        // 1) 홱 돌아본다.
        float t = 0f;
        Quaternion from = specimen.rotation;
        Vector3 to = player.position - specimen.position; to.y = 0f;
        Quaternion look = Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to : Vector3.forward);
        AetherAudio.PlayAt("robot_servo", transform.position + Vector3.up * 2f, 1f);
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            specimen.rotation = Quaternion.Slerp(from, look, t / 0.35f);
            if (eye != null) eye.intensity = Mathf.Lerp(0f, 6f, t / 0.35f);
            yield return null;
        }

        AetherObjectiveToast.Show("탱크 안의 무언가가 움직였다…", 3.5f);

        // 2) 유리를 세 번 두드린다.
        Vector3 basePos = transform.position;
        for (int i = 0; i < 3; i++)
        {
            AetherAudio.PlayAt("robot_hit", basePos + Vector3.up * 2f, 1f, 2f, 30f, 0.1f, 0.7f);
            if (i == 2) AetherAudio.PlayAt("spark", basePos + Vector3.up * 2.4f, 0.9f);
            float s = 0f;
            Vector3 lunge = (player.position - basePos); lunge.y = 0f; lunge = lunge.normalized * 0.25f;
            while (s < 0.25f)
            {
                s += Time.deltaTime;
                float k = Mathf.Sin(s / 0.25f * Mathf.PI);
                specimen.localPosition = specimenBase + transform.InverseTransformVector(lunge) * k;
                transform.position = basePos + Random.insideUnitSphere * 0.03f * k;
                if (inner != null) inner.color = Color.Lerp(new Color(0.25f, 1f, 0.45f), new Color(1f, 0.25f, 0.15f), k);
                yield return null;
            }
            transform.position = basePos;
            yield return new WaitForSeconds(0.25f);
        }
        if (inner != null) inner.color = new Color(0.25f, 1f, 0.45f);
        waking = false;
    }
}

// ====================================================================== 정전

/// <summary>
/// 큰 홀에 들어서면 불이 깜빡이다 나간다. 붉은 경광등이 돌고 사이렌이 울리며, 천장 탐조등 두 개가 플레이어를 늦게 쫓는다.
/// 선반 구역(z 14)을 지나면 비상 전원이 돌아온다.
/// </summary>
public class LabBlackout : MonoBehaviour
{
    private AetherLabCorridor owner;
    private enum Phase { Waiting, Dark, Done }
    private Phase phase = Phase.Waiting;

    private readonly List<Light> ceiling = new List<Light>();
    private readonly List<Transform> beacons = new List<Transform>();
    private readonly List<Light> beaconLights = new List<Light>();
    private readonly List<Light> searchlights = new List<Light>();
    private readonly List<Vector3> searchAim = new List<Vector3>();
    private GameObject beaconRoot;
    private AetherAudioLoop siren;
    private AetherAudioLoop buzz;

    private float savedAmbient;
    private UnityEngine.Rendering.PostProcessing.PostProcessVolume darkVolume;

    /// <summary>06 홀은 구운 라이트맵으로 밝아 환경광만 줄여서는 어두워지지 않는다. 노출을 낮추는 볼륨을 잠시 덮는다.</summary>
    private void SetDark(float weight)
    {
        if (darkVolume == null)
        {
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.PostProcessing.PostProcessProfile>();
            var grading = profile.AddSettings<UnityEngine.Rendering.PostProcessing.ColorGrading>();
            grading.enabled.Override(true);
            grading.postExposure.Override(-1.9f);
            grading.saturation.Override(-25f);
            var vignette = profile.AddSettings<UnityEngine.Rendering.PostProcessing.Vignette>();
            vignette.enabled.Override(true);
            vignette.intensity.Override(0.5f);
            GameObject go = new GameObject("BlackoutVolume");
            go.layer = 0;
            go.transform.SetParent(transform, false);
            darkVolume = go.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            darkVolume.isGlobal = true;
            darkVolume.priority = 50f;
            darkVolume.sharedProfile = profile;
        }
        darkVolume.weight = weight;
    }

    private IEnumerator FadeDark(float target, float seconds)
    {
        float start = darkVolume != null ? darkVolume.weight : 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetDark(Mathf.Lerp(start, target, t / seconds));
            yield return null;
        }
        SetDark(target);
    }
    private Color savedSky, savedEquator, savedGround, savedFog;

    public void Init(AetherLabCorridor owner)
    {
        this.owner = owner;

        // 큰 홀 천장 형광등(정전 전엔 켜져 있다).
        GameObject lamps = new GameObject("BlackoutCeilingLamps");
        lamps.transform.SetParent(transform, false);
        Material tube = AetherLabCorridor.Solid(Color.black, new Color(1.6f, 1.75f, 1.9f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(-11.5f, 8.6f, -28f + i * 11f);
            GameObject lamp = AetherLabCorridor.Primitive(PrimitiveType.Cube, lamps.transform, p, new Vector3(0.25f, 0.08f, 3.2f), tube);
            Light l = AetherLabCorridor.PointLight(lamp.transform, Vector3.down * 2f, new Color(0.85f, 0.93f, 1f), 14f, 1.4f);
            l.transform.localScale = Vector3.one;
            ceiling.Add(l);
        }
        buzz = AetherAudio.StartLoop("light_buzz", lamps.transform, 0.12f, 2f, 20f, false);
    }

    private void Update()
    {
        Transform player = owner != null ? owner.Player : null;
        if (player == null) return;
        float z = player.position.z;
        bool inHall = player.position.x > AetherLabCorridor.LeftX - 1f && player.position.x < AetherLabCorridor.RightX + 1f;

        if (phase == Phase.Waiting && inHall && z > AetherLabCorridor.BlackoutStart)
        {
            phase = Phase.Dark;
            StartCoroutine(GoDark());
        }
        else if (phase == Phase.Dark && z > AetherLabCorridor.BlackoutEnd && !goingDark)
        {
            phase = Phase.Done;
            StartCoroutine(Restore());
        }

        if (phase == Phase.Dark)
            UpdateDark(player);
    }

    private bool goingDark;

    private IEnumerator GoDark()
    {
        goingDark = true;
        savedAmbient = RenderSettings.ambientIntensity;
        savedSky = RenderSettings.ambientSkyColor;
        savedEquator = RenderSettings.ambientEquatorColor;
        savedGround = RenderSettings.ambientGroundColor;
        savedFog = RenderSettings.fogColor;

        AetherAudio.Play2D("breaker_hit", 0.9f);
        // 깜빡깜빡.
        float[] pattern = { 0.08f, 0.12f, 0.05f, 0.2f, 0.06f, 0.1f, 0.04f, 0.3f, 0.05f };
        bool on = false;
        foreach (float wait in pattern)
        {
            SetCeiling(on ? 1.4f : 0.05f);
            RenderSettings.ambientIntensity = on ? savedAmbient : savedAmbient * 0.4f;
            SetDark(on ? 0f : 0.8f);
            if (on) AetherAudio.Play2D("spark", 0.35f, 1.2f);
            on = !on;
            yield return new WaitForSeconds(wait);
        }
        SetCeiling(0f);
        if (buzz != null) Destroy(buzz.gameObject);
        AetherAudio.Play2D("emp_blast", 0.5f, 0.6f);

        // 어둠.
        RenderSettings.ambientIntensity = savedAmbient * 0.22f;
        StartCoroutine(FadeDark(1f, 0.6f));
        RenderSettings.ambientSkyColor = savedSky * 0.5f;
        RenderSettings.ambientEquatorColor = savedEquator * 0.5f;
        RenderSettings.ambientGroundColor = savedGround * 0.5f;
        RenderSettings.fogColor = savedFog * 0.4f;

        BuildEmergency();
        AetherObjectiveToast.Show("정전 — 비상 전원으로 전환 중", 4f);
        goingDark = false;
    }

    private void SetCeiling(float intensity)
    {
        foreach (Light l in ceiling)
        {
            if (l == null) continue;
            l.intensity = intensity;
            Renderer r = l.transform.parent.GetComponent<Renderer>();
            if (r != null) r.enabled = intensity > 0.2f;
        }
    }

    private void BuildEmergency()
    {
        beaconRoot = new GameObject("EmergencyLights");
        beaconRoot.transform.SetParent(transform, false);

        // 벽의 붉은 경광등: 양쪽 벽에 번갈아.
        Material red = AetherLabCorridor.Solid(new Color(0.3f, 0f, 0f), new Color(2.2f, 0.15f, 0.08f));
        float[] zs = { -26f, -16f, -6f, 4f, 12f };
        for (int i = 0; i < zs.Length; i++)
        {
            bool left = i % 2 == 0;
            float x = left ? AetherLabCorridor.LeftX + 0.3f : AetherLabCorridor.RightX - 0.3f;
            GameObject b = new GameObject("Beacon_" + i);
            b.transform.SetParent(beaconRoot.transform, false);
            b.transform.position = new Vector3(x, 4.2f, zs[i]);
            AetherLabCorridor.Primitive(PrimitiveType.Sphere, b.transform, Vector3.zero, Vector3.one * 0.3f, red);
            GameObject spin = new GameObject("Spin");
            spin.transform.SetParent(b.transform, false);
            Light l = spin.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.08f, 0.04f);
            l.range = 22f;
            l.spotAngle = 55f;
            l.intensity = 5f;
            l.shadows = LightShadows.None;
            beacons.Add(spin.transform);
            beaconLights.Add(l);
            beaconLights.Add(AetherLabCorridor.PointLight(b.transform, Vector3.zero, new Color(1f, 0.1f, 0.05f), 4f, 1.5f));
        }

        // 천장 탐조등 두 개.
        for (int i = 0; i < 2; i++)
        {
            GameObject s = new GameObject("Searchlight_" + i);
            s.transform.SetParent(beaconRoot.transform, false);
            s.transform.position = new Vector3(i == 0 ? -16f : -7f, 9f, -4f + i * 6f);
            AetherLabCorridor.Primitive(PrimitiveType.Cylinder, s.transform, Vector3.up * 0.2f, new Vector3(0.6f, 0.25f, 0.6f),
                AetherLabCorridor.Solid(new Color(0.1f, 0.1f, 0.1f), new Color(0.6f, 0.65f, 0.7f)));
            Light l = s.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.85f, 0.92f, 1f);
            l.range = 30f;
            l.spotAngle = 20f;
            l.intensity = 6f;
            l.shadows = LightShadows.Hard;
            searchlights.Add(l);
            searchAim.Add(new Vector3(-11.5f, 0f, -10f));
        }

        // 사이렌(합성): 600~900Hz 를 오르내린다.
        AudioClip clip = MakeSirenClip();
        GameObject sg = new GameObject("Siren");
        sg.transform.SetParent(beaconRoot.transform, false);
        siren = sg.AddComponent<AetherAudioLoop>();
        siren.Init(clip, 0.13f, 1f, 30f, false);
    }

    private static AudioClip sirenClip;

    private static AudioClip MakeSirenClip()
    {
        if (sirenClip != null) return sirenClip;
        const int rate = 22050;
        const float seconds = 2.4f;
        int n = Mathf.RoundToInt(rate * seconds);
        float[] data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float sweep = 0.5f - 0.5f * Mathf.Cos(t / seconds * Mathf.PI * 2f);
            float freq = 560f + sweep * 380f;
            phase += 2.0 * System.Math.PI * freq / rate;
            float s = Mathf.Sin((float)phase);
            s = Mathf.Clamp(s * 1.6f, -1f, 1f);   // 조금 거칠게
            data[i] = s * 0.5f;
        }
        sirenClip = AudioClip.Create("AETHER_Siren", n, 1, rate, false);
        sirenClip.SetData(data, 0);
        return sirenClip;
    }

    private void UpdateDark(Transform player)
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < beacons.Count; i++)
        {
            if (beacons[i] == null) continue;
            beacons[i].localRotation = Quaternion.Euler(10f, Time.time * 220f + i * 70f, 0f);
        }

        for (int i = 0; i < searchlights.Count; i++)
        {
            Light l = searchlights[i];
            if (l == null) continue;
            // 늦게 쫓는다: 목표점이 플레이어 쪽으로 천천히 다가간다(조금씩 흔들리며).
            Vector3 target = player.position + new Vector3(Mathf.Sin(Time.time * 0.9f + i * 2f) * 1.5f, 0f, Mathf.Cos(Time.time * 0.7f + i) * 1.5f);
            searchAim[i] = Vector3.MoveTowards(searchAim[i], target, (3.5f + i) * dt);
            l.transform.rotation = Quaternion.LookRotation(searchAim[i] - l.transform.position);
        }
    }

    private IEnumerator Restore()
    {
        AetherLabCorridor.MarkBlackoutDone();
        AetherAudio.Play2D("breaker_hit", 0.8f, 1.1f);
        yield return new WaitForSeconds(0.4f);

        RenderSettings.ambientIntensity = savedAmbient;
        StartCoroutine(FadeDark(0f, 1.2f));
        RenderSettings.ambientSkyColor = savedSky;
        RenderSettings.ambientEquatorColor = savedEquator;
        RenderSettings.ambientGroundColor = savedGround;
        RenderSettings.fogColor = savedFog;
        SetCeiling(1.4f);
        if (siren != null) Destroy(siren.gameObject);
        if (beaconRoot != null) Destroy(beaconRoot);

        AetherObjectiveToast.Show("비상 전원 복구 — 게이트 옆 보안 단말기를 찾아라", 5f);
    }

    private void OnDestroy()
    {
        // 정전 중에 씬을 떠나면(죽음·퍼즐) 다음 씬 분위기는 AetherAtmosphere 가 다시 칠한다.
    }
}

// ====================================================================== 보안 레이저

/// <summary>홀을 가로지르는 붉은 감지선. 높이가 오르내린다. 닿으면 경보 + 아주 약한 피해.</summary>
public class AetherSecurityLasers : MonoBehaviour
{
    private AetherLabCorridor owner;
    private float[] zs;
    private float left, right, floor;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<Light> glows = new List<Light>();
    private readonly List<float> heights = new List<float>();
    private float lastTrip = -100f;
    private float flashUntil;
    private Material laserMat;

    public void Init(AetherLabCorridor owner, float[] zs, float left, float right, float floor)
    {
        this.owner = owner;
        this.zs = zs;
        this.left = left;
        this.right = right;
        this.floor = floor;

        laserMat = AetherLabCorridor.Additive(new Color(1f, 0.08f, 0.05f, 0.95f));
        Material emitter = AetherLabCorridor.Solid(new Color(0.08f, 0.08f, 0.09f), new Color(0.9f, 0.05f, 0.03f));
        for (int i = 0; i < zs.Length; i++)
        {
            LineRenderer line = AetherLabCorridor.Beam(transform, laserMat, 0.05f);
            line.startColor = line.endColor = new Color(1f, 0.15f, 0.1f, 1f);
            lines.Add(line);
            heights.Add(1f);

            // 양쪽 벽의 레일(발사기가 오르내린다).
            AetherLabCorridor.Primitive(PrimitiveType.Cube, transform, new Vector3(left + 0.08f, floor + 1.3f, zs[i]), new Vector3(0.12f, 2.6f, 0.18f), emitter);
            AetherLabCorridor.Primitive(PrimitiveType.Cube, transform, new Vector3(right - 0.08f, floor + 1.3f, zs[i]), new Vector3(0.12f, 2.6f, 0.18f), emitter);
            glows.Add(AetherLabCorridor.PointLight(transform, new Vector3((left + right) * 0.5f, floor + 1f, zs[i]), new Color(1f, 0.1f, 0.05f), 6f, 1.2f));
        }
    }

    private void Update()
    {
        Transform player = owner != null ? owner.Player : null;
        float top = Mathf.Max(2.1f, (owner != null ? owner.PlayerHeight : 1.8f) + 0.4f);
        float low = 0.25f;
        bool flashing = Time.time < flashUntil;

        for (int i = 0; i < lines.Count; i++)
        {
            // 세 줄이 서로 다른 박자로. 위쪽에서 조금 더 머문다(지나갈 틈).
            float s = 0.5f + 0.5f * Mathf.Sin(Time.time * (0.55f + i * 0.17f) + i * 2.1f);
            s = Mathf.Pow(s, 0.7f);
            float h = floor + low + (top - low) * s;
            heights[i] = h;

            Vector3 a = new Vector3(left + 0.15f, h, zs[i]);
            Vector3 b = new Vector3(right - 0.15f, h, zs[i]);
            lines[i].SetPosition(0, a);
            lines[i].SetPosition(1, b);
            float w = flashing ? 0.12f : 0.07f + Mathf.Sin(Time.time * 30f + i) * 0.008f;
            lines[i].startWidth = lines[i].endWidth = w;
            Color c = flashing && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? new Color(1f, 0.9f, 0.8f, 1f) : new Color(1f, 0.15f, 0.1f, 1f);
            lines[i].startColor = lines[i].endColor = c;
            glows[i].transform.position = new Vector3(glows[i].transform.position.x, h, zs[i]);
            glows[i].intensity = flashing ? 3f : 1.2f;
        }

        if (player == null)
            return;

        Vector3 p = player.position;
        if (p.x < left || p.x > right)
            return;
        float height = owner.PlayerHeight;
        bool touching = false;
        for (int i = 0; i < zs.Length; i++)
        {
            if (Mathf.Abs(p.z - zs[i]) > 0.35f)
                continue;
            float h = heights[i];
            if (h > p.y + 0.05f && h < p.y + height)
            {
                touching = true;
                break;
            }
        }
        if (!touching)
            return;

        // 닿아 있는 동안 아주 약하게 지진다. 0.6초마다 3.
        if (Time.time >= nextZap)
        {
            nextZap = Time.time + ZapInterval;
            PlayerStatus status = player.GetComponentInParent<PlayerStatus>();
            if (status == null) status = player.GetComponentInChildren<PlayerStatus>();
            if (status != null) status.ApplyDamage(ZapDamage);
            AetherAudio.PlayAt("spark", p + Vector3.up * 1.2f, 0.6f, 1f, 12f, 0.1f, 1.3f);
        }

        if (Time.time - lastTrip >= 1.5f)
            Trip();
    }

    private const float ZapDamage = 3f;
    private const float ZapInterval = 0.6f;
    private float nextZap;

    private void Trip()
    {
        bool responders = Time.time - lastTrip > owner.Cooldown && owner.CanSpawnResponders;
        lastTrip = Time.time;
        flashUntil = Time.time + 2.5f;

        AetherAudio.Play2D("robot_alert", 1f);
        AetherAudio.Play2D("drone_alert", 0.8f, 0.8f);
        if (responders)
        {
            AetherObjectiveToast.Show("경보! 보안 레이저에 감지되었다 — 경비 로봇 출동", 4f);
            owner.SpawnResponders();
        }
        else
        {
            AetherObjectiveToast.Show("경보! 보안 레이저에 감지되었다", 3f);
        }
    }
}

// ====================================================================== 보안 게이트

/// <summary>엘리베이터 앞을 막는 붉은 레이저 벽. 보안 단말기 해독이 끝나면 아래로 꺼지며 사라진다.</summary>
public class AetherSecurityGate : MonoBehaviour
{
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<Vector3[]> ends = new List<Vector3[]>();
    private AetherAudioLoop hum;
    private bool opening;
    private float left, right, floor;

    public void Init(float left, float right, float floor)
    {
        this.left = left;
        this.right = right;
        this.floor = floor;
        Material mat = AetherLabCorridor.Additive(new Color(1f, 0.1f, 0.06f, 0.9f));
        Material frame = AetherLabCorridor.Solid(new Color(0.08f, 0.08f, 0.09f), new Color(0.8f, 0.05f, 0.03f));
        float z = transform.position.z;

        // 세로줄.
        int cols = 16;
        for (int i = 0; i <= cols; i++)
        {
            float x = Mathf.Lerp(left + 0.2f, right - 0.2f, i / (float)cols);
            AddLine(mat, new Vector3(x, floor + 0.05f, z), new Vector3(x, floor + 4.2f, z), 0.035f);
        }
        // 가로줄.
        for (int j = 0; j < 6; j++)
        {
            float y = floor + 0.4f + j * 0.7f;
            AddLine(mat, new Vector3(left + 0.2f, y, z), new Vector3(right - 0.2f, y, z), 0.03f);
        }
        // 위·아래 틀.
        AetherLabCorridor.Primitive(PrimitiveType.Cube, transform, new Vector3((left + right) * 0.5f, 0.04f, 0f), new Vector3(right - left, 0.08f, 0.3f), frame);
        AetherLabCorridor.Primitive(PrimitiveType.Cube, transform, new Vector3((left + right) * 0.5f, 4.3f, 0f), new Vector3(right - left, 0.15f, 0.3f), frame);
        for (int k = 0; k < 3; k++)
            AetherLabCorridor.PointLight(transform, new Vector3(Mathf.Lerp(left, right, (k + 0.5f) / 3f), 2f, -0.8f), new Color(1f, 0.1f, 0.05f), 7f, 1.6f);

        hum = AetherAudio.StartLoop("shield_hum", transform, 0.35f, 2f, 18f);
    }

    private void AddLine(Material mat, Vector3 a, Vector3 b, float width)
    {
        LineRenderer line = AetherLabCorridor.Beam(transform, mat, width);
        line.startColor = line.endColor = new Color(1f, 0.15f, 0.1f, 1f);
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        lines.Add(line);
        ends.Add(new[] { a, b });
    }

    private void Update()
    {
        if (opening) return;
        float flicker = 0.9f + Mathf.Sin(Time.time * 25f) * 0.1f;
        for (int i = 0; i < lines.Count; i++)
            lines[i].startColor = lines[i].endColor = new Color(1f, 0.15f, 0.1f, flicker);
    }

    public void Open()
    {
        if (opening) return;
        opening = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Collider block = GetComponent<Collider>();
        if (block != null) block.enabled = false;
        AetherAudio.PlayAt("shield_off", transform.position + new Vector3((left + right) * 0.5f, 2f, 0f), 1f, 3f, 40f);
        AetherAudio.PlayAt("door_slide_open", transform.position + new Vector3((left + right) * 0.5f, 1.5f, 0f), 0.8f, 3f, 40f);
        if (hum != null) Destroy(hum.gameObject);

        // 줄들이 위에서부터 바닥으로 접히며 꺼진다.
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 1.2f);
            for (int i = 0; i < lines.Count; i++)
            {
                Vector3 a = ends[i][0], b = ends[i][1];
                Vector3 a2 = new Vector3(a.x, Mathf.Lerp(a.y, floor, k), a.z);
                Vector3 b2 = new Vector3(b.x, Mathf.Lerp(b.y, floor, k), b.z);
                lines[i].SetPosition(0, a2);
                lines[i].SetPosition(1, b2);
                lines[i].startColor = lines[i].endColor = new Color(1f, 0.15f, 0.1f, 1f - k);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
