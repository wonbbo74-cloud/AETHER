using UnityEngine;
using UnityEngine.SceneManagement;

public class PartSceneAutoBinder : MonoBehaviour
{
    [SerializeField] private bool bindOnStart = true;
    [SerializeField] private bool addTriggerColliderWhenMissing = true;
    [SerializeField] private Vector3 defaultTriggerSize = Vector3.one;
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAutoBinding()
    {
        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        BindLoadedScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindLoadedScene();
    }

    private static void BindLoadedScene()
    {
        PartSceneAutoBinder binder = FindObjectOfType<PartSceneAutoBinder>();
        if (binder != null)
        {
            binder.BindSceneObjects();
            return;
        }

        GameObject helper = new GameObject("PartSceneAutoBinder_Runtime");
        PartSceneAutoBinder runtimeBinder = helper.AddComponent<PartSceneAutoBinder>();
        runtimeBinder.bindOnStart = false;
        runtimeBinder.BindSceneObjects();
        Destroy(helper);
    }

    private void Start()
    {
        if (bindOnStart)
            BindSceneObjects();
    }

    [ContextMenu("Bind Scene Objects")]
    public void BindSceneObjects()
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            string normalized = ItemNameRuleUtility.NormalizeName(target.name);
            InteractableItem pickupItem = target.GetComponent<InteractableItem>();
            ItemData itemData = pickupItem != null ? pickupItem.itemData : null;

            bool isModule = ItemNameRuleUtility.IsModuleItem(normalized)
                || ItemNameRuleUtility.IsModuleObject(target, itemData);
            bool isPaper = ItemNameRuleUtility.IsPaperObject(normalized)
                || ItemNameRuleUtility.IsPaperObject(target, itemData);

            if (isPaper)
                EnsurePaper(target, itemData, pickupItem);
            else if (isModule)
                EnsureModule(target, itemData, pickupItem);
        }

        EnsureRuntimePlayerParts();
    }

    private void EnsureModule(GameObject target, ItemData itemData, InteractableItem existingPickup)
    {
        EnsureCollider(target);

        if (existingPickup != null)
            existingPickup.enabled = false;

        ModulePickupInteractable pickup = target.GetComponent<ModulePickupInteractable>();
        if (pickup == null)
        {
            pickup = target.AddComponent<ModulePickupInteractable>();
            Debug.Log($"[PartSceneAutoBinder] Bound module pickup: {target.name}", target);
        }

        if (pickup != null)
            pickup.Configure(itemData);
        else
            Debug.LogWarning($"[PartSceneAutoBinder] Failed to add ModulePickupInteractable: {target.name}", target);

        ModuleWorldMarker marker = target.GetComponent<ModuleWorldMarker>();
        if (marker == null)
            marker = target.AddComponent<ModuleWorldMarker>();

        if (marker != null)
            marker.Configure(itemData);
    }

    private void EnsurePaper(GameObject target, ItemData itemData, InteractableItem existingPickup)
    {
        EnsureCollider(target);

        PaperInteractable paper = target.GetComponentInParent<PaperInteractable>();
        if (paper == null)
            paper = target.AddComponent<PaperInteractable>();
        if (paper != null)
            paper.Configure(itemData);

        if (target.GetComponentInParent<ProximityHoldPrompt>() == null)
            target.AddComponent<ProximityHoldPrompt>();

        if (existingPickup != null)
            existingPickup.enabled = false;
    }

    private void EnsureCollider(GameObject target)
    {
        if (!addTriggerColliderWhenMissing)
            return;

        // 콜라이더는 '루트'에 있어야 한다. ModulePickupInteractable / PaperInteractable이
        // RequireComponent(Collider)라, 콜라이더가 자식에만 있으면 컴포넌트 추가가 실패한다.
        // (자식까지 세던 예전 방식은 "Adding component failed" 오류를 냈다.)
        Collider existing = target.GetComponent<Collider>();
        if (existing != null)
            return;

        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = defaultTriggerSize;
    }

    private void EnsureRuntimePlayerParts()
    {
        Move_Hankyul[] hankyulMoves = FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < hankyulMoves.Length; i++)
        {
            Move_Hankyul move = hankyulMoves[i];
            if (move == null)
                continue;

            EquipmentManager equipment = move.GetComponent<EquipmentManager>();
            if (equipment == null)
                equipment = move.gameObject.AddComponent<EquipmentManager>();

            PlayerInventory inventory = move.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = move.GetComponentInChildren<PlayerInventory>(true);
            if (inventory != null)
                equipment.SetInventory(inventory);

            HangyeolPartAbilityController ability = move.GetComponent<HangyeolPartAbilityController>();
            if (ability == null)
                ability = move.gameObject.AddComponent<HangyeolPartAbilityController>();

            PartSystemRuntimeState.BindEquipment(equipment);
            ability.RefreshSuperJumpState();

            EquipmentUIController equipmentUI = move.GetComponentInChildren<EquipmentUIController>(true);
            if (equipmentUI != null)
            {
                if (equipmentUI.GetComponent<PartsSlot2EquipmentBridge>() == null)
                    equipmentUI.gameObject.AddComponent<PartsSlot2EquipmentBridge>();

                equipmentUI.SetSourceInventory(inventory);
                equipmentUI.RefreshFromInventory();
            }
        }

        Move_Yunseo[] yunseoMoves = FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < yunseoMoves.Length; i++)
        {
            Move_Yunseo move = yunseoMoves[i];
            if (move == null)
                continue;

            // 예전에는 장비·능력 부품만 붙이고 인벤토리·씬 간 상태를 연결하지 않아
            // 윤서로 주운 파츠가 장착되지 않았다. 한결과 같은 공용 규칙을 쓴다.
            AetherPlayerCharacter.Prepare(move.transform.root.gameObject);

            PlayerInventory inventory = move.GetComponent<PlayerInventory>() ?? move.GetComponentInChildren<PlayerInventory>(true);
            EquipmentUIController equipmentUI = move.GetComponentInChildren<EquipmentUIController>(true);
            if (equipmentUI != null)
            {
                if (equipmentUI.GetComponent<PartsSlot2EquipmentBridge>() == null)
                    equipmentUI.gameObject.AddComponent<PartsSlot2EquipmentBridge>();

                equipmentUI.SetSourceInventory(inventory);
                equipmentUI.RefreshFromInventory();
            }
        }
    }
}
