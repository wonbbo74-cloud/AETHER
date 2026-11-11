using System.Collections;
using CircuitPuzzle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PuzzleSceneReturnBootstrap : MonoBehaviour
{
    private static PuzzleSceneReturnBootstrap instance;
    private CircuitPuzzleManager currentManager;
    private bool isReturning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null)
            return;

        GameObject runner = new GameObject("PuzzleSceneReturnBootstrap");
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<PuzzleSceneReturnBootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookCurrentScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnhookCurrentManager();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCursorStateForSceneFlow();
        RestorePlayerPoseIfNeeded(scene.name);
        HookCurrentScene();
        StartCoroutine(TriggerRewardSpawnersAcrossFrames());
    }

    private void HookCurrentScene()
    {
        UnhookCurrentManager();
        isReturning = false;

        if (!PuzzleSceneFlowState.HasPendingReturn)
            return;

        // The Puzzle2 scene was swapped to the new circuit puzzle (CircuitPuzzleManager_Final), but this code only
        // looked for the old class. After solving, the player never went back to 05 and the reward never appeared.
        finalManager = FindFinalManager();
        if (finalManager != null)
            finalManager.OnPuzzleSuccess.AddListener(HandleFinalPuzzleSuccess);

        // 06 보안 게이트의 숫자 해독(퍼즐3). 결과판·다시 시도·나가기를 여기서 맡는다.
        codeManager = FindInScene<CodebreakerGameManager_Final>();
        if (codeManager != null)
        {
            codeManager.OnPuzzleSuccess.AddListener(HandleCodeSuccess);
            codeManager.OnPuzzleFailed.AddListener(HandleCodeFailed);
            codeOverlay = CodebreakerSceneOverlay.Create(codeManager);
            codeFailed = false;
        }

        currentManager = FindCircuitPuzzleManager();
        if (currentManager == null)
            return;

        currentManager.OnPuzzleCleared += HandlePuzzleCleared;
    }

    private void UnhookCurrentManager()
    {
        if (codeManager != null)
        {
            codeManager.OnPuzzleSuccess.RemoveListener(HandleCodeSuccess);
            codeManager.OnPuzzleFailed.RemoveListener(HandleCodeFailed);
            codeManager = null;
        }
        codeOverlay = null;

        if (finalManager != null)
        {
            finalManager.OnPuzzleSuccess.RemoveListener(HandleFinalPuzzleSuccess);
            finalManager = null;
        }

        if (currentManager == null)
            return;

        currentManager.OnPuzzleCleared -= HandlePuzzleCleared;
        currentManager = null;
    }

    // The result screen (Puzzle2MinigameSkin) has a "계속 진행 (F)" button; wait for it, or go back on our own after a while.
    private const float FinalResultShowTime = 20f;
    private CircuitPuzzleManager_Final finalManager;
    private bool continueRequested;

    public static bool IsWaitingForReturn => instance != null && instance.finalManager != null && PuzzleSceneFlowState.HasPendingReturn;

    public static void RequestContinue()
    {
        if (instance != null)
            instance.continueRequested = true;
    }

    // "포기 (ESC)": back to the previous scene without the reward (same as ESC).
    public static void RequestGiveUp()
    {
        if (instance != null)
            instance.GiveUp();
    }

    private void HandleFinalPuzzleSuccess(PuzzleDifficulty_Final difficulty)
    {
        if (isReturning)
            return;

        isReturning = true;
        continueRequested = false;
        StartCoroutine(ReturnAfterDelay(FinalResultShowTime));
    }

    private void GiveUp()
    {
        if (isReturning || !PuzzleSceneFlowState.HasPendingReturn)
            return;

        isReturning = true;
        string returnSceneName = PuzzleSceneFlowState.ConsumeReturnScene();
        if (!string.IsNullOrWhiteSpace(returnSceneName))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            SceneManager.LoadScene(returnSceneName);
        }
    }

    private static CircuitPuzzleManager_Final FindFinalManager()
    {
        CircuitPuzzleManager_Final[] managers = Resources.FindObjectsOfTypeAll<CircuitPuzzleManager_Final>();
        foreach (CircuitPuzzleManager_Final manager in managers)
        {
            if (manager != null && manager.gameObject.scene.IsValid())
                return manager;
        }

        return null;
    }

    private CodebreakerGameManager_Final codeManager;
    private CodebreakerSceneOverlay codeOverlay;
    private bool codeFailed;

    private void HandleCodeSuccess(PuzzleDifficulty_Final difficulty)
    {
        if (isReturning)
            return;

        if (codeOverlay != null) codeOverlay.ShowSuccess();
        isReturning = true;
        continueRequested = false;
        StartCoroutine(ReturnAfterDelay(FinalResultShowTime));
    }

    private void HandleCodeFailed(PuzzleDifficulty_Final difficulty)
    {
        codeFailed = true;
        AetherAudio.Play2D("ui_error", 0.8f);
        if (codeOverlay != null) codeOverlay.ShowFail(codeManager != null && codeManager.RemainingTime <= 0f);
    }

    private static T FindInScene<T>() where T : Component
    {
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
        {
            if (c != null && c.gameObject.scene.IsValid())
                return c;
        }
        return null;
    }

    private void UpdateCodebreaker()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !PuzzleSceneFlowState.HasPendingReturn)
            return;

        if (isReturning)
        {
            // 성공 결과판에서 F 로 바로 돌아간다.
            if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                continueRequested = true;
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            GiveUp();
            return;
        }

        if (codeFailed && kb.fKey.wasPressedThisFrame)
            RetryCode();
    }

    /// <summary>실패 화면의 [재시도] 버튼.</summary>
    public static void RequestCodeRetry()
    {
        if (instance != null && instance.codeFailed)
            instance.RetryCode();
    }

    private void RetryCode()
    {
        if (codeManager == null) return;
        codeFailed = false;
        if (codeOverlay != null) codeOverlay.HideResult();
        AetherAudio.Play2D("ui_confirm", 0.7f);
        codeManager.StartPuzzle(codeManager.ActiveDifficulty);
        if (codeOverlay != null) codeOverlay.ApplyLimits();
    }

    private void Update()
    {
        if (codeManager != null)
        {
            UpdateCodebreaker();
            return;
        }

        // ESC in the puzzle scene goes back without solving (it can be tried again).
        if (isReturning || finalManager == null || !PuzzleSceneFlowState.HasPendingReturn)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame)
            return;

        GiveUp();
    }

    private void HandlePuzzleCleared()
    {
        if (currentManager == null || isReturning)
            return;

        isReturning = true;
        StartCoroutine(ReturnAfterDelay(Mathf.Max(0.01f, currentManager.ResultShowTime)));
    }

    private CircuitPuzzleManager FindCircuitPuzzleManager()
    {
        CircuitPuzzleManager[] managers = Resources.FindObjectsOfTypeAll<CircuitPuzzleManager>();
        foreach (CircuitPuzzleManager manager in managers)
        {
            if (manager == null)
                continue;

            GameObject managerObject = manager.gameObject;
            if (managerObject == null || !managerObject.scene.IsValid())
                continue;

            return manager;
        }

        return null;
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        string puzzleId = PuzzleSceneFlowState.ActivePuzzleId;
        if (!string.IsNullOrWhiteSpace(puzzleId))
            PuzzleSceneFlowState.MarkPuzzleCleared(puzzleId);

        float waitUntil = Time.unscaledTime + delay;
        while (Time.unscaledTime < waitUntil && !continueRequested)
            yield return null;
        continueRequested = false;

        string returnSceneName = PuzzleSceneFlowState.ConsumeReturnScene();
        if (!string.IsNullOrWhiteSpace(returnSceneName))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            SceneManager.LoadScene(returnSceneName);
        }
    }

    private IEnumerator TriggerRewardSpawnersAcrossFrames()
    {
        if (!PuzzleSceneFlowState.HasAnyPendingReward)
            yield break;

        for (int frame = 0; frame < 10; frame++)
        {
            yield return null;

            RewardGunSpawner[] rewardSpawners = Resources.FindObjectsOfTypeAll<RewardGunSpawner>();
            for (int i = 0; i < rewardSpawners.Length; i++)
            {
                RewardGunSpawner spawner = rewardSpawners[i];
                if (spawner == null)
                    continue;

                GameObject spawnerObject = spawner.gameObject;
                if (spawnerObject == null || !spawnerObject.scene.IsValid())
                    continue;

                spawner.TrySpawnFromPuzzleState();
            }
        }
    }

    private void ApplyCursorStateForSceneFlow()
    {
        if (!PuzzleSceneFlowState.HasPendingReturn)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestorePlayerPoseIfNeeded(string sceneName)
    {
        if (!PuzzleSceneFlowState.TryConsumeReturnPose(sceneName, out Vector3 position, out Quaternion rotation))
            return;

        StartCoroutine(RestorePlayerPoseAcrossFrames(position, rotation));
    }

    private IEnumerator RestorePlayerPoseAcrossFrames(Vector3 position, Quaternion rotation)
    {
        Transform playerTransform = null;

        for (int frame = 0; frame < 6; frame++)
        {
            yield return null;

            if (playerTransform == null)
                playerTransform = FindPlayerTransform();

            if (playerTransform == null)
                continue;

            SetPlayerMovementState(playerTransform, false);
            ApplyPlayerPose(playerTransform, position, rotation);
        }

        if (playerTransform != null)
            RestorePlayerControlState(playerTransform);
    }

    private void ApplyPlayerPose(Transform playerTransform, Vector3 position, Quaternion rotation)
    {
        if (playerTransform == null)
            return;

        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool hadController = characterController != null && characterController.enabled;
        if (hadController)
            characterController.enabled = false;

        Rigidbody rigidbody = playerTransform.GetComponent<Rigidbody>();
        bool hadRigidbody = rigidbody != null;
        bool wasKinematic = hadRigidbody && rigidbody.isKinematic;

        if (hadRigidbody)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
            rigidbody.position = position;
            rigidbody.rotation = rotation;
        }

        playerTransform.SetPositionAndRotation(position, rotation);

        AetherPlayerCharacter.SnapLook(playerTransform.root.gameObject, rotation);

        if (hadRigidbody)
            rigidbody.isKinematic = wasKinematic;

        if (hadController)
            characterController.enabled = true;
    }

    private Transform FindPlayerTransform()
    {
        // 지금 조작 중인 캐릭터(한결·윤서).
        Transform active = AetherPlayerCharacter.FindActiveTransform();
        if (active != null)
            return active;

        Move_Hankyul moveHankyul = FindObjectOfType<Move_Hankyul>();
        if (moveHankyul != null)
            return moveHankyul.transform;

        Hankyul_Move hankyulMove = FindObjectOfType<Hankyul_Move>();
        if (hankyulMove != null)
            return hankyulMove.transform;

        Yunseo_Move yunseoMove = FindObjectOfType<Yunseo_Move>();
        if (yunseoMove != null)
            return yunseoMove.transform;

        PlayerInput playerInput = FindObjectOfType<PlayerInput>();
        if (playerInput != null)
            return playerInput.transform;

        return Camera.main != null ? Camera.main.transform.root : null;
    }

    private void RestorePlayerControlState(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        Move_Hankyul moveHankyul = playerTransform.GetComponent<Move_Hankyul>() ?? playerTransform.GetComponentInChildren<Move_Hankyul>(true);
        if (moveHankyul != null)
        {
            moveHankyul.allowMove = true;
            moveHankyul.allowLook = true;
            moveHankyul.allowInput = true;
        }

        Hankyul_Move hankyulMove = playerTransform.GetComponent<Hankyul_Move>() ?? playerTransform.GetComponentInChildren<Hankyul_Move>(true);
        if (hankyulMove != null)
        {
            hankyulMove.allowMove = true;
            hankyulMove.allowLook = true;
        }

        Yunseo_Move yunseoMove = playerTransform.GetComponent<Yunseo_Move>() ?? playerTransform.GetComponentInChildren<Yunseo_Move>(true);
        if (yunseoMove != null)
        {
            yunseoMove.allowMove = true;
            yunseoMove.allowLook = true;
        }

        // 실제로 쓰는 윤서 이동 스크립트는 Move_Yunseo 다(Yunseo_Move 는 옛 것).
        Move_Yunseo moveYunseo = playerTransform.GetComponent<Move_Yunseo>() ?? playerTransform.GetComponentInChildren<Move_Yunseo>(true);
        if (moveYunseo != null)
        {
            moveYunseo.SetMoveEnabled(true);
            moveYunseo.SetLookEnabled(true);
        }

        PlayerInput playerInput = playerTransform.GetComponent<PlayerInput>() ?? playerTransform.GetComponentInChildren<PlayerInput>(true);
        if (playerInput != null)
            playerInput.ActivateInput();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SetPlayerMovementState(Transform playerTransform, bool enabled)
    {
        if (playerTransform == null)
            return;

        Move_Hankyul moveHankyul = playerTransform.GetComponent<Move_Hankyul>() ?? playerTransform.GetComponentInChildren<Move_Hankyul>(true);
        if (moveHankyul != null)
        {
            moveHankyul.allowMove = enabled;
            moveHankyul.allowLook = enabled;
            moveHankyul.allowInput = enabled;
        }

        Hankyul_Move hankyulMove = playerTransform.GetComponent<Hankyul_Move>() ?? playerTransform.GetComponentInChildren<Hankyul_Move>(true);
        if (hankyulMove != null)
        {
            hankyulMove.allowMove = enabled;
            hankyulMove.allowLook = enabled;
        }

        Yunseo_Move yunseoMove = playerTransform.GetComponent<Yunseo_Move>() ?? playerTransform.GetComponentInChildren<Yunseo_Move>(true);
        if (yunseoMove != null)
        {
            yunseoMove.allowMove = enabled;
            yunseoMove.allowLook = enabled;
        }

        Move_Yunseo moveYunseo = playerTransform.GetComponent<Move_Yunseo>() ?? playerTransform.GetComponentInChildren<Move_Yunseo>(true);
        if (moveYunseo != null)
        {
            moveYunseo.SetMoveEnabled(enabled);
            moveYunseo.SetLookEnabled(enabled);
        }
    }
}
