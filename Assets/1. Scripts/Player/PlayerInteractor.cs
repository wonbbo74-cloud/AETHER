using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInventory))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Camera / Raycast")]
    public Camera playerCamera;
    public float detectDistance = 4.0f;     // 정면 후보 탐지 거리
    public float uiDistance = 2.2f;         // UI 표시 거리
    public float interactDistance = 2.2f;   // 실제 Interact 허용 거리
    public LayerMask interactableLayerMask = ~0;

    [Header("Keys")]
    public KeyCode interactKey = KeyCode.F;
    public KeyCode inventoryKey = KeyCode.I;

    [Header("SFX Key")]
    public string pickupSfxKey = "pickup";

    public PlayerInventory Inventory { get; private set; }

    // ===== UI에 전달할 상태 =====
    public IInteractable CurrentInteractable { get; private set; }
    public float CurrentDistance { get; private set; } = Mathf.Infinity;

    public bool CanShowUI => CurrentInteractable != null && CurrentDistance <= uiDistance;
    public bool CanInteract => CurrentInteractable != null && CurrentDistance <= interactDistance;

    // UI 업데이트 이벤트(상태가 바뀌었을 때만 호출)
    public System.Action OnTargetChanged;

    private IInteractable prevInteractable;
    private bool prevCanShowUI;

    private void Reset()
    {
        playerCamera = Camera.main;
    }

    private void Awake()
    {
        Inventory = GetComponent<PlayerInventory>();
        if (playerCamera == null) playerCamera = Camera.main;
        detectDistance = Mathf.Max(detectDistance, 5.0f);
        uiDistance = Mathf.Max(uiDistance, 3.6f);
        // 안내는 3.6m 에서 뜨는데 줍기는 3.0m 안에서만 돼, 그 사이에서는 "[F] 물건"이 보여도 F 가 먹지 않았다
        // (05 설계서 앞에서 확인). 보이면 주울 수 있게 같은 거리로 맞춘다.
        interactDistance = Mathf.Max(interactDistance, uiDistance);
    }

    private void Update()
    {
        RaycastDetect();
        NotifyIfChanged();

        if (GetKeyDown(interactKey) && CanInteract)
        {
            CurrentInteractable.Interact(this);
            PlayPickupSfx();

            // 기존 방식처럼 상호작용 후 UI 내리려면 즉시 클리어
            ClearTarget();
            NotifyIfChanged(force: true);
        }

        if (GetKeyDown(inventoryKey) && Inventory != null)
        {
            Inventory.DebugLogInventory();
        }
    }

    private void RaycastDetect()
    {
        CurrentInteractable = null;
        CurrentDistance = Mathf.Infinity;

        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, detectDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                // Puzzles that open with the circle prompt (hold F) are left out of the regular interaction (tap F).
                if (interactable is PuzzleOpener holdPuzzle && holdPuzzle.UsesHoldPrompt)
                    interactable = null;
                if (interactable != null)
                {
                    CurrentInteractable = interactable;
                    CurrentDistance = hit.distance;
                    return;
                }
            }
        }

        DetectNearbyPickupOrPaper();
    }

    private void DetectNearbyPickupOrPaper()
    {
        float searchDistance = Mathf.Max(uiDistance, interactDistance);
        Collider[] hits = Physics.OverlapSphere(transform.position, searchDistance, interactableLayerMask, QueryTriggerInteraction.Collide);

        IInteractable nearest = null;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            var interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            bool isPickupLike = interactable is IPickupInteractable || interactable is ILongPressInteractable;
            if (!isPickupLike) continue;

            Component component = interactable as Component;
            if (component == null) continue;

            float distance = Vector3.Distance(transform.position, component.transform.position);
            // 줍기 상자가 크면 물건이 멀리 있어도 상자 가장자리만 걸려 안내가 떴다. 물건 자리까지의 거리로 거른다.
            if (distance > searchDistance)
                continue;
            if (distance < nearestDistance)
            {
                nearest = interactable;
                nearestDistance = distance;
            }
        }

        if (nearest != null)
        {
            CurrentInteractable = nearest;
            CurrentDistance = nearestDistance;
        }
    }
    private void ClearTarget()
    {
        CurrentInteractable = null;
        CurrentDistance = Mathf.Infinity;
    }

    private void NotifyIfChanged(bool force = false)
    {
        bool canShow = CanShowUI;

        if (force ||
            prevInteractable != CurrentInteractable ||
            prevCanShowUI != canShow)
        {
            prevInteractable = CurrentInteractable;
            prevCanShowUI = canShow;
            OnTargetChanged?.Invoke();
        }
    }

    private void PlayPickupSfx()
    {
        if (string.IsNullOrEmpty(pickupSfxKey)) return;
        if (GlobalManager.Audio != null)
            GlobalManager.Audio.PlaySFX(pickupSfxKey);
    }

    private static bool GetKeyDown(KeyCode key)
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.F: return kb.fKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.I: return kb.iKey.wasPressedThisFrame;
                default: break;
            }
        }
        return Input.GetKeyDown(key);
    }
}
