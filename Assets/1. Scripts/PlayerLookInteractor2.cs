using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLookInteractor2 : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InteractionHoldUI interactionUI;

    [Header("감지 설정")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayerMask = ~0;

    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private float holdTime = 1.5f;

    private DoorSceneTransitionTarget currentTarget;
    private float currentHoldTimer = 0f;
    private bool isLoading = false;

    private void Update()
    {
        if (isLoading)
            return;

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
            DoorSceneTransitionTarget target = hit.collider.GetComponentInParent<DoorSceneTransitionTarget>();

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

                if (!string.IsNullOrEmpty(currentTarget.nextSceneName))
                {
                    isLoading = true;
                    SceneManager.LoadScene(currentTarget.nextSceneName);
                }
            }
        }
        else
        {
            currentHoldTimer = 0f;
            interactionUI.UpdateProgress(0f);
        }
    }
}