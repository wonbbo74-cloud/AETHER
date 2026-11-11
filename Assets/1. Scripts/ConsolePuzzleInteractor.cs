using UnityEngine;

public class ConsolePuzzleInteractor : MonoBehaviour
{
    [Header("Interaction Player Tag")]
    [SerializeField] private string allowedPlayerTag = "Player";

    [Header("F Hold")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private float holdDuration = 2f;

    [Header("UI")]
    [SerializeField] private HoldInteractionUI holdInteractionUI;
    [SerializeField] private string interactionMessage = "해킹 시작";

    [Header("Puzzle Controller")]
    [SerializeField] private PuzzleConsoleController puzzleConsoleController;

    [Header("Solved")]
    [SerializeField] private string solvedMessage = "이미 해킹 완료된 콘솔입니다.";

    private bool isPlayerInside;
    private float currentHoldTime;
    private GameObject currentPlayer;

    private void Update()
    {
        if (!isPlayerInside)
            return;

        if (puzzleConsoleController != null && puzzleConsoleController.IsPuzzleOpen())
            return;

        if (puzzleConsoleController != null && puzzleConsoleController.IsSolved())
        {
            ShowMessage(solvedMessage, 1f);
            return;
        }

        if (Input.GetKey(interactKey))
        {
            currentHoldTime += Time.deltaTime;
            float progress = currentHoldTime / Mathf.Max(0.01f, holdDuration);
            ShowMessage(interactionMessage, progress);

            if (currentHoldTime >= holdDuration)
            {
                currentHoldTime = 0f;

                if (holdInteractionUI != null)
                    holdInteractionUI.HideUI();

                if (puzzleConsoleController != null)
                    puzzleConsoleController.OpenPuzzle();
            }
        }
        else
        {
            currentHoldTime = 0f;
            ShowMessage(interactionMessage, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(allowedPlayerTag))
            return;

        isPlayerInside = true;
        currentPlayer = other.gameObject;
        currentHoldTime = 0f;

        string message = puzzleConsoleController != null && puzzleConsoleController.IsSolved()
            ? solvedMessage
            : interactionMessage;
        float progress = puzzleConsoleController != null && puzzleConsoleController.IsSolved() ? 1f : 0f;
        ShowMessage(message, progress);
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentPlayer == null || other.gameObject != currentPlayer)
            return;

        isPlayerInside = false;
        currentPlayer = null;
        currentHoldTime = 0f;

        if (holdInteractionUI != null)
            holdInteractionUI.HideUI();
    }

    private void ShowMessage(string message, float progress)
    {
        if (holdInteractionUI == null)
            return;

        holdInteractionUI.ShowUI(message);
        holdInteractionUI.SetProgress(progress);
    }
}
