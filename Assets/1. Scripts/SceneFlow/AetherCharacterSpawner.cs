using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬이 열릴 때마다 플레이어 선택 화면에서 고른 캐릭터로 플레이하게 만든다.
///
/// 씬마다 플레이어가 놓인 방식이 다르다.
///   04·05·11 : 씬에 한결(Hankyul_찐)이 켜져 있다. 05 에는 윤서도 꺼진 채로 있다.
///   07       : 씬에 한결(Hankyul_Player)이 있다.
///   06·08·09·11 : 씬에는 없고 AetherZoneBootstrap 등이 생성한다(EnsureSelectedPlayer 를 부른다).
/// 그래서 '켜져 있는 플레이어'를 고른 캐릭터로 바꿔 끼운다. 씬에 고른 캐릭터가 꺼진 채로
/// 있으면 그것을 같은 자리에 켜고, 없으면 Resources/AETHERRuntime 에서 만든다.
///
/// 적은 바뀐 플레이어를 AetherPlayerCharacter.RefreshTarget 으로 다시 찾는다.
/// </summary>
public static class AetherCharacterSpawner
{
    private const string ResourceFolder = "AETHERRuntime/";
    private const string HankyulResource = ResourceFolder + "Hankyul_Player";
    private const string YunseoResource = ResourceFolder + "Yunseo_Player";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyChoice(scene);
    }

    /// <summary>
    /// 이 씬의 플레이어를 고른 캐릭터로 맞춘다. 켜져 있는 플레이어가 없으면 아무것도 하지 않는다
    /// (그런 씬은 부트스트랩이 EnsureSelectedPlayer 로 만든다).
    /// </summary>
    /// <returns>플레이할 캐릭터. 없으면 null.</returns>
    public static GameObject ApplyChoice(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        List<GameObject> players = FindPlayerRoots(scene);
        if (players.Count == 0)
            return null;

        AetherCharacter want = AetherCharacterChoice.Selected;

        // 엘리베이터처럼 앞 씬에서 데리고 온 플레이어가 이미 켜져 있으면 그대로 쓴다.
        // 이 씬에 놓여 있던 플레이어는 꺼 둔다(체력바가 두 겹이 되고 빈 몸이 서 있게 된다).
        GameObject carried = FindActiveOutside(scene, want);
        if (carried != null)
        {
            foreach (GameObject root in players)
            {
                if (root.activeSelf)
                    root.SetActive(false);
            }

            EnsureMainCamera(carried);
            return carried;
        }

        GameObject chosen = null;
        GameObject standIn = null;

        foreach (GameObject root in players)
        {
            if (!root.activeSelf)
                continue;

            if (AetherPlayerCharacter.Identify(root) == want)
            {
                if (chosen == null)
                    chosen = root;
            }
            else if (standIn == null)
            {
                standIn = root;
            }
        }

        // 켜진 플레이어가 하나도 없는 씬은 부트스트랩이 만든다.
        if (chosen == null && standIn == null)
            return null;

        Vector3 position = (chosen != null ? chosen : standIn).transform.position;
        Quaternion rotation = (chosen != null ? chosen : standIn).transform.rotation;

        if (chosen == null)
        {
            chosen = PickInactive(players, want);
            if (chosen != null)
                chosen.transform.SetPositionAndRotation(position, rotation);
            else
                chosen = Spawn(want, position, rotation, scene);
        }

        if (chosen == null)
        {
            Debug.LogWarning($"[AetherCharacterSpawner] {AetherCharacterChoice.DisplayName(want)}를 세울 수 없어 씬에 있던 캐릭터로 진행합니다.");
            return standIn;
        }

        // 고른 캐릭터 말고는 끈다. 둘 다 켜져 있으면 적·HUD·카메라가 아무나 잡는다.
        foreach (GameObject root in players)
        {
            if (root != chosen && root.activeSelf)
                root.SetActive(false);
        }

        if (!chosen.activeSelf)
            chosen.SetActive(true);

        Finish(chosen, rotation);
        return chosen;
    }

    /// <summary>
    /// 플레이어가 씬에 없는 구역(06·08·09·11)에서 부트스트랩이 부른다.
    /// 고른 캐릭터가 이미 있으면 그것을, 없으면 이 자리에 만들어 돌려준다.
    /// </summary>
    public static GameObject EnsureSelectedPlayer(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Scene scene = SceneManager.GetActiveScene();

        GameObject applied = ApplyChoice(scene);
        if (applied != null)
            return applied;

        AetherCharacter want = AetherCharacterChoice.Selected;
        List<GameObject> players = FindPlayerRoots(scene);

        GameObject chosen = PickInactive(players, want);
        if (chosen != null)
        {
            chosen.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            chosen.SetActive(true);
        }
        else
        {
            chosen = Spawn(want, spawnPosition, spawnRotation, scene);
        }

        if (chosen == null)
            return null;

        Finish(chosen, spawnRotation);
        return chosen;
    }

    private static void Finish(GameObject player, Quaternion rotation)
    {
        AetherPlayerCharacter.Prepare(player);
        EnsureMainCamera(player);
        AetherPlayerCharacter.SnapLook(player, rotation);
    }

    /// <summary>
    /// 플레이어 카메라를 주 카메라로 만든다. 05 의 윤서는 머리에 카메라가 있지만
    /// 태그가 비어 있어서 Camera.main 이 null 이었다(HUD·조준·미니맵이 Camera.main 을 본다).
    /// </summary>
    private static void EnsureMainCamera(GameObject player)
    {
        Camera own = null;
        foreach (Camera camera in player.GetComponentsInChildren<Camera>(true))
        {
            if (camera == null || camera.targetTexture != null)
                continue;

            own = camera;
            break;
        }

        if (own == null)
            return;

        own.gameObject.SetActive(true);
        own.enabled = true;

        foreach (Camera other in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (other != own && other.CompareTag("MainCamera") && other.targetTexture == null)
                other.tag = "Untagged";
        }

        own.tag = "MainCamera";

        // 소리를 듣는 귀도 하나만 둔다.
        AudioListener listener = own.GetComponent<AudioListener>();
        if (listener == null)
            listener = own.gameObject.AddComponent<AudioListener>();

        foreach (AudioListener other in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            other.enabled = other == listener;
    }

    private static GameObject FindActiveOutside(Scene scene, AetherCharacter want)
    {
        MonoBehaviour[] movers = want == AetherCharacter.Yunseo
            ? (MonoBehaviour[])Object.FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            : Object.FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (MonoBehaviour mover in movers)
        {
            if (mover != null && mover.gameObject.scene != scene)
                return mover.transform.root.gameObject;
        }

        return null;
    }

    private static List<GameObject> FindPlayerRoots(Scene scene)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (AetherPlayerCharacter.Identify(root).HasValue)
                result.Add(root);
        }

        return result;
    }

    /// <summary>씬에 꺼진 채 놓인 같은 캐릭터. 전투 스크립트가 붙은 완성본을 먼저 고른다.</summary>
    private static GameObject PickInactive(List<GameObject> players, AetherCharacter want)
    {
        GameObject fallback = null;

        foreach (GameObject root in players)
        {
            if (root.activeSelf || AetherPlayerCharacter.Identify(root) != want)
                continue;

            bool complete = want == AetherCharacter.Yunseo
                ? root.GetComponentInChildren<Yunseo_Battle>(true) != null && root.GetComponentInChildren<Move_Yunseo>(true) != null
                : root.GetComponentInChildren<Hankyul_Fire>(true) != null && root.GetComponentInChildren<Move_Hankyul>(true) != null;

            if (complete)
                return root;

            if (fallback == null)
                fallback = root;
        }

        return fallback;
    }

    private static GameObject Spawn(AetherCharacter want, Vector3 position, Quaternion rotation, Scene scene)
    {
        GameObject prefab = Resources.Load<GameObject>(want == AetherCharacter.Yunseo ? YunseoResource : HankyulResource);
        if (prefab == null)
        {
            Debug.LogWarning($"[AetherCharacterSpawner] Resources/{(want == AetherCharacter.Yunseo ? YunseoResource : HankyulResource)} 가 없습니다.");
            return null;
        }

        GameObject player = Object.Instantiate(prefab, position, rotation);
        player.name = $"{AetherCharacterChoice.DisplayName(want)} (선택)";

        if (scene.IsValid() && player.scene != scene)
            SceneManager.MoveGameObjectToScene(player, scene);

        return player;
    }
}
