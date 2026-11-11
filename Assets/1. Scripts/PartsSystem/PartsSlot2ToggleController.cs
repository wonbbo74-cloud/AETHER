using UnityEngine;
using UnityEngine.InputSystem;

public class PartsSlot2ToggleController : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;

    private Move_Hankyul hankyulMove;
    private Move_Yunseo yunseoMove;

    private bool isOpen;

    private void Awake()
    {
        PartsSlot2GlobalBootstrap.EnsureEventSystem();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        RefreshPlayerReferences();

        SetOpen(false);
    }

    private void Update()
    {
        // P 는 새 파츠 장착 화면(AetherPartsEquipScreen)이 받는다. 이 예전 캔버스까지 같이 열려 두 화면이 겹쳤고,
        // 일시정지 중에 누르면 커서를 잠가 메뉴를 누를 수 없었다. 새 화면이 있으면 이쪽은 키를 받지 않는다.
        if (AetherPartsEquipScreen.Exists || AetherPauseMenu.IsPaused || AetherUIState.AnyScreenOpen)
        {
            if (isOpen) SetOpen(false);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool pressed = keyboard != null
            ? keyboard.pKey.wasPressedThisFrame
            : Input.GetKeyDown(toggleKey);

        if (pressed)
            SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        if (open)
            PartsSlot2GlobalBootstrap.EnsureEventSystem();

        RefreshPlayerReferences();
        isOpen = open;

        if (canvas != null)
            canvas.enabled = open;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = open ? 1f : 0f;
            canvasGroup.interactable = open;
            canvasGroup.blocksRaycasts = open;
        }

        if (open)
            RefreshEquipmentUI();

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(!open);
    }

    private void RefreshEquipmentUI()
    {
        EquipmentUIController[] controllers = GetComponentsInChildren<EquipmentUIController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].RefreshFromInventory();
        }
    }

    public void RefreshPlayerReferences()
    {
        hankyulMove = GetComponentInParent<Move_Hankyul>();
        if (hankyulMove == null)
            hankyulMove = FindObjectOfType<Move_Hankyul>();

        yunseoMove = GetComponentInParent<Move_Yunseo>();
        if (yunseoMove == null)
            yunseoMove = FindObjectOfType<Move_Yunseo>();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (hankyulMove != null)
        {
            hankyulMove.allowInput = enabled;
            hankyulMove.allowMove = enabled;
            hankyulMove.allowLook = enabled;
        }

        if (yunseoMove != null)
        {
            yunseoMove.SetMoveEnabled(enabled);
            yunseoMove.SetLookEnabled(enabled);
        }
    }
}
