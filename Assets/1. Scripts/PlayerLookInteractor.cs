using UnityEngine;

public class PlayerLookInteractor : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InteractionHoldUI interactionUI;

    [Header("감지 설정")]
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private LayerMask interactLayerMask = ~0;

    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private float holdTime = 1.5f;

    private ConsoleInteractTarget currentTarget;
    private float currentHoldTimer = 0f;

    private void Start()
    {
        LockGameplayCursor();
    }

    private void Update()
    {
        CheckTarget();
        HandleHoldInteraction();
    }

    private void CheckTarget()
    {
        if (playerCamera == null || interactionUI == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayerMask))
        {
            ConsoleInteractTarget target = hit.collider.GetComponentInParent<ConsoleInteractTarget>();

            if (target != null)
            {
                if (currentTarget != target)
                {
                    currentTarget = target;
                    currentHoldTimer = 0f;
                    interactionUI.Show();
                    interactionUI.UpdateProgress(0f);
                }

                return;
            }
        }

        currentTarget = null;
        currentHoldTimer = 0f;
        interactionUI.HideImmediate();
    }

    private void HandleHoldInteraction()
    {
        if (currentTarget == null)
            return;

        if (Input.GetKey(interactKey))
        {
            currentHoldTimer += Time.deltaTime;
            float progress = currentHoldTimer / holdTime;
            interactionUI.UpdateProgress(progress);

            if (currentHoldTimer >= holdTime)
            {
                currentHoldTimer = 0f;
                interactionUI.UpdateProgress(0f);
                interactionUI.HideImmediate();

                // 여기서는 퍼즐 UI를 직접 열지 않음
                // 필요한 다른 상호작용이 있으면 여기서 추가
            }
        }
        else
        {
            currentHoldTimer = 0f;
            interactionUI.UpdateProgress(0f);
        }
    }

    private void LockGameplayCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}