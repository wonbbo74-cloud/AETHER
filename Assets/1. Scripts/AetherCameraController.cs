using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// AETHER ?꾩슜 1?몄묶 移대찓??而⑦듃濡ㅻ윭
/// - ?뚮젅?댁뼱 紐몄껜??醫뚯슦(Y異? ?뚯쟾
/// - 移대찓???쇰쿁? ?곹븯(X異? ?뚯쟾
/// - 留덉슦???낅젰 ?ㅻТ??
/// - ?곹븯/醫뚯슦 ?뚯쟾 ?쒗븳
/// - 嫄룰린/?щ━湲??ㅻ뱶諛?
/// - ?몃? UI?먯꽌 ?쒖꽑 ?좉툑 媛??
/// </summary>
public class AetherCameraController : MonoBehaviour
{
    [Header("?꾩닔 李몄“")]
    [Tooltip("?뚮젅?댁뼱 紐몄껜 Transform. 鍮꾩뼱 ?덉쑝硫??꾩옱 ?ㅻ툕?앺듃 ?ъ슜")]
    public Transform playerBody;

    [Tooltip("?곹븯 ?뚯쟾???대떦??移대찓???쇰쿁")]
    public Transform cameraPitchPivot;

    [Tooltip("?ㅼ젣 移대찓??Transform")]
    public Transform playerCamera;

    [Header("?쒖꽑 ?쒖뼱")]
    [Tooltip("?쒖꽑 ?뚯쟾 ?덉슜 ?щ?")]
    public bool allowLook = true;

    [Tooltip("?쒖옉 ??留덉슦???좉툑")]
    public bool lockCursorOnStart = true;

    [Header("留덉슦??媛먮룄")]
    [Tooltip("湲곕낯 留덉슦??媛먮룄")]
    public float mouseSensitivity = 0.065f;

    [Tooltip("?낅젰 蹂닿컙 ?쒓컙. ?댁닔濡???湲됲븯寃??뚯쟾")]
    public float lookSmoothTime = 0.03f;

    [Tooltip("醫뚯슦 理쒕? ?뚯쟾 ?띾룄(??珥?")]
    public float maxYawSpeed = 520f;

    [Tooltip("?곹븯 理쒕? ?뚯쟾 ?띾룄(??珥?")]
    public float maxPitchSpeed = 400f;

    [Header("?곹븯 ?뚯쟾 ?쒗븳")]
    [Tooltip("?꾩そ 理쒕? ?쒖꽑 媛곷룄")]
    public float maxPitchUp = 65f;

    [Tooltip("?꾨옒履?理쒕? ?쒖꽑 媛곷룄")]
    public float maxPitchDown = 60f;

    [Header("醫뚯슦 ?뚯쟾 ?쒗븳(?좏깮)")]
    [Tooltip("泥댄겕?섎㈃ ?쒖옉 諛⑺뼢 湲곗??쇰줈 醫뚯슦 ?뚯쟾 踰붿쐞瑜??쒗븳")]
    public bool useYawClamp = false;

    [Tooltip("?쇱そ 理쒕? ?뚯쟾 踰붿쐞")]
    public float maxYawLeft = 120f;

    [Tooltip("?ㅻⅨ履?理쒕? ?뚯쟾 踰붿쐞")]
    public float maxYawRight = 120f;

    [Header("Head Bob")]
    [Tooltip("?ㅻ뱶諛??ъ슜 ?щ?")]
    public bool useHeadBob = true;

    [Tooltip("?ㅻ뱶諛??덉슜 ?щ?")]
    public bool allowHeadBob = true;

    [Tooltip("嫄룰린 ?ㅻ뱶諛??띾룄")]
    public float walkBobSpeed = 8f;

    [Tooltip("嫄룰린 ?ㅻ뱶諛??곹븯 媛뺣룄")]
    public float walkBobAmountY = 0.01f;

    [Tooltip("嫄룰린 ?ㅻ뱶諛?醫뚯슦 媛뺣룄")]
    public float walkBobAmountX = 0.004f;

    [Tooltip("?щ━湲??ㅻ뱶諛??띾룄")]
    public float runBobSpeed = 12f;

