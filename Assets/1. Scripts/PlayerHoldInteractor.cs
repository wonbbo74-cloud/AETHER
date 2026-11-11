using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHoldInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayer = ~0;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("UI")]
    [SerializeField] private GameObject interactionUIRoot;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private Image progressFillImage;

    private IHoldInteractable currentInteractable;
    private float holdTimer;
    private bool isHolding;

    private void Start()
    {
        ResolveReferences();
        HideUI();
    }

    private void Update()
    {
        ResolveReferences();
        FindInteractable();
        HandleInput();
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>() ?? Camera.main;

        if (interactionText == null && interactionUIRoot != null)
            interactionText = interactionUIRoot.GetComponentInChildren<TMP_Text>(true);

        if (progressFillImage == null && interactionUIRoot != null)
        {
            Image[] images = interactionUIRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].type == Image.Type.Filled)
                {
                    progressFillImage = images[i];
                    break;
                }
            }
        }
    }

    private void FindInteractable()
    {
        IHoldInteractable found = null;

        if (playerCamera != null)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
            {
                found = hit.collider.GetComponentInParent<IHoldInteractable>();
                if (found is Component component && component.GetComponentInParent<ProximityHoldPrompt>() != null)
                    found = null;
            }
        }

        if (currentInteractable != found)
        {
            CancelHold();
            currentInteractable = found;
        }

        RefreshUI();
    }

    private void HandleInput()
    {
        if (currentInteractable == null)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            isHolding = true;
            holdTimer = 0f;
            currentInteractable.BeginHold(gameObject);
        }

        if (isHolding && Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            float duration = Mathf.Max(0.01f, currentInteractable.GetHoldDuration());
            float progress = Mathf.Clamp01(holdTimer / duration);

            currentInteractable.UpdateHold(gameObject, progress);
            SetProgress(progress);
            AetherAudio.HoldProgress(progress);

            if (holdTimer >= duration)
            {
                currentInteractable.CompleteHold(gameObject);
                ResetHold();
            }
        }

        if (isHolding && Input.GetKeyUp(interactKey))
            CancelHold();
    }

    private void CancelHold()
    {
        if (isHolding && currentInteractable != null)
            currentInteractable.CancelHold(gameObject);

        ResetHold();
    }

    private void ResetHold()
    {
        isHolding = false;
        holdTimer = 0f;
        SetProgress(0f);
        AetherAudio.HoldEnd();
    }

    private void RefreshUI()
    {
        if (currentInteractable == null)
        {
            HideUI();
            return;
        }

        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(true);

        if (interactionText != null)
            interactionText.text = InteractionPromptTextUtility.FormatHoldPrompt(currentInteractable.GetInteractionName());

        if (!isHolding)
            SetProgress(0f);
    }

    private void SetProgress(float progress)
    {
        if (progressFillImage != null)
            progressFillImage.fillAmount = Mathf.Clamp01(progress);
    }

    private void HideUI()
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(false);

        SetProgress(0f);
    }
}
