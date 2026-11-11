using UnityEngine;
using UnityEngine.SceneManagement;

public class ProximityPuzzleOpener : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float interactDistance = 3.2f;
    [SerializeField] private bool useDistanceFallback = false;

    [Header("Interaction UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string promptTitle = "데이터 연결회로";
    [SerializeField] private string promptActionText = "데이터 연결회로 작동";
    [SerializeField] private bool hidePromptText = false;

    [Header("Local Puzzle Link")]
    [SerializeField] private PuzzleOpener puzzleOpener;

    [Header("Scene Puzzle Link")]
    [SerializeField] private bool openPuzzleScene = false;
    [SerializeField] private string puzzleSceneName = "puzzle2";
    [SerializeField] private string puzzleId = "";

    private bool playerInside;
    private float holdTimer;
    private Move_Hankyul currentPlayerMove;
    private Transform currentPlayerTransform;

    private void Awake()
    {
        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        if (puzzleOpener == null)
            puzzleOpener = GetComponent<PuzzleOpener>();
    }

    private void Start()
    {
        HidePrompt();
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void Update()
    {
        bool playerInRange = playerInside || (useDistanceFallback && IsPlayerInRange());
        if (!playerInRange)
        {
            holdTimer = 0f;
            HidePrompt();
            return;
        }

        if (!openPuzzleScene && puzzleOpener != null && puzzleOpener.IsPuzzleOpen)
            return;

        // Once solved (the bunker door opened), stop showing the circle prompt.
        if (!openPuzzleScene && puzzleOpener != null && puzzleOpener.IsCleared)
        {
            holdTimer = 0f;
            HidePrompt();
            return;
        }

        if (!string.IsNullOrWhiteSpace(puzzleId) && PuzzleSceneFlowState.IsPuzzleCleared(puzzleId))
        {
            HidePrompt();
            return;
        }


        ShowPrompt();

        if (Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            UpdateProgress(holdTimer / Mathf.Max(0.01f, holdTime));

            if (holdTimer >= holdTime)
            {
                holdTimer = 0f;
                UpdateProgress(0f);
                HidePrompt();

                if (openPuzzleScene)
                    BeginPuzzleSceneFlow();
                else if (puzzleOpener != null)
                    puzzleOpener.OpenFromProximity(currentPlayerMove);
            }
        }
        else
        {
            holdTimer = 0f;
            UpdateProgress(0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = true;
        CachePlayer(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = false;
        holdTimer = 0f;
        currentPlayerMove = null;
        currentPlayerTransform = null;
        HidePrompt();
    }

    private void BeginPuzzleSceneFlow()
    {
        if (string.IsNullOrWhiteSpace(puzzleSceneName))
        {
            Debug.LogError("[ProximityPuzzleOpener] puzzleSceneName is empty.", this);
            return;
        }

        Transform playerTransform = currentPlayerMove != null ? currentPlayerMove.transform : null;
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
                playerTransform = player.transform;
        }

        Vector3 returnPosition = playerTransform != null ? playerTransform.position : transform.position;
        Quaternion returnRotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity;
        string returnSceneName = SceneManager.GetActiveScene().name;

        PuzzleSceneFlowState.BeginPuzzle(puzzleId, returnSceneName, returnPosition, returnRotation);
        SceneManager.LoadScene(puzzleSceneName);
    }

    private bool IsPlayerInRange()
    {
        if (currentPlayerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
                CachePlayer(player.transform);
        }

        if (currentPlayerTransform == null)
            return false;

        float distance = Vector3.Distance(currentPlayerTransform.position, transform.position);
        return distance <= interactDistance;
    }

    private void CachePlayer(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        currentPlayerTransform = playerTransform;
        currentPlayerMove = playerTransform.GetComponent<Move_Hankyul>() ?? playerTransform.GetComponentInParent<Move_Hankyul>();
    }

    private void ShowPrompt()
    {
        if (interactionUI != null)
            interactionUI.ShowFor(this, promptTitle, promptActionText, hidePromptText);
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
            interactionUI.HideFor(this);
    }

    private void UpdateProgress(float progress)
    {
        if (interactionUI != null)
            interactionUI.UpdateProgressFor(this, progress);
    }
}