    [Tooltip("?щ━湲??ㅻ뱶諛??곹븯 媛뺣룄")]
    public float runBobAmountY = 0.018f;

    [Tooltip("?щ━湲??ㅻ뱶諛?醫뚯슦 媛뺣룄")]
    public float runBobAmountX = 0.007f;

    [Tooltip("?뺤? ???먯쐞移?蹂듦? ?띾룄")]
    public float headBobReturnSpeed = 10f;

    [Header("?대룞 ?먯젙")]
    [Tooltip("??媛믩낫???섑룊 ?대룞 ?띾룄媛 ?묒쑝硫??뺤?濡?媛꾩＜")]
    public float moveThreshold = 0.05f;

    [Tooltip("??媛믩낫???섑룊 ?대룞 ?띾룄媛 ?щ㈃ ?щ━湲곕줈 媛꾩＜")]
    public float runSpeedThreshold = 4.0f;

    [Header("Movement Smoothing")]
    [Tooltip("Movement speed smoothing strength.")]
    public float speedSmoothing = 10f;

    [Tooltip("?ㅻ뱶諛?媛以묒튂 ?꾪솚 ?띾룄. ?댁닔濡??쒖옉/?뺤?媛 鍮좊쫫")]
    public float headBobBlendSpeed = 8f;

    [Header("Debug")]
    public bool showDebug = false;

    private float currentYaw;
    private float currentPitch;
    private float initialYaw;

    private float rawYawInput;
    private float rawPitchInput;
    private float smoothedYawInput;
    private float smoothedPitchInput;
    private float yawInputVelocity;
    private float pitchInputVelocity;

    private Vector3 cameraInitialLocalPosition;
    private Vector3 previousBodyPosition;
    private float headBobTimer;
    private float smoothedHorizontalSpeed;
    private float currentHeadBobWeight;
    private Rigidbody bodyRigidbody;

