using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AetherModernHUDGlobalBootstrap : MonoBehaviour
{
    private const string HudPrefabResourcePath = "UI/PF_GameplayHUD_ReferenceExact";

    /// <summary>
    /// 플레이 중이 아닌 화면(시작·선택·로딩·로비)에는 HUD를 띄우지 않는다.
    ///
    /// 플레이어 선택 씬의 선택 버튼 이름이 "Yunseo"라서, 이름으로 플레이어를 찾는 검사에
    /// 걸려 체력·탄약 표시가 메뉴 위에 떠 있었다. 씬 이름으로 먼저 걸러 낸다.
    /// </summary>
    private static readonly string[] NonGameplayScenes =
    {
        "01.스타트씬",
        "02.플레이어선택",
        "03.LoadingScene",
        "Loading",
        "MainMenu",
        "LobbyScene",
        // The 05 robot circuit puzzle scene. The gameplay HUD (HP, ammo, compass, crosshair) stayed on top of the puzzle and covered the timer.
        "Puzzle2",
        // 06 보안 게이트의 숫자 해독(퍼즐3)도 같은 이유.
        "Puzzle3",
    };

    private static AetherModernHUDGlobalBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance == null)
        {
            GameObject runner = new GameObject("AETHER_ModernHUD_GlobalBootstrap");
            instance = runner.AddComponent<AetherModernHUDGlobalBootstrap>();
            DontDestroyOnLoad(runner);
        }

        instance.EnsureHudForCurrentScene();

        // 구역 씬처럼 플레이어가 나중에 생성되는 경우, 처음 한 번만 확인하면 HUD가 영영 안 생긴다.
        // (sceneLoaded 이벤트는 '처음 열린 씬'에는 발생하지 않는다.)
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.DelayedEnsureHud());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureHudForCurrentScene();
        StopAllCoroutines();
        StartCoroutine(DelayedEnsureHud());
    }

    private IEnumerator DelayedEnsureHud()
    {
        // 구역 씬은 AetherZoneBootstrap이 Start에서 플레이어를 만들고,
        // NavMesh 배치까지 끝나야 자리를 잡는다. 넉넉히 기다린다.
        float endTime = Time.unscaledTime + 15f;
        while (Time.unscaledTime < endTime)
        {
            if (EnsureHudForCurrentScene())
                yield break;

            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    /// <summary>HUD가 (이미 있든 방금 만들었든) 화면에 있으면 true.</summary>
    private bool EnsureHudForCurrentScene()
    {
        // 메뉴 화면이면 더 볼 것 없이 끝낸다(이미 떠 있던 HUD도 치운다).
        if (IsNonGameplayScene(SceneManager.GetActiveScene().name))
        {
            foreach (AetherModernHUD stray in FindObjectsOfType<AetherModernHUD>(true))
            {
                if (stray != null)
                    Destroy(stray.gameObject);
            }

            return true;
        }

        AetherModernHUD[] huds = FindObjectsOfType<AetherModernHUD>(true);
        if (huds.Length > 0)
            return true;

        if (!HasGameplayPlayerInScene())
            return false;

        // 파츠 제작실(07)에서 쓰는 HUD 프리팹을 그대로 띄워, 모든 씬의 화면 구성이 같게 만든다.
        GameObject prefab = Resources.Load<GameObject>(HudPrefabResourcePath);
        GameObject hudObject;

        if (prefab != null)
        {
            hudObject = Instantiate(prefab);
            hudObject.name = prefab.name;
        }
        else
        {
            Debug.LogWarning($"[ModernHUD] Resources/{HudPrefabResourcePath} 를 찾지 못해 기본 HUD를 만듭니다.");
            hudObject = new GameObject(AetherModernHUD.RootName, typeof(RectTransform));
            hudObject.AddComponent<AetherModernHUD>();
        }

        DontDestroyOnLoad(hudObject);

        HUDDuplicateValidator validator = hudObject.GetComponent<HUDDuplicateValidator>();
        if (validator == null)
            validator = hudObject.AddComponent<HUDDuplicateValidator>();
        validator.Validate();

        Debug.Log($"[ModernHUD] '{SceneManager.GetActiveScene().name}'에 HUD를 띄웠습니다: {hudObject.name}");
        return true;
    }

    private static bool IsNonGameplayScene(string sceneName)
    {
        if (AetherCinematics.IsCinematicScene(sceneName)) return true;
        for (int i = 0; i < NonGameplayScenes.Length; i++)
        {
            if (string.Equals(sceneName, NonGameplayScenes[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasGameplayPlayerInScene()
    {
        if (FindObjectOfType<Move_Hankyul>() != null)
            return true;

        if (FindObjectOfType<Hankyul_Move>() != null)
            return true;

        if (FindObjectOfType<Move_Yunseo>() != null)
            return true;

        if (FindObjectOfType<Yunseo_Move>() != null)
            return true;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            string playerName = taggedPlayer.name.ToLowerInvariant();
            if (playerName.Contains("hankyul") || playerName.Contains("yunseo"))
                return true;
        }

        GameObject named = GameObject.Find("Hankyul");
        if (named != null)
            return true;

        named = GameObject.Find("Hankyul_찐");
        if (named != null)
            return true;

        named = GameObject.Find("Yunseo");
        if (named != null)
            return true;

        named = GameObject.Find("Yunseo 1");
        if (named != null)
            return true;

        named = GameObject.Find("Yunseo 1_1");
        if (named != null)
            return true;

        named = GameObject.Find("Yunseo_cloth'");
        return named != null;
    }
}
