using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 씬의 플레이어가 파츠 제작실(07)의 플레이어와 같은 기능·설정을 갖도록 맞춘다.
///
/// 씬마다 다른 플레이어 프리팹이 놓여 있었다.
///   07 파츠 제작실 : Hankyul_Player  — 스크립트 13종, EMP 수류탄 O
///   05 / 11        : Hankyul_찐      — 스크립트 11종, EMP 수류탄 X
///
/// 처음에는 씬의 플레이어를 통째로 07 프리팹으로 바꿔 끼웠는데, 실행 중에 플레이어를 부수면
/// PhysicBones·Animator·오디오 리스너가 한꺼번에 무너지면서 'Invalid AABB'와
/// 'IsFinite(d)' 어서션이 쏟아졌다. 그래서 <b>부수지 않고 모자란 것만 채우는</b> 방식으로 바꿨다.
///
///  - 새로 스폰하는 구역(1~3구역)은 처음부터 07 프리팹을 쓴다(AetherZoneBootstrap).
///  - 이미 플레이어가 놓여 있는 씬(05/11)은 빠진 기능을 07 프리팹에서 그대로 복사해 붙인다.
/// </summary>
public class AetherPlayerUnifier : MonoBehaviour
{
    private const string CanonicalResourcePath = "AETHERRuntime/Hankyul_Player";

    private static readonly string[] IgnoredScenePrefixes = { "01.", "02.", "03.", "MainMenu", "Loading" };

    private static bool subscribed;
    private static AetherPlayerUnifier instance;

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

        Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();

        if (instance != null)
            instance.Rescan();
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherPlayerUnifier");
        instance = host.AddComponent<AetherPlayerUnifier>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Rescan();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Rescan()
    {
        StopAllCoroutines();
        StartCoroutine(UnifyWhenReady());
    }

    private static bool IsIgnoredScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        for (int i = 0; i < IgnoredScenePrefixes.Length; i++)
        {
            if (sceneName.StartsWith(IgnoredScenePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private IEnumerator UnifyWhenReady()
    {
        if (IsIgnoredScene(SceneManager.GetActiveScene().name))
            yield break;

        for (int frame = 0; frame < 240; frame++)
        {
            Move_Hankyul existing = FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Exclude);
            if (existing != null)
            {
                // 물리·애니메이션이 자리를 잡은 뒤에 손대야 안전하다.
                yield return new WaitForSeconds(0.5f);
                Unify(existing);
                yield break;
            }

            yield return null;
        }
    }

    private void Unify(Move_Hankyul move)
    {
        GameObject player = move.transform.root.gameObject;

        GameObject canonical = Resources.Load<GameObject>(CanonicalResourcePath);
        if (canonical == null)
        {
            Debug.LogWarning($"[AetherPlayerUnifier] 기준 프리팹을 찾지 못했습니다: Resources/{CanonicalResourcePath}");
            return;
        }

        int added = 0;

        // 1) EMP 수류탄 — 07 플레이어에만 있던 기능
        if (player.GetComponentInChildren<PlayerEMPThrower>(true) == null)
        {
            PlayerEMPThrower template = canonical.GetComponentInChildren<PlayerEMPThrower>(true);
            if (template != null)
            {
                PlayerEMPThrower thrower = move.gameObject.AddComponent<PlayerEMPThrower>();
                CopySerializedFields(template, thrower);

                // 씬마다 다른 참조는 이 플레이어 것으로 다시 잡아 준다.
                SetField(thrower, "animator", player.GetComponentInChildren<Animator>(true));
                SetField(thrower, "throwPoint", FindThrowPoint(player));
                added++;
            }
        }

        // 2) 파츠 능력에 필요한 구성 보장
        PlayerInventory inventory = move.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = move.GetComponentInChildren<PlayerInventory>(true);

        EquipmentManager equipment = move.GetComponent<EquipmentManager>();
        if (equipment == null)
        {
            equipment = move.gameObject.AddComponent<EquipmentManager>();
            added++;
        }

        if (inventory != null)
            equipment.SetInventory(inventory);

        if (move.GetComponent<HangyeolPartAbilityController>() == null)
        {
            move.gameObject.AddComponent<HangyeolPartAbilityController>();
            added++;
        }

        // 3) 조작 설정을 기준 프리팹과 동일하게 맞춘다(감도·시점 제한 등).
        Move_Hankyul canonicalMove = canonical.GetComponentInChildren<Move_Hankyul>(true);
        if (canonicalMove != null)
            CopyTuning(canonicalMove, move);

        // 4) 저장된 설정(감도·반전·음량)을 다시 적용
        AetherSettings.Apply();

        if (added > 0)
            Debug.Log($"[AetherPlayerUnifier] '{SceneManager.GetActiveScene().name}'의 플레이어에 " +
                      $"빠져 있던 기능 {added}개를 파츠 제작실 기준으로 채웠습니다.");
    }

    /// <summary>수류탄을 던질 위치. 없으면 카메라나 플레이어 앞쪽을 쓴다.</summary>
    private static Transform FindThrowPoint(GameObject player)
    {
        Transform[] all = player.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string n = all[i].name.ToLowerInvariant();
            if (n.Contains("throw") || n.Contains("hand_r") || n.Contains("righthand"))
                return all[i];
        }

        Camera camera = player.GetComponentInChildren<Camera>(true);
        return camera != null ? camera.transform : player.transform;
    }

    /// <summary>프리팹 컴포넌트의 인스펙터 값을 그대로 복사한다(씬 참조는 제외).</summary>
    private static void CopySerializedFields(Component source, Component target)
    {
        FieldInfo[] fields = source.GetType().GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.IsNotSerialized)
                continue;

            // 다른 씬의 오브젝트를 가리키는 참조는 복사하지 않는다.
            if (typeof(Component).IsAssignableFrom(field.FieldType) || field.FieldType == typeof(GameObject))
                continue;

            try
            {
                field.SetValue(target, field.GetValue(source));
            }
            catch (System.Exception)
            {
            }
        }
    }

    /// <summary>이동·시점 관련 수치만 기준 프리팹과 맞춘다.</summary>
    private static void CopyTuning(Move_Hankyul source, Move_Hankyul target)
    {
        target.walkSpeed = source.walkSpeed;
        target.runSpeed = source.runSpeed;
        target.crouchSpeed = source.crouchSpeed;
        target.jumpForce = source.jumpForce;
        target.minLookAngle = source.minLookAngle;
        target.maxLookAngle = source.maxLookAngle;
        target.smoothLook = source.smoothLook;
        target.lookSmoothTime = source.lookSmoothTime;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        if (target == null || value == null)
            return;

        FieldInfo field = target.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (field != null && field.FieldType.IsInstanceOfType(value))
            field.SetValue(target, value);
    }
}
