using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 한 판 동안의 전적을 센다. 작전 실패 화면의 빈칸을 채우는 숫자들이다.
///
/// 생존 시간·처치·가한 피해를 어디서도 세고 있지 않아서, 화면에 채울 값이 없었다.
/// 구역에 들어설 때마다 0 부터 다시 센다.
/// </summary>
public static class AetherRunStats
{
    private static float startedAt;
    private static string sector = "-";

    public static int Kills { get; private set; }
    public static float DamageDealt { get; private set; }

    /// <summary>이번 구역에서 버틴 시간(초).</summary>
    public static float SurvivedSeconds => Mathf.Max(0f, Time.time - startedAt);

    /// <summary>도달 구역 이름. 씬 이름 앞의 번호를 떼어 낸다. ("05.폐허된 도시" → "폐허된 도시")</summary>
    public static string Sector => sector;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        BeginRun(SceneManager.GetActiveScene().name);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        // 05 에서 퍼즐2 를 다녀오면 같은 구역인데도 0 부터 다시 세어, 실패 화면의 처치 수가 퍼즐 뒤의 것만 나왔다.
        // 퍼즐2 는 세지 않고, 퍼즐2 에서 원래 구역으로 돌아올 때도 이어서 센다.
        if (scene.name == "Puzzle2" || scene.name == "Puzzle3")
        {
            inPuzzle = true;
            return;
        }

        if (inPuzzle && TrimSceneNumber(scene.name) == sector)
        {
            inPuzzle = false;
            return;
        }

        inPuzzle = false;
        BeginRun(scene.name);
    }

    private static bool inPuzzle;

    private static void BeginRun(string sceneName)
    {
        startedAt = Time.time;
        Kills = 0;
        DamageDealt = 0f;
        sector = TrimSceneNumber(sceneName);
    }

    /// <summary>플레이어가 적에게 입힌 피해. 적 체력 쪽에서 불러 준다.</summary>
    public static void ReportDamageDealt(float amount)
    {
        if (amount > 0f)
            DamageDealt += amount;
    }

    /// <summary>플레이어가 적을 쓰러뜨렸을 때.</summary>
    public static void ReportKill()
    {
        Kills++;
    }

    /// <summary>"05.폐허된 도시" 처럼 앞에 붙은 번호를 떼어 낸다.</summary>
    private static string TrimSceneNumber(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return "-";

        // 04 는 씬 이름이 "04.Tutorial1" 이라 작전 실패 화면에 영어 내부 이름이 그대로 나왔다. 장소 이름으로 보여 준다.
        if (sceneName.StartsWith("04.")) return "지하 벙커";
        if (sceneName.StartsWith("06.")) return "연구소";

        int dot = sceneName.IndexOf('.');
        if (dot < 0 || dot + 1 >= sceneName.Length)
            return sceneName;

        // 점 앞이 숫자일 때만 뗀다.
        for (int i = 0; i < dot; i++)
        {
            if (!char.IsDigit(sceneName[i]))
                return sceneName;
        }

        return sceneName.Substring(dot + 1).Trim();
    }

    /// <summary>분:초 로 적는다. 화면의 "— : —" 자리에 들어간다.</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int whole = Mathf.FloorToInt(seconds);
        return $"{whole / 60:00} : {whole % 60:00}";
    }
}
