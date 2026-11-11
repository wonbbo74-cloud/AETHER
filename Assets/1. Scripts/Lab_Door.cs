using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Lab_Door : MonoBehaviour
{
    private enum DoorStartState
    {
        Lab_Normal,
        Lab_Downed
    }

    [Header("Test Start State")]
    [SerializeField] private DoorStartState startState = DoorStartState.Lab_Normal;
    [SerializeField] private bool invokeDisabledEventWhenStartingDowned;

    [Header("State Roots")]
    [SerializeField] private GameObject normalStateRoot;
    [SerializeField] private GameObject disabledStateRoot;
    [SerializeField] private GameObject breachedStateRoot;

    [Header("Pointers")]
    [SerializeField] private List<Lab_DoorPointer> pointers = new List<Lab_DoorPointer>();

    [Header("Breach Visual")]
    [SerializeField] private GameObject shatteredDoorPrefab;
    [SerializeField] private Transform shatteredSpawnPoint;
    [SerializeField] private GameObject breachEffectPrefab;
    [SerializeField] private ParticleSystem breachEffect;
    [SerializeField] private bool destroyShatteredAfterDelay = true;
    [SerializeField] private float shatteredDestroyDelay = 8f;

    [Header("Breach Force")]
    [SerializeField] private float pieceForwardForce = 7f;
    [SerializeField] private float pieceUpwardForce = 2.5f;
    [SerializeField] private float pieceRandomForce = 2f;
    [SerializeField] private float pieceTorqueForce = 8f;

    [Header("Events")]
    [SerializeField] private UnityEvent onDoorDisabled;
    [SerializeField] private UnityEvent onDoorBreached;

    private readonly List<Lab_DoorPointer> shuffledPointers = new List<Lab_DoorPointer>();

    private int currentProgressIndex;
    private bool isNeutralized;
    private bool isBreached;

    public int CurrentProgressIndex => currentProgressIndex;
    public bool IsDisabled => isNeutralized;
    public bool IsNeutralized => isNeutralized;
    public bool IsBreached => isBreached;
    public UnityEvent OnDoorDisabled => onDoorDisabled;
    public UnityEvent OnDoorBreached => onDoorBreached;
    public Lab_DoorPointer CurrentActivePointer
    {
        get
        {
            if (isNeutralized) return null;
            if (currentProgressIndex < 0 || currentProgressIndex >= shuffledPointers.Count) return null;

            return shuffledPointers[currentProgressIndex];
        }
    }

    private void Awake()
    {
        ApplyStartState();
    }

    private void OnValidate()
    {
        AssignDoorToPointers();
    }

    [ContextMenu("Reset Door")]
    public void ResetDoor()
    {
        isNeutralized = false;
        isBreached = false;
        currentProgressIndex = 0;

        AssignDoorToPointers();
        SetStateRoots(false);
        SetBreachedState(false);
        BuildShuffledOrder();
        ResetAllPointers();
        ActivateCurrentPointer();
    }

    [ContextMenu("Set Door To Downed For Test")]
    public void SetDoorToDownedForTest()
    {
        isNeutralized = true;
        isBreached = false;
        currentProgressIndex = pointers.Count;

        AssignDoorToPointers();
        SetStateRoots(true);
        SetBreachedState(false);
        ResetAllPointers();

        if (invokeDisabledEventWhenStartingDowned)
            onDoorDisabled?.Invoke();
    }

    public void RegisterHit(Lab_DoorPointer completedPointer)
    {
        if (completedPointer == null) return;
        if (isNeutralized) return;

        Lab_DoorPointer activePointer = CurrentActivePointer;
        if (activePointer == null) return;
        if (completedPointer != activePointer) return;

        currentProgressIndex++;

        // 표적이 맞았다: 금속 종소리, 하나씩 맞힐수록 음이 높아진다.
        AetherAudio.PlayAt("target_hit", completedPointer.transform.position, 0.8f, 3f, 45f, 0f,
            1f + 0.08f * currentProgressIndex, 70);

        if (currentProgressIndex >= shuffledPointers.Count)
        {
            CompleteNeutralization();
            return;
        }

        ActivateCurrentPointer();
    }

    /// <summary>Shiri 는 총이 없어 표적을 못 쏜다. 주먹으로 문을 부수면 표적을 다 맞힌 것과 똑같이 열린다.</summary>
    public void PunchOpen()
    {
        if (isNeutralized) return;
        currentProgressIndex = shuffledPointers.Count;
        ResetAllPointers();
        CompleteNeutralization();
    }

    public bool IsCurrentPointer(Lab_DoorPointer pointer)
    {
        return pointer != null && pointer == CurrentActivePointer;
    }

    public void BreachDoor()
    {
        BreachDoor(null);
    }

    public void BreachDoor(Transform attacker)
    {
        if (!isNeutralized) return;
        if (isBreached) return;

        isBreached = true;

        if (disabledStateRoot != null)
            disabledStateRoot.SetActive(false);

        SetBreachedState(true);
        SpawnBreachEffect();
        SpawnShatteredDoor(attacker);

        ApplyAlertEvent(AlertEventType.SuccessMedium);
        onDoorBreached?.Invoke();
    }

    private void CompleteNeutralization()
    {
        isNeutralized = true;
        SetStateRoots(true);
        ApplyAlertEvent(AlertEventType.SuccessMedium);
        onDoorDisabled?.Invoke();

        // 문이 열린 뒤에도 목표가 '표적을 맞혀라'로 남아 있었다.
        AetherModernHUD.SetCurrentObjective("연구소 문이 열렸다.\n안으로 들어가 연구소를 지나라.");
        AetherSave.SetFlag("05-lab-door");
    }

    private void ApplyAlertEvent(AlertEventType eventType)
    {
        AlertLevelManager.Instance?.ApplyAlertEvent(eventType);
    }

    private void ApplyStartState()
    {
        if (startState == DoorStartState.Lab_Downed)
        {
            SetDoorToDownedForTest();
            return;
        }

        ResetDoor();
    }

    private void ActivateCurrentPointer()
    {
        for (int i = 0; i < shuffledPointers.Count; i++)
        {
            Lab_DoorPointer pointer = shuffledPointers[i];
            if (pointer == null) continue;

            if (i == currentProgressIndex)
                pointer.SetActiveVisual(true);
            else if (!pointer.IsCleared)
                pointer.SetToOriginalVisual();
        }
    }

    private void ResetAllPointers()
    {
        for (int i = 0; i < pointers.Count; i++)
        {
            Lab_DoorPointer pointer = pointers[i];
            if (pointer == null) continue;

            pointer.AssignDoor(this);
            pointer.ResetPointer();
        }
    }

    private void BuildShuffledOrder()
    {
        shuffledPointers.Clear();

        for (int i = 0; i < pointers.Count; i++)
        {
            Lab_DoorPointer pointer = pointers[i];
            if (pointer == null) continue;
            if (shuffledPointers.Contains(pointer)) continue;

            shuffledPointers.Add(pointer);
        }

        for (int i = shuffledPointers.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Lab_DoorPointer temp = shuffledPointers[i];
            shuffledPointers[i] = shuffledPointers[randomIndex];
            shuffledPointers[randomIndex] = temp;
        }
    }

    private void AssignDoorToPointers()
    {
        for (int i = 0; i < pointers.Count; i++)
        {
            Lab_DoorPointer pointer = pointers[i];
            if (pointer == null) continue;

            pointer.AssignDoor(this);
        }
    }

    private void SetStateRoots(bool neutralized)
    {
        if (normalStateRoot != null)
            normalStateRoot.SetActive(!neutralized);

        if (disabledStateRoot != null)
            disabledStateRoot.SetActive(neutralized);
    }

    private void SetBreachedState(bool breached)
    {
        if (breachedStateRoot != null)
            breachedStateRoot.SetActive(breached);
    }

    private void SpawnBreachEffect()
    {
        Transform spawnPoint = shatteredSpawnPoint != null ? shatteredSpawnPoint : transform;

        if (breachEffectPrefab != null)
            Instantiate(breachEffectPrefab, spawnPoint.position, spawnPoint.rotation);

        if (breachEffect != null)
            breachEffect.Play();
    }

    private void SpawnShatteredDoor(Transform attacker)
    {
        if (shatteredDoorPrefab == null) return;

        Transform spawnPoint = shatteredSpawnPoint != null ? shatteredSpawnPoint : transform;
        GameObject shatteredDoor = Instantiate(shatteredDoorPrefab, spawnPoint.position, spawnPoint.rotation);

        Rigidbody[] pieces = shatteredDoor.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < pieces.Length; i++)
        {
            Rigidbody piece = pieces[i];
            if (piece == null) continue;

            Vector3 forceDirection = GetBreachForceDirection(piece.transform, attacker);
            float forceScale = Random.Range(0.85f, 1.2f);

            piece.AddForce(forceDirection * forceScale, ForceMode.Impulse);
            piece.AddTorque(Random.insideUnitSphere * pieceTorqueForce, ForceMode.Impulse);
        }

        if (destroyShatteredAfterDelay && shatteredDestroyDelay > 0f)
            Destroy(shatteredDoor, shatteredDestroyDelay);
    }

    private Vector3 GetBreachForceDirection(Transform piece, Transform attacker)
    {
        Vector3 forward = shatteredSpawnPoint != null ? shatteredSpawnPoint.forward : transform.forward;

        if (attacker != null)
        {
            Vector3 awayFromAttacker = piece.position - attacker.position;
            awayFromAttacker.y = 0f;

            if (awayFromAttacker.sqrMagnitude > 0.001f)
                forward = awayFromAttacker.normalized;
        }

        Vector3 randomSpread = new Vector3(
            Random.Range(-pieceRandomForce, pieceRandomForce),
            0f,
            Random.Range(-pieceRandomForce, pieceRandomForce)
        );

        return forward * pieceForwardForce + Vector3.up * pieceUpwardForce + randomSpread;
    }
}
