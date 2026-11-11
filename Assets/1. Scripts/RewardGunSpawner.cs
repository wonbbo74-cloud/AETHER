using System.Collections;
using UnityEngine;

public class RewardGunSpawner : MonoBehaviour
{
    public enum SpawnMode
    {
        ActivateHiddenObject,
        InstantiatePrefab
    }

    [Header("Spawn Mode")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.ActivateHiddenObject;

    [Header("Hidden Object")]
    [SerializeField] private GameObject hiddenGunObject;

    [Header("Prefab Spawn")]
    [SerializeField] private GameObject gunPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Puzzle Gate")]
    [SerializeField] private bool requirePuzzleClearState = false;
    [SerializeField] private string puzzleId = "RuinedCityRobotBook";

    [Header("Inventory Pickup")]
    [SerializeField] private ItemData rewardItemData;
    [SerializeField] private int rewardAmount = 1;

    [Header("Pop Animation")]
    [SerializeField] private bool playPopAnimation = true;
    [SerializeField] private float popHeight = 1.15f;
    [SerializeField] private float popDuration = 0.55f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Spawn Rules")]
    [SerializeField] private bool spawnOnlyOnce = true;

    [Tooltip("If the puzzle was solved but this item is not in the inventory (e.g. died before picking it up and the scene reloaded), put the reward back.")]
    [SerializeField] private ItemData collectedCheckItem;

    private bool hasSpawned;

    private void Awake()
    {
        if (hiddenGunObject != null && requirePuzzleClearState && !PuzzleSceneFlowState.IsPuzzleCleared(puzzleId))
            hiddenGunObject.SetActive(false);
    }

    private void Start()
    {
        if (requirePuzzleClearState && collectedCheckItem != null)
            StartCoroutine(RestoreUncollectedReward());
    }

    private IEnumerator RestoreUncollectedReward()
    {
        // Wait for the player (and inventory) to be created.
        yield return new WaitForSeconds(1f);

        if (hasSpawned || hiddenGunObject == null || hiddenGunObject.activeSelf)
            yield break;
        if (!PuzzleSceneFlowState.IsPuzzleCleared(puzzleId))
            yield break;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null && inventory.HasItem(collectedCheckItem))
            yield break;

        // 설계서는 제작할 때 쓰인다. 이미 강화파츠를 만들었으면 다시 놓지 않는다.
        if (PartSystemRuntimeState.GetCraftedEquipmentParts().Count > 0)
            yield break;

        hiddenGunObject.SetActive(true);
        hasSpawned = true;
    }

    public void SpawnReward()
    {
        if (spawnOnlyOnce && hasSpawned)
            return;

        GameObject spawnedObject = null;
        switch (spawnMode)
        {
            case SpawnMode.ActivateHiddenObject:
                spawnedObject = ActivateHiddenGun();
                break;

            case SpawnMode.InstantiatePrefab:
                spawnedObject = InstantiateGunPrefab();
                break;
        }

        if (spawnedObject != null)
        {
            ConfigureInventoryPickup(spawnedObject);

            if (playPopAnimation)
                StartCoroutine(PopRewardRoutine(spawnedObject.transform));

            // 퍼즐 보상(제작설계서)은 F 를 길게 눌러야 주워진다. 모르고 지나치면 07 에서 강화파츠를 만들 수 없다.
            if (spawnedObject.GetComponentInChildren<PaperInteractable>(true) != null)
                AetherModernHUD.SetCurrentObjective("로봇 옆에 떨어진 제작설계서를\nF 를 눌러 주워라.");
        }

        hasSpawned = true;
    }

    public void TrySpawnFromPuzzleState()
    {
        if (!requirePuzzleClearState)
            return;

        if (!PuzzleSceneFlowState.ConsumePendingReward(puzzleId))
            return;

        SpawnReward();
    }

    private GameObject ActivateHiddenGun()
    {
        if (hiddenGunObject == null)
        {
            Debug.LogWarning("[RewardGunSpawner] hiddenGunObject is not assigned.", this);
            return null;
        }

        hiddenGunObject.SetActive(true);
        return hiddenGunObject;
    }

    private GameObject InstantiateGunPrefab()
    {
        if (gunPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[RewardGunSpawner] gunPrefab or spawnPoint is not assigned.", this);
            return null;
        }

        return Instantiate(gunPrefab, spawnPoint.position, spawnPoint.rotation);
    }

    private void ConfigureInventoryPickup(GameObject rewardObject)
    {
        if (rewardObject == null || rewardItemData == null)
            return;

        Collider collider = rewardObject.GetComponent<Collider>();
        if (collider == null)
            collider = rewardObject.AddComponent<BoxCollider>();

        collider.isTrigger = true;

        InteractableItem pickup = rewardObject.GetComponent<InteractableItem>();
        if (pickup == null)
            pickup = rewardObject.AddComponent<InteractableItem>();

        pickup.itemData = rewardItemData;
        pickup.amount = Mathf.Max(1, rewardAmount);

        InteractionUIProfile profile = rewardObject.GetComponent<InteractionUIProfile>();
        if (profile == null)
            profile = rewardObject.AddComponent<InteractionUIProfile>();

        profile.uiType = InteractionUIType.ItemArrow;
        profile.titleOverride = rewardItemData.displayName;
        profile.actionTextOverride = "F키로 획득";
        profile.useHoldInteraction = false;
    }

    private IEnumerator PopRewardRoutine(Transform rewardTransform)
    {
        if (rewardTransform == null)
            yield break;

        Vector3 startPosition = rewardTransform.position;
        Vector3 peakPosition = startPosition + Vector3.up * popHeight;
        // Pop up, then settle back on the ground (it used to stay floating in the air).
        Vector3 endPosition = startPosition;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, popDuration);
            float eased = popCurve.Evaluate(Mathf.Clamp01(t));
            if (rewardTransform == null) yield break;   // 튀어 오르는 도중에 주우면 사라진다(10/12 최종 점검 오류)
            rewardTransform.position = Vector3.LerpUnclamped(startPosition, peakPosition, eased);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, popDuration * 0.65f);
            float eased = popCurve.Evaluate(Mathf.Clamp01(t));
            if (rewardTransform == null) yield break;
            rewardTransform.position = Vector3.LerpUnclamped(peakPosition, endPosition, eased);
            yield return null;
        }

        if (rewardTransform != null)
            rewardTransform.position = endPosition;
    }
}
