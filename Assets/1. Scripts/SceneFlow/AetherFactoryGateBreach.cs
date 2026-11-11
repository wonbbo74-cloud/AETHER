using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 폐허된 도시(05)의 공장 서쪽 출입구를 <b>총으로 쏴서 열고 들어갈 수 있게</b> 만든다.
///
/// 이 자리(간판 'AETHER Control Room' 이 붙은 공장 서쪽 벽)에는 원래부터
/// 쏘아서 여는 문 <see cref="Lab_Door"/> 이 놓여 있었는데, 네 군데가 끊겨 있었다.
///
///  1. 문 오브젝트(Map/LabDoor)가 <b>꺼진 채로</b> 저장돼 있었다.
///     꺼져 있으면 Awake 가 돌지 않아 '지금 쏴야 할 표적'이 비어서, 켜도 총이 먹히지 않는다.
///  2. 문(가로 3.5m)이 출입구(가로 5.4m, 높이 3.75m)보다 작아 사방에 틈이 있었다.
///  3. 표적을 다 맞혀도 문은 '무력화'까지만 되고, 실제로 부수는 건 윤서가 QTE 로 한다.
///     그런데 이 씬에는 그 QTE(Lab_DoorQTE)가 없어서 혼자서는 영영 열 수 없다.
///  4. 무력화되면 문짝이 '잠금해제' 그림(LabDoor_Downed)으로 바뀌는데, 이것도 닫힌 문 모양이다.
///     콜라이더가 없어 지나갈 수는 있지만 <b>보기에는 닫힌 문 그대로</b>였다.
///
/// 벽은 손대지 않는다 — 출입구 자리에는 원래 구멍이 뚫려 있다(실측: y 0.0~3.75, z -105.4~-100.0).
/// 막고 있던 것은 이 문뿐이다.
/// </summary>
public class AetherFactoryGateBreach : MonoBehaviour
{
    private const string SceneName = "05.폐허된 도시";

    // 출입구 실측값(월드 기준). 구멍 안(x -9.3)에서 옆·위로 광선을 쏴서 문설주·상인방까지 잰 값.
    // (바깥에서 0.25m 간격으로 잰 첫 값 -105.40~-100.00, 높이 3.75 는 조금 작았다.)
    private const float OpeningMinZ = -105.43f;
    private const float OpeningMaxZ = -99.87f;
    private const float OpeningFloorY = 0.00f;
    private const float OpeningTopY = 3.93f;

    /// <summary>
    /// 벽 가운데. 실측한 벽면은 바깥 x -9.37, 안쪽 x -9.07 (두께 0.3m)이다.
    /// 문을 이 사이에 끼워 넣어야 벽에 박힌 문처럼 보인다.
    /// </summary>
    private const float WallCenterX = -9.22f;

    /// <summary>
    /// 문 가장자리를 벽 속으로 묻는 여유. 벽 두께 안에 숨으므로 겉에서는 안 보이고 틈도 안 생긴다.
    /// (예전엔 0.35m 씩 키워 문이 벽면 위로 덧댄 판처럼 삐져나왔다.)
    /// </summary>
    private const float Overlap = 0.04f;

    /// <summary>문 두께. 벽(0.3m)보다 얇게 해서 앞뒤 어느 쪽 벽면 위로도 튀어나오지 않게 한다.</summary>
    private const float DoorThickness = 0.24f;

    /// <summary>
    /// 벽이 놓인 방향. 벽면이 z 6.6m 에 x 0.04m 기울어 있어(0.35°) 문 정면도 그만큼 돌린다.
    /// 문이 93° 로 3° 틀어져 있어서 한쪽 끝은 튀어나오고 반대쪽엔 틈이 보였다.
    /// </summary>
    private const float WallFacingYaw = 90.35f;

    // 게임 전체에서 쓰는 강조색(로비·파츠·엔딩과 같은 호박색)
    private static readonly Color Amber = new Color32(0xF0, 0xA8, 0x30, 0xFF);
    private static readonly Color Idle = new Color32(0x3A, 0x3F, 0x44, 0xFF);
    private static readonly Color Cleared = new Color32(0x14, 0x16, 0x18, 0xFF);

    private Lab_Door door;
    private bool breached;
    private bool quietBreach;
    private GameObject gateModel;

