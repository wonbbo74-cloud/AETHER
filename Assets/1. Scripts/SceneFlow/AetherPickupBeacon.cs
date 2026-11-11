using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 바닥에 떨어진 작은 물건(물·식량·에너지팩·실드팩·버린 물건)에 05 모듈과 같은 표식(청록 마름모 + 아이템 그림)을 띄운다.
/// 04 의 물병처럼 바닥에 놓인 작은 물체는 눈에 잘 띄지 않아 지나치기 쉬웠다.
/// 씬에 놓인 것뿐 아니라 나중에 생기는 것(연구소 보급품, 버린 물건)도 1초마다 찾아 붙인다.
///
/// 05 모듈도 같은 표식으로 벙커 아이템처럼 맞춘다. 모듈은 원래 작은 표식이 2m 위에 떠 있어 잘 안 보였고,
/// 작은 열쇠·케이블은 땅속에 완전히 묻혀(맨 위가 지면보다 낮음) 아예 보이지 않았다. 처음 한 번 바닥 위로 올린다.
/// </summary>
public class AetherPickupBeacon : MonoBehaviour
{
    private static readonly Vector3 Offset = new Vector3(0f, 0.55f, 0f);
    private static readonly Vector2 Size = new Vector2(0.2f, 0.2f);
    private const float VisibleDistance = 16f;

    // 0.28 은 10m 밖에서 작은 점으로만 보였다(화면 확인). 모듈은 찾으러 다니는 물건이라 크게.
    private static readonly Vector2 ModuleSize = new Vector2(0.9f, 0.9f);   // 0.55 → 0.9 로 키움
    private const float ModuleVisibleDistance = 22f;
    private const float ModuleMarkerGap = 0.45f;

    private static AetherPickupBeacon instance;
    private float nextScan;
    private readonly HashSet<int> modulesDone = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_PickupBeacon");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherPickupBeacon>();
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (instance == null) return;
            instance.nextScan = 0f;
            instance.modulesDone.Clear();
        };
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 1f;

        foreach (InteractableItem item in FindObjectsByType<InteractableItem>(FindObjectsSortMode.None))
        {
            if (item == null || item.itemData == null || item.GetComponent<ModuleWorldMarker>() != null)
                continue;

            ModuleWorldMarker marker = item.gameObject.AddComponent<ModuleWorldMarker>();
            marker.SetLayout(Offset, Size, VisibleDistance);
            marker.Configure(item.itemData);
        }

        foreach (ModulePickupInteractable module in FindObjectsByType<ModulePickupInteractable>(FindObjectsSortMode.None))
        {
            if (module == null)
                continue;

            // 겹쳐 놓인 안쪽 본체(같은 자리의 자식 모듈)는 바깥 것만 다룬다.
            Transform parent = module.transform.parent;
            if (parent != null && parent.GetComponentInParent<ModulePickupInteractable>() != null)
                continue;

            // 줍기 상자가 모듈(약 1m)보다 훨씬 커서(6~9m) 모듈이 없는 곳을 봐도 줍기 안내가 떴다.
            // 안쪽 본체의 줍기는 끄고, 바깥 상자를 보이는 모양에 맞춘다. 본체가 나중에 붙을 수 있어 매번 맞춘다.
            FitPickupColliders(module);

            if (!modulesDone.Add(module.GetInstanceID()))
                continue;

            float top = LiftAboveGround(module.transform);
            ModuleWorldMarker marker = module.GetComponent<ModuleWorldMarker>();
            if (marker == null)
                continue;   // 표식은 모듈 부트스트랩이 붙인다. 없으면 건드리지 않는다.

            float above = Mathf.Max(0.3f, top - module.transform.position.y) + ModuleMarkerGap;
            marker.SetLayout(new Vector3(0f, above, 0f), ModuleSize, ModuleVisibleDistance);
        }
    }

    private const float PickupPad = 0.35f;     // 보이는 모양보다 이만큼(m) 넉넉히
    private const float PickupMinSize = 0.8f;  // 작은 열쇠도 조준하기 쉽게 최소 크기(m)

    private static void FitPickupColliders(ModulePickupInteractable root)
    {
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null)
                continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!any)
            return;

        foreach (ModulePickupInteractable inner in root.GetComponentsInChildren<ModulePickupInteractable>(true))
        {
            if (inner == root) continue;
            foreach (Collider c in inner.GetComponents<Collider>()) c.enabled = false;
            inner.enabled = false;
        }

        Vector3 worldSize = bounds.size + Vector3.one * PickupPad;
        worldSize = Vector3.Max(worldSize, Vector3.one * PickupMinSize);
        bool first = true;
        foreach (BoxCollider box in root.GetComponents<BoxCollider>())
        {
            if (!first) { box.enabled = false; continue; }   // 같은 물체에 두 개씩 붙어 있었다
            first = false;
            Transform t = box.transform;
            Vector3 s = t.lossyScale;
            box.center = t.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        }
    }

    /// <summary>보이는 모양의 맨 아래가 땅 밑이면 땅 위로 올린다. 올린 뒤의 맨 위 높이를 돌려준다.</summary>
    private static float LiftAboveGround(Transform root)
    {
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null)
                continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!any)
            return root.position.y;

        Vector3 probe = new Vector3(bounds.center.x, bounds.max.y + 3f, bounds.center.z);
        float ground = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(probe, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신·같은 자리에 겹친 다른 모듈은 땅이 아니다. 머리 위 지붕·잔해 윗면도 땅으로 치지 않는다.
            if (hit.collider.transform.IsChildOf(root) || hit.collider.GetComponentInParent<ModulePickupInteractable>() != null)
                continue;
            if (hit.point.y > bounds.max.y + 0.3f)
                continue;
            if (hit.point.y > ground)
                ground = hit.point.y;
        }
        if (float.IsNegativeInfinity(ground))
            return bounds.max.y;

        float lift = ground + 0.02f - bounds.min.y;
        if (lift > 0.005f && lift < 1.5f)
        {
            root.position += Vector3.up * lift;
            return bounds.max.y + lift;
        }
        return bounds.max.y;
    }
}
