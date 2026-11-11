using UnityEngine;
using UnityEngine.InputSystem;

public class PartCraftingOpener : MonoBehaviour, IInteractable, IHoldInteractable
{
    [SerializeField] private string interactionName = "파츠 제작기 가동";
    [SerializeField] private float holdDuration = 0.35f;
    [SerializeField] private bool openOnShortPress = true;
    [SerializeField] private PartCraftingUI craftingUI;
    [SerializeField] private Canvas craftingCanvas;
    [SerializeField] private CanvasGroup craftingGroup;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;

    private GameObject currentInteractor;
    private bool isOpen;

    private void Awake()
    {
        ResolveUI();
        Hide();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        bool escape = keyboard != null ? keyboard.escapeKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Escape);
        if (escape && !PaperInventoryPreviewUI.IsOpen && !PaperInventoryPreviewUI.ClosedThisFrame)
            Close();
    }

    public string GetDisplayName()
    {
        return interactionName;
    }

    public Sprite GetIcon()
    {
        return null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (openOnShortPress)
            Open(interactor != null ? interactor.gameObject : null);
    }

    public string GetInteractionName()
    {
        return interactionName;
    }

    public float GetHoldDuration()
    {
        return holdDuration;
    }

    public void BeginHold(GameObject interactor)
    {
    }

    public void UpdateHold(GameObject interactor, float holdProgress)
    {
    }

    public void CancelHold(GameObject interactor)
    {
    }

    public void CompleteHold(GameObject interactor)
    {
        Open(interactor);
    }

    private void Open(GameObject interactor)
    {
        ResolveUI();
        currentInteractor = interactor;
        isOpen = true;

        if (craftingCanvas != null)
            craftingCanvas.enabled = true;

        if (craftingGroup != null)
        {
            craftingGroup.alpha = 1f;
            craftingGroup.interactable = true;
            craftingGroup.blocksRaycasts = true;
        }

        if (craftingUI != null)
        {
            craftingUI.SetActivePaper(PaperReadUI.LastReadPaper);
            craftingUI.RefreshInventoryList();
        }

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(currentInteractor, false);

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Close()
    {
        Hide();

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(currentInteractor, true);

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        currentInteractor = null;
        isOpen = false;
    }

    private void Hide()
    {
        if (craftingGroup != null)
        {
            craftingGroup.alpha = 0f;
            craftingGroup.interactable = false;
            craftingGroup.blocksRaycasts = false;
        }

        if (craftingCanvas != null)
            craftingCanvas.enabled = false;
    }

    private void ResolveUI()
    {
        if (craftingUI == null)
            craftingUI = FindObjectOfType<PartCraftingUI>(true);

        if (craftingCanvas == null && craftingUI != null)
            craftingCanvas = craftingUI.GetComponentInParent<Canvas>(true);

        if (craftingGroup == null && craftingUI != null)
            craftingGroup = craftingUI.GetComponent<CanvasGroup>();

        if (craftingGroup == null && craftingUI != null)
            craftingGroup = craftingUI.gameObject.AddComponent<CanvasGroup>();
    }

    private static void SetPlayerControl(GameObject interactor, bool enabled)
    {
        if (interactor == null)
            return;

        Move_Hankyul hankyul = interactor.GetComponentInParent<Move_Hankyul>();
        if (hankyul != null)
        {
            hankyul.allowInput = enabled;
            hankyul.allowMove = enabled;
            hankyul.allowLook = enabled;
        }

        Move_Yunseo yunseo = interactor.GetComponentInParent<Move_Yunseo>();
        if (yunseo != null)
        {
            yunseo.SetMoveEnabled(enabled);
            yunseo.SetLookEnabled(enabled);
        }

        PlayerInput input = interactor.GetComponentInParent<PlayerInput>();
        if (input == null)
            return;

        if (enabled)
            input.ActivateInput();
        else
            input.DeactivateInput();
    }
}
