using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 미니맵 표식(캐릭터 머리 위에 눕혀 둔 화살표·점 캔버스)을 'Minimap' 층으로 옮긴다.
///
/// 표식이 'floor'·'Enemy' 층에 들어 있는 것이 많아 게임 화면에도 그려졌다.
/// 05 에서 Shiri 머리 위의 노란 화살표가 위를 볼 때 화면 위쪽을 크게 덮었고, 로봇 위의 점도 같았다.
/// 미니맵 카메라는 모든 층을 찍으므로 층만 바꾸면 미니맵에는 그대로 보인다.
/// 웨이브로 나중에 생기는 적도 있어 2초마다 다시 본다.
/// </summary>
public class AetherMinimapIconLayer : MonoBehaviour
{
    private static AetherMinimapIconLayer instance;
    private int minimapLayer = -1;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_MinimapIconLayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherMinimapIconLayer>();
        SceneManager.sceneLoaded += (s, m) => { if (instance != null) instance.nextScan = 0f; };
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 2f;

        if (minimapLayer < 0)
            minimapLayer = LayerMask.NameToLayer("Minimap");
        if (minimapLayer < 0)
            return;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || canvas.gameObject.layer == minimapLayer)
                continue;
            if (!IsMinimapIcon(canvas))
                continue;

            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = minimapLayer;
        }
    }

    /// <summary>위를 보고 눕혀 있고, 글자 없이 그림만 있으며, 캐릭터(플레이어·적) 아래에 달린 캔버스.</summary>
    private static bool IsMinimapIcon(Canvas canvas)
    {
        Transform t = canvas.transform;
        if (Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.x, 90f)) > 5f)
            return false;
        if (canvas.GetComponentInChildren<TMPro.TMP_Text>(true) != null || canvas.GetComponentInChildren<UnityEngine.UI.Text>(true) != null)
            return false;
        if (canvas.GetComponentInChildren<UnityEngine.UI.Image>(true) == null)
            return false;

        Transform root = t.root;
        return root.CompareTag("Player")
            || root.GetComponentInChildren<PlayerStatus>(true) != null
            || root.GetComponentInChildren<Robots>(true) != null
            || root.GetComponentInChildren<IDamageable>(true) != null
            || t.parent != null && t.parent.GetComponentInParent<IDamageable>(true) != null;
    }
}
