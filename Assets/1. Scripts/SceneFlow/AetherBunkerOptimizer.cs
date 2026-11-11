using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 04 벙커 전용 정리. 벙커에서 프레임이 크게 떨어지고 계단에서 버벅거리던 문제를 줄인다.
///
/// 원인(실측, 04 시작 지점):
///  - 플레이어는 지하 벙커에만 있는데 씬에 05 의 바깥 도시 전체(Map/Area1·Area2·DM 산)가 들어 있고,
///    카메라 거리 1000m·가림 정보 없음이라 매 프레임 4106 드로우콜·347만 삼각형을 그렸다.
///  - 그림자를 그리는 실시간 점광원이 23개(점광원 그림자는 한 개에 6면)라 그림자 물체 2682개를 여러 번 다시 그렸다.
///  - 계단(SM_Architecture_descent)이 13cm 단차가 촘촘한 메시 콜라이더 하나라 캡슐이 단마다 걸려 덜컹였다.
///
/// 하는 일(씬 파일은 건드리지 않고 실행 중에만):
///  - 벙커 밖 + 계단 출구에서도 먼 바깥 물체는 '그리기'만 끈다(충돌은 그대로).
///  - 점광원·태양 그림자를 끈다(지하라 태양 그림자는 원래 보이지 않는다).
///  - 플레이어 카메라 거리를 120m 로 줄인다.
///  - 계단 위에 보이지 않는 경사로를 깔아 미끄러지듯 오르게 한다.
/// </summary>
public class AetherBunkerOptimizer : MonoBehaviour
{
    private const string ScenePrefix = "04.";
    private const float KeepRadiusFromExit = 40f;
    private const float BunkerMargin = 8f;
    private const float CameraFar = 120f;

    // 계단(실측): 첫 단 x -102.45 에서 꼭대기 x -108.9(지면 y 0.0)까지. 아래쪽 단이 더 가팔라서
    // 바닥~꼭대기를 그냥 이으면 첫 단들이 경사로 위로 튀어나와 걸렸다. 단 끝(모서리)을 모두 덮는 선으로 잡는다:
    // (-102.75, -2.80) ~ (-108.75, 0.02), 기울기 0.47 → 바닥(y -3.26)과는 x -101.77 에서 만난다. 폭 z -109.0 ~ -105.9.
    private static readonly Vector3 StairBottom = new Vector3(-101.77f, -3.26f, -107.45f);
    private static readonly Vector3 StairTop = new Vector3(-108.9f, 0.09f, -107.45f);
    private const float StairWidth = 3.0f;
    private static readonly Vector3 StairExit = new Vector3(-109f, 0f, -107.9f);

    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += (s, m) => TryRun(s);
            subscribed = true;
        }
        TryRun(SceneManager.GetActiveScene());
    }

    private static void TryRun(Scene scene)
    {
        if (!scene.IsValid() || !scene.name.StartsWith(ScenePrefix))
            return;

        // AfterSceneLoad 와 sceneLoaded 가 첫 장면에서 둘 다 불러 두 번 돌던 것을 막는다.
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "AETHER_BunkerOptimizer")
                return;

        var go = new GameObject("AETHER_BunkerOptimizer");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherBunkerOptimizer>().scene = scene;
    }

    private Scene scene;

    private IEnumerator Start()
    {
        // 다른 부트스트랩(플레이어 교체, HUD)이 끝난 뒤에 한다.
        yield return null;
        yield return null;

        Bounds bunker = FindBunkerBounds();
        int culled = CullOutside(bunker);
        int lights = ReduceShadows();
        BuildStairRamp();
        Debug.Log($"[벙커 최적화] 바깥 물체 {culled}개 그리기 끔, 그림자 광원 {lights}개 끔, 계단 경사로 설치");

        // 카메라는 플레이어 교체·부활 때 바뀔 수 있어 가끔 다시 맞춘다.
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            Camera cam = Camera.main;
            if (cam != null && cam.farClipPlane > CameraFar)
                cam.farClipPlane = CameraFar;
            yield return wait;
        }
    }

    private Bounds FindBunkerBounds()
    {
        Bounds b = new Bounds(StairBottom, Vector3.one);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "BunkerEnd")
                    continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                    b.Encapsulate(r.bounds);
                return b;
            }
        }
        return b;
    }

    private int CullOutside(Bounds bunker)
    {
        Bounds keep = bunker;
        keep.Expand(BunkerMargin * 2f);
        int count = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Map")
                continue;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer)
                    continue;

                Bounds rb = r.bounds;

                // 계단 출구에서 보일 수 있는 가까운 바깥은 남긴다.
                if (rb.SqrDistance(StairExit) < KeepRadiusFromExit * KeepRadiusFromExit)
                    continue;

                // 벙커 안(천장 아래)에 걸친 것은 남긴다. 벙커 바로 위 지상 건물은 범위에 걸쳐도 안 보이므로 끈다.
                bool aboveGround = rb.min.y > -0.5f;
                if (keep.Intersects(rb) && !aboveGround)
                    continue;

                r.enabled = false;
                count++;
            }
        }

        return count;
    }

    private int ReduceShadows()
    {
        int count = 0;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l == null || l.gameObject.scene != scene || l.shadows == LightShadows.None)
                continue;
            if (l.type == LightType.Point || l.type == LightType.Directional || l.type == LightType.Spot)
            {
                l.shadows = LightShadows.None;
                count++;
            }
        }
        return count;
    }

    private void BuildStairRamp()
    {
        Vector3 along = StairTop - StairBottom;
        float length = along.magnitude;
        Vector3 dir = along / length;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 normal = Vector3.Cross(dir, side).normalized;
        if (normal.y < 0f) normal = -normal;

        // 단 모서리보다 살짝 위(10cm)에 표면을 두고, 아래쪽만 0.5m 더 늘려 벙커 바닥 속으로 묻는다
        // (위쪽을 늘리면 꼭대기 지면 위로 턱이 생긴다).
        const float lift = 0.0f;
        const float thickness = 0.3f;
        const float extendBottom = 0.3f;
        Vector3 surfaceMid = (StairBottom + StairTop) * 0.5f + Vector3.up * lift - dir * (extendBottom * 0.5f);

        var go = new GameObject("AETHER_StairRamp");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(surfaceMid - normal * (thickness * 0.5f), Quaternion.LookRotation(dir, normal));
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(StairWidth, thickness, length + extendBottom);
    }
}
