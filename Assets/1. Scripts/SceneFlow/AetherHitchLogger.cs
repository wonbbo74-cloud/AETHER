using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 점검용: 실행 파일을 "-hitchlog" 로 켜면 0.08초가 넘는 프레임을 Player.log 에 적는다.
/// "-hitchscene=장면이름" 을 주면 그 장면을 바로 연다. 플래그가 없으면 아무 일도 하지 않는다.
/// </summary>
public class AetherHitchLogger : MonoBehaviour
{
    private float sceneStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool enabled = false;
        string scene = null;
        foreach (string a in args)
        {
            if (a == "-hitchlog") enabled = true;
            if (a.StartsWith("-hitchscene=")) scene = a.Substring("-hitchscene=".Length);
        }
        if (!enabled) return;

        GameObject host = new GameObject("AETHER_HitchLogger");
        DontDestroyOnLoad(host);
        host.AddComponent<AetherHitchLogger>();
        SceneManager.sceneLoaded += (s, m) => Debug.Log($"[HITCH] scene loaded {s.name} at {Time.realtimeSinceStartup:F2}s");
        if (!string.IsNullOrEmpty(scene))
            SceneManager.LoadScene(scene);
    }

    private float actionsAt = -1f;
    private int step;

    private void Start()
    {
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a == "-hitchactions") actionsAt = 6f;
    }

    /// <summary>처음 하는 행동(첫 사격·알림·레벨업·소리)에서 멈칫하는지 본다.</summary>
    private void RunActions()
    {
        if (actionsAt < 0f || Time.realtimeSinceStartup < actionsAt) return;
        actionsAt = Time.realtimeSinceStartup + 1.5f;
        step++;
        Debug.Log($"[HITCH] action {step} at {Time.realtimeSinceStartup:F2}s");

        Hankyul_Fire fire = FindFirstObjectByType<Hankyul_Fire>();
        switch (step)
        {
            case 1:
                Hankyul_Battle battle = FindFirstObjectByType<Hankyul_Battle>();
                if (battle != null)
                {
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    typeof(Hankyul_Battle).GetField("isBattle", flags)?.SetValue(battle, true);
                    typeof(Hankyul_Battle).GetMethod("RefreshCombatPresentation", flags)?.Invoke(battle, null);
                }
                break;
            case 2: if (fire != null) fire.ShootOnce(); break;
            case 3: if (fire != null) fire.ShootOnce(); break;
            case 4: AetherObjectiveToast.Show("hitch test", 2f); break;
            case 5: AetherLevel.ReportPuzzle("hitch-test", 400, "test"); break;
            case 6: AetherAudio.Play2D("ui_confirm", 0.5f, 1f, 0f); AetherAudio.Play2D("receive", 0.5f, 1f, 0f); break;
            case 7: if (fire != null) fire.TryReload(); break;
            default: actionsAt = -1f; break;
        }
    }

    private void Update()
    {
        RunActions();
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.08f && Time.frameCount > 5)
            Debug.Log($"[HITCH] {dt * 1000f:F0}ms frame {Time.frameCount} t={Time.realtimeSinceStartup:F2}s scene={SceneManager.GetActiveScene().name}");
    }
}
