using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PartsSlot2GlobalBootstrap : MonoBehaviour
{
    private const string PrefabPath = "AETHERParts/PartsSlot2_EquipmentCanvas";
    private const string SceneInstanceName = "PartsSlot2_PlayerEquipmentCanvas";
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAfterSceneLoad()
    {
        SubscribeSceneEvents();
        EnsureScenePlayerUI();
    }

    private static void SubscribeSceneEvents()
    {
        if (subscribed)
            return;

        SceneManager.sceneLoaded += HandleStaticSceneLoaded;
        subscribed = true;
    }

    private static void HandleStaticSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureScenePlayerUI();
    }

    public static GameObject EnsureScenePlayerUI()
    {
        EnsureEventSystem();

        Transform playerRoot = FindHankyulRoot();
        if (playerRoot == null)
            return null;

        GameObject existingObject = GameObject.Find(SceneInstanceName);
        Transform existing = existingObject != null ? existingObject.transform : null;
        if (existing != null)
        {
            if (existing.parent != null)
                existing.SetParent(null, false);

            ResetScreenSpaceRect(existing);

            PartsSlot2GlobalBootstrap existingBootstrap = existing.GetComponent<PartsSlot2GlobalBootstrap>();
            if (existingBootstrap != null)
                existingBootstrap.BindToCurrentScene();
            return existing.gameObject;
        }

        RemoveDetachedSceneInstances(playerRoot);

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[PartsSlot2GlobalBootstrap] Parts slot2 prefab was not found in Resources/AETHERParts.");
            return null;
        }

        GameObject ui = Instantiate(prefab);
        ui.name = SceneInstanceName;
        ui.transform.SetParent(null, false);
        ResetScreenSpaceRect(ui.transform);

        PartsSlot2GlobalBootstrap bootstrap = ui.GetComponent<PartsSlot2GlobalBootstrap>();
        if (bootstrap == null)
            bootstrap = ui.AddComponent<PartsSlot2GlobalBootstrap>();

        bootstrap.BindToCurrentScene();
        return ui;
    }

    private void OnEnable()
    {
        SubscribeSceneEvents();
        BindToCurrentScene();
    }

    private void OnDisable()
    {
    }

    public void BindToCurrentScene()
    {
        PlayerInventory inventory = PartSystemRuntimeState.GetOrBindBestInventory();
        EquipmentManager resolvedEquipmentManager = null;

        // 윤서로 플레이 중이면 윤서의 장비에 붙인다. (예전에는 한결만 봤고, 없으면 꺼진 한결의
        // EquipmentManager 까지 집어 와서 장착이 엉뚱한 곳으로 갔다.)
        GameObject activePlayer = AetherPlayerCharacter.FindActive();
        if (activePlayer != null && AetherPlayerCharacter.Identify(activePlayer) == AetherCharacter.Yunseo)
        {
            AetherPlayerCharacter.Prepare(activePlayer);
            resolvedEquipmentManager = activePlayer.GetComponentInChildren<EquipmentManager>(true);
            if (resolvedEquipmentManager != null && inventory != null)
                resolvedEquipmentManager.SetInventory(inventory);
        }

        Move_Hankyul moveHankyul = resolvedEquipmentManager == null ? FindObjectOfType<Move_Hankyul>() : null;
        if (moveHankyul != null)
        {
            EquipmentManager equipmentManager = moveHankyul.GetComponent<EquipmentManager>();
            if (equipmentManager == null)
                equipmentManager = moveHankyul.gameObject.AddComponent<EquipmentManager>();
            equipmentManager.SetInventory(inventory);
            resolvedEquipmentManager = equipmentManager;

            PartSystemRuntimeState.BindEquipment(equipmentManager);

            HangyeolPartAbilityController hangyeolParts = moveHankyul.GetComponent<HangyeolPartAbilityController>();
            if (hangyeolParts == null)
                hangyeolParts = moveHankyul.gameObject.AddComponent<HangyeolPartAbilityController>();

            hangyeolParts.RefreshSuperJumpState();
        }

        if (resolvedEquipmentManager == null)
            resolvedEquipmentManager = GetComponentInParent<EquipmentManager>();

        if (resolvedEquipmentManager == null)
            resolvedEquipmentManager = FindObjectOfType<EquipmentManager>(true);

        if (resolvedEquipmentManager == null)
        {
            Transform ownerRoot = transform.root;
            if (ownerRoot != null)
                resolvedEquipmentManager = ownerRoot.gameObject.AddComponent<EquipmentManager>();
        }

        if (resolvedEquipmentManager != null)
        {
            resolvedEquipmentManager.SetInventory(inventory);
            PartSystemRuntimeState.BindEquipment(resolvedEquipmentManager);
        }

        PartsEquipModernRuntimeUI modernUi = GetComponentInChildren<PartsEquipModernRuntimeUI>(true);
        if (modernUi == null)
            modernUi = gameObject.AddComponent<PartsEquipModernRuntimeUI>();
        modernUi.SetContext(inventory, resolvedEquipmentManager);

        EquipmentUIController controller = GetComponentInChildren<EquipmentUIController>(true);
        if (controller != null)
            controller.SetSourceInventory(inventory);

        PartsSlot2EquipmentBridge bridge = GetComponentInChildren<PartsSlot2EquipmentBridge>(true);
        if (bridge == null && controller != null)
            bridge = controller.gameObject.AddComponent<PartsSlot2EquipmentBridge>();

        PartsSlot2ToggleController toggle = GetComponent<PartsSlot2ToggleController>();
        if (toggle == null)
            toggle = gameObject.AddComponent<PartsSlot2ToggleController>();

        if (toggle != null)
            toggle.RefreshPlayerReferences();
    }

    private static Transform FindHankyulRoot()
    {
        Move_Hankyul moveHankyul = FindObjectOfType<Move_Hankyul>();
        if (moveHankyul != null)
            return moveHankyul.transform;

        Hankyul_Move legacyMove = FindObjectOfType<Hankyul_Move>();
        if (legacyMove != null)
            return legacyMove.transform;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null && taggedPlayer.name.ToLowerInvariant().Contains("hankyul"))
            return taggedPlayer.transform;

        GameObject named = GameObject.Find("Hankyul");
        if (named != null)
            return named.transform;

        return taggedPlayer != null ? taggedPlayer.transform : null;
    }

    private static void RemoveDetachedSceneInstances(Transform playerRoot)
    {
        PartsSlot2GlobalBootstrap[] bootstraps = FindObjectsOfType<PartsSlot2GlobalBootstrap>(true);
        for (int i = bootstraps.Length - 1; i >= 0; i--)
        {
            PartsSlot2GlobalBootstrap bootstrap = bootstraps[i];
            if (bootstrap == null || bootstrap.transform == playerRoot || bootstrap.transform.IsChildOf(playerRoot))
                continue;

            if (bootstrap.gameObject.name.Contains("PartsSlot2"))
                Destroy(bootstrap.gameObject);
        }
    }

    private static void ResetScreenSpaceRect(Transform uiTransform)
    {
        uiTransform.localPosition = Vector3.zero;
        uiTransform.localRotation = Quaternion.identity;
        uiTransform.localScale = Vector3.one;

        RectTransform rect = uiTransform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(960f, 540f);
        rect.sizeDelta = new Vector2(1920f, 1080f);
    }

    public static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (Application.isPlaying)
            DontDestroyOnLoad(eventSystem);
    }
}
