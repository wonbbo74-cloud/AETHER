using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬을 넘어가도 플레이어의 생존 수치(HP/목마름/실드)가 유지되도록 하는 런 단위 상태.
/// 인벤토리·장비는 PartSystemRuntimeState가, 재화는 GlobalManager(CurrencyManager)가 담당한다.
/// 이 클래스는 그 둘이 다루지 않던 PlayerStatus 값을 맡는다.
/// </summary>
public static class AetherRunState
{
    // 새 판이 시작되는 씬들. 여기로 들어오면 이전 판 기록을 버린다.
    private static readonly string[] RunResetScenePrefixes = { "01.", "02.", "MainMenu", "Tutorial0" };

    private static bool hasSnapshot;
    private static float hp;
    private static float thirst;
    private static float shield;

    private static PlayerStatus bound;
    private static bool subscribed;

    public static bool HasSnapshot => hasSnapshot;

    /// <summary>현재 추적 중인 플레이어가 살아있는지. 리스폰 등으로 사라지면 false가 된다.</summary>
    public static bool IsBound => bound != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            subscribed = true;
        }

        AetherRunStateRunner.Ensure();
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleActiveSceneChanged(Scene from, Scene to)
    {
        // 씬을 떠나기 직전 값이 남아있도록 한 번 더 확정 저장한다.
        Capture();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsRunResetScene(scene.name))
        {
            ResetRun();
            return;
        }

        bound = null;
        AetherRunStateRunner.Ensure();
        AetherRunStateRunner.RequestRebind();
    }

    private static bool IsRunResetScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < RunResetScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(RunResetScenePrefixes[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>새 판 시작: 생존 수치·수집 기록·재화를 모두 초기화한다.</summary>
    public static void ResetRun()
    {
        hasSnapshot = false;
        bound = null;
        hp = thirst = shield = 0f;

        PartSystemRuntimeState.ResetForNewRun();
        AetherLevel.ResetRun();
        AetherSave.ResetRun();
        // 지난 판에 푼 퍼즐 기록이 남아, 새 판에서 로봇 퍼즐 보상(설계서)이 퍼즐 없이 놓였다.
        PuzzleSceneFlowState.ImportCleared(null);

        // 구역 클리어 기록은 PlayerPrefs에 남아서 다음 판까지 따라온다.
        // 새 판에서는 1구역부터 다시 열어야 하므로 지운다.
        ElevatorZoneProgress.ResetProgressForDebug();

        if (GlobalManager.HasInstance && GlobalManager.Currency != null)
            GlobalManager.Currency.currentGold = 0;
    }

    /// <summary>씬에 있는 플레이어를 찾아 붙이고, 저장된 값이 있으면 복원한다.</summary>
    public static bool TryBind()
    {
        PlayerStatus status = FindPlayerStatus();
        if (status == null)
            return false;

        if (bound == status)
            return true;

        bound = status;

        if (hasSnapshot)
            Restore(status);
        else
            Capture();

        return true;
    }

    private static PlayerStatus FindPlayerStatus()
    {
        // 지금 조작 중인 캐릭터(한결·윤서)의 PlayerStatus를 최우선으로 고른다.
        GameObject player = AetherPlayerCharacter.FindActive();
        if (player != null)
        {
            PlayerStatus onPlayer = player.GetComponentInChildren<PlayerStatus>(true);
            if (onPlayer != null)
                return onPlayer;
        }

        PlayerStatus[] all = Object.FindObjectsByType<PlayerStatus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.activeInHierarchy)
                return all[i];
        }

        return null;
    }

    /// <summary>이 플레이어에 저장된 수치를 이미 되돌려 넣었는가(그 뒤에 해야 덮어써지지 않는 일이 있다).</summary>
    public static bool IsBoundTo(PlayerStatus status) => status != null && bound == status;

    public static void Capture()
    {
        if (bound == null)
            return;

        hp = bound.currentHP;
        thirst = bound.currentThirst;
        shield = bound.currentShield;
        hasSnapshot = true;
    }

    /// <summary>저장: 체력·목마름·실드.</summary>
    public static void Export(AetherSaveData data)
    {
        Capture();
        data.hp = hasSnapshot ? hp : -1f;
        data.thirst = thirst;
        data.shield = shield;
    }

    /// <summary>이어하기: 다음 장면의 플레이어에 이 값을 넣는다.</summary>
    public static void Import(AetherSaveData data)
    {
        bound = null;
        hasSnapshot = data.hp > 0f;
        hp = data.hp;
        thirst = data.thirst;
        shield = data.shield;
    }

    private static void Restore(PlayerStatus status)
    {
        // 죽은 상태로 저장됐다면 되살려서 넘긴다(씬 이동 직후 즉사 방지).
        float restoredHp = hp > 0f ? hp : Mathf.Max(1f, status.maxHP * 0.25f);

        status.SetHP(Mathf.Clamp(restoredHp, 0f, status.maxHP));
        status.SetThirst(Mathf.Clamp(thirst, 0f, status.maxThirst));

        if (status.useShield)
            status.SetShield(Mathf.Clamp(shield, 0f, status.maxShield));
    }
}

/// <summary>
/// AetherRunState가 매 프레임 값을 따라갈 수 있게 해주는 DontDestroyOnLoad 러너.
/// 플레이어는 씬 로드 직후 몇 프레임 뒤에 생성될 수 있으므로 잠시 재시도한다.
/// </summary>
[DefaultExecutionOrder(10000)]
public class AetherRunStateRunner : MonoBehaviour
{
    private const float RebindWindowSeconds = 5f;

    private static AetherRunStateRunner instance;
    private float rebindTimer;

    public static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherRunStateRunner");
        instance = host.AddComponent<AetherRunStateRunner>();
        DontDestroyOnLoad(host);
    }

    public static void RequestRebind()
    {
        if (instance != null)
            instance.rebindTimer = RebindWindowSeconds;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        rebindTimer = RebindWindowSeconds;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (rebindTimer > 0f)
        {
            rebindTimer -= Time.unscaledDeltaTime;
            if (AetherRunState.TryBind())
                rebindTimer = 0f;
            return;
        }

        // 리스폰 등으로 추적하던 플레이어가 사라졌으면 다시 찾는다.
        if (!AetherRunState.IsBound)
        {
            rebindTimer = RebindWindowSeconds;
            return;
        }

        AetherRunState.Capture();
    }

    private void OnApplicationQuit()
    {
        AetherRunState.Capture();
    }
}
