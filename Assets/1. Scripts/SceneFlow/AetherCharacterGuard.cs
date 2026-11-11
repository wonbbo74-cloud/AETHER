using UnityEngine;

/// <summary>
/// Shiri 는 전투(총) 담당이 아니다. 2026-10-08 사용자 "Shiri 인데 총이 나온다(전투가 가능하다)".
///
/// 편집기에서는 재현되지 않았지만(선택 → 로비 → 04~07 모두 Shiri 만 켜짐), 씬에 미리 놓인 한결(Hankyul_찐, 총 포함)이
/// 다른 장치(네트워크 오브젝트 동기화, 부활, 씬 이동 등)에 의해 다시 켜지면 총과 한결 카메라가 함께 살아난다.
/// Shiri 를 고른 동안에는 1초마다 확인해서 켜져 있는 한결을 끈다. Shiri 가 실제로 켜져 있을 때만 끈다
/// (Shiri 가 없는 장면에서 플레이어를 통째로 없애지 않도록).
/// </summary>
public class AetherCharacterGuard : MonoBehaviour
{
    private static AetherCharacterGuard instance;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (AetherRuntimeSwitch.DisableAll || instance != null)
            return;

        var go = new GameObject("AETHER_CharacterGuard");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AetherCharacterGuard>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + 1f;

        if (AetherCharacterChoice.Selected != AetherCharacter.Yunseo)
            return;

        Move_Yunseo shiri = FindFirstObjectByType<Move_Yunseo>(FindObjectsInactive.Exclude);
        if (shiri == null)
            return;

        foreach (Move_Hankyul meka in FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (meka == null)
                continue;

            GameObject root = meka.transform.root.gameObject;
            if (root == shiri.transform.root.gameObject)
                continue;

            root.SetActive(false);
            Debug.Log($"[캐릭터] Shiri 로 플레이 중인데 한결({root.name})이 켜져 있어 껐습니다.");
        }

        // 한결 몸 밖에 따로 떨어진 총 스크립트도 끈다.
        foreach (Hankyul_Fire gun in FindObjectsByType<Hankyul_Fire>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (gun != null && gun.transform.root != shiri.transform.root && gun.GetComponentInParent<Move_Hankyul>() == null)
                gun.gameObject.SetActive(false);
        }
    }
}
