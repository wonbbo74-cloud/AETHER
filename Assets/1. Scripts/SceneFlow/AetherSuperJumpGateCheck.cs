using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 슈퍼점프가 "파츠를 제작해서 장착했을 때만" 켜지는지 실제로 돌려서 확인한다.
///
/// 코드를 읽어서 그럴 것 같다고 말하는 것과, 돌려 보고 그렇다고 말하는 것은 다르다.
/// 플레이 중에 다음을 그대로 찍는다.
///   1) 장착 전 — EquipmentManager.HasPart(SuperJump) 와 Move_Hankyul.IsSuperJumpEnabled()
///   2) 장착 후 — 같은 두 값이 켜지는지
///   3) 해제 후 — 다시 꺼지는지
///   4) 07에서 시작했다면, 장착한 채 1구역으로 넘어가서 유지되는지
///
/// 평소에는 아무것도 하지 않는다. AETHER/진단 메뉴로 켤 때만 돈다.
/// </summary>
public class AetherSuperJumpGateCheck : MonoBehaviour
{
    /// <summary>검사를 켜 두는 스위치. 에디터 메뉴가 이 값을 만진다.</summary>
    public const string EnabledKey = "AETHER_SUPERJUMP_GATE_CHECK";

    private const string SuperJumpPartResource = "AETHERParts/Part_superjump_part";

    /// <summary>07에서 장착한 뒤 씬을 넘어가는 검사를 진행 중인가.</summary>
    private static bool crossSceneCheckPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // 편집기 진단 전용. 스위치가 남아 있으면 07 에서 파츠를 멋대로 장착·해제하고 08 로 넘어가 버린다.
        // 빌드(실제 플레이)에서는 절대 돌지 않게 한다.
        if (!Application.isEditor)
            return;

        if (PlayerPrefs.GetInt(EnabledKey, 0) != 1)
            return;

        GameObject host = new GameObject("AETHER_SuperJumpGateCheck");
        DontDestroyOnLoad(host);
        host.AddComponent<AetherSuperJumpGateCheck>();
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

    private void Start()
    {
        StartCoroutine(RunCheck());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!crossSceneCheckPending)
            return;

        crossSceneCheckPending = false;
        StartCoroutine(ReportAfterTransition(scene.name));
    }

    private IEnumerator RunCheck()
    {
        Move_Hankyul move = null;
        for (int frame = 0; frame < 900 && move == null; frame++)
        {
            move = FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Exclude);
            if (move == null)
                yield return null;
        }

        if (move == null)
        {
            Report("플레이어를 찾지 못해 검사를 못 했습니다.");
            yield break;
        }

        yield return new WaitForSecondsRealtime(1.5f);

        EquipmentManager equipment = move.GetComponent<EquipmentManager>()
                                     ?? move.GetComponentInChildren<EquipmentManager>(true);
        HangyeolPartAbilityController ability = move.GetComponent<HangyeolPartAbilityController>()
                                                ?? move.GetComponentInChildren<HangyeolPartAbilityController>(true);

        string sceneName = SceneManager.GetActiveScene().name;
        Report($"씬 = {sceneName}");
        Report($"EquipmentManager {(equipment != null ? "있음" : "없음")} / " +
               $"HangyeolPartAbilityController {(ability != null ? "있음" : "없음")}");

        if (equipment == null || ability == null)
        {
            Report("필요한 부품이 없어 검사를 중단합니다.");
            yield break;
        }

        LogState("1) 장착 전", equipment, move);

        PartData part = Resources.Load<PartData>(SuperJumpPartResource);
        if (part == null)
        {
            Report($"슈퍼점프 파츠를 찾지 못했습니다: Resources/{SuperJumpPartResource}");
            yield break;
        }

        Report($"파츠 '{part.displayName}' — partType={part.partType}, slotType={part.slotType}");

        // 제작 후 장착했을 때와 같은 상태를 만든다.
        equipment.RestoreEquippedPart(part.slotType, part);
        ability.RefreshSuperJumpState();

        yield return new WaitForSecondsRealtime(0.5f);
        LogState("2) 장착 후", equipment, move);

        equipment.Unequip(part.slotType);
        ability.RefreshSuperJumpState();

        yield return new WaitForSecondsRealtime(0.5f);
        LogState("3) 해제 후", equipment, move);

        Report("여기까지: 1)과 3)은 꺼짐, 2)는 켜짐이어야 정상입니다.");

        // 제작실에서 시작했다면 씬을 넘어가도 유지되는지까지 본다.
        //
        // 제작실(07)에서 장착하고 1구역(08)으로 가면 새 플레이어가 생길 수 있다.
        // 그때 PartSystemRuntimeState가 장착 상태를 되살려 주지 않으면,
        // 분명히 장착했는데 1구역에서는 슈퍼점프가 안 되는 일이 생긴다.
        if (!sceneName.StartsWith("07.", System.StringComparison.Ordinal))
            yield break;

        equipment.RestoreEquippedPart(part.slotType, part);
        ability.RefreshSuperJumpState();
        PartSystemRuntimeState.BindEquipment(equipment);

        yield return new WaitForSecondsRealtime(0.5f);
        LogState("4) 씬 이동 직전", equipment, move);

        Report("4) 1구역으로 넘어갑니다 — 넘어간 뒤 상태를 다시 찍습니다.");
        crossSceneCheckPending = true;

        SceneManager.LoadScene("08.1구역_2");
    }

    private IEnumerator ReportAfterTransition(string sceneName)
    {
        Move_Hankyul move = null;
        for (int frame = 0; frame < 900 && move == null; frame++)
        {
            move = FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Exclude);
            if (move == null)
                yield return null;
        }

        yield return new WaitForSecondsRealtime(2.5f);

        if (move == null)
        {
            Report($"5) '{sceneName}'에서 플레이어를 찾지 못했습니다.");
            yield break;
        }

        EquipmentManager equipment = move.GetComponent<EquipmentManager>()
                                     ?? move.GetComponentInChildren<EquipmentManager>(true);

        if (equipment == null)
        {
            Report($"5) '{sceneName}'에 EquipmentManager가 없습니다.");
            yield break;
        }

        LogState($"5) 씬 이동 후 ({sceneName})", equipment, move);
        Report("5)가 켜짐이면 제작실에서 장착한 것이 1구역까지 따라옵니다.");
    }

    private static void LogState(string label, EquipmentManager equipment, Move_Hankyul move)
    {
        bool hasPart = equipment.HasPart(PartType.SuperJump);
        bool canJump = move.IsSuperJumpEnabled();

        Report($"{label}  HasPart(SuperJump)={hasPart}  /  IsSuperJumpEnabled()={canJump}");

        if (hasPart != canJump)
            Report($"{label}  ※ 두 값이 어긋납니다. 장착 상태와 실제 능력이 따로 놉니다.");
    }

    /// <summary>
    /// 이 Unity 콘솔은 Debug.LogWarning을 MCP로 읽을 때 걸러 버린다.
    /// 검사 결과를 놓치지 않도록 전부 일반 로그로 남긴다.
    /// </summary>
    private static void Report(string message)
    {
        Debug.Log("[슈퍼점프 검사] " + message);
    }
}
