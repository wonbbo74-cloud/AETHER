using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PaperReadUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text requiredModulesText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text resultPartText;
    [SerializeField] private Button closeButton;

    [Header("Linked Systems")]
    [SerializeField] private PartCraftingUI craftingUI;
    [SerializeField] private bool unlockCursorWhenOpened = true;
    [SerializeField] private bool disablePlayerControlWhenOpened = true;

    public static PaperData LastReadPaper { get; private set; }

    private GameObject currentInteractor;
    private bool isOpen;

    private void Awake()
    {
        EnsureReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Hide();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        bool escapePressed = keyboard != null
            ? keyboard.escapeKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Escape);

        if (escapePressed)
            Close();
    }

    public void Open(PaperInteractable paper, PlayerInteractor interactor)
    {
        EnsureReferences();
        AetherAudio.Play2D("paper_open", 0.6f, 1f, 0.05f); // paper rustle

        currentInteractor = interactor != null ? interactor.gameObject : null;
        PaperData data = paper != null ? paper.PaperData : null;
        LastReadPaper = data;

        if (titleText != null)
            titleText.text = paper != null ? paper.GetTitle() : "Manual";

        if (bodyText != null)
            bodyText.text = paper != null ? paper.GetBody() : "No manual data.";

        if (hintText != null)
            hintText.text = paper != null ? paper.GetHint() : string.Empty;

        if (requiredModulesText != null)
            requiredModulesText.text = BuildRequiredModuleText(paper);

        if (resultPartText != null)
            resultPartText.text = BuildResultPartText(paper);
        else if (requiredModulesText != null)
            requiredModulesText.text += "\n" + BuildResultPartText(paper);

        if (craftingUI == null)
            craftingUI = FindObjectOfType<PartCraftingUI>(true);

        if (craftingUI != null && data != null)
            craftingUI.SetActivePaper(data);

        Show();
    }

    public void Close()
    {
        AetherAudio.Play2D("paper_close", 0.5f, 1f, 0.05f);
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

    private void Show()
    {
        if (canvas != null)
            canvas.enabled = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (disablePlayerControlWhenOpened)
            SetPlayerControl(currentInteractor, false);

        if (unlockCursorWhenOpened)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        isOpen = true;
    }

    private void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (canvas != null)
            canvas.enabled = false;
    }

    private void EnsureReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>(true);
    }

    private static string BuildRequiredModuleText(PaperInteractable paper)
    {
        string[] modules = paper != null ? paper.GetRequiredModules() : null;
        if (modules == null || modules.Length == 0)
            return "Required modules: none";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Required modules");
        for (int i = 0; i < modules.Length; i++)
            builder.AppendLine("- " + modules[i]);

        return builder.ToString();
    }

    private static string BuildResultPartText(PaperInteractable paper)
    {
        string resultName = paper != null ? paper.GetResultPartName() : string.Empty;
        if (string.IsNullOrWhiteSpace(resultName))
            resultName = "Unknown Part";

        return "Result part: " + resultName;
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
