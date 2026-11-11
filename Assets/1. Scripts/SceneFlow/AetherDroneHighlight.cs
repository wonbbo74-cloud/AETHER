using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 드론을 눈에 띄게 한다(10/11 사용자 요청).
///
/// 드론은 1.4m 남짓한 어두운 기체라 05 의 안개·연기 속에서 거의 보이지 않았다.
/// - 기체를 조금 키우고(1.3배), 몸체의 청록 발광을 두 배로 밝힌다.
/// - 아래에 붉은 경고등을 달아 깜빡이게 한다(자폭 드론이 다가오는 걸 멀리서도 알 수 있게).
/// 2초마다 새로 생긴 드론도 찾아 붙인다.
/// </summary>
public class AetherDroneHighlight : MonoBehaviour
{
    private const float ScaleUp = 1.3f;
    private const float EmissionBoost = 2.2f;

    private static AetherDroneHighlight instance;
    private readonly HashSet<int> done = new HashSet<int>();
    private readonly List<Light> beacons = new List<Light>();
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_DroneHighlight");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherDroneHighlight>();
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (instance == null) return;
            instance.done.Clear();
            instance.beacons.Clear();
            instance.nextScan = 0f;
        };
    }

    private void Update()
    {
        // 경고등 깜빡임: 1초에 한 번 크게, 사이는 약하게.
        float pulse = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI)), 6f);
        for (int i = beacons.Count - 1; i >= 0; i--)
        {
            if (beacons[i] == null) { beacons.RemoveAt(i); continue; }
            beacons[i].intensity = 6f * pulse;
        }

        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 2f;

        foreach (Drones drone in FindObjectsByType<Drones>(FindObjectsSortMode.None))
        {
            if (drone == null || !done.Add(drone.GetInstanceID()))
                continue;
            Highlight(drone);
        }
    }

    /// <summary>드론이 아닌 연출용 비행체(AetherSkyDrones)에도 같은 강조를 붙인다.</summary>
    public static void Apply(Transform root)
    {
        if (instance != null && root != null)
            instance.HighlightRoot(root);
    }

    private void Highlight(Drones drone) => HighlightRoot(drone.transform);

    private void HighlightRoot(Transform t)
    {
        Component drone = t;

        // 이미 강조된 드론을 복제한 것이면(경고등까지 같이 복제됨) 다시 키우거나 밝히지 않고 깜빡임만 맡는다.
        Transform copied = t.Find("DroneWarningLight");
        if (copied != null)
        {
            Light existing = copied.GetComponent<Light>();
            if (existing != null && !beacons.Contains(existing)) beacons.Add(existing);
            return;
        }

        t.localScale *= ScaleUp;

        Bounds bounds = new Bounds(t.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in drone.GetComponentsInChildren<Renderer>())
        {
            if (r == null || r is ParticleSystemRenderer || r.GetComponentInParent<Canvas>() != null)
                continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);

            // 재질을 복사해서 이 드론의 발광만 밝힌다(공용 재질을 건드리지 않는다).
            Material[] mats = r.materials;
            foreach (Material m in mats)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                Color e = m.GetColor("_EmissionColor");
                if (e.maxColorComponent < 0.05f) e = new Color(0f, 1.2f, 1.4f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", e * EmissionBoost);
            }
            r.materials = mats;
        }

        GameObject lightGo = new GameObject("DroneWarningLight");
        lightGo.transform.SetParent(t, false);
        lightGo.transform.position = (any ? bounds.center : t.position) + Vector3.down * 0.25f;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.18f, 0.12f);
        light.range = 7f;
        light.intensity = 4f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        beacons.Add(light);
    }
}
