using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 두 캐릭터의 겉모습이 뒤바뀌어 있었다(10/12 사용자 확인).
///  - Shiri(Move_Yunseo, 주먹) 프리팹에는 헬멧 쓴 남자 몸(SK_PP801C)이,
///  - Meka(Move_Hankyul, 총) 프리팹에는 크롭탑 여자 몸(SK_LP287C)이 붙어 있었다.
/// 캐릭터 선택 그림·시네마틱은 Shiri = 여자, Meka = 헬멧 남자다.
///
/// 두 모델은 같은 팩(G2_MedicSoldier)이라 뼈대가 같다(본 이름·위치 동일, 머리카락 본만 다름).
/// 그래서 스크립트·총·카메라·펀치 판정 본은 그대로 두고, 보이는 피부(SkinnedMeshRenderer)만
/// 상대 프리팹의 것으로 바꿔 끼운다. 장면마다 놓인 캐릭터(04·05 의 Hankyul_찐, Yunseo_cloth' 등)와
/// Resources 에서 생성되는 캐릭터 모두 같은 방법으로 처리된다.
///
/// 1인칭: 몸은 'Player' 레이어(카메라에 안 보임), 손·장갑·팔뚝은 'ViewModel' 레이어(보임).
/// Meka 는 원래 이렇게 하고 있었고, Shiri 는 몸 전체가 'floor' 레이어라 자기 몸이 카메라에 보였다.
/// </summary>
public class AetherBodySkin : MonoBehaviour
{
    private const string ShiriLookSource = "AETHERRuntime/Hankyul_Player";   // 여자 몸
    private const string MekaLookSource = "AETHERRuntime/Yunseo_Player";     // 헬멧 남자 몸
    private const string SkinRootName = "AETHER_Skin";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<AetherBodySkin>() != null)
            return;
        GameObject go = new GameObject("AETHER_BodySkin");
        DontDestroyOnLoad(go);
        go.AddComponent<AetherBodySkin>();
    }

    private float nextScan;

    private void Update()
    {
        // 캐릭터는 장면이 열린 뒤 선택에 따라 켜지거나 생성되므로 가볍게 계속 살핀다.
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 0.25f;

        if (AetherCinematics.IsPlaying)
            return;

        foreach (Move_Yunseo m in FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Apply(m.transform, ShiriLookSource);
        foreach (Move_Hankyul m in FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Apply(m.transform, MekaLookSource);
    }

    private static readonly Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();

    private static GameObject Source(string path)
    {
        if (!sources.TryGetValue(path, out GameObject go) || go == null)
        {
            go = Resources.Load<GameObject>(path);
            sources[path] = go;
        }
        return go;
    }

    public static void Apply(Transform playerRoot, string sourcePath)
    {
        if (playerRoot == null || playerRoot.Find(SkinRootName) != null)
            return;

        GameObject source = Source(sourcePath);
        Transform targetSkeleton = playerRoot.Find("root");
        Transform sourceSkeleton = source != null ? source.transform.Find("root") : null;
        if (source == null || targetSkeleton == null || sourceSkeleton == null)
            return;

        var bones = new Dictionary<string, Transform>();
        foreach (Transform t in targetSkeleton.GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;

        // 원래 피부를 끈다(총·표식 같은 일반 MeshRenderer 는 그대로).
        foreach (SkinnedMeshRenderer old in playerRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            old.enabled = false;

        Transform skinRoot = new GameObject(SkinRootName).transform;
        skinRoot.SetParent(playerRoot, false);

        int bodyLayer = LayerMask.NameToLayer("Player");
        int viewLayer = LayerMask.NameToLayer("ViewModel");

        foreach (SkinnedMeshRenderer src in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // 프리팹 원본은 장면 밖이라 activeInHierarchy 가 늘 false 다. 부모까지 activeSelf 로 본다.
            if (src.sharedMesh == null || !src.enabled || !ActiveInPrefab(src.transform, source.transform))
                continue;

            Transform[] mapped = new Transform[src.bones.Length];
            bool ok = true;
            for (int i = 0; i < src.bones.Length; i++)
            {
                Transform b = src.bones[i];
                if (b == null) continue;
                mapped[i] = MapBone(b, sourceSkeleton, bones);
                if (mapped[i] == null) { ok = false; break; }
            }
            if (!ok)
                continue;

            string path = PathFrom(source.transform, src.transform);
            GameObject go = new GameObject(src.name);
            go.transform.SetParent(skinRoot, false);
            SkinnedMeshRenderer smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = src.sharedMesh;
            smr.sharedMaterials = src.sharedMaterials;
            smr.bones = mapped;
            smr.rootBone = src.rootBone != null ? MapBone(src.rootBone, sourceSkeleton, bones) : targetSkeleton;
            smr.localBounds = src.localBounds;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = src.shadowCastingMode;
            for (int i = 0; i < src.sharedMesh.blendShapeCount; i++)
                smr.SetBlendShapeWeight(i, src.GetBlendShapeWeight(i));

            bool hands = IsViewModelPath(path);
            // Shiri 1인칭: 팔만 떼어 보이면 어디선가 잘린 단면이 보였다(10/12 사용자 "어디라도 잘려 있는 느낌이 없게").
            // 상체(어깨·팔·상의)를 통째로 1인칭에 보이게 하고 머리·머리카락만 숨긴다. 이음매는 목, 즉 카메라 자리라 안 보인다.
            if (false && !hands && playerRoot.GetComponent<Move_Yunseo>() != null && path.ToLowerInvariant().Contains("upper"))   // 상체 통째: 앉으면 상의가 화면을 가려 쓰지 않음
                hands = true;
            int layer = hands ? viewLayer : bodyLayer;
            if (layer >= 0) go.layer = layer;

            // Shiri 1인칭: 손만 보이면 손목에서 잘려 보였다(10/12 사용자). 몸통 피부에서 팔 부분만 떼어 1인칭에 보이게 한다.
            if (!hands && viewLayer >= 0 && playerRoot.GetComponent<Move_Yunseo>() != null && path.ToLowerInvariant().Contains("upperskin"))   // 맨살 팔만(어깨~손). 상의·몸통은 1인칭에 안 보임
            {
                Mesh arms = ArmsOnly(src.sharedMesh, src.bones);
                if (arms != null)
                {
                    GameObject armGo = new GameObject(src.name + "_FPArms");
                    armGo.transform.SetParent(skinRoot, false);
                    armGo.layer = viewLayer;
                    SkinnedMeshRenderer a = armGo.AddComponent<SkinnedMeshRenderer>();
                    a.sharedMesh = arms;
                    a.sharedMaterials = src.sharedMaterials;
                    a.bones = mapped;
                    a.rootBone = smr.rootBone;
                    a.localBounds = src.localBounds;
                    a.updateWhenOffscreen = true;
                    a.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        if (playerRoot.GetComponent<Move_Yunseo>() != null && playerRoot.GetComponent<AetherShiriFirstPersonArms>() == null)
            playerRoot.gameObject.AddComponent<AetherShiriFirstPersonArms>();

        // 1인칭 카메라: 자기 몸은 빼고 손은 넣는다.
        foreach (Camera cam in playerRoot.GetComponentsInChildren<Camera>(true))
        {
            if (bodyLayer >= 0) cam.cullingMask &= ~(1 << bodyLayer);
            if (viewLayer >= 0) cam.cullingMask |= 1 << viewLayer;
        }
    }

    /// <summary>원본 뼈대의 본을 대상 뼈대의 같은 이름 본으로. 없으면(머리카락 본) 부모를 따라 같은 모양으로 만든다.</summary>
    private static Transform MapBone(Transform srcBone, Transform srcSkeleton, Dictionary<string, Transform> bones)
    {
        if (bones.TryGetValue(srcBone.name, out Transform found))
            return found;
        if (srcBone == srcSkeleton || srcBone.parent == null)
            return null;

        Transform parent = MapBone(srcBone.parent, srcSkeleton, bones);
        if (parent == null)
            return null;

        Transform made = new GameObject(srcBone.name).transform;
        made.SetParent(parent, false);
        made.localPosition = srcBone.localPosition;
        made.localRotation = srcBone.localRotation;
        made.localScale = srcBone.localScale;
        bones[srcBone.name] = made;
        return made;
    }

    private static readonly Dictionary<Mesh, Mesh> armCache = new Dictionary<Mesh, Mesh>();

    /// <summary>
    /// 메시에서 팔(아래팔·위팔 앞쪽 절반·손)에 주로 묶인 삼각형만 남긴 복사본. 팔이 없으면 null.
    /// 위팔은 어깨 가까이까지 남기면 1인칭 화면 양옆에 어깨 덩어리가 보여, 팔꿈치 쪽 절반만 쓴다.
    /// </summary>
    private static Mesh ArmsOnly(Mesh mesh, Transform[] bones)
    {
        if (mesh == null || !mesh.isReadable)
            return null;
        if (armCache.TryGetValue(mesh, out Mesh cached))
            return cached;

        var armBone = new bool[bones.Length];
        bool any = false;
        for (int i = 0; i < bones.Length; i++)
        {
            string n = bones[i] != null ? bones[i].name.ToLowerInvariant() : string.Empty;
            armBone[i] = n.StartsWith("upperarm") || n.StartsWith("lowerarm") || n.StartsWith("hand") || n.Contains("thumb") || n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("pinky");
            any |= armBone[i];
        }

        Mesh result = null;
        if (any)
        {
            BoneWeight[] w = mesh.boneWeights;
            var keepVertex = new bool[w.Length];
            for (int v = 0; v < w.Length; v++)
            {
                float arm = 0f;
                if (w[v].boneIndex0 < armBone.Length && armBone[w[v].boneIndex0]) arm += w[v].weight0;
                if (w[v].boneIndex1 < armBone.Length && armBone[w[v].boneIndex1]) arm += w[v].weight1;
                if (w[v].boneIndex2 < armBone.Length && armBone[w[v].boneIndex2]) arm += w[v].weight2;
                if (w[v].boneIndex3 < armBone.Length && armBone[w[v].boneIndex3]) arm += w[v].weight3;
                keepVertex[v] = arm >= 0.35f;   // 어깨 쪽까지 넉넉히 남겨 잘린 단면이 카메라 옆(화면 밖)에 오게
            }

            result = Object.Instantiate(mesh);
            result.name = mesh.name + "_FPArms";
            int kept = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] tris = mesh.GetTriangles(s);
                var list = new List<int>(tris.Length);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (keepVertex[tris[t]] && keepVertex[tris[t + 1]] && keepVertex[tris[t + 2]])
                    {
                        list.Add(tris[t]); list.Add(tris[t + 1]); list.Add(tris[t + 2]);
                    }
                }
                kept += list.Count;
                result.SetTriangles(list, s);
            }
            if (kept == 0)
            {
                Object.Destroy(result);
                result = null;
            }
        }

        armCache[mesh] = result;
        return result;
    }

    private static bool ActiveInPrefab(Transform t, Transform root)
    {
        for (Transform c = t; c != null && c != root; c = c.parent)
            if (!c.gameObject.activeSelf) return false;
        return true;
    }

    private static string PathFrom(Transform root, Transform t)
    {
        string path = t.name;
        for (Transform c = t.parent; c != null && c != root; c = c.parent)
            path = c.name + "/" + path;
        return path;
    }

    private static bool IsViewModelPath(string path)
    {
        string p = path.ToLowerInvariant();
        return p.Contains("hand") || p.Contains("glove") || p.Contains("glover");
    }
}

/// <summary>
/// Shiri 1인칭 주먹. 펀치 애니메이션은 3인칭용이라 주먹이 카메라 아래·뒤에 머물러 화면에 한 번도 안 보였다
/// (10/12 사용자: "맨손전투가 시선에서 안 보여"). 카메라를 눈 위치로 옮기고, 애니메이션이 끝난 뒤 양쪽 어깨(쇄골)를
/// 카메라 기준 위·앞으로 조금 옮겨 주먹이 화면 아래쪽에 보이게 한다. 몸은 자기 카메라에 안 보이므로(Player 레이어)
/// 손·장갑만 보인다.
/// </summary>
[DefaultExecutionOrder(10000)]
public class AetherShiriFirstPersonArms : MonoBehaviour
{
    // 1인칭 팔 보정. 예전엔 쇄골을 '위치'로 끌어올려 어깨 피부가 늘어나고 팔이 꺾여 보였다(10/12 사용자).
    // 이제는 위팔을 어깨 관절에서 '회전'만 시켜 팔을 앞으로 든다(실제 팔이 움직이는 방식이라 모양이 안 깨진다).
    public static readonly Vector3 EyeLocal = new Vector3(0f, 1.66f, -0.02f);
    public static float RaiseDegrees = 30f;   // 위팔을 카메라 기준 위로 드는 각도
    public static float InwardDegrees = 6f;   // 양팔을 가운데로 모으는 각도
    // 1인칭 팔 전체를 카메라 기준으로 옮기는 양(몸 기준 좌표). 싸움 자세는 주먹이 얼굴 높이라 그대로면 화면을 가린다.
    public static Vector3 ViewOffset = new Vector3(0f, -0.14f, 0.14f);

    private Transform cam, upperL, upperR;

    private void Start()
    {
        Transform cameraPos = transform.Find("CameraPos");
        if (cameraPos != null)
        {
            cameraPos.localPosition = EyeLocal;
            Camera c = cameraPos.GetComponentInChildren<Camera>(true);
            cam = c != null ? c.transform : cameraPos;
        }
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "upperarm_l") upperL = t;
            else if (t.name == "upperarm_r") upperR = t;
            else if (t.name == "spine_03" && chest == null) chest = t;   // 복사 뼈대는 아직 없다(LateUpdate 에서 만든다)
        }
        startTime = Time.time;
        move = GetComponent<Move_Yunseo>();
    }

    // 1인칭에 보이는 손·팔은 몸 뼈대를 그대로 쓰지 않고 '복사 뼈대'를 쓴다. 매 프레임 몸 뼈의 자세를 복사한 뒤
    // 눈을 중심으로 시선의 위아래 각도만큼 통째로 돌린다(총 게임의 1인칭 팔). 그래서 아래를 봐도 팔 모양·화면 속 위치가
    // 정면을 볼 때와 똑같고, 위팔 끝(잘린 자리)이 화면 안으로 들어오지 않는다(10/12 사용자 "어디라도 잘려 있는 느낌이 없게").
    private Transform rig;
    private Transform[] srcBones = new Transform[0], fpBones = new Transform[0];

    private void BuildViewRig()
    {
        int viewLayer = LayerMask.NameToLayer("ViewModel");
        if (viewLayer < 0)
            return;
        var map = new System.Collections.Generic.Dictionary<Transform, Transform>();
        var src = new System.Collections.Generic.List<Transform>();
        var dst = new System.Collections.Generic.List<Transform>();
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.gameObject.layer != viewLayer)
                continue;
            viewRenderers.Add(r);
            if (rig == null)
            {
                rig = new GameObject("AETHER_FPRig").transform;
                rig.SetParent(transform, false);
            }
            Transform[] bones = r.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null) continue;
                if (!map.TryGetValue(b, out Transform copy))
                {
                    copy = new GameObject(b.name).transform;
                    copy.SetParent(rig, false);
                    map[b] = copy;
                    src.Add(b);
                    dst.Add(copy);
                }
                bones[i] = copy;
            }
            r.bones = bones;
            if (r.rootBone != null && map.TryGetValue(r.rootBone, out Transform root))
                r.rootBone = root;
        }
        srcBones = src.ToArray();
        fpBones = dst.ToArray();
    }

    private void LateUpdate()
    {
        if (cam == null || upperL == null || upperR == null)
            return;
        if (rig == null)
            BuildViewRig();

        // 점프 중엔 1인칭 팔을 숨긴다. 점프 애니메이션이 팔을 얼굴 앞으로 휘둘러 손이 화면을 크게 가렸고,
        // 공중에선 펀치 애니메이션도 안 나와 공격해도 같은 모습이었다.
        bool airborne = move != null && !move.IsGroundedNow;
        if (airborne) airTime += Time.deltaTime; else airTime = 0f;
        // 싸울 때(펀치 후 3초간 InBattle)만 팔을 보인다. 그냥 걷고 뛰고 앉을 때는 걷기 애니메이션의 팔 흔들림이
        // 화면 가장자리에서 손만 불쑥 들어와 팔이 이상하게 모여 보였다(10/12 사용자).
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            battleParam = FindBattleParam(animator);
            crouchParam = 0;
            if (animator != null)
                foreach (AnimatorControllerParameter p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Bool && p.name == "IsCrouching") crouchParam = p.nameHash;
        }
        bool inBattle = animator == null || battleParam == 0 || animator.GetBool(battleParam);
        // 배틀그라운드식: 싸우는 동안 두 주먹이 화면 아래 가운데에 있고, 싸움이 끝나면 아래로 내려가며 사라진다.
        // 자세는 애니메이션이 아니라 카메라 기준 목표점으로 직접 정한다(SolveArm) — 앉기·점프·걷기에 상관없이 같다.
        raise = Mathf.MoveTowards(raise, inBattle ? 1f : 0f, Time.deltaTime / 0.18f);
        bool showArms = raise > 0.02f;
        if (showArms != armsShown)
        {
            armsShown = showArms;
            foreach (SkinnedMeshRenderer r in viewRenderers)
                if (r != null) r.enabled = showArms;
        }

        if (chest == null)
            return;

        // 팔을 앞으로 드는 보정은 1인칭 복사 뼈대에만 쓴다. 몸(남에게 보이는 3인칭·그림자)은 애니메이션 그대로 둔다.
        // 잠깐 몸 팔을 돌려 복사한 뒤 원래대로 되돌린다(스키닝은 LateUpdate 뒤라 몸에는 반영되지 않는다).
        Quaternion keepL = upperL.localRotation, keepR = upperR.localRotation;
        Quaternion lift = Quaternion.AngleAxis(-RaiseDegrees, transform.right);
        upperL.rotation = lift * Quaternion.AngleAxis(InwardDegrees, transform.up) * upperL.rotation;
        upperR.rotation = lift * Quaternion.AngleAxis(-InwardDegrees, transform.up) * upperR.rotation;
        try { CopyToViewRig(); }
        finally { upperL.localRotation = keepL; upperR.localRotation = keepR; }
        if (hasReference && showArms)
            ApplyJabPose();
    }

    // ---------- 배틀그라운드식 잽 자세 (카메라 기준, 미터) ----------
    public static Vector3 RestL = new Vector3(-0.13f, -0.17f, 0.30f);
    public static Vector3 RestR = new Vector3(0.13f, -0.17f, 0.30f);
    public static Vector3 PunchL = new Vector3(-0.025f, -0.07f, 0.62f);
    public static Vector3 PunchR = new Vector3(0.025f, -0.07f, 0.62f);
    public static Vector3 Lowered = new Vector3(0f, -0.45f, 0.05f);   // 싸움이 끝나면 여기로 내려간다
    private float raise;
    private Yunseo_Battle battle;
    private int iUpL = -1, iLoL = -1, iHdL = -1, iUpR = -1, iLoR = -1, iHdR = -1;
    private int[] subUpL, subLoL, subUpR, subLoR;

    private void IndexArmChains()
    {
        for (int i = 0; i < srcBones.Length; i++)
        {
            if (srcBones[i] == null) continue;
            switch (srcBones[i].name)
            {
                case "upperarm_l": iUpL = i; break;
                case "lowerarm_l": iLoL = i; break;
                case "hand_l": iHdL = i; break;
                case "upperarm_r": iUpR = i; break;
                case "lowerarm_r": iLoR = i; break;
                case "hand_r": iHdR = i; break;
            }
        }
        subUpL = Subtree(iUpL); subLoL = Subtree(iLoL); subUpR = Subtree(iUpR); subLoR = Subtree(iLoR);
    }

    private int[] Subtree(int root)
    {
        var list = new System.Collections.Generic.List<int>();
        if (root >= 0)
            for (int i = 0; i < srcBones.Length; i++)
                if (srcBones[i] != null && srcBones[i].IsChildOf(srcBones[root])) list.Add(i);
        return list.ToArray();
    }

    private static float PunchCurve(float t)
    {
        const float outT = 0.07f, hold = 0.04f, back = 0.17f;
        if (t < 0f) return 0f;
        if (t < outT) { float k = t / outT; return 1f - (1f - k) * (1f - k); }
        if (t < outT + hold) return 1f;
        if (t < outT + hold + back) { float k = (t - outT - hold) / back; return 1f - k * k * (3f - 2f * k); }
        return 0f;
    }

    private void ApplyJabPose()
    {
        if (subUpL == null) IndexArmChains();
        if (iUpL < 0 || iLoL < 0 || iHdL < 0 || iUpR < 0 || iLoR < 0 || iHdR < 0) return;
        if (battle == null) battle = GetComponentInChildren<Yunseo_Battle>();

        float since = battle != null ? Time.time - battle.LastJabTime : 99f;
        bool right = battle != null && battle.JabCount % 2 == 1;
        float k = PunchCurve(since);
        float bob = Mathf.Sin(Time.time * 2.2f) * 0.006f;

        Vector3 l = Vector3.Lerp(RestL, PunchL, right ? 0f : k) + Vector3.up * bob;
        Vector3 r = Vector3.Lerp(RestR, PunchR, right ? k : 0f) + Vector3.up * bob;
        l = Vector3.Lerp(Lowered + new Vector3(-0.15f, 0f, 0f), l, raise);
        r = Vector3.Lerp(Lowered + new Vector3(0.15f, 0f, 0f), r, raise);

        Quaternion cr = cam.rotation;
        SolveArm(iUpL, iLoL, iHdL, subUpL, subLoL, cam.position + cr * l, cr * new Vector3(-0.7f, -1f, -0.2f));
        SolveArm(iUpR, iLoR, iHdR, subUpR, subLoR, cam.position + cr * r, cr * new Vector3(0.7f, -1f, -0.2f));
    }

    /// <summary>두 뼈 IK: 위팔·아래팔을 돌려 손목이 target 에 오게 한다. 아래 뼈들(손·손가락)은 같이 따라온다.</summary>
    private void SolveArm(int iu, int il, int ih, int[] subUp, int[] subLo, Vector3 target, Vector3 poleHint)
    {
        Transform up = fpBones[iu], lo = fpBones[il], hd = fpBones[ih];
        Vector3 s = up.position, e = lo.position, h = hd.position;
        float l1 = (e - s).magnitude, l2 = (h - e).magnitude;
        if (l1 < 1e-4f || l2 < 1e-4f) return;
        Vector3 toT = target - s;
        float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
        Vector3 dir = toT.normalized;
        float a = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
        float hh = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
        Vector3 pole = Vector3.ProjectOnPlane(poleHint, dir).normalized;
        Vector3 e2 = s + dir * a + pole * hh;

        Rotate(subUp, s, Quaternion.FromToRotation(e - s, e2 - s));
        Vector3 h2 = hd.position, t2 = s + dir * d;
        Rotate(subLo, e2, Quaternion.FromToRotation(h2 - e2, t2 - e2));
    }

    private void Rotate(int[] idx, Vector3 pivot, Quaternion q)
    {
        foreach (int i in idx)
        {
            Transform c = fpBones[i];
            c.SetPositionAndRotation(pivot + q * (c.position - pivot), q * c.rotation);
        }
    }

    private void CopyToViewRig()
    {

        // 기준 자세: 처음(서 있는 상태) 가슴 뼈가 카메라에서 얼마나 떨어져 있었는지. 앉기·점프 애니메이션이 몸을
        // 숙이거나 웅크려도 1인칭 팔은 이 거리에 고정한다. 안 그러면 앉을 때 손이 카메라 코앞에 와 손 안쪽이 보였다.
        if (!hasReference)
        {
            if (Time.time - startTime < 0.5f)
                return;
            refChestPos = transform.InverseTransformPoint(chest.position) - transform.InverseTransformPoint(cam.position);
            refChestRot = Quaternion.Inverse(transform.rotation) * chest.rotation;
            hasReference = true;
        }

        // 복사 뼈대 = (가슴 기준 팔 동작) 을 기준 가슴 자리에 놓고, 눈을 중심으로 시선 각도만큼 돌린 것.
        float pitchDown = -Mathf.Asin(Mathf.Clamp(cam.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        Quaternion view = Quaternion.AngleAxis(pitchDown, cam.right);
        Vector3 eye = cam.position;
        Vector3 chestPos = eye + view * transform.TransformVector(refChestPos + ViewOffset);
        Quaternion chestRot = view * transform.rotation * refChestRot;
        Quaternion toVirtual = chestRot * Quaternion.Inverse(chest.rotation);
        Vector3 rigScale = rig != null ? rig.lossyScale : Vector3.one;
        for (int i = 0; i < srcBones.Length; i++)
        {
            Transform b = srcBones[i], c = fpBones[i];
            if (b == null || c == null) continue;
            c.SetPositionAndRotation(chestPos + toVirtual * (b.position - chest.position), toVirtual * b.rotation);
            Vector3 s = b.lossyScale;
            c.localScale = new Vector3(s.x / rigScale.x, s.y / rigScale.y, s.z / rigScale.z);
        }
    }

    private Animator animator;
    private int battleParam, crouchParam;

    private static int FindBattleParam(Animator a)
    {
        if (a == null) return 0;
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "InBattle") return p.nameHash;
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name.Contains("Battle")) return p.nameHash;
        return 0;
    }

    private Transform chest;
    private Move_Yunseo move;
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> viewRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();
    private bool armsShown = true;
    private float airTime;
    private bool hasReference;
    private float startTime;
    private Vector3 refChestPos;
    private Quaternion refChestRot;
}
