using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ElevatorInteractionController : MonoBehaviour, IHoldInteractable
{
    [Header("?곹샇?묒슜")]
    [SerializeField] private string interactionName = "엘레베이터 탑승";
    [SerializeField] private float holdDuration = 1.2f;

    [Header("Door Animation")]
    [SerializeField] private Animation doorAnimation;
    [SerializeField] private string openClipName = "OpenDoors";
    [SerializeField] private string closeClipName = "CloseDoors";
    [SerializeField] private float fallbackDoorAnimationTime = 1f;

    [Header("?뚮젅?댁뼱 ?묒듅")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform boardingPoint;
    [SerializeField] private Transform boardingLookTarget;
    [SerializeField] private float boardMoveDuration = 0.65f;
    [SerializeField] private MonoBehaviour[] controlsToDisable;

    [Header("痢??좏깮 UI")]
    [SerializeField] private ElevatorFloorSelectionUI floorSelectionUI;
    [SerializeField] private bool firstBoardingOnlyRooftop = true;
    [SerializeField] private string firstBoardingPlayerPrefsKey = "AETHER_ELEVATOR_FIRST_BOARDING";
    [SerializeField] private ElevatorFloorOption[] firstBoardingFloors =
    {
        new ElevatorFloorOption { floorTitle = "옥상", floorSubtitle = "1구역", sceneName = "08.1구역_2", unlocked = true },
        new ElevatorFloorOption { floorTitle = "2구역", floorSubtitle = "SECTION 02", sceneName = "09.2구역_2", unlocked = false },
        new ElevatorFloorOption { floorTitle = "3구역", floorSubtitle = "SECTION 03", sceneName = "11.3구역_찐", unlocked = false },
        new ElevatorFloorOption { floorTitle = "1층 연구실", floorSubtitle = "LAB 1F", sceneName = "06.연구소 메인 장소", unlocked = true }
    };

    [Header("?대룞 ?곗텧")]
    [SerializeField] private Transform elevatorCabinRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float travelDuration = 4.5f;
    [SerializeField] private float cabinShakeAmplitude = 0.025f;
    [SerializeField] private float cabinVerticalMotion = 0.045f;
    [SerializeField] private float cameraShakeAmplitude = 0.01f;
    [SerializeField] private float cameraTravelPitchAmplitude = 0.75f;
    [SerializeField] private float cameraTravelRollAmplitude = 0.45f;
    [SerializeField] private Light[] cabinLights;
    [SerializeField] private float lightFlickerSpeed = 11f;
    [SerializeField] private float lightIntensityMultiplier = 0.18f;

    [Header("???꾪솚")]
    [SerializeField] private string fallbackTargetSceneName = "08.1구역_2";
    [SerializeField] private bool useSceneFaderIfAvailable = true;
    [SerializeField] private float sceneFadeOutTime = 0.75f;
    [SerializeField] private float sceneFadeInTime = 0.75f;
    [SerializeField] private float arrivalDoorOpenDelay = 0.45f;
    [SerializeField] private string pendingArrivalSceneKey = "AETHER_ELEVATOR_PENDING_ARRIVAL_SCENE";
    [SerializeField] private bool carryPlayerAcrossElevatorSceneLoad = true;
    [SerializeField] private bool carryElevatorVisualAcrossSceneLoad = true;
    [SerializeField] private bool boostInteriorLightingAfterSceneLoad = true;
    [SerializeField] private float fallbackArrivalInsideDistance = 1.7f;
    [SerializeField] private bool overrideArrivalPlayerScale;
    [SerializeField] private Vector3 arrivalPlayerScale = new Vector3(1.6f, 1.6f, 1.6f);
    [SerializeField] private bool overrideArrivalMovementSpeedMultiplier;
    [SerializeField] private float arrivalMovementSpeedMultiplier = 1f;
    [SerializeField] private bool overrideArrivalJumpForceMultiplier;
    [SerializeField] private float arrivalJumpForceMultiplier = 1f;

    [Header("Sound")]
    [SerializeField] private AudioSource movementLoopAudio;
    [SerializeField] private AudioSource oneShotAudio;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField] private AudioClip arrivalDingClip;

    [Header("Events")]
    [SerializeField] private UnityEvent onBoardingStarted;
    [SerializeField] private UnityEvent onFloorUIOpened;
    [SerializeField] private UnityEvent onTravelStarted;
    [SerializeField] private UnityEvent onArrived;

    private Vector3 cabinInitialLocalPosition;
    private Vector3 cameraInitialLocalPosition;
    private Quaternion cameraInitialLocalRotation;
    private float[] baseLightIntensities;
    private bool busy;
    private bool boarded;
    private bool savedUseGravity;
    private bool savedIsKinematic;
    private RigidbodyConstraints savedConstraints;
    private Move_Hankyul lockedHankyulMove;
    private Move_Yunseo lockedYunseoMove;
    private bool savedAllowInput;
    private bool savedAllowMove;
    private bool savedAllowLook;
    private bool arrivalPlayerPreparedBeforeStart;
    private static Transform carriedPlayerRoot;
    private static string carriedTargetSceneName;
    private static ElevatorInteractionController carriedElevatorController;
    private static string carriedElevatorTargetSceneName;

    public bool CanInteract => !busy && !boarded;

    public string GetInteractionName()
    {
        if (string.IsNullOrWhiteSpace(interactionName) || InteractionPromptTextUtility.LooksCorrupted(interactionName))
            interactionName = "엘레베이터 탑승";

        return interactionName;
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
        if (busy || boarded)
            return;

        if (playerRoot == null && interactor != null)
            playerRoot = interactor.transform;

        if (playerRigidbody == null && playerRoot != null)
            playerRigidbody = playerRoot.GetComponent<Rigidbody>();

        StartCoroutine(BoardingRoutine());
    }

    /// <summary>
    /// 모든 씬에서 엘리베이터가 같은 크기로 보이게 하는 기준 배율.
    ///
    /// 실측(AETHER/진단/월드 배율 측정)해 보니 씬마다 제각각이었다.
    ///   06.연구소 메인 장소 : ElevatorV2 x1.50 → 높이 4.45m  (플레이어 1.55m의 2.9배)
    ///   08.1구역_2          : ElevatorV2 x5.00 → 높이 14.83m (플레이어의 9.6배)
    ///
    /// 게다가 도착 처리가 타고 온 칸을 목적지 칸의 배율로 바꿔 버려서,
    /// 06에서 1구역으로 가면 타고 있던 엘리베이터가 눈앞에서 3.3배로 부풀어 올랐다.
    ///
    /// 재어 보니 06과 08의 '실제로 쓰이는' 엘리베이터는 둘 다 4.45m로 이미 같았다.
    /// (08에는 14.83m짜리 비활성 중복본이 하나 더 있는데, 꺼져 있어 보이지 않는다.)
    /// 체감 크기가 달라졌던 진짜 원인은 1구역에서만 플레이어를 키운 것이었고 그건 걷어냈다.
    ///
    /// 이 보정은 그 뒤에도 씬마다 칸 크기가 어긋나지 않게 잡아 두는 안전장치다.
    /// localScale은 씬마다 기준이 달라 믿을 수 없으므로 <b>실제로 그려지는 높이</b>로 맞춘다.
    /// </summary>
    private const float CanonicalCabinHeight = 4.45f;

    private void Awake()
    {
        NormalizeFloorOptions();
        NormalizeCabinScale();
        EnsureElevatorAudio();

        if (elevatorCabinRoot != null)
            cabinInitialLocalPosition = elevatorCabinRoot.localPosition;

        if (playerCamera != null)
        {
            cameraInitialLocalPosition = playerCamera.transform.localPosition;
            cameraInitialLocalRotation = playerCamera.transform.localRotation;
        }

        if (cabinLights != null && cabinLights.Length > 0)
        {
            baseLightIntensities = new float[cabinLights.Length];
            for (int i = 0; i < cabinLights.Length; i++)
                baseLightIntensities[i] = cabinLights[i] != null ? cabinLights[i].intensity : 1f;
        }

        if (floorSelectionUI != null)
        {
            floorSelectionUI.FloorSelected -= HandleFloorSelected;
            floorSelectionUI.FloorSelected += HandleFloorSelected;
            floorSelectionUI.HideImmediate();
        }
    }

    private void Start()
    {
        NormalizeFloorOptions();
        ForceDoorClosedState();

        if (ShouldOpenDoorForArrivalScene())
        {
            if (TryHandOffArrivalToCarriedElevator())
                return;

            StartCoroutine(OpenDoorForArrivalSceneRoutine());
        }
    }

    private void OnDestroy()
    {
        if (floorSelectionUI != null)
            floorSelectionUI.FloorSelected -= HandleFloorSelected;
    }

    /// <summary>
    /// 칸이 실제로 그려지는 높이를 기준값에 맞춘다.
    /// 바닥 높이는 그대로 둬야 플레이어가 바닥에 빠지거나 공중에 뜨지 않는다.
    /// </summary>
    private void NormalizeCabinScale()
    {
        // 플레이어는 모든 씬에서 같아야 한다. 도착할 때 크기를 덮어쓰지 않는다.
        overrideArrivalPlayerScale = false;

        if (!TryGetCabinBounds(out Bounds before))
        {
            Debug.LogWarning($"[Elevator] '{name}'의 렌더러를 찾지 못해 크기를 맞추지 못했습니다.");
            return;
        }

        float height = before.size.y;
        if (height < 0.01f)
            return;

        float factor = CanonicalCabinHeight / height;
        if (Mathf.Abs(factor - 1f) < 0.05f)
            return;   // 이미 기준에 가깝다

        transform.localScale *= factor;

        if (TryGetCabinBounds(out Bounds after))
            transform.position += Vector3.up * (before.min.y - after.min.y);

        Debug.Log($"[Elevator] '{gameObject.scene.name}'의 엘리베이터 높이를 " +
                  $"{height:0.00}m → {CanonicalCabinHeight:0.00}m로 맞췄습니다 (바닥 높이 유지, 배율 x{factor:0.###}).");
    }

    /// <summary>칸 전체를 감싸는 월드 경계. 렌더러가 하나도 없으면 false.</summary>
    private bool TryGetCabinBounds(out Bounds bounds)
    {
        bounds = default;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool started = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Bounds rendererBounds = renderer.bounds;
            if (float.IsNaN(rendererBounds.size.y) || float.IsInfinity(rendererBounds.size.y))
                continue;

            if (!started)
            {
                bounds = rendererBounds;
                started = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return started;
    }

    private IEnumerator BoardingRoutine()
    {
        busy = true;
        onBoardingStarted?.Invoke();

        LockPlayer();

        PlayDoor(openClipName, doorOpenClip);
        yield return new WaitForSeconds(GetDoorClipLength(openClipName));

        yield return MovePlayerToBoardingPoint();

        PlayDoor(closeClipName, doorCloseClip);
        yield return new WaitForSeconds(GetDoorClipLength(closeClipName));

        boarded = true;
        busy = false;

        OpenFloorSelectionUI();
    }

    private IEnumerator MovePlayerToBoardingPoint()
    {
        if (playerRoot == null || boardingPoint == null)
            yield break;

        Vector3 startPosition = playerRoot.position;
        Quaternion startRotation = playerRoot.rotation;
        Quaternion targetRotation = GetBoardingTargetRotation(startRotation);
        float time = 0f;

        while (time < boardMoveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, boardMoveDuration));
            float smoothT = t * t * (3f - 2f * t);

            playerRoot.position = Vector3.Lerp(startPosition, boardingPoint.position, smoothT);
            playerRoot.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            yield return null;
        }

        playerRoot.SetPositionAndRotation(boardingPoint.position, targetRotation);
        SyncPlayerLook(targetRotation);
    }

    private void OpenFloorSelectionUI()
    {
        if (floorSelectionUI == null)
        {
            GameObject uiObject = new GameObject("Elevator Floor Selection UI");
            floorSelectionUI = uiObject.AddComponent<ElevatorFloorSelectionUI>();
            floorSelectionUI.FloorSelected += HandleFloorSelected;
        }

        NormalizeFloorOptions();

        if (firstBoardingFloors != null && firstBoardingFloors.Length > 0)
            floorSelectionUI.ConfigureFloors(firstBoardingFloors);

        floorSelectionUI.Open();
        onFloorUIOpened?.Invoke();
    }

    private void HandleFloorSelected(ElevatorFloorOption floor)
    {
        if (floor == null || busy)
            return;

        string targetScene = string.IsNullOrEmpty(floor.sceneName) ? fallbackTargetSceneName : floor.sceneName;
        if (string.IsNullOrEmpty(targetScene))
            return;

        // 이번 빌드는 엘리베이터까지만 담는다. 아직 넣지 않은 구역을 고르면
        // 씬을 부르는 대신 "TO BE CONTINUED"를 보여 주고 시작 화면으로 돌려보낸다.
        if (IsNotInThisBuild(targetScene))
        {
            // 06 에 들어오자마자 복도 끝 엘리베이터로 가서 옥상을 고르면, 파츠 제작실(07)을 한 번도 안 거치고
            // 바로 끝 화면이 떴다(실측). 파츠를 하나도 만들지 않았으면 먼저 제작실로 안내한다.
            if (PartSystemRuntimeState.GetCraftedEquipmentParts().Count == 0)
            {
                AetherAudio.Play2D("ui_error", 0.5f, 1f, 0f);
                AetherObjectiveToast.Show("옥상은 강화파츠가 있어야 간다.\n1층 연구실의 파츠 제작실로 가자.", 7f);
                return;
            }

            busy = true;

            if (floorSelectionUI != null)
                floorSelectionUI.Close();

            AetherToBeContinued.Play();
            return;
        }

        StartCoroutine(TravelRoutine(targetScene));
    }

    /// <summary>
    /// 이번 빌드에 담기지 않은 구역인가.
    ///
    /// 씬 이름 앞의 번호로 가린다. 08(1구역) 이후가 아직 준비되지 않았다.
    /// 나중에 구역을 넣을 때는 이 목록에서 빼면 바로 이어진다.
    /// </summary>
    private static bool IsNotInThisBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < ScenesNotInThisBuild.Length; i++)
        {
            if (sceneName.StartsWith(ScenesNotInThisBuild[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static readonly string[] ScenesNotInThisBuild = { "08.", "09.", "10.", "11." };

    private void NormalizeFloorOptions()
    {
        firstBoardingFloors = new[]
        {
            new ElevatorFloorOption { floorTitle = "옥상", floorSubtitle = "1구역", sceneName = "08.1구역_2", unlocked = true },
            new ElevatorFloorOption { floorTitle = "2구역", floorSubtitle = "SECTION 02", sceneName = "09.2구역_2", unlocked = ElevatorZoneProgress.IsZoneUnlocked(2) },
            new ElevatorFloorOption { floorTitle = "3구역", floorSubtitle = "SECTION 03", sceneName = "11.3구역_찐", unlocked = ElevatorZoneProgress.IsZoneUnlocked(3) },
            // 구역에서 모은 재료로 다음 파츠를 만들러 돌아가는 층.
            // 한 구역을 끝내기 전에는 돌아갈 이유가 없으므로 그때 열린다.
            new ElevatorFloorOption { floorTitle = "파츠 제작실", floorSubtitle = "PARTS LAB", sceneName = "07.파츠 제작실", unlocked = ElevatorZoneProgress.IsAnyZoneCleared() },
            new ElevatorFloorOption { floorTitle = "1층 연구실", floorSubtitle = "LAB 1F", sceneName = "06.연구소 메인 장소", unlocked = true }
        };

        fallbackTargetSceneName = "08.1구역_2";
    }

    private IEnumerator TravelRoutine(string targetScene)
    {
        busy = true;

        if (floorSelectionUI != null)
            floorSelectionUI.Close();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayDoor(closeClipName, doorCloseClip);
        yield return new WaitForSeconds(GetDoorClipLength(closeClipName));

        if (movementLoopAudio != null)
        {
            movementLoopAudio.loop = true;
            movementLoopAudio.Play();
        }

        onTravelStarted?.Invoke();

        float elapsed = 0f;
        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            UpdateTravelMotion(elapsed);
            yield return null;
        }

        ResetTravelMotion();

        if (movementLoopAudio != null && movementLoopAudio.isPlaying)
            movementLoopAudio.Stop();

        if (oneShotAudio != null && arrivalDingClip != null)
            oneShotAudio.PlayOneShot(arrivalDingClip);

        onArrived?.Invoke();

        MarkFirstBoardingCompleted();
        MarkPendingArrival(targetScene);
        PreparePlayerForSceneLoad();
        LoadTargetScene(targetScene);
    }

    private void UpdateTravelMotion(float elapsed)
    {
        if (elevatorCabinRoot != null)
        {
            float x = Mathf.Sin(elapsed * 18f) * cabinShakeAmplitude;
            float y = Mathf.Sin(elapsed * 2.2f) * cabinVerticalMotion + Mathf.Cos(elapsed * 15f) * cabinShakeAmplitude;
            float z = Mathf.Cos(elapsed * 13f) * cabinShakeAmplitude;
            elevatorCabinRoot.localPosition = cabinInitialLocalPosition + new Vector3(x, y, z);
        }

        if (playerCamera != null)
        {
            float x = Mathf.Sin(elapsed * 22f) * cameraShakeAmplitude;
            float y = Mathf.Cos(elapsed * 19f) * cameraShakeAmplitude;
            playerCamera.transform.localPosition = cameraInitialLocalPosition + new Vector3(x, y, 0f);

            float pitch = Mathf.Sin(elapsed * 3.1f) * cameraTravelPitchAmplitude;
            float roll = Mathf.Sin(elapsed * 4.7f) * cameraTravelRollAmplitude;
            playerCamera.transform.localRotation = cameraInitialLocalRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        if (cabinLights != null && baseLightIntensities != null)
        {
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * lightFlickerSpeed, 0.31f) - 0.5f) * lightIntensityMultiplier;

            for (int i = 0; i < cabinLights.Length; i++)
            {
                if (cabinLights[i] != null)
                    cabinLights[i].intensity = baseLightIntensities[i] * flicker;
            }
        }
    }

    private void ResetTravelMotion()
    {
        if (elevatorCabinRoot != null)
            elevatorCabinRoot.localPosition = cabinInitialLocalPosition;

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = cameraInitialLocalPosition;
            playerCamera.transform.localRotation = cameraInitialLocalRotation;
        }

        if (cabinLights != null && baseLightIntensities != null)
        {
            for (int i = 0; i < cabinLights.Length; i++)
            {
                if (cabinLights[i] != null)
                    cabinLights[i].intensity = baseLightIntensities[i];
            }
        }
    }

    private void LockPlayer()
    {
        if (playerRigidbody != null)
        {
            savedUseGravity = playerRigidbody.useGravity;
            savedIsKinematic = playerRigidbody.isKinematic;
            savedConstraints = playerRigidbody.constraints;

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
        }

        if (controlsToDisable != null)
        {
            for (int i = 0; i < controlsToDisable.Length; i++)
            {
                if (controlsToDisable[i] != null)
                    controlsToDisable[i].enabled = false;
            }
        }

        LockKnownPlayerControls();
    }

    public void UnlockPlayerForCancelOrDebug()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = savedIsKinematic;
            playerRigidbody.useGravity = savedUseGravity;
            playerRigidbody.constraints = savedConstraints;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (controlsToDisable != null)
        {
            for (int i = 0; i < controlsToDisable.Length; i++)
            {
                if (controlsToDisable[i] != null)
                    controlsToDisable[i].enabled = true;
            }
        }

        RestoreKnownPlayerControls();
    }

    private Quaternion GetBoardingTargetRotation(Quaternion fallbackRotation)
    {
        if (boardingPoint == null)
            return fallbackRotation;

        Transform lookTarget = boardingLookTarget != null ? boardingLookTarget : FindDoorLookTarget();
        if (lookTarget == null)
            return boardingPoint.rotation;

        // 문 오브젝트의 기준점은 문짝 가장자리에 있어서 그쪽을 보면 시선이 33° 비껴 모서리를 봤다.
        // 문 그림의 한가운데를 본다.
        // 문짝은 두 장(안/밖)이 옆으로 미끄러지는 미닫이라, 한 장만 보면 여전히 12° 비낀다. 문짝·문틀 넷의 가운데를 본다.
        Vector3 doorCenter = lookTarget.position;
        if (boardingLookTarget == null)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (string doorName in new[] { "elevDoorInner", "elevDoorOuter", "elevFramDoorR", "elevFrameDoorL" })
            {
                Transform part = FindDeepChild(transform, doorName);
                if (part == null) continue;
                Renderer partRenderer = part.GetComponentInChildren<Renderer>();
                sum += partRenderer != null ? partRenderer.bounds.center : part.position;
                count++;
            }
            if (count > 0) doorCenter = sum / count;
        }

        Vector3 direction = doorCenter - boardingPoint.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return boardingPoint.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Transform FindDoorLookTarget()
    {
        string[] doorNames = { "elevDoorInner", "elevDoorOuter", "elevFramDoorR", "elevFrameDoorL" };

        for (int i = 0; i < doorNames.Length; i++)
        {
            Transform found = FindDeepChild(transform, doorNames[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void SyncPlayerLook(Quaternion targetRotation)
    {
        if (playerRoot == null)
            return;

        AetherPlayerCharacter.SnapLook(playerRoot.gameObject, targetRotation);
    }

    private void LockKnownPlayerControls()
    {
        if (playerRoot == null)
            return;

        // 윤서는 allowInput 이 없어 이동·시점만 잠근다.
        lockedYunseoMove = playerRoot.GetComponent<Move_Yunseo>();
        if (lockedYunseoMove != null)
        {
            savedAllowMove = lockedYunseoMove.allowMove;
            savedAllowLook = lockedYunseoMove.allowLook;
            lockedYunseoMove.SetMoveEnabled(false);
            lockedYunseoMove.SetLookEnabled(false);
        }

        lockedHankyulMove = playerRoot.GetComponent<Move_Hankyul>();
        if (lockedHankyulMove == null)
            return;

        savedAllowInput = lockedHankyulMove.allowInput;
        savedAllowMove = lockedHankyulMove.allowMove;
        savedAllowLook = lockedHankyulMove.allowLook;

        lockedHankyulMove.allowInput = false;
        lockedHankyulMove.allowMove = false;
        lockedHankyulMove.allowLook = false;
    }

    private void RestoreKnownPlayerControls()
    {
        if (lockedYunseoMove != null)
        {
            lockedYunseoMove.SetMoveEnabled(savedAllowMove);
            lockedYunseoMove.SetLookEnabled(savedAllowLook);
            lockedYunseoMove = null;
        }

        if (lockedHankyulMove == null)
            return;

        lockedHankyulMove.allowInput = savedAllowInput;
        lockedHankyulMove.allowMove = savedAllowMove;
        lockedHankyulMove.allowLook = savedAllowLook;
        lockedHankyulMove = null;
    }

    /// <summary>
    /// 06 씬에 소리통·소리가 전부 비어 있어(NULL) 엘리베이터가 무음이었다. 비어 있으면 AETHER 효과음으로 채운다.
    /// 둘 다 3D(엘리베이터 자리에서 들림)이고 효과음 음량을 따른다.
    /// </summary>
    private void EnsureElevatorAudio()
    {
        if (doorOpenClip == null) doorOpenClip = AetherAudio.Clip("door_slide_open");
        if (doorCloseClip == null) doorCloseClip = AetherAudio.Clip("door_slide_close");
        if (arrivalDingClip == null) arrivalDingClip = AetherAudio.Clip("elevator_ding");

        if (oneShotAudio == null)
            oneShotAudio = MakeElevatorSource("ElevatorOneShot", null, false, 0.9f);
        if (movementLoopAudio == null)
            movementLoopAudio = MakeElevatorSource("ElevatorMoveLoop", AetherAudio.Clip("elevator_move"), true, 0.6f);
    }

    private AudioSource MakeElevatorSource(string name, AudioClip clip, bool loop, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(elevatorCabinRoot != null ? elevatorCabinRoot : transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.clip = clip;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 2f;
        src.maxDistance = 25f;
        src.dopplerLevel = 0f;
        src.volume = volume;
        go.AddComponent<AetherSfxScaler>().Bind(src);
        return src;
    }

    private void PlayDoor(string clipName, AudioClip audioClip)
    {
        if (oneShotAudio != null && audioClip != null)
            oneShotAudio.PlayOneShot(audioClip);

        if (doorAnimation == null || string.IsNullOrEmpty(clipName))
            return;

        if (doorAnimation.GetClip(clipName) == null)
            return;

        doorAnimation.Stop();
        doorAnimation[clipName].wrapMode = WrapMode.ClampForever;
        doorAnimation.Play(clipName);
    }

    private void ForceDoorClosedState()
    {
        if (doorAnimation == null || string.IsNullOrEmpty(closeClipName))
            return;

        AnimationClip closeClip = doorAnimation.GetClip(closeClipName);
        if (closeClip == null)
            return;

        doorAnimation.Stop();
        doorAnimation[closeClipName].wrapMode = WrapMode.ClampForever;
        doorAnimation.Play(closeClipName);
        doorAnimation[closeClipName].time = closeClip.length;
        doorAnimation.Sample();
        doorAnimation.Stop();
    }

    private float GetDoorClipLength(string clipName)
    {
        if (doorAnimation == null || string.IsNullOrEmpty(clipName))
            return fallbackDoorAnimationTime;

        AnimationClip clip = doorAnimation.GetClip(clipName);
        return clip != null ? clip.length : fallbackDoorAnimationTime;
    }

    private void LoadTargetScene(string sceneName)
    {
        if (useSceneFaderIfAvailable && SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeOutAndLoadScene(sceneName, sceneFadeOutTime, sceneFadeInTime);
            return;
        }

        if (useSceneFaderIfAvailable && SceneFadeController.Instance != null)
        {
            SceneFadeController.Instance.FadeToScene(sceneName, sceneFadeOutTime, 0.1f);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void MarkPendingArrival(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        PlayerPrefs.SetString(pendingArrivalSceneKey, sceneName);
        PlayerPrefs.Save();
    }

    private bool ShouldOpenDoorForArrivalScene()
    {
        string pendingScene = PlayerPrefs.GetString(pendingArrivalSceneKey, string.Empty);
        string activeScene = SceneManager.GetActiveScene().name;
        bool prefsPending = !string.IsNullOrEmpty(pendingScene) && pendingScene == activeScene;
        bool carriedPending = carriedPlayerRoot != null && carriedTargetSceneName == activeScene;
        bool carriedElevatorPending = carriedElevatorController != null && carriedElevatorTargetSceneName == activeScene;
        return prefsPending || carriedPending || carriedElevatorPending;
    }

    private bool TryHandOffArrivalToCarriedElevator()
    {
        if (carriedElevatorController == null || carriedElevatorController == this)
            return false;

        if (carriedElevatorTargetSceneName != SceneManager.GetActiveScene().name)
            return false;

        StartCoroutine(HandOffArrivalToCarriedElevatorRoutine(carriedElevatorController));
        return true;
    }

    private IEnumerator HandOffArrivalToCarriedElevatorRoutine(ElevatorInteractionController carriedElevator)
    {
        PlayerPrefs.DeleteKey(pendingArrivalSceneKey);
        PlayerPrefs.Save();

        carriedElevator.transform.SetPositionAndRotation(transform.position, transform.rotation);
        carriedElevator.transform.localScale = transform.localScale;
        SceneManager.MoveGameObjectToScene(carriedElevator.gameObject, SceneManager.GetActiveScene());

        carriedElevator.playerRoot = carriedPlayerRoot != null ? carriedPlayerRoot : carriedElevator.FindPlayerRoot();
        carriedElevator.playerRigidbody = carriedElevator.playerRoot != null ? carriedElevator.playerRoot.GetComponent<Rigidbody>() : null;
        carriedElevator.ForceDoorClosedState();

        SetElevatorVisualsEnabled(false);
        yield return carriedElevator.OpenDoorForArrivalSceneRoutine();
    }

    private void SetElevatorVisualsEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = enabled;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            lights[i].enabled = enabled;
    }

    private IEnumerator OpenDoorForArrivalSceneRoutine()
    {
        PlayerPrefs.DeleteKey(pendingArrivalSceneKey);
        PlayerPrefs.Save();

        yield return PlacePlayerInArrivalElevatorRoutine();
        yield return new WaitForSeconds(arrivalDoorOpenDelay);

        if (oneShotAudio != null && arrivalDingClip != null)
            oneShotAudio.PlayOneShot(arrivalDingClip);

        PlayDoor(openClipName, doorOpenClip);
        yield return new WaitForSeconds(GetDoorClipLength(openClipName));

        RestoreArrivalPlayerControls();
    }

    private void PreparePlayerForSceneLoad()
    {
        if (!carryPlayerAcrossElevatorSceneLoad || playerRoot == null)
            return;

        playerRoot.SetParent(null, true);
        DontDestroyOnLoad(playerRoot.gameObject);
        carriedPlayerRoot = playerRoot;
        carriedTargetSceneName = PlayerPrefs.GetString(pendingArrivalSceneKey, string.Empty);

        SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedPlayer;
        SceneManager.sceneLoaded += HandleSceneLoadedForCarriedPlayer;

        if (carryElevatorVisualAcrossSceneLoad)
        {
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            carriedElevatorController = this;
            carriedElevatorTargetSceneName = carriedTargetSceneName;
            SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedElevator;
            SceneManager.sceneLoaded += HandleSceneLoadedForCarriedElevator;
        }
    }

    /// <summary>
    /// 도착한 씬에도 그 씬 몫의 플레이어가 서 있다.
    ///
    /// 엘리베이터는 타고 있던 플레이어를 그대로 데려오므로, 씬에 있던 쪽은 한 명 더 남는다.
    /// 그 몸에도 Player_UI 캔버스가 달려 있어서 체력바·AETHER LEVEL 이 두 겹으로 겹쳐 보였고,
    /// 조종되지 않는 빈 몸이 엘리베이터 밖에 그대로 서 있었다.
    ///
    /// 씬이 참조를 들고 있을 수 있으니 지우지는 않고 꺼 두기만 한다.
    /// </summary>
    private static void HandleSceneLoadedForCarriedPlayer(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedPlayer;

        if (carriedPlayerRoot == null)
            return;

        int turnedOff = 0;

        // 한결·윤서 모두. 타고 온 캐릭터가 아닌 플레이어는 다 끈다.
        List<MonoBehaviour> movers = new List<MonoBehaviour>();
        movers.AddRange(FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        movers.AddRange(FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        foreach (MonoBehaviour player in movers)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            Transform root = player.transform;
            if (root == carriedPlayerRoot || root.IsChildOf(carriedPlayerRoot) || carriedPlayerRoot.IsChildOf(root))
                continue;

            if (player.gameObject.scene != scene)
                continue;

            player.gameObject.SetActive(false);
            turnedOff++;
        }

        if (turnedOff > 0)
            Debug.Log($"[엘리베이터] 도착한 씬에 있던 플레이어 {turnedOff}명을 껐습니다(타고 온 플레이어를 그대로 씁니다).");
    }

    private static void HandleSceneLoadedForCarriedElevator(Scene scene, LoadSceneMode mode)
    {
        if (carriedElevatorController == null || carriedElevatorTargetSceneName != scene.name)
            return;

        ElevatorInteractionController destinationElevator = FindDestinationElevatorInScene(scene);
        if (destinationElevator == null)
            return;

        carriedElevatorController.PrepareCarriedElevatorBeforeFirstFrame(destinationElevator, scene);
        SceneManager.sceneLoaded -= HandleSceneLoadedForCarriedElevator;
    }

    private static ElevatorInteractionController FindDestinationElevatorInScene(Scene scene)
    {
        ElevatorInteractionController[] elevators = FindObjectsByType<ElevatorInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < elevators.Length; i++)
        {
            ElevatorInteractionController elevator = elevators[i];
            if (elevator == null || elevator == carriedElevatorController)
                continue;

            if (elevator.gameObject.scene == scene)
                return elevator;
        }

        return null;
    }

    private void PrepareCarriedElevatorBeforeFirstFrame(ElevatorInteractionController destinationElevator, Scene destinationScene)
    {
        transform.SetPositionAndRotation(destinationElevator.transform.position, destinationElevator.transform.rotation);
        transform.localScale = destinationElevator.transform.localScale;
        SceneManager.MoveGameObjectToScene(gameObject, destinationScene);
        SetElevatorVisualsEnabled(true);
        ApplyArrivalInteriorLightingBoost();

        playerRoot = carriedPlayerRoot != null ? carriedPlayerRoot : FindPlayerRoot();
        playerRigidbody = playerRoot != null ? playerRoot.GetComponent<Rigidbody>() : null;
        overrideArrivalPlayerScale = destinationElevator.overrideArrivalPlayerScale;
        arrivalPlayerScale = destinationElevator.arrivalPlayerScale;
        overrideArrivalMovementSpeedMultiplier = destinationElevator.overrideArrivalMovementSpeedMultiplier;
        arrivalMovementSpeedMultiplier = destinationElevator.arrivalMovementSpeedMultiplier;
        ForceDoorClosedState();
        PlacePlayerInArrivalElevatorImmediate();

        destinationElevator.SetElevatorVisualsEnabled(false);
    }

    private void ApplyArrivalInteriorLightingBoost()
    {
        if (!boostInteriorLightingAfterSceneLoad)
            return;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (light.name == "Interior Cool Ceiling")
            {
                light.color = new Color(0.6708f, 0.846f, 0.91f, 1f);
                light.intensity = 10.85f;
                light.range = 4.1f;
            }
            else if (light.name == "Interior Front Wash")
            {
                light.color = new Color(0.7396f, 0.8836f, 0.91f, 1f);
                light.intensity = 5.642f;
                light.range = 3.4f;
            }
            else if (light.name == "Interior Floor Soft Fill")
            {
                light.color = new Color(0.602f, 0.7708f, 0.91f, 1f);
                light.intensity = 2.4304f;
                light.range = 2.8f;
            }
        }
    }

    private void PlacePlayerInArrivalElevatorImmediate()
    {
        if (playerRoot == null)
            return;

        playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        LockArrivalPlayerControls();
        ApplyArrivalPlayerScale();
        ApplyArrivalMovementSpeedMultiplier();

        Vector3 arrivalPosition = GetArrivalPlayerPosition();
        Quaternion arrivalRotation = GetArrivalPlayerRotation();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        playerRoot.SetPositionAndRotation(arrivalPosition, arrivalRotation);
        SyncPlayerLook(arrivalRotation);
        ActivatePlayerCameraForGameView();
        arrivalPlayerPreparedBeforeStart = true;
    }

    private IEnumerator PlacePlayerInArrivalElevatorRoutine()
    {
        if (arrivalPlayerPreparedBeforeStart)
        {
            arrivalPlayerPreparedBeforeStart = false;
            yield break;
        }

        for (int i = 0; i < 10 && playerRoot == null; i++)
        {
            playerRoot = FindPlayerRoot();
            yield return null;
        }

        if (playerRoot == null)
            yield break;

        playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        LockArrivalPlayerControls();
        ApplyArrivalPlayerScale();
        ApplyArrivalMovementSpeedMultiplier();

        Vector3 arrivalPosition = GetArrivalPlayerPosition();
        Quaternion arrivalRotation = GetArrivalPlayerRotation();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        playerRoot.SetPositionAndRotation(arrivalPosition, arrivalRotation);
        SyncPlayerLook(arrivalRotation);
        ActivatePlayerCameraForGameView();
    }

    private void ApplyArrivalPlayerScale()
    {
        if (!overrideArrivalPlayerScale || playerRoot == null)
            return;

        playerRoot.localScale = arrivalPlayerScale;
    }

    private void ApplyArrivalMovementSpeedMultiplier()
    {
        if (playerRoot == null)
            return;

        Move_Hankyul moveHankyul = playerRoot.GetComponent<Move_Hankyul>();
        if (moveHankyul == null)
            moveHankyul = playerRoot.GetComponentInChildren<Move_Hankyul>(true);

        if (moveHankyul != null)
        {
            moveHankyul.SetMovementSpeedMultiplier(
                overrideArrivalMovementSpeedMultiplier ? arrivalMovementSpeedMultiplier : 1f);
            moveHankyul.SetJumpForceMultiplier(
                overrideArrivalJumpForceMultiplier ? arrivalJumpForceMultiplier : 1f);
        }
    }

    private Transform FindPlayerRoot()
    {
        if (carriedPlayerRoot != null)
            return carriedPlayerRoot;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer.transform;

        Move_Hankyul moveHankyul = FindFirstObjectByType<Move_Hankyul>();
        if (moveHankyul != null)
            return moveHankyul.transform;

        return null;
    }

    private Vector3 GetArrivalPlayerPosition()
    {
        if (boardingPoint != null)
            return boardingPoint.position;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        return transform.position - flatForward.normalized * fallbackArrivalInsideDistance;
    }

    private Quaternion GetArrivalPlayerRotation()
    {
        if (boardingPoint != null)
            return GetBoardingTargetRotation(boardingPoint.rotation);

        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private void LockArrivalPlayerControls()
    {
        if (playerRigidbody != null)
        {
            savedUseGravity = playerRigidbody.useGravity;
            savedIsKinematic = playerRigidbody.isKinematic;
            savedConstraints = playerRigidbody.constraints;

            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        LockKnownPlayerControls();
    }

    private void RestoreArrivalPlayerControls()
    {
        // 내렸으니 다시 탈 수 있어야 한다.
        //
        // 타고 온 엘리베이터 칸은 도착 씬까지 그대로 따라와서 그 씬의 엘리베이터 자리를 대신한다.
        // 그런데 boarded 를 한 번 켜 놓고 끄는 데가 없어서, 한 번 타고 내리면
        // 그 엘리베이터는 영영 다시 탈 수 없었다.
        // 1층 연구실로 내려오면 1구역으로 갈 방법이 사라져 게임이 막히는 자리였다.
        boarded = false;
        busy = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.useGravity = true;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        Move_Hankyul moveHankyul = playerRoot != null ? playerRoot.GetComponent<Move_Hankyul>() : null;
        if (moveHankyul != null)
        {
            moveHankyul.allowInput = true;
            moveHankyul.allowMove = true;
            moveHankyul.allowLook = true;
            moveHankyul.enabled = true;
        }

        Move_Yunseo moveYunseo = playerRoot != null ? playerRoot.GetComponent<Move_Yunseo>() : null;
        if (moveYunseo != null)
        {
            moveYunseo.enabled = true;
            moveYunseo.SetMoveEnabled(true);
            moveYunseo.SetLookEnabled(true);
        }

        PlayerInput playerInput = playerRoot != null ? playerRoot.GetComponent<PlayerInput>() : null;
        if (playerInput != null)
            playerInput.ActivateInput();

        if (playerRoot != null)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerRoot.gameObject, SceneManager.GetActiveScene());

        if (carriedPlayerRoot == playerRoot)
        {
            carriedPlayerRoot = null;
            carriedTargetSceneName = string.Empty;
        }

        if (carriedElevatorController == this)
        {
            carriedElevatorController = null;
            carriedElevatorTargetSceneName = string.Empty;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ActivatePlayerCameraForGameView()
    {
        if (playerRoot == null)
            return;

        Camera playerViewCamera = playerRoot.GetComponentInChildren<Camera>(true);
        if (playerViewCamera == null)
            return;

        EnsureCameraCanSeeElevator(playerViewCamera);

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera targetCamera = cameras[i];
            if (targetCamera == null)
                continue;

            bool isPlayerCamera = targetCamera == playerViewCamera;
            targetCamera.enabled = isPlayerCamera;

            if (isPlayerCamera)
                targetCamera.tag = "MainCamera";
            else if (targetCamera.CompareTag("MainCamera"))
                targetCamera.tag = "Untagged";
        }

        AudioListener playerListener = playerViewCamera.GetComponent<AudioListener>();
        if (playerListener == null)
            playerListener = playerViewCamera.gameObject.AddComponent<AudioListener>();

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
                listeners[i].enabled = listeners[i] == playerListener;
        }
    }

    private void EnsureCameraCanSeeElevator(Camera targetCamera)
    {
        if (targetCamera == null)
            return;

        int elevatorLayer = LayerMask.NameToLayer("ElevatorInterior");
        if (elevatorLayer < 0)
            return;

        targetCamera.cullingMask |= 1 << elevatorLayer;
    }

    private bool HasCompletedFirstBoarding()
    {
        return PlayerPrefs.GetInt(firstBoardingPlayerPrefsKey, 0) == 1;
    }

    private void MarkFirstBoardingCompleted()
    {
        if (!firstBoardingOnlyRooftop)
            return;

        PlayerPrefs.SetInt(firstBoardingPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }
}
