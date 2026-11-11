using UnityEngine;
using UnityEngine.InputSystem;

public class PartsUIHotkeyController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject partsUIRoot;

    [Header("플레이어")]
    [SerializeField] private Move_Yunseo moveYunseo;

    private bool isOpen = false;

    private void Start()
    {
        if (moveYunseo == null)
            moveYunseo = FindObjectOfType<Move_Yunseo>();

        if (partsUIRoot != null)
            partsUIRoot.SetActive(false);

        ApplyClosedState();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.pKey.wasPressedThisFrame)
            ToggleUI();
    }

    public void ToggleUI()
    {
        isOpen = !isOpen;

        if (partsUIRoot != null)
            partsUIRoot.SetActive(isOpen);

        if (isOpen)
            ApplyOpenState();
        else
            ApplyClosedState();
    }

    public void OpenUI()
    {
        isOpen = true;

        if (partsUIRoot != null)
            partsUIRoot.SetActive(true);

        ApplyOpenState();
    }

    public void CloseUI()
    {
        isOpen = false;

        if (partsUIRoot != null)
            partsUIRoot.SetActive(false);

        ApplyClosedState();
    }

    private void ApplyOpenState()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (moveYunseo != null)
        {
            moveYunseo.SetMoveEnabled(false);
            moveYunseo.SetLookEnabled(false);
        }
    }

    private void ApplyClosedState()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (moveYunseo != null)
        {
            moveYunseo.SetMoveEnabled(true);
            moveYunseo.SetLookEnabled(true);
        }
    }
}