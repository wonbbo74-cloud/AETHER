using UnityEngine;
using UnityEngine.InputSystem;

public class PartsWorkbenchInteractable : MonoBehaviour, IHoldInteractable, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "\uD30C\uCE20 \uC81C\uC791\uAE30 \uAC00\uB3D9";
    [SerializeField] private float holdDuration = 1.1f;

    [Header("Parts UI")]
    [SerializeField] private Canvas partsCanvas;
    [SerializeField] private CanvasGroup partsCanvasGroup;
    [SerializeField] private GameObject partsUIRoot;
    [SerializeField] private string fallbackCanvasNameContains = "parts make";
    [SerializeField] private bool pauseGameWhenOpened;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;
    [SerializeField] private bool closeWithEscape = true;
    [SerializeField] private int openedSortingOrder = 900;

    [Header("Proximity Open")]
    [SerializeField] private bool openWhenPlayerNearby;
    [SerializeField] private float autoOpenDistance = 2.2f;
    [SerializeField] private float autoCloseDistance = 3.0f;
    [SerializeField] private string playerTag = "Player";

    private GameObject currentInteractor;
    private bool isOpen;
    private Transform proximityPlayer;
    private Collider[] distanceColliders;

    private void Start()
    {
        CacheDistanceColliders();
    }

    public string GetInteractionName()
    {
        return interactionName;
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
        // The parts workbench is opened by holding F through ProximityHoldPrompt.
        // Implementing IInteractable lets PlayerInteractor recognize the console
        // so nearby pickup/paper prompts do not steal the UI while looking at it.
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
        OpenPartsUI(interactor);
    }

    private void Awake()
    {
        ResolvePartsUI();
        HidePartsUI();
    }

    private void Update()
    {
        if (openWhenPlayerNearby)
            TickProximityOpen();

        if (!isOpen || !closeWithEscape)
            return;

        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null
            ? keyboard.escapeKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Escape);

        if (escapePressed && !PaperInventoryPreviewUI.IsOpen && !PaperInventoryPreviewUI.ClosedThisFrame)
            ClosePartsUI();
    }

    private void ResolvePartsUI()
    {
        if (partsUIRoot != null)
        {
            if (partsCanvas == null)
                partsCanvas = partsUIRoot.GetComponent<Canvas>();

            if (partsCanvasGroup == null)
                partsCanvasGroup = partsUIRoot.GetComponent<CanvasGroup>();

            if (partsCanvasGroup == null)
                partsCanvasGroup = partsUIRoot.AddComponent<CanvasGroup>();

            return;
        }

        if (partsCanvas == null && !string.IsNullOrEmpty(fallbackCanvasNameContains))
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && ContainsIgnoreCase(canvas.name, fallbackCanvasNameContains))
                {
                    partsCanvas = canvas;
                    break;
                }
            }
        }

        if (partsCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && IsFallbackPartsCanvasName(canvas.name))
                {
                    partsCanvas = canvas;
                    break;
                }
            }
        }

        if (partsCanvas == null)
            return;

        partsUIRoot = partsCanvas.gameObject;

        if (partsCanvasGroup == null)
            partsCanvasGroup = partsCanvas.GetComponent<CanvasGroup>();

        if (partsCanvasGroup == null)
            partsCanvasGroup = partsCanvas.gameObject.AddComponent<CanvasGroup>();
    }

    private void OpenPartsUI(GameObject interactor)
    {
        ResolvePartsUI();
        currentInteractor = interactor;
        isOpen = true;

        if (partsUIRoot != null)
        {
            ActivateParents(partsUIRoot.transform);
            partsUIRoot.SetActive(true);
        }

        if (partsCanvas != null)
        {
            partsCanvas.overrideSorting = true;
            partsCanvas.sortingOrder = openedSortingOrder;
            partsCanvas.enabled = true;
        }

        if (partsCanvasGroup != null)
        {
            partsCanvasGroup.alpha = 1f;
            partsCanvasGroup.interactable = true;
            partsCanvasGroup.blocksRaycasts = true;
        }

        HideOtherBlockingCanvases();
        HideDuplicateCraftingCanvases();

        PartCraftingUI craftingUI = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<PartCraftingUI>(true) : null;
        if (craftingUI == null)
            craftingUI = FindObjectOfType<PartCraftingUI>(true);
        if (craftingUI != null)
        {
            craftingUI.SetActivePaper(PaperReadUI.LastReadPaper);
            craftingUI.RefreshInventoryList();
        }

        PartsCraftingInventoryBridge bridge = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<PartsCraftingInventoryBridge>(true) : null;
        if (bridge == null)
            bridge = FindObjectOfType<PartsCraftingInventoryBridge>(true);
        if (bridge != null)
            bridge.RefreshFromInventory();

        CraftingController controller = partsUIRoot != null ? partsUIRoot.GetComponentInChildren<CraftingController>(true) : null;
        if (controller == null)
            controller = FindObjectOfType<CraftingController>(true);
        if (controller != null)
        {
            CraftingRecipeData activeRecipe = PaperReadUI.LastReadPaper != null && PaperReadUI.LastReadPaper.relatedRecipe != null
                ? PaperReadUI.LastReadPaper.relatedRecipe
                : PartResourceResolver.FindRecipe("superjump_paper");
            controller.SetActivePartRecipe(activeRecipe);
            controller.OnMaterialsChanged();
        }

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(interactor, false);

        if (pauseGameWhenOpened)
            Time.timeScale = 0f;

        // 새 디자인 제작 화면으로 대체한다. 기존 UI는 감춘다.
        HidePartsUI();
        AetherPartsCraftingScreen.OpenScreen();

        // 새 화면이 뜨지 못했다면(인벤토리 없음 등) 시간 정지·조작 잠금을 그대로 두면
        // 게임이 멈춘 채로 아무것도 안 보이게 되므로 즉시 되돌린다.
        if (!AetherPartsCraftingScreen.IsOpen)
        {
            Debug.LogWarning("[PartsWorkbench] 제작 화면을 열지 못해 상호작용을 취소합니다.");
            ClosePartsUI();
        }
    }

    public void OpenPartsUIFromExternal(GameObject interactor)
    {
        OpenPartsUI(interactor);
    }

    private void ClosePartsUI()
    {
        HidePartsUI();

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(currentInteractor, true);

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (pauseGameWhenOpened)
            Time.timeScale = 1f;

        currentInteractor = null;
        isOpen = false;

        AetherPartsCraftingScreen.CloseScreen();
    }

    private void TickProximityOpen()
    {
        if (proximityPlayer == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (foundPlayer != null)
                proximityPlayer = foundPlayer.transform;
            else
            {
                Move_Hankyul hankyul = FindObjectOfType<Move_Hankyul>();
                if (hankyul != null)
                    proximityPlayer = hankyul.transform;
                else
                {
                    Move_Yunseo yunseo = FindObjectOfType<Move_Yunseo>();
                    if (yunseo != null)
                        proximityPlayer = yunseo.transform;
                }
            }
        }

        if (proximityPlayer == null)
            return;

        float distance = Vector3.Distance(transform.position, proximityPlayer.position);
        if (distanceColliders == null || distanceColliders.Length == 0)
            CacheDistanceColliders();

        if (distanceColliders != null)
        {
            for (int i = 0; i < distanceColliders.Length; i++)
            {
                Collider targetCollider = distanceColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                distance = Mathf.Min(
                    distance,
                    Vector3.Distance(targetCollider.ClosestPointSafe(proximityPlayer.position), proximityPlayer.position)
                );
            }
        }

        if (!isOpen && distance <= autoOpenDistance)
            OpenPartsUI(proximityPlayer.gameObject);
        else if (isOpen && distance >= autoCloseDistance)
            ClosePartsUI();
    }

    private void CacheDistanceColliders()
    {
        distanceColliders = GetComponentsInChildren<Collider>(true);
    }

    public void CloseOpenedPartsUI()
    {
        if (isOpen)
            ClosePartsUI();
        else
            HidePartsUI();
    }

    private void HidePartsUI()
    {
        if (partsCanvasGroup != null)
        {
            partsCanvasGroup.alpha = 0f;
            partsCanvasGroup.interactable = false;
            partsCanvasGroup.blocksRaycasts = false;
        }

        if (partsCanvas != null)
            partsCanvas.enabled = false;
    }

    private void HideOtherBlockingCanvases()
    {
        PaperReadUI paperUI = FindObjectOfType<PaperReadUI>(true);
        if (paperUI != null)
        {
            CanvasGroup paperGroup = paperUI.GetComponent<CanvasGroup>();
            if (paperGroup != null)
            {
                paperGroup.alpha = 0f;
                paperGroup.interactable = false;
                paperGroup.blocksRaycasts = false;
            }
        }
    }

    private void HideDuplicateCraftingCanvases()
    {
        CraftingController[] controllers = FindObjectsOfType<CraftingController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            CraftingController controller = controllers[i];
            if (controller == null)
                continue;

            if (partsUIRoot != null && controller.transform.IsChildOf(partsUIRoot.transform))
                continue;

            Canvas canvas = controller.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvas.enabled = false;
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }
        }
    }

    private static void ActivateParents(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private static void SetPlayerControl(GameObject interactor, bool enabled)
    {
        if (interactor == null)
            return;

        Move_Hankyul hankyulMove = interactor.GetComponentInParent<Move_Hankyul>();
        if (hankyulMove != null)
        {
            hankyulMove.allowInput = enabled;
            hankyulMove.allowMove = enabled;
            hankyulMove.allowLook = enabled;
        }

        Move_Yunseo yunseoMove = interactor.GetComponentInParent<Move_Yunseo>();
        if (yunseoMove != null)
        {
            yunseoMove.SetMoveEnabled(enabled);
            yunseoMove.SetLookEnabled(enabled);
        }

        PlayerInput playerInput = interactor.GetComponentInParent<PlayerInput>();
        if (playerInput == null)
            return;

        if (enabled)
            playerInput.ActivateInput();
        else
            playerInput.DeactivateInput();
    }

    private static bool IsFallbackPartsCanvasName(string canvasName)
    {
        return string.Equals(canvasName, "parts make", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(canvasName, "Canvas (1)", System.StringComparison.OrdinalIgnoreCase)
            || ContainsIgnoreCase(canvasName, "\uD30C\uCE20");
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return false;

        return source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
