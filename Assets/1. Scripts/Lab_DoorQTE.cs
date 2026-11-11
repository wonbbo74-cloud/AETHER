using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Lab_DoorQTE : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Lab_Door targetDoor;

    [Header("Yunseo References")]
    [SerializeField] private Yunseo_Battle yunseoBattle;
    [SerializeField] private Yunseo_Move yunseoMove;
    [SerializeField] private bool lockYunseoMoveDuringQTE = true;
    [SerializeField] private bool lockYunseoLookDuringQTE;

    [Header("QTE Rules")]
    [SerializeField] private int visibleNodeCount = 5;
    [SerializeField] private int targetSuccessCount = 10;
    [SerializeField] private float timeLimit = 12f;
    [SerializeField] private float resultTextDuration = 0.35f;

    [Header("QTE UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private List<GameObject> qteObjectsVisibleOnlyWhileRunning = new List<GameObject>();
    [SerializeField] private List<Image> nodeSlots = new List<Image>();
    [SerializeField] private TMP_Text successFailText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image timerBar;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text progressText;

    [Header("Downed Door Object")]
    [SerializeField] private bool disableDownedObjectOnSuccess = true;
    [SerializeField] private GameObject downedObjectToDisableOnSuccess;

    [Header("Number Sprites")]
    [SerializeField] private Sprite number1Sprite;
    [SerializeField] private Sprite number2Sprite;
    [SerializeField] private Sprite number3Sprite;
    [SerializeField] private Sprite number4Sprite;

    [Header("Events")]
    [SerializeField] private UnityEvent onQTEStarted;
    [SerializeField] private UnityEvent onQTESucceeded;
    [SerializeField] private UnityEvent onQTEFailed;
    [SerializeField] private UnityEvent onQTEEnded;

    private readonly List<int> sequence = new List<int>();
    private Coroutine resultTextRoutine;
    private int successCount;
    private float remainingTime;
    private bool isRunning;
    private bool previousAllowMove;
    private bool previousAllowLook;
    private bool hasLockedYunseoMovement;

    public bool IsRunning => isRunning;
    public int SuccessCount => successCount;
    public int TargetSuccessCount => targetSuccessCount;

    private void Reset()
    {
        targetDoor = GetComponentInParent<Lab_Door>();
    }

    private void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<Lab_Door>();

        HideQTEPanel();
    }

    private void OnEnable()
    {
        if (!isRunning)
            HideQTEPanel();
    }

    private void OnDisable()
    {
        ForceEndQTEWithoutBreach();
    }

    private void OnDestroy()
    {
        ForceEndQTEWithoutBreach();
    }

    private void Update()
    {
        if (!isRunning) return;

        remainingTime -= Time.deltaTime;
        UpdateTimerUI();

        if (remainingTime <= 0f)
        {
            FailQTE();
            return;
        }

        int inputNumber = ReadNumberInput();
        if (inputNumber > 0)
            JudgeInput(inputNumber);
    }

    public bool CanStartQTE(GameObject interactor = null)
    {
        if (isRunning) return false;
        if (targetDoor == null) return false;
        if (!targetDoor.IsNeutralized) return false;
        if (targetDoor.IsBreached) return false;

        return true;
    }

    public void StartQTE()
    {
        StartQTE(null);
    }

    public void StartQTE(GameObject interactor)
    {
        if (!CanStartQTE(interactor)) return;

        TryCacheYunseoReferences(interactor);

        isRunning = true;
        successCount = 0;
        remainingTime = timeLimit;

        BuildInitialSequence();
        ShowQTEPanel();
        UpdateAllUI();
        LockYunseoMovement(true);

        // Yunseo_Battle has small public wrapper methods added for this door QTE.
        if (yunseoBattle != null)
            yunseoBattle.BeginDoorBreachCharge();

        onQTEStarted?.Invoke();
    }

    private void JudgeInput(int inputNumber)
    {
        if (sequence.Count == 0)
            BuildInitialSequence();

        int answer = sequence[0];
        bool isCorrect = inputNumber == answer;

        if (isCorrect)
        {
            successCount++;
            ShowResultText("Success");
        }
        else
        {
            ShowResultText("Fail");
        }

        ShiftSequence();
        UpdateAllUI();

        if (successCount >= targetSuccessCount)
            SucceedQTE();
    }

    private int ReadNumberInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;

        return 0;
    }

    private void BuildInitialSequence()
    {
        sequence.Clear();

        int count = Mathf.Max(1, visibleNodeCount);
        for (int i = 0; i < count; i++)
            sequence.Add(Random.Range(1, 5));
    }

    private void ShiftSequence()
    {
        if (sequence.Count > 0)
            sequence.RemoveAt(0);

        sequence.Add(Random.Range(1, 5));
    }

    private void SucceedQTE()
    {
        if (!isRunning) return;

        isRunning = false;
        HideQTEPanel();
        LockYunseoMovement(false);

        if (yunseoBattle != null)
            yunseoBattle.ExecuteDoorBreachPowerPunch();

        onQTESucceeded?.Invoke();

        if (targetDoor != null)
            targetDoor.BreachDoor(yunseoBattle != null ? yunseoBattle.transform : transform);

        onQTEEnded?.Invoke();

        DisableDownedObjectAfterSuccess();
    }

    private void FailQTE()
    {
        if (!isRunning) return;

        isRunning = false;
        HideQTEPanel();
        LockYunseoMovement(false);

        if (yunseoBattle != null)
            yunseoBattle.CancelDoorBreachCharge();

        onQTEFailed?.Invoke();
        onQTEEnded?.Invoke();
    }

    private void TryCacheYunseoReferences(GameObject interactor)
    {
        if (interactor == null) return;

        Yunseo_Battle interactorBattle = interactor.GetComponentInParent<Yunseo_Battle>();
        Yunseo_Move interactorMove = interactor.GetComponentInParent<Yunseo_Move>();

        if (interactorBattle != null)
            yunseoBattle = interactorBattle;

        if (interactorMove != null)
            yunseoMove = interactorMove;
    }

    private void LockYunseoMovement(bool locked)
    {
        if (yunseoMove == null) return;

        if (locked)
        {
            if (hasLockedYunseoMovement) return;

            previousAllowMove = yunseoMove.allowMove;
            previousAllowLook = yunseoMove.allowLook;
            hasLockedYunseoMovement = true;

            if (lockYunseoMoveDuringQTE)
                yunseoMove.allowMove = false;

            if (lockYunseoLookDuringQTE)
                yunseoMove.allowLook = false;
        }
        else
        {
            if (!hasLockedYunseoMovement) return;

            if (lockYunseoMoveDuringQTE)
                yunseoMove.allowMove = previousAllowMove;

            if (lockYunseoLookDuringQTE)
                yunseoMove.allowLook = previousAllowLook;

            hasLockedYunseoMovement = false;
        }
    }

    private void ShowQTEPanel()
    {
        SetQTEObjectsVisible(true);

        if (qtePanel != null)
            qtePanel.SetActive(true);

        if (successFailText != null)
            successFailText.gameObject.SetActive(false);
    }

    private void HideQTEPanel()
    {
        SetQTEObjectsVisible(false);

        if (qtePanel != null)
            qtePanel.SetActive(false);

        if (resultTextRoutine != null)
        {
            StopCoroutine(resultTextRoutine);
            resultTextRoutine = null;
        }

        for (int i = 0; i < nodeSlots.Count; i++)
        {
            if (nodeSlots[i] != null)
                nodeSlots[i].gameObject.SetActive(false);
        }

        if (successFailText != null)
            successFailText.gameObject.SetActive(false);
    }

    private void SetQTEObjectsVisible(bool visible)
    {
        for (int i = 0; i < qteObjectsVisibleOnlyWhileRunning.Count; i++)
        {
            GameObject qteObject = qteObjectsVisibleOnlyWhileRunning[i];
            if (qteObject == null) continue;

            qteObject.SetActive(visible);
        }
    }

    private void DisableDownedObjectAfterSuccess()
    {
        if (!disableDownedObjectOnSuccess) return;

        GameObject target = downedObjectToDisableOnSuccess != null
            ? downedObjectToDisableOnSuccess
            : gameObject;

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;

        target.SetActive(false);
    }

    private void ForceEndQTEWithoutBreach()
    {
        bool wasRunning = isRunning;
        isRunning = false;

        LockYunseoMovement(false);

        if (wasRunning && yunseoBattle != null)
            yunseoBattle.CancelDoorBreachCharge();
    }

    private void UpdateAllUI()
    {
        UpdateNodeUI();
        UpdateTimerUI();
        UpdateProgressUI();
    }

    private void UpdateNodeUI()
    {
        for (int i = 0; i < nodeSlots.Count; i++)
        {
            Image slot = nodeSlots[i];
            if (slot == null) continue;

            bool hasNumber = i < sequence.Count;
            slot.gameObject.SetActive(hasNumber);

            if (hasNumber)
                slot.sprite = GetNumberSprite(sequence[i]);
        }
    }

    private void UpdateTimerUI()
    {
        float normalizedTime = timeLimit <= 0f ? 0f : Mathf.Clamp01(remainingTime / timeLimit);

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, remainingTime)).ToString();

        if (timerBar != null)
            timerBar.fillAmount = normalizedTime;

        if (timerSlider != null)
            timerSlider.value = normalizedTime;
    }

    private void UpdateProgressUI()
    {
        if (progressText != null)
            progressText.text = successCount + " / " + targetSuccessCount;
    }

    private Sprite GetNumberSprite(int number)
    {
        switch (number)
        {
            case 1: return number1Sprite;
            case 2: return number2Sprite;
            case 3: return number3Sprite;
            case 4: return number4Sprite;
            default: return null;
        }
    }

    private void ShowResultText(string message)
    {
        if (successFailText == null) return;

        if (resultTextRoutine != null)
            StopCoroutine(resultTextRoutine);

        resultTextRoutine = StartCoroutine(ResultTextCoroutine(message));
    }

    private IEnumerator ResultTextCoroutine(string message)
    {
        successFailText.text = message;
        successFailText.gameObject.SetActive(true);

        yield return new WaitForSeconds(resultTextDuration);

        if (successFailText != null)
            successFailText.gameObject.SetActive(false);

        resultTextRoutine = null;
    }
}