    private void Reset()
    {
        playerBody = transform;

        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;

            if (playerCamera.parent != null)
                cameraPitchPivot = playerCamera.parent;
            else
                cameraPitchPivot = playerCamera;
        }
    }

    private void Awake()
    {
        if (playerBody == null)
            playerBody = transform;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (cameraPitchPivot == null)
        {
            if (playerCamera != null && playerCamera.parent != null)
                cameraPitchPivot = playerCamera.parent;
            else
                cameraPitchPivot = playerCamera;
        }

        if (playerBody == null || cameraPitchPivot == null || playerCamera == null)
        {
            Debug.LogError("[AetherCameraController] ?꾩닔 李몄“媛 鍮꾩뼱 ?덉뒿?덈떎.", this);
            enabled = false;
            return;
        }

        currentYaw = NormalizeAngle(playerBody.eulerAngles.y);
        initialYaw = currentYaw;
        currentPitch = NormalizeAngle(cameraPitchPivot.localEulerAngles.x);

        cameraInitialLocalPosition = playerCamera.localPosition;
        previousBodyPosition = playerBody.position;
        bodyRigidbody = playerBody.GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (!allowLook)
            return;

        Vector2 lookDelta = ReadLookInput();
        ApplyLook(lookDelta);
    }

    private void LateUpdate()
    {
        ApplyHeadBob();
        previousBodyPosition = playerBody.position;
    }

    private void ApplyLook(Vector2 lookDelta)
    {
        // ?낅젰媛????
        rawYawInput = lookDelta.x * mouseSensitivity;
        rawPitchInput = lookDelta.y * mouseSensitivity;

        // 遺?쒕읇寃?蹂닿컙
        smoothedYawInput = Mathf.SmoothDamp(
            smoothedYawInput,
            rawYawInput,
            ref yawInputVelocity,
            lookSmoothTime
        );

        smoothedPitchInput = Mathf.SmoothDamp(
            smoothedPitchInput,
            rawPitchInput,
            ref pitchInputVelocity,
            lookSmoothTime
        );

        // ?꾨젅???낅┰ ?띾룄 ?곸슜
        float yawStep = smoothedYawInput * 1000f * Time.deltaTime;
        float pitchStep = smoothedPitchInput * 1000f * Time.deltaTime;

        // 理쒕? ?뚯쟾 ?띾룄 ?쒗븳
        yawStep = Mathf.Clamp(yawStep, -maxYawSpeed * Time.deltaTime, maxYawSpeed * Time.deltaTime);
        pitchStep = Mathf.Clamp(pitchStep, -maxPitchSpeed * Time.deltaTime, maxPitchSpeed * Time.deltaTime);

        currentYaw += yawStep;
        currentPitch -= pitchStep;

        // ?곹븯 ?쒗븳
        currentPitch = Mathf.Clamp(currentPitch, -maxPitchDown, maxPitchUp);

        // 醫뚯슦 ?쒗븳
        if (useYawClamp)
        {
            float relativeYaw = Mathf.DeltaAngle(initialYaw, currentYaw);
            relativeYaw = Mathf.Clamp(relativeYaw, -maxYawLeft, maxYawRight);
            currentYaw = initialYaw + relativeYaw;
        }

        // ?ㅼ젣 ?뚯쟾 諛섏쁺
        playerBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        cameraPitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

        if (showDebug)
        {
            Debug.Log($"[移대찓?? 醫뚯슦媛곷룄: {currentYaw:F2}, ?곹븯媛곷룄: {currentPitch:F2}", this);
        }
    }

    private void ApplyHeadBob()
    {
        if (playerCamera == null)
            return;

        if (!useHeadBob || !allowHeadBob)
        {
            playerCamera.localPosition = Vector3.Lerp(
                playerCamera.localPosition,
                cameraInitialLocalPosition,
                Time.deltaTime * headBobReturnSpeed
            );
            return;
        }

        float horizontalSpeed;
        if (bodyRigidbody != null)
        {
            Vector3 horizontalVelocity = bodyRigidbody.linearVelocity;
            horizontalVelocity.y = 0f;
            horizontalSpeed = horizontalVelocity.magnitude;
        }
        else
        {
            Vector3 bodyDelta = playerBody.position - previousBodyPosition;
            Vector2 horizontalDelta = new Vector2(bodyDelta.x, bodyDelta.z);
            horizontalSpeed = horizontalDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        float speedLerpFactor = 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime);
        smoothedHorizontalSpeed = Mathf.Lerp(smoothedHorizontalSpeed, horizontalSpeed, speedLerpFactor);

        bool isMoving = smoothedHorizontalSpeed > moveThreshold;
        bool isRunning = smoothedHorizontalSpeed > runSpeedThreshold;

        float bobSpeed = isRunning ? runBobSpeed : walkBobSpeed;
        float bobAmountY = isRunning ? runBobAmountY : walkBobAmountY;
        float bobAmountX = isRunning ? runBobAmountX : walkBobAmountX;

        float bobWeightTarget = isMoving ? 1f : 0f;
        currentHeadBobWeight = Mathf.MoveTowards(
            currentHeadBobWeight,
            bobWeightTarget,
            headBobBlendSpeed * Time.deltaTime
        );

        float normalizedMove = Mathf.Clamp01(smoothedHorizontalSpeed / Mathf.Max(moveThreshold, 0.0001f));
        headBobTimer += Time.deltaTime * bobSpeed * Mathf.Max(0.25f, normalizedMove);

        float bobX = Mathf.Cos(headBobTimer * 0.5f) * bobAmountX;
        float bobY = Mathf.Sin(headBobTimer) * bobAmountY;

        Vector3 targetLocalPos = cameraInitialLocalPosition + new Vector3(
            bobX * currentHeadBobWeight,
            bobY * currentHeadBobWeight,
            0f
        );

        playerCamera.localPosition = Vector3.Lerp(
            playerCamera.localPosition,
            targetLocalPos,
            Time.deltaTime * headBobReturnSpeed
        );
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.delta.ReadValue();
#endif
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    }

    public void SetLookEnabled(bool enabledLook)
    {
        allowLook = enabledLook;
    }

    public void SetHeadBobEnabled(bool enabledHeadBob)
    {
        allowHeadBob = enabledHeadBob;
    }

    public void ResetCameraPositionImmediate()
    {
        if (playerCamera != null)
            playerCamera.localPosition = cameraInitialLocalPosition;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
