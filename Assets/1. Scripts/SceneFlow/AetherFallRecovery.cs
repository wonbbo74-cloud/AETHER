using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 밖으로 떨어졌을 때의 안전장치(04~07).
///
/// 2구역에는 전용 장치(AetherJumpCourseRecovery)가 있지만 04~07 에는 없어서,
/// 틈에 끼거나 벽 너머로 빠지면 캐릭터가 끝없이 떨어지기만 했다(점검 중 높이 -131m 까지 추락).
/// 바닥을 밟고 서 있을 때의 위치를 계속 기억해 두었다가, 그보다 25m 넘게 떨어지면 그 자리로 되돌린다.
/// </summary>
public class AetherFallRecovery : MonoBehaviour
{
    private const float FallDistance = 25f;
    private const float SampleInterval = 0.25f;

    private static AetherFallRecovery instance;

    private Transform player;
    private Vector3 safePosition;
    private bool hasSafe;
    private float nextSample;
    private bool enabledForScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (AetherRuntimeSwitch.DisableAll || instance != null)
            return;

        var go = new GameObject("AETHER_FallRecovery");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AetherFallRecovery>();
        // With domain reload off in the editor, an earlier subscription survives into the next play session, so unsubscribe before subscribing again.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        instance.OnScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
            instance.OnScene(scene);
    }

    private void OnScene(Scene scene)
    {
        string n = scene.name;
        enabledForScene = n.StartsWith("04.") || n.StartsWith("05.") || n.StartsWith("06.") || n.StartsWith("07.");
        player = null;
        hasSafe = false;
        KeepSingleEventSystem(); // 첫 프레임에 경고가 뜨기 전에 바로 한 번
        StartCoroutine(DedupeEventSystems());
    }

    /// <summary>
    /// 05 에는 화면 입력 장치(EventSystem)가 두 개 켜져 있어(씬 최상위 + labn 안) 매 프레임 경고가 쏟아졌다(2초에 120개 이상).
    /// 하나만 남기고 나머지는 끈다. 씬 시작 직후와 1초 뒤(실행 중 만들어지는 것) 두 번 정리한다.
    /// </summary>
    private System.Collections.IEnumerator DedupeEventSystems()
    {
        yield return null;
        KeepSingleEventSystem();
        yield return new WaitForSecondsRealtime(1f);
        KeepSingleEventSystem();
    }

    private static void KeepSingleEventSystem()
    {
        var all = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (all.Length <= 1)
            return;

        // 이미 쓰이고 있는 것(current)을 우선, 없으면 최상위에 있는 것을 남긴다.
        UnityEngine.EventSystems.EventSystem keep = UnityEngine.EventSystems.EventSystem.current;
        if (keep == null || !keep.isActiveAndEnabled)
        {
            keep = all[0];
            foreach (var es in all)
                if (es.transform.parent == null) { keep = es; break; }
        }

        foreach (var es in all)
        {
            if (es == keep) continue;
            foreach (var module in es.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
                module.enabled = false;
            es.enabled = false;
        }
    }

    private void Update()
    {
        if (!enabledForScene || Time.unscaledTime < nextSample)
            return;
        nextSample = Time.unscaledTime + SampleInterval;

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            hasSafe = false;
            if (player == null) return;
        }

        // 다른 구역 전용 복구 장치가 붙어 있으면 그쪽에 맡긴다.
        if (player.GetComponent<AetherJumpCourseRecovery>() != null)
            return;

        Vector3 p = player.position;

        if (IsStandingOnGround(p))
        {
            safePosition = p;
            hasSafe = true;
            return;
        }

        if (hasSafe && p.y < safePosition.y - FallDistance)
            Recover();
    }

    private bool IsStandingOnGround(Vector3 p)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 1.5f)
            return false;

        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(player.root)) continue;
            return true;
        }
        return false;
    }

    private void Recover()
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        Vector3 target = safePosition + Vector3.up * 0.3f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = target;
        }
        player.position = target;

        AetherObjectiveToast.Show("추락했다!\n마지막으로 서 있던 곳으로 돌아옴.", 3f);
        Debug.Log($"[추락 복구] {player.name} → {target}");
    }
}
