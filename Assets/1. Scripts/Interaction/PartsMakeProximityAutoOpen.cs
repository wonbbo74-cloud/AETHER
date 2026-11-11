using UnityEngine;
using UnityEngine.InputSystem;

public class PartsMakeProximityAutoOpen : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private CanvasGroup targetGroup;
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private float openDistance = 2.2f;
    [SerializeField] private float closeDistance = 3.2f;
    [SerializeField] private int sortingOrder = 900;
    [SerializeField] private bool unlockCursor = true;
    [SerializeField] private bool disablePlayerControl = true;

    private Transform player;
    private Collider[] colliders;
    private bool isOpen;

    private void Awake()
    {
        CacheColliders();
        ResolveCanvas();
        SetVisible(false);
    }

    private void Update()
    {
        ResolvePlayer();
        if (player == null)
            return;

        float distance = GetDistanceToPlayer();
        if (!isOpen && distance <= openDistance)
            Open();
        else if (isOpen && distance >= closeDistance)
            Close();

        if (isOpen)
        {
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null ? keyboard.escapeKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Escape);
            if (escape && !PaperInventoryPreviewUI.IsOpen && !PaperInventoryPreviewUI.ClosedThisFrame)
                Close();
        }
    }

    public void Open()
    {
        ResolveCanvas();
        HideDuplicateCraftingCanvases();
        SetVisible(true);
        RefreshCraftingUI();
        SetPlayerControl(false);
        isOpen = true;
    }

    public void Close()
    {
        SetVisible(false);
        SetPlayerControl(true);
        isOpen = false;
    }

    private void SetVisible(bool visible)
    {
        if (targetRoot != null)
        {
            ActivateParents(targetRoot.transform);
            targetRoot.SetActive(true);
        }

        if (targetCanvas != null)
        {
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = sortingOrder;
            targetCanvas.enabled = visible;
        }

        if (targetGroup != null)
        {
            targetGroup.alpha = visible ? 1f : 0f;
            targetGroup.interactable = visible;
            targetGroup.blocksRaycasts = visible;
        }

        if (unlockCursor)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }
    }

    private void RefreshCraftingUI()
    {
        PartsCraftingInventoryBridge bridge = targetRoot != null ? targetRoot.GetComponentInChildren<PartsCraftingInventoryBridge>(true) : null;
        if (bridge == null)
            bridge = FindObjectOfType<PartsCraftingInventoryBridge>(true);
        if (bridge != null)
            bridge.RefreshFromInventory();

        CraftingController controller = targetRoot != null ? targetRoot.GetComponentInChildren<CraftingController>(true) : null;
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
    }

    private void ResolveCanvas()
    {
        if (targetCanvas == null)
        {
            GameObject exactRoot = GameObject.Find("PartsMakeSceneUI_Exact");
            if (exactRoot != null)
                targetCanvas = exactRoot.GetComponentInChildren<Canvas>(true);
        }

        if (targetCanvas == null)
            return;

        targetRoot = targetCanvas.gameObject;

        if (targetGroup == null)
            targetGroup = targetCanvas.GetComponent<CanvasGroup>();
        if (targetGroup == null)
            targetGroup = targetCanvas.gameObject.AddComponent<CanvasGroup>();
    }

    private void HideDuplicateCraftingCanvases()
    {
        CraftingController[] controllers = FindObjectsOfType<CraftingController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            CraftingController controller = controllers[i];
            if (controller == null)
                continue;

            if (targetRoot != null && controller.transform.IsChildOf(targetRoot.transform))
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

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
            return;
        }

        Move_Hankyul hankyul = FindObjectOfType<Move_Hankyul>();
        if (hankyul != null)
        {
            player = hankyul.transform;
            return;
        }

        Move_Yunseo yunseo = FindObjectOfType<Move_Yunseo>();
        if (yunseo != null)
            player = yunseo.transform;
    }

    private float GetDistanceToPlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (colliders == null || colliders.Length == 0)
            CacheColliders();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null || !targetCollider.enabled)
                continue;

            distance = Mathf.Min(distance, Vector3.Distance(targetCollider.ClosestPointSafe(player.position), player.position));
        }

        return distance;
    }

    private void CacheColliders()
    {
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetPlayerControl(bool enabled)
    {
        if (!disablePlayerControl || player == null)
            return;

        Move_Hankyul hankyul = player.GetComponentInParent<Move_Hankyul>();
        if (hankyul != null)
        {
            hankyul.allowInput = enabled;
            hankyul.allowMove = enabled;
            hankyul.allowLook = enabled;
        }

        Move_Yunseo yunseo = player.GetComponentInParent<Move_Yunseo>();
        if (yunseo != null)
        {
            yunseo.SetMoveEnabled(enabled);
            yunseo.SetLookEnabled(enabled);
        }
    }
}