    private const string GateResource = "AETHERRuntime/AetherFactoryGate";
    private const string DebrisResource = "AETHERRuntime/AetherDebrisChunks";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Apply();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    private static void Apply()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
            return;

        if (FindFirstObjectByType<AetherFactoryGateBreach>(FindObjectsInactive.Include) != null)
            return;

        GameObject host = new GameObject("AetherFactoryGateBreach");
        host.AddComponent<AetherFactoryGateBreach>();
    }

    private void Start()
    {
        door = FindDoor();

        if (door == null)
        {
            Debug.Log("[공장출입구] 쏘아서 여는 문(Lab_Door)을 찾지 못했습니다.");
            Destroy(gameObject);
            return;
        }

        WakeUpDoor();

        // 이어하기: 이미 연 문이면 터지는 연출 없이 열린 채로 둔다.
        if (AetherSave.HasFlag("05-lab-door") && !door.IsDisabled)
        {
            door.SetDoorToDownedForTest();
            AetherModernHUD.SetCurrentObjective("연구소 문이 열렸다.\n안으로 들어가 연구소를 지나라.");
        }

        FitDoorToOpening();
        AttachGateModel();
        StylePointers();

        door.OnDoorDisabled.RemoveListener(Breach);
        door.OnDoorDisabled.AddListener(Breach);

        // 이미 연 판이라면 길을 막지 않도록 바로 치운다(터지는 연출 없이).
        if (door.IsDisabled)
        {
            quietBreach = true;
            Breach();
        }

        Debug.Log($"[공장출입구] 준비 완료. 표적 네 개를 쏘면 열립니다.");
    }

    /// <summary>
    /// 꺼진 채로 저장된 문을 켜고, 표적 순서를 다시 짠다.
    /// 꺼져 있던 동안에는 Awake 가 돌지 않아 '지금 쏴야 할 표적'이 비어 있다.
    /// </summary>
    private void WakeUpDoor()
    {
        if (!door.gameObject.activeSelf)
        {
            door.gameObject.SetActive(true);
            Debug.Log("[공장출입구] 꺼져 있던 문을 켰습니다.");
        }

        if (door.CurrentActivePointer == null && !door.IsDisabled)
            door.ResetDoor();
    }

    /// <summary>
    /// 문을 출입구 구멍에 꼭 맞춘다.
    ///
    /// 원래 문은 가로 3.5m · 높이 2.5m 라 출입구(5.56m × 3.93m)보다 작아 사방에 틈이 있었다.
    /// 한때는 비율을 유지한 채 높이 기준으로 키웠는데, 그러면 가로가 0.6m 넘쳐 벽면 위로 판이
    /// 덧대진 것처럼 보였다. 지금은 문짝 판(LabDoor_Normal)만 재서 가로·높이·두께를 따로
    /// 구멍 크기 + 벽 속에 묻히는 4cm 로 맞춘다. 가장자리가 벽 두께 안에 숨어 틈도, 삐져나옴도 없다.
    /// </summary>
    private void FitDoorToOpening()
    {
        Renderer panel = FindPanel();
        if (panel == null)
            return;

        Transform t = door.transform;
        t.rotation = Quaternion.Euler(0f, WallFacingYaw, 0f);

        Vector3 target = new Vector3(
            DoorThickness,
            (OpeningTopY - OpeningFloorY) + Overlap * 2f,
            (OpeningMaxZ - OpeningMinZ) + Overlap * 2f);

        // 두어 번 재고 고치면 회전된 자식까지 포함한 실제 크기가 목표에 수렴한다.
        for (int i = 0; i < 3; i++)
        {
            Bounds b = panel.bounds;
            if (b.size.x < 0.01f || b.size.y < 0.01f || b.size.z < 0.01f)
                return;

            Vector3 s = t.localScale;
            // 문 기준 축: 정면(z)=월드 x(두께), 위(y)=월드 y(높이), 오른쪽(x)=월드 z(가로)
            s.z *= target.x / b.size.x;
            s.y *= target.y / b.size.y;
            s.x *= target.z / b.size.z;
            t.localScale = s;
        }

        Bounds after = panel.bounds;
        t.position += new Vector3(
            WallCenterX - after.center.x,
            (OpeningFloorY - Overlap + after.size.y * 0.5f) - after.center.y,
            ((OpeningMinZ + OpeningMaxZ) * 0.5f) - after.center.z);

        Bounds placed = panel.bounds;
        Debug.Log($"[공장출입구] 문을 출입구에 끼웠습니다. " +
                  $"가로 z {placed.min.z:F2}~{placed.max.z:F2}(구멍 {OpeningMinZ:F2}~{OpeningMaxZ:F2}), " +
                  $"높이 {placed.min.y:F2}~{placed.max.y:F2}(구멍 {OpeningFloorY:F2}~{OpeningTopY:F2}), " +
                  $"깊이 x {placed.min.x:F2}~{placed.max.x:F2}(벽 -9.37~-9.07)");
    }

    /// <summary>닫힌 문짝 판. 표적·레이저는 판 위에 붙은 장식이라 크기 재기에서 뺀다.</summary>
    private Renderer FindPanel()
    {
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.name == "LabDoor_Normal")
                return r;
        }

        return null;
    }

    /// <summary>
    /// 표적 색을 게임 전체의 강조색(호박색)에 맞춘다.
    ///
    /// 기본값은 빨강이라 이 게임의 색감과 겉돈다. 쏴야 할 표적은 호박색으로 빛나게 하고,
    /// 아직 차례가 아닌 것은 어두운 강철색, 맞힌 것은 꺼진 색으로 둔다.
    /// 색은 인스펙터에 직접 꽂는 값이라 코드에서 바꿔 넣는다.
    /// </summary>
    private void StylePointers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        FieldInfo original = typeof(Lab_DoorPointer).GetField("originalColor", flags);
        FieldInfo active = typeof(Lab_DoorPointer).GetField("activeColor", flags);
        FieldInfo cleared = typeof(Lab_DoorPointer).GetField("clearedColor", flags);
        FieldInfo autoRead = typeof(Lab_DoorPointer).GetField("autoReadOriginalColorOnAwake", flags);

        if (original == null || active == null || cleared == null)
            return;

        foreach (Lab_DoorPointer pointer in door.GetComponentsInChildren<Lab_DoorPointer>(true))
        {
            if (pointer == null)
                continue;

            // 머티리얼 색을 그대로 읽어 오는 설정이 켜져 있으면 우리 값이 덮인다.
            if (autoRead != null)
                autoRead.SetValue(pointer, false);

            original.SetValue(pointer, Idle);
            active.SetValue(pointer, Amber);
            cleared.SetValue(pointer, Cleared);
        }

        // 바꾼 색을 지금 상태에 반영한다.
        if (!door.IsDisabled)
            door.ResetDoor();

        StyleDoorSurface();
    }

    /// <summary>
    /// 문짝과 레이저 색을 게임 분위기에 맞춘다.
    ///
    /// 기본 문짝은 밝은 회청색 판이라 폐허가 된 공장 벽에 종이 한 장 붙여 놓은 것처럼 보였고,
    /// 표적을 잇는 레이저도 파란색이라 이 게임의 호박색 계열과 겉돌았다.
    /// 문짝은 어두운 강철색으로, 레이저는 호박색으로 바꾼다.
    /// </summary>
    private void StyleDoorSurface()
    {
        Color steel = new Color32(0x2A, 0x2E, 0x33, 0xFF);

        foreach (Renderer renderer in door.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            // 표적은 Lab_DoorPointer 가 직접 색을 관리하므로 건드리지 않는다.
            if (renderer.GetComponentInParent<Lab_DoorPointer>() != null)
            {
                if (IsBeam(renderer))
                    Paint(renderer, Amber);

                continue;
            }

            if (IsBeam(renderer))
            {
                Paint(renderer, Amber);
                continue;
            }

            Paint(renderer, steel);
        }
    }

    private static bool IsBeam(Renderer renderer)
    {
        if (renderer is LineRenderer)
            return true;

        // 표적을 잇는 파란 선은 가늘고 긴 원기둥("Cylinder")으로 만들어져 있다.
        string lower = renderer.gameObject.name.ToLowerInvariant();
        return lower.Contains("laser") || lower.Contains("beam") || lower.Contains("line") || lower.Contains("cylinder");
    }

    private static void Paint(Renderer renderer, Color color)
    {
        // 선은 머티리얼이 아니라 자기 색 값으로 그려진다.
        if (renderer is LineRenderer line)
        {
            line.startColor = color;
            line.endColor = color;
        }

        // 공유 머티리얼을 바꾸면 같은 재질을 쓰는 다른 물건까지 바뀐다. 사본에만 칠한다.
        Material[] materials = renderer.materials;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 0.35f);
        }

        renderer.materials = materials;
    }

    /// <summary>
    /// 표적을 다 맞혔을 때 문짝을 치워 길을 연다.
    ///
    /// 원래는 윤서가 QTE 로 마무리해 부수는 설계인데 이 씬에는 그 QTE 가 없다.
    /// 혼자서도 진행할 수 있도록 무력화되는 즉시 부수고, 남은 문짝 그림까지 치운다.
    /// </summary>
    private void Breach()
    {
        if (breached)
            return;

        breached = true;

        if (!door.IsBreached)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            door.BreachDoor(player != null ? player.transform : null);
        }

        // 문짝 그림을 치우기 전에, 그 자리에서 문이 터져 나가는 모습을 만든다.
        if (!quietBreach)
            Blast();

        // 이미 열린 판으로 시작했으면 게이트 문짝만 조용히 치운다(문틀은 남긴다).
        if (quietBreach && gateModel != null)
        {
            foreach (string leaf in new[] { "GatesDoorLeft", "GatesDoorRight", "GatesDoorSmall" })
            {
                Transform part = gateModel.transform.Find(leaf);
                if (part != null) part.gameObject.SetActive(false);
            }
        }

        int hidden = HideDoorPanels();

        Debug.Log($"[공장출입구] 문을 부쉈습니다(문짝 {hidden}개 치움). 이제 공장 안으로 들어갈 수 있습니다.");
    }

    /// <summary>
    /// 문이 터져 나가는 연출.
    ///
    /// 파편 프리팹·폭발 효과가 비어 있어서, 표적을 다 맞히면 문이 그 자리에서 그냥 사라졌다.
    /// 문짝을 두 쪽으로 갈라 안쪽 양옆으로 쓰러뜨리고(통로는 비워 둔다), 작은 파편·먼지 구름·
    /// 불꽃·섬광·폭음·화면 흔들림을 함께 낸다. 쓰러진 문짝은 바닥에 남지만 플레이어와는 부딪히지 않는다
    /// (플레이어가 리지드바디라 0.24m 판에도 걸릴 수 있다).
    /// </summary>
    private void Blast()
    {
        // 무력화되는 순간 '정상' 문짝은 꺼지고 '해제' 문짝(LabDoor_Downed)이 켜진다. 켜진 쪽을 잰다.
        Renderer panel = null;
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>(false))
        {
            if (r != null && r.name.StartsWith("LabDoor_", System.StringComparison.OrdinalIgnoreCase))
            {
                panel = r;
                break;
            }
        }

        if (panel == null)
            panel = FindPanel();
        if (panel == null)
            return;

        // 게이트 모델이 붙어 있으면 그 크기를 쓴다(문짝 상자는 그림이 꺼져 있다).
        Bounds b = gateModel != null ? LeafBounds(new[] { gateModel.transform }) : panel.bounds;
        if (b.size.sqrMagnitude < 0.01f)
            return;
        Material mat = panel.sharedMaterial;
        Vector3 inward = Vector3.right;   // 공장 안쪽(+x)
        Collider[] playerColliders = FindPlayerColliders();

        if (gateModel != null)
            LaunchGateLeaves(inward, playerColliders);
        else
            LaunchBoxHalves(b, mat, inward, playerColliders);

        LaunchChunks(b, inward, playerColliders);

        // 먼지 구름·불꽃·섬광
        AetherHitFX.DustCloud(b.center + inward * 0.3f, new Vector3(b.size.z, b.size.y, 0.4f), inward);
        AetherHitFX.DustCloud(b.center - inward * 0.3f, new Vector3(b.size.z * 0.8f, b.size.y * 0.8f, 0.3f), -inward);
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = new Vector3(b.min.x, Random.Range(b.min.y + 0.3f, b.max.y - 0.3f), Random.Range(b.min.z + 0.3f, b.max.z - 0.3f));
            AetherHitFX.SurfaceImpact(p, -inward, false, null);
        }

        AetherShotAudio.PlayTurretBoom(b.center, 1f);
        // 폭발 + 문짝·파편이 무너지며 부딪히는 금속음(예전엔 낮춘 총성 하나뿐)
        AetherAudio.PlayAt("explosion", b.center, 1f, 6f, 90f, 0.03f, 0.9f, 20);
        AetherAudio.PlayAt("debris", b.center + inward * 1.5f, 1f, 4f, 60f, 0.05f, 1f, 25);
        AetherHitFX.Shake(2.2f);
    }

    /// <summary>
    /// 게이트 모델의 문짝(왼쪽 / 오른쪽+쪽문)을 떼어 안쪽으로 넘어뜨린다. 문틀(GatesBorder)은 벽에 남는다.
    /// </summary>
    private void LaunchGateLeaves(Vector3 inward, Collider[] playerColliders)
    {
        Transform left = gateModel.transform.Find("GatesDoorLeft");
        Transform right = gateModel.transform.Find("GatesDoorRight");
        Transform small = gateModel.transform.Find("GatesDoorSmall");

        Transform[][] leaves = { new[] { left }, new[] { right, small } };
        for (int i = 0; i < leaves.Length; i++)
        {
            Transform main = leaves[i][0];
            if (main == null)
                continue;

            // 떼어 낸 문짝을 담을 빈 몸통. 문짝 메시는 그대로 복사해 넣는다.
            GameObject body = new GameObject(i == 0 ? "AETHER_GateLeafLeft" : "AETHER_GateLeafRight");
            Bounds lb = LeafBounds(leaves[i]);
            body.transform.position = lb.center;

            foreach (Transform part in leaves[i])
            {
                if (part == null) continue;
                GameObject copy = Instantiate(part.gameObject, part.position, part.rotation);
                copy.transform.localScale = part.lossyScale;
                foreach (Collider old in copy.GetComponentsInChildren<Collider>(true))
                    Destroy(old);
                copy.transform.SetParent(body.transform, true);
                part.gameObject.SetActive(false);
            }

            // 구멍 안에서 굴리면 문설주에 끼어 선 채로 남는다. 벽 안쪽 면(-9.07) 너머로 옮겨 시작한다.
            float shift = (-9.07f + 0.08f) - (lb.min.x);
            if (shift > 0f)
                body.transform.position += Vector3.right * shift;

            BoxCollider box = body.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(0.08f, lb.size.x), lb.size.y * 0.98f, lb.size.z * 0.98f);

            Rigidbody rb = body.AddComponent<Rigidbody>();
            rb.mass = 150f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            float side = lb.center.z > (OpeningMinZ + OpeningMaxZ) * 0.5f ? 1f : -1f;
            Vector3 top = body.transform.position + Vector3.up * lb.size.y * 0.4f;
            rb.AddForceAtPosition((inward * 4.5f + new Vector3(0f, 0.3f, side * 1.4f)) * rb.mass, top, ForceMode.Impulse);

            foreach (Collider pc in playerColliders)
                if (pc != null) Physics.IgnoreCollision(box, pc, true);

            StartCoroutine(SettleLater(rb, 6f));
        }
    }

    private static Bounds LeafBounds(Transform[] parts)
    {
        bool has = false;
        Bounds b = default;
        foreach (Transform p in parts)
        {
            if (p == null) continue;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    /// <summary>게이트 모델을 못 읽었을 때 쓰는 예전 방식(문 재질 상자 두 쪽).</summary>
    private void LaunchBoxHalves(Bounds b, Material mat, Vector3 inward, Collider[] playerColliders)
    {
        float halfWidth = b.size.z * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject half = GameObject.CreatePrimitive(PrimitiveType.Cube);
            half.name = "AETHER_GateHalf";
            float height = b.size.y * 0.92f;
            half.transform.position = new Vector3(-9.07f + b.size.x * 0.5f + 0.08f, OpeningFloorY + height * 0.5f + 0.03f,
                b.center.z + side * halfWidth * 0.5f);
            half.transform.localScale = new Vector3(b.size.x, height, halfWidth * 0.9f);
            half.GetComponent<Renderer>().sharedMaterial = mat;

            Rigidbody rb = half.AddComponent<Rigidbody>();
            rb.mass = 150f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 top = half.transform.position + Vector3.up * b.size.y * 0.4f;
            rb.AddForceAtPosition((inward * 4.5f + new Vector3(0f, 0.3f, side * 1.4f)) * rb.mass, top, ForceMode.Impulse);

            Collider c = half.GetComponent<Collider>();
            foreach (Collider pc in playerColliders)
                if (pc != null) Physics.IgnoreCollision(c, pc, true);

            StartCoroutine(SettleLater(rb, 6f));
        }
    }

    /// <summary>
    /// 작은 파편: 문틀 주변 벽에서 떨어져 나온 콘크리트 조각(Factory 팩 Debris01 을 작게 줄임)과
    /// 찢겨 나간 얇은 철판 조각을 섞는다.
    /// </summary>
    private void LaunchChunks(Bounds b, Vector3 inward, Collider[] playerColliders)
    {
        GameObject debrisSet = Resources.Load<GameObject>(DebrisResource);
        Material sheetMat = null;
        if (gateModel != null)
        {
            Renderer border = gateModel.GetComponentInChildren<Renderer>();
            if (border != null) sheetMat = border.sharedMaterial;
        }

        for (int i = 0; i < 24; i++)
        {
            bool concrete = debrisSet != null && debrisSet.transform.childCount > 0 && i % 3 != 0;
            GameObject chunk;

            if (concrete)
            {
                Transform pick = debrisSet.transform.GetChild(Random.Range(0, debrisSet.transform.childCount));
                chunk = Instantiate(pick.gameObject);
                foreach (Collider old in chunk.GetComponentsInChildren<Collider>(true))
                    DestroyImmediate(old);
                chunk.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
                chunk.AddComponent<BoxCollider>();
            }
            else
            {
                chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.transform.localScale = new Vector3(Random.Range(0.15f, 0.45f), 0.015f, Random.Range(0.1f, 0.3f));
                if (sheetMat != null) chunk.GetComponent<Renderer>().sharedMaterial = sheetMat;
            }

            chunk.name = "AETHER_GateChunk";
            chunk.transform.position = new Vector3(-8.65f, Random.Range(b.min.y + 0.2f, b.max.y - 0.2f), Random.Range(b.min.z + 0.2f, b.max.z - 0.2f));
            chunk.transform.rotation = Random.rotation;

            Rigidbody rb = chunk.AddComponent<Rigidbody>();
            rb.mass = concrete ? 4f : 1f;
            rb.linearVelocity = inward * Random.Range(4f, 10f) + Random.insideUnitSphere * 2.5f + Vector3.up * Random.Range(0.5f, 3f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;

            foreach (Collider c in chunk.GetComponentsInChildren<Collider>())
                foreach (Collider pc in playerColliders)
                    if (pc != null) Physics.IgnoreCollision(c, pc, true);

            Destroy(chunk, Random.Range(14f, 20f));
        }
    }

    /// <summary>
    /// 닫힌 문의 겉모습을 Factory 팩의 녹슨 양문 게이트(LargeGates1, 5.78×3.98m)로 바꾼다.
    ///
    /// 원래 문짝은 상자를 늘린 것이라 무늬가 늘어나 보였다. 출입구(5.56×3.93m)와 크기가 거의 같은
    /// 게이트를 구멍에 맞춰 끼우고, 문짝 상자는 그림만 끈다(충돌체·표적은 그대로 둔다).
    /// 문의 크기 비율이 서로 달라 문(늘어난 축척) 밑에 두면 찌그러지므로 따로 둔다.
    /// </summary>
    private void AttachGateModel()
    {
        GameObject prefab = Resources.Load<GameObject>(GateResource);
        if (prefab == null)
            return;

        gateModel = Instantiate(prefab);
        gateModel.name = "AETHER_FactoryGate";
        foreach (Collider c in gateModel.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        // 녹슨 면(모델 +z)이 바깥(-x)을 보게 한다. 벽 기울기 0.35° 도 맞춘다.
        Transform t = gateModel.transform;
        t.rotation = Quaternion.Euler(0f, WallFacingYaw + 180f, 0f);

        Bounds before = LeafBounds(new[] { t });
        if (before.size.y < 0.1f)
            return;

        float width = (OpeningMaxZ - OpeningMinZ) + Overlap * 2f;
        float height = (OpeningTopY - OpeningFloorY) + Overlap * 2f;
        t.localScale = new Vector3(width / before.size.z, height / before.size.y, 1f);

        Bounds after = LeafBounds(new[] { t });
        t.position += new Vector3(
            WallCenterX - after.center.x,
            (OpeningFloorY - Overlap + after.size.y * 0.5f) - after.center.y,
            ((OpeningMinZ + OpeningMaxZ) * 0.5f) - after.center.z);

        // 문짝 상자는 그림만 끈다. 무력화 그림(LabDoor_Downed)도 끄고 게이트가 대신 보이게 한다.
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.name.StartsWith("LabDoor_", System.StringComparison.OrdinalIgnoreCase))
                r.enabled = false;
        }

        Bounds placed = LeafBounds(new[] { t });
        Debug.Log($"[공장출입구] 녹슨 게이트 모델을 끼웠습니다. z {placed.min.z:F2}~{placed.max.z:F2}, y {placed.min.y:F2}~{placed.max.y:F2}, x {placed.min.x:F2}~{placed.max.x:F2}");
    }

    /// <summary>다 쓰러지고 나면 물리를 멈춰 바닥에 남긴다(계속 굴러다니지 않게).</summary>
    private static System.Collections.IEnumerator SettleLater(Rigidbody rb, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (rb != null)
        {
            rb.isKinematic = true;

            // 혹시라도 구멍 안에 선 채로 남았으면 길을 막으므로 치운다.
            Vector3 p = rb.position;
            bool upright = Vector3.Dot(rb.transform.up, Vector3.up) > 0.8f;
            if (upright && p.x < -8.4f && p.z > OpeningMinZ - 0.5f && p.z < OpeningMaxZ + 0.5f)
                rb.gameObject.SetActive(false);
        }
    }

    private static Collider[] FindPlayerColliders()
    {
        System.Collections.Generic.List<Collider> list = new System.Collections.Generic.List<Collider>();
        foreach (GameObject p in GameObject.FindGameObjectsWithTag("Player"))
            list.AddRange(p.transform.root.GetComponentsInChildren<Collider>(true));
        return list.ToArray();
    }

    /// <summary>
    /// 문짝 그림을 확실히 치운다.
    ///
    /// <see cref="Lab_Door"/> 는 '정상/무력화' 두 상태 오브젝트를 켜고 끄는 식으로 동작하는데,
    /// 그 참조가 실제 문짝(LabDoor_Normal / LabDoor_Downed)을 가리키지 않아
    /// 부순 뒤에도 닫힌 문이 그대로 보였다. 그래서 이름으로 직접 찾아 끈다.
    ///
    /// 혹시 이름이 다른 문짝이 더 있어도 놓치지 않도록, 남아 있는 그림도 함께 끈다.
    /// (표적은 문짝 안에 들어 있으므로 같이 사라진다.)
    /// </summary>
    private int HideDoorPanels()
    {
        int hidden = 0;

        foreach (Transform child in door.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == door.transform)
                continue;

            if (child.name.IndexOf("LabDoor_", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!child.gameObject.activeSelf)
                continue;

            child.gameObject.SetActive(false);
            hidden++;
        }

        // 이름이 다른 조각이 남아 있으면 그것도 치운다.
        foreach (Renderer renderer in door.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer == null || !renderer.gameObject.activeSelf)
                continue;

            renderer.gameObject.SetActive(false);
            hidden++;
        }

        return hidden;
    }

    private static Lab_Door FindDoor()
    {
        Vector3 openingCenter = new Vector3(-10.1f, 1.6f, (OpeningMinZ + OpeningMaxZ) * 0.5f);

        Lab_Door best = null;
        float bestDistance = float.MaxValue;

        foreach (Lab_Door candidate in FindObjectsByType<Lab_Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null)
                continue;

            float distance = Vector3.Distance(candidate.transform.position, openingCenter);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance < 8f ? best : null;
    }
}
