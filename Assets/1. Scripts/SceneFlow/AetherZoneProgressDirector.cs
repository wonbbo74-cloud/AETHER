using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 구역 클리어를 판정해서 엘리베이터의 다음 층 버튼을 열어준다.
///
/// ElevatorZoneProgress(잠금 해제 판정)에 클리어를 기록해 2·3구역 버튼이 차례로 열리게 한다.
///
/// 판정 기준은 "그 구역의 적을 모두 처치하면 클리어"이다.
/// </summary>
public class AetherZoneProgressDirector : MonoBehaviour
{
    private const float CheckInterval = 1f;

    // 씬 이름 → 구역 번호
    private static readonly string[] ZoneScenePrefixes = { "08.", "09.", "11." };
    private static readonly int[] ZoneNumbers = { 1, 2, 3 };

    private static bool subscribed;

    private int zoneNumber;
    private float timer;
    private bool sawAnyEnemy;
    private bool reported;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        EnsureForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    private static void EnsureForActiveScene()
    {
        int zone = GetZoneNumber(SceneManager.GetActiveScene().name);
        if (zone <= 0)
            return;

        GameObject host = new GameObject("AetherZoneProgressDirector");
        AetherZoneProgressDirector director = host.AddComponent<AetherZoneProgressDirector>();
        director.zoneNumber = zone;
    }

    /// <summary>씬 이름에서 구역 번호를 뽑는다. 구역 씬이 아니면 0.</summary>
    public static int GetZoneNumber(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return 0;

        for (int i = 0; i < ZoneScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(ZoneScenePrefixes[i], System.StringComparison.Ordinal))
                return ZoneNumbers[i];
        }

        return 0;
    }

    private void Update()
    {
        if (reported)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = CheckInterval;

        int alive = CountLivingEnemies();
        if (alive > 0)
        {
            sawAnyEnemy = true;
            return;
        }

        // 적이 한 번도 없었던 씬은 "클리어"로 볼 수 없다(스폰 실패일 수 있으므로).
        if (!sawAnyEnemy)
            return;

        reported = true;

        // 3구역은 최종 구역이라 열어 줄 다음 층이 없다.
        if (zoneNumber <= 2)
            ElevatorZoneProgress.MarkZoneCleared(zoneNumber);

        Debug.Log($"[AetherZoneProgress] {zoneNumber}구역의 적을 모두 처치했습니다. " +
                  (zoneNumber <= 2 ? $"엘리베이터의 {zoneNumber + 1}구역 버튼이 열립니다." : "최종 구역 정리 완료."));

        AlertLevelManager alert = AlertLevelManager.Instance;
        if (alert != null)
            alert.ApplyAlertEvent(AlertEventType.SuccessLarge);
    }

    /// <summary>살아있는 적 수. 로봇·드론·터렛을 모두 센다.</summary>
    private static int CountLivingEnemies()
    {
        int count = 0;

        RobotHealth[] robots = FindObjectsByType<RobotHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < robots.Length; i++)
        {
            if (robots[i] != null && robots[i].gameObject.activeInHierarchy)
                count++;
        }

        DroneHealth[] drones = FindObjectsByType<DroneHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drones.Length; i++)
        {
            if (drones[i] != null && drones[i].gameObject.activeInHierarchy)
                count++;
        }

        LegacyDroneHealth[] legacyDrones = FindObjectsByType<LegacyDroneHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < legacyDrones.Length; i++)
        {
            if (legacyDrones[i] != null && legacyDrones[i].gameObject.activeInHierarchy)
                count++;
        }

        EnemyTurretController[] turrets = FindObjectsByType<EnemyTurretController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] != null && turrets[i].gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }
}
