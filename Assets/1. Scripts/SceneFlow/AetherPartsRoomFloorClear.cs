using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 07 파츠 제작실 바닥에는 떠 있는 모듈·아이템을 하나도 두지 않는다.
/// 씬에 놓인 재료(Pickup_MicroEnergyCore, 어떤 레시피에도 안 쓰임)와, 들어올 때 저장에서 되살아난 떨어진 물건을
/// 들어온 뒤 몇 초 동안 찾아 끈다. 플레이어가 그 뒤에 직접 버린 물건은 건드리지 않는다.
/// </summary>
public class AetherPartsRoomFloorClear : MonoBehaviour
{
    private const float WatchSeconds = 6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("07.") || FindFirstObjectByType<AetherPartsRoomFloorClear>() != null)
            return;

        GameObject go = new GameObject("AETHER_PartsRoomFloorClear");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherPartsRoomFloorClear>();
    }

    private IEnumerator Start()
    {
        float until = Time.unscaledTime + WatchSeconds;
        while (Time.unscaledTime < until)
        {
            Clear();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private static void Clear()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null)
                continue;
            if (!(mb is PartsMaterialPickup || mb is InteractableItem || mb is ModulePickupInteractable || mb is PaperInteractable || mb is ModuleWorldMarker))
                continue;
            // 손에 든 것·플레이어 몸에 붙은 것은 제외.
            if (player != null && mb.transform.IsChildOf(player.root))
                continue;
            mb.gameObject.SetActive(false);
        }
    }
}
