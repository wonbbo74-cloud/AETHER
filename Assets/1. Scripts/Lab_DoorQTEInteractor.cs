using TMPro;
using UnityEngine;

public class Lab_DoorQTEInteractor : MonoBehaviour
{
    [Header("QTE")]
    [SerializeField] private Lab_DoorQTE doorQTE;

    [Header("Interactor Filter")]
    [SerializeField] private string yunseoTag = "Yunseo";
    [SerializeField] private bool requireYunseoBattle = true;

    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "잠금장치 해제";

    private GameObject currentInteractor;
    private bool canInteract;

    private void Reset()
    {
        doorQTE = GetComponentInParent<Lab_DoorQTE>();
    }

    private void Awake()
    {
        if (doorQTE == null)
            doorQTE = GetComponentInParent<Lab_DoorQTE>();

        SetPromptVisible(false);
    }

    private void Update()
    {
        if (!canInteract) return;
        if (currentInteractor == null) return;
        if (doorQTE == null) return;

        if (doorQTE.IsRunning)
        {
            SetPromptVisible(false);
            return;
        }

        if (!doorQTE.CanStartQTE(currentInteractor))
        {
            ClearInteractor();
            return;
        }

        SetPromptVisible(true);

        if (!Input.GetKeyDown(interactKey)) return;

        SetPromptVisible(false);
        doorQTE.StartQTE(currentInteractor);
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject candidate = other.gameObject;
        if (!IsValidYunseo(candidate)) return;
        if (doorQTE != null && !doorQTE.CanStartQTE(candidate)) return;

        currentInteractor = candidate;
        canInteract = true;
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentInteractor == null) return;
        if (other.gameObject != currentInteractor && !other.transform.IsChildOf(currentInteractor.transform)) return;

        ClearInteractor();
    }

    private bool IsValidYunseo(GameObject candidate)
    {
        if (candidate == null) return false;

        Yunseo_Battle battle = candidate.GetComponentInParent<Yunseo_Battle>();
        Transform tagTarget = battle != null ? battle.transform : candidate.transform.root;

        if (!string.IsNullOrEmpty(yunseoTag) && tagTarget != null && !tagTarget.CompareTag(yunseoTag))
            return false;

        if (requireYunseoBattle && battle == null)
            return false;

        return true;
    }

    private void ClearInteractor()
    {
        currentInteractor = null;
        canInteract = false;
        SetPromptVisible(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null)
            promptRoot.SetActive(visible);

        if (promptText != null)
        {
            promptText.text = InteractionPromptTextUtility.FormatHoldPrompt(promptMessage);
            promptText.gameObject.SetActive(visible);
        }
    }
}
