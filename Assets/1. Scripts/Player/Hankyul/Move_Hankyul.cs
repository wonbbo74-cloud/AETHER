using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Move_Hankyul : MonoBehaviour
{
    [Header("Movement Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;

    [Header("Crouch Collider")]
    [Range(0.3f, 0.8f)]
    public float crouchHeightRatio = 0.6f;

    [Header("Movement Basis")]
    public Transform orientationTransform;
    public bool moveRelativeToCamera = false;

    [Header("Movement Feel")]
    public float acceleration = 18f;
    public float deceleration = 24f;
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.45f;
    public float stopSnapSpeed = 0.08f;

    [Header("Scene Movement Scale")]
    [SerializeField] private float movementSpeedMultiplier = 1f;
    [SerializeField] private float jumpForceMultiplier = 1f;

    [Header("Recoil")]
    [Tooltip("반동으로 밀린 시선이 제자리로 돌아오는 속도. 클수록 빨리 돌아옵니다.")]
    [SerializeField] private float recoilRecoverySpeed = 9f;

    private float recoilPitch;   // 위로 튀어 오른 각도
    private float recoilYaw;     // 좌우로 밀린 각도
    private float recoilRoll;    // 화면이 기운 각도

    [Header("Stair Climb")]
    [SerializeField] private bool enableStairStepAssist = true;
    [SerializeField] private float maxStepHeight = 4f;
    [SerializeField] private float stepCheckDistance = 1.15f;
    [SerializeField] private float stepClimbSpeed = 55f;
    [SerializeField] private LayerMask stepMask = ~0;

    [Header("Animation Feel")]
    public float animatorDampTime = 0.08f;
    public float crouchAnimatorSpeed = 0.35f;
    public float walkAnimatorSpeed = 0.55f;
    public float runAnimatorSpeed = 1f;

    [Header("Camera")]
    public Transform camTransform;
    public float mouseSensitivity = 200f;
    private Hankyul_Battle aimSource;
    private bool aimSourceSearched;
    [Tooltip("설정 화면의 '마우스 반전' 항목이 제어한다.")]
    public bool invertLookY = false;
    public bool invertLookX = false;
    public float minLookAngle = -60f;
    public float maxLookAngle = 60f;
    public bool smoothLook = true;
    [Range(0.005f, 0.12f)]
    public float lookSmoothTime = 0.025f;

    [Header("Camera Stance")]
    public bool adjustCameraForStance = true;
    public Vector3 standingCameraLocalPosition;
    public Vector3 crouchingCameraLocalPosition;
    public Vector3 sprintingCameraLocalPosition;
    public float cameraStanceLerpSpeed = 14f;
    [Range(0.75f, 0.98f)]
    public float minCameraHeightRatio = 0.9f;
    public float minCameraForwardOffset = 0.28f;

    [Header("View Model Clipping")]
    [Range(0.005f, 0.1f)]
    public float viewModelNearClip = 0.035f;
    public bool hideOwnBodyFromFirstPersonCamera = true;
    public string ownBodyLayerName = "Player";
    public string viewModelLayerName = "ViewModel";

    [Header("Fall Safety")]
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundMask;
    public float jumpCooldown = 0.5f;

    [Header("Super Jump Module")]
    [SerializeField] private bool canSuperJump;
    [SerializeField] private float superJumpMinForce = 5f;
    [SerializeField] private float superJumpMaxChargeTime = 1.5f;
    [SerializeField] private float superJumpMaxMultiplier = 2.5f;
    [SerializeField] private float superJumpMaxForce = 12.5f;
    [Range(0f, 1f)]
    [SerializeField] private float superJumpAlertChargeThreshold = 0.85f;
    [SerializeField] private int superJumpLandingAlertAmount = 5;
    [SerializeField] private float sceneLoadInputGraceTime = 0.2f;

    [Header("Input Allowed")]
    public bool allowMove = true;
    public bool allowLook = true;
    public bool allowInput = true;

    private float xRotation;
    private float targetXRotation;
    private float currentYaw;
    private float targetYaw;
    private float yawSmoothVelocity;
    private float pitchSmoothVelocity;
    private Vector2 moveInput;
    private Vector2 lookInput;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator anim;
    private PlayerInput playerInput;

    private bool isSprinting;
    private bool isCrouching;
    private float originalHeight;
    private Vector3 originalCenter;
    private bool isGrounded = true;
    private bool wasGrounded = true;
    private float jumpCooldownTimer;
    private bool isChargingSuperJump;
    private float superJumpChargeTime;
    private bool pendingSuperJumpLandingAlert;
    private float inputResumeTime;
    private float lastMoveInputMagnitude;
    private Vector3 cameraDefaultLocalPosition;
    private bool hasCameraDefaultLocalPosition;
    private AetherCameraController duplicateAetherCameraController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        anim = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        DisableDuplicateLookControllers();

        if (anim != null)
            anim.applyRootMotion = false;

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            // 낙하 중 얇은 콜라이더 통과 방지
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        originalHeight = capsule != null ? capsule.height : 2f;
        originalCenter = capsule != null ? capsule.center : Vector3.up;

        ResolveOwnedControlReferences();

        currentYaw = orientationTransform.eulerAngles.y;
        targetYaw = currentYaw;
        targetXRotation = xRotation;

        if (camTransform != null)
        {
            cameraDefaultLocalPosition = camTransform.localPosition;
            hasCameraDefaultLocalPosition = true;

            cameraDefaultLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition);
            camTransform.localPosition = cameraDefaultLocalPosition;

            if (standingCameraLocalPosition == Vector3.zero)
                standingCameraLocalPosition = cameraDefaultLocalPosition;

            if (crouchingCameraLocalPosition == Vector3.zero)
                crouchingCameraLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition + new Vector3(0f, -0.45f, 0.02f), true);

            if (sprintingCameraLocalPosition == Vector3.zero)
                sprintingCameraLocalPosition = GetSafeCameraLocalPosition(cameraDefaultLocalPosition + new Vector3(0f, 0.06f, 0.04f));

            standingCameraLocalPosition = GetSafeCameraLocalPosition(standingCameraLocalPosition);
            sprintingCameraLocalPosition = GetSafeCameraLocalPosition(sprintingCameraLocalPosition);

            ApplyCameraClipSettings();
            ConfigureFirstPersonSelfVisibility();
        }

        LockCursor();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SuppressInputBriefly();

        if (playerInput != null && !playerInput.inputIsActive)
            playerInput.ActivateInput();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (orientationTransform != null && !IsOwnedTransform(orientationTransform))
            orientationTransform = transform;

        if (camTransform != null && !IsOwnedTransform(camTransform))
            camTransform = FindOwnedCameraTransform();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ClearTransientInputState();
    }

    private void Update()
    {
        bool inputReady = Time.unscaledTime >= inputResumeTime;
        bool canMove = allowInput && allowMove && inputReady;
        bool canLook = allowInput && allowLook && inputReady;

        if (canLook)
            LockCursor();
        else
            UnlockCursor();

        ReadFallbackInput(canMove, canLook);
        HandleLook(canLook);
        UpdateGrounded();

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && canMove)
        {
            isSprinting = keyboard.leftShiftKey.isPressed;

            if (keyboard.leftCtrlKey.wasPressedThisFrame)
                ToggleCrouch();

            if (canSuperJump)
                HandleSuperJumpInput(keyboard);
            else if (keyboard.spaceKey.wasPressedThisFrame)
                TryJump();
        }
        else if (!canMove)
        {
            ClearTransientInputState();
        }

        UpdateCameraStance();
        UpdateAnimator(canMove);
    }

    private void FixedUpdate()
    {
        // While an elevator or cutscene holds the body kinematic, touching velocity only spams warnings (50 per second while riding).
        if (rb != null && rb.isKinematic)
            return;

        HandleMovement();
        ClampFallSpeed();
    }

    private void ClampFallSpeed()
    {
        if (rb == null) return;
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = -maxFallSpeed;
            rb.linearVelocity = v;
        }
    }

    private void OnMove(InputValue value)
    {
        if (!allowInput || !allowMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = value.Get<Vector2>();
    }

    private void OnLook(InputValue value)
    {
        if (!allowInput || !allowLook)
        {
            lookInput = Vector2.zero;
            return;
        }

        lookInput = value.Get<Vector2>();
    }

    private void ReadFallbackInput(bool canMove, bool canLook)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && canMove)
        {
            Vector2 keyboardMove = Vector2.zero;

            if (keyboard.aKey.isPressed) keyboardMove.x -= 1f;
            if (keyboard.dKey.isPressed) keyboardMove.x += 1f;
            if (keyboard.sKey.isPressed) keyboardMove.y -= 1f;
            if (keyboard.wKey.isPressed) keyboardMove.y += 1f;

            moveInput = Vector2.ClampMagnitude(keyboardMove, 1f);
        }
        else if (!canMove)
        {
            moveInput = Vector2.zero;
        }
        else if (playerInput == null || !playerInput.inputIsActive)
        {
            moveInput = Vector2.zero;
        }

        if (canLook && !AetherLookGuard.ShouldIgnoreLook())
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                lookInput = mouse.delta.ReadValue();
            else
                lookInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }
        else
        {
            lookInput = Vector2.zero;
        }
    }

    private void HandleMovement()
    {
        if (rb == null)
            return;

        if (!allowInput || !allowMove || Time.unscaledTime < inputResumeTime)
        {
            lastMoveInputMagnitude = 0f;
            Vector3 stopVelocity = rb.linearVelocity;
            stopVelocity.x = 0f;
            stopVelocity.z = 0f;
            rb.linearVelocity = stopVelocity;
            return;
        }

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        lastMoveInputMagnitude = inputDir.magnitude;
        float currentSpeed = (isCrouching ? crouchSpeed : (isSprinting ? runSpeed : walkSpeed)) * Mathf.Max(0.05f, movementSpeedMultiplier);
        GetMovementBasis(out Vector3 forward, out Vector3 right);
        Vector3 moveDir = forward * inputDir.z + right * inputDir.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (inputDir.sqrMagnitude <= 0.001f)
        {
            Vector3 stopVelocity = rb.linearVelocity;
            stopVelocity.x = 0f;
            stopVelocity.z = 0f;
            rb.linearVelocity = stopVelocity;
            return;
        }

        Vector3 moveDelta = moveDir * currentSpeed * Time.fixedDeltaTime;
        // Check the path first so running does not go through thin doors and gates. Below step height is left to the step assist.
        moveDelta = AetherMoveSweep.ClampHorizontal(rb, capsule, moveDelta, 0.75f);
        Vector3 nextPosition = rb.position + moveDelta;
        if (TryGetStepAdjustedPosition(moveDir, nextPosition, out Vector3 stepAdjustedPosition))
            nextPosition = stepAdjustedPosition;

        rb.MovePosition(nextPosition);
    }

    private bool TryGetStepAdjustedPosition(Vector3 moveDir, Vector3 nextPosition, out Vector3 adjustedPosition)
    {
        adjustedPosition = nextPosition;

        if (!enableStairStepAssist || capsule == null || rb == null || !isGrounded)
            return false;

        if (moveDir.sqrMagnitude <= 0.001f)
            return false;

        // Shiri 와 같은 계단 보조를 쓴다(아래 예전 계산은 발이 조금만 떠도 턱을 놓쳐 0.4m 턱에 걸렸다).
        if (useSharedStepAssist)
            return AetherStepAssist.TryStep(rb, capsule, moveDir, nextPosition, out adjustedPosition);

        Vector3 direction = moveDir.normalized;
        float radius = Mathf.Max(0.05f, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        // The prefab value is 4 m, so Meka walked straight up crates, vents and walls lower than 4 m,
        // and even through thin ceilings (found in the 04 bunker). Cap it at stair size (top surface up to ~0.65 m above the feet).
        float stepHeight = Mathf.Clamp(maxStepHeight, 0.05f, MaxStepAssistHeight);
        float forwardDistance = radius + Mathf.Max(0.05f, stepCheckDistance);

        Vector3 capsuleBottom = transform.TransformPoint(capsule.center) + Vector3.down * (capsule.height * transform.lossyScale.y * 0.5f - radius);
        Vector3 lowerOrigin = capsuleBottom + Vector3.up * 0.08f;
        Vector3 upperOrigin = capsuleBottom + Vector3.up * (stepHeight + 0.08f);

        if (!Physics.Raycast(lowerOrigin, direction, out RaycastHit lowerHit, forwardDistance, stepMask, QueryTriggerInteraction.Ignore))
            return false;

        // A walkable slope (ramp, stairs with a ramp on top) isn't a step. Stepping on it made him rise early and drop back, so the climb went up in jerks.
        if (lowerHit.normal.y > 0.5f)
            return false;

        if (Physics.Raycast(upperOrigin, direction, forwardDistance, stepMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 topProbeOrigin = capsuleBottom + direction * forwardDistance + Vector3.up * (stepHeight + 0.35f);
        float downDistance = stepHeight + 0.55f;
        // Climb only when a step surface is actually found. It used to rise by the full step height even with no surface.
        if (!Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit stepHit, downDistance, stepMask, QueryTriggerInteraction.Ignore)
            || stepHit.normal.y < 0.35f)
            return false;

        // 발바닥 기준 높이. 예전엔 아래 구의 중심(발에서 반지름만큼 위)을 기준으로 재서 턱보다 덜 올라가 결국 걸렸다.
        float heightDelta = stepHit.point.y - (capsuleBottom.y - radius);
        if (heightDelta <= 0.01f || heightDelta > stepHeight + 0.05f)
            return false;

        float targetY = rb.position.y + heightDelta + 0.06f;

        // Only step up if the body fits at the new height. Otherwise it went up through low ceilings and overhangs.
        if (!HasRoomAt(new Vector3(nextPosition.x, targetY, nextPosition.z), radius))
            return false;

        float smoothedY = Mathf.MoveTowards(rb.position.y, targetY, stepClimbSpeed * Time.fixedDeltaTime);
        if (smoothedY <= rb.position.y + 0.001f)
            smoothedY = rb.position.y + Mathf.Min(stepHeight, stepClimbSpeed * Time.fixedDeltaTime);

        adjustedPosition = new Vector3(nextPosition.x, smoothedY, nextPosition.z);
        return true;
    }

    private static readonly bool useSharedStepAssist = true;
    private const float MaxStepAssistHeight = AetherStepAssist.MaxStepHeight; // 0.45m. 0.35m 로는 05 잔해 모서리에 자주 걸렸다.
    private static readonly Collider[] stepRoomHits = new Collider[16];

    private bool HasRoomAt(Vector3 bodyPosition, float radius)
    {
        Vector3 centerOffset = transform.TransformPoint(capsule.center) - transform.position;
        float half = Mathf.Max(radius, capsule.height * transform.lossyScale.y * 0.5f);
        Vector3 center = bodyPosition + centerOffset;
        Vector3 bottom = center + Vector3.down * (half - radius) + Vector3.up * 0.04f;
        Vector3 top = center + Vector3.up * (half - radius);

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.92f, stepRoomHits, stepMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = stepRoomHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    public void SetMovementSpeedMultiplier(float multiplier)
    {
        movementSpeedMultiplier = Mathf.Max(0.05f, multiplier);
    }

    public void SetJumpForceMultiplier(float multiplier)
    {
        jumpForceMultiplier = Mathf.Max(0.05f, multiplier);
    }

    private void GetMovementBasis(out Vector3 forward, out Vector3 right)
    {
        ResolveOwnedControlReferences();
        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        forward = yawRotation * Vector3.forward;
        right = yawRotation * Vector3.right;
        forward.Normalize();
        right.Normalize();
    }

    private void HandleLook(bool canLook)
    {
        if (!canLook)
            return;

        if (duplicateAetherCameraController != null && duplicateAetherCameraController.enabled)
            duplicateAetherCameraController.enabled = false;

        // Mouse movement is already counted per frame, so don't multiply by frame time again (that made the look shift with frame rate).
        // 조준(ADS) 중에는 설정의 조준 감도를 쓴다.
        if (!aimSourceSearched) { aimSource = GetComponentInChildren<Hankyul_Battle>(true) ?? transform.root.GetComponentInChildren<Hankyul_Battle>(true); aimSourceSearched = true; }
        float ads = aimSource != null && aimSource.IsAiming ? AetherSettings.AdsSensitivityMultiplier : 1f;
        float mouseX = lookInput.x * mouseSensitivity * ads * AetherCameraFeel.MouseScale * (invertLookX ? -1f : 1f);
        float mouseY = lookInput.y * mouseSensitivity * ads * AetherCameraFeel.MouseScale * (invertLookY ? -1f : 1f);

        // While Alt is held, only the camera looks around and the body (aim) stays put.
        if (AetherCameraFeel.TryCaptureFreeLook(this, mouseX, -mouseY))
        {
            mouseX = 0f;
            mouseY = 0f;
        }

        ResolveOwnedControlReferences();
        Transform basis = orientationTransform;
        targetYaw += mouseX;
        targetXRotation = Mathf.Clamp(targetXRotation - mouseY, minLookAngle, Mathf.Min(maxLookAngle, AetherCameraFeel.LookDownLimit));

        if (smoothLook)
        {
            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawSmoothVelocity,
                Mathf.Max(0.001f, lookSmoothTime)
            );

            xRotation = Mathf.SmoothDampAngle(
                xRotation,
                targetXRotation,
                ref pitchSmoothVelocity,
                Mathf.Max(0.001f, lookSmoothTime)
            );
        }
        else
        {
            currentYaw = targetYaw;
            xRotation = targetXRotation;
        }

        // 반동은 여기서 더해야 한다. 카메라를 밖에서 직접 돌리면
        // 이 줄이 다음 프레임에 그대로 덮어써서 아무 일도 안 일어난 것처럼 보인다.
        UpdateRecoil();

        basis.rotation = Quaternion.Euler(0f, currentYaw + recoilYaw, 0f);

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation - recoilPitch, 0f, recoilRoll);
    }

    /// <summary>총을 쏠 때 총구가 튀어 오르는 만큼 시선을 밀어 올린다.</summary>
    /// <param name="pitch">위로 튀는 각도(도).</param>
    /// <param name="yaw">좌우로 밀리는 각도(도). 부호가 방향이다.</param>
    /// <param name="roll">화면이 기우는 각도(도).</param>
    public void AddRecoil(float pitch, float yaw, float roll = 0f)
    {
        recoilPitch += pitch;
        recoilYaw += yaw;
        recoilRoll += roll;
    }

    /// <summary>
    /// 밀린 시선을 제자리로 되돌린다.
    ///
    /// 완전히 되돌려 준다. 조준을 영구히 빼앗으면 쏠수록 하늘을 보게 되고,
    /// 그건 손맛이 아니라 불편함이 된다.
    /// </summary>
    private void UpdateRecoil()
    {
        if (Mathf.Abs(recoilPitch) < 0.001f && Mathf.Abs(recoilYaw) < 0.001f && Mathf.Abs(recoilRoll) < 0.001f)
            return;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, recoilRecoverySpeed) * Time.deltaTime);

        recoilPitch = Mathf.Lerp(recoilPitch, 0f, t);
        recoilYaw = Mathf.Lerp(recoilYaw, 0f, t);
        recoilRoll = Mathf.Lerp(recoilRoll, 0f, t);
    }

    private void TryJump()
    {
        if (!allowInput || !allowMove || rb == null)
            return;

        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f)
            return;

        isGrounded = false;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce * Mathf.Max(0.05f, jumpForceMultiplier), ForceMode.Impulse);

        if (anim != null)
            anim.SetTrigger("JumpStart");
    }

    private void HandleSuperJumpInput(Keyboard keyboard)
    {
        if (keyboard == null)
            return;

        if (keyboard.spaceKey.wasPressedThisFrame)
            BeginSuperJumpCharge();

        if (isChargingSuperJump && keyboard.spaceKey.isPressed)
        {
            superJumpChargeTime = Mathf.Min(superJumpChargeTime + Time.deltaTime, superJumpMaxChargeTime);
            if (superJumpChargeLoop == null && superJumpChargeTime >= SuperJumpTapThreshold)
                superJumpChargeLoop = AetherAudio.StartLoop("superjump_charge", transform, 0.4f, 1f, 10f, false);
            if (superJumpChargeLoop != null && superJumpMaxChargeTime > 0f)
                superJumpChargeLoop.Pitch = Mathf.Lerp(0.8f, 1.8f, superJumpChargeTime / superJumpMaxChargeTime); // 모일수록 높아지는 다리 모터음
        }

        if (isChargingSuperJump && keyboard.spaceKey.wasReleasedThisFrame)
            ReleaseSuperJump();
    }

    private void BeginSuperJumpCharge()
    {
        if (rb == null || !isGrounded || isCrouching || jumpCooldownTimer > 0f)
            return;

        isChargingSuperJump = true;
        superJumpChargeTime = 0f;

        if (superJumpChargeLoop != null) { superJumpChargeLoop.Stop(); superJumpChargeLoop = null; }
        // 충전음은 짧은 누름(일반 점프)을 넘겼을 때부터 낸다(HandleSuperJumpInput).
    }

    private AetherAudioLoop superJumpChargeLoop;

    /// <summary>이보다 짧게 누르면 일반 점프(초). 그 이상부터 충전이 시작된다.</summary>
    private const float SuperJumpTapThreshold = 0.25f;

    private void ReleaseSuperJump()
    {
        if (superJumpChargeLoop != null) { superJumpChargeLoop.Stop(); superJumpChargeLoop = null; }

        if (rb == null)
            return;

        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f)
        {
            isChargingSuperJump = false;
            superJumpChargeTime = 0f;
            return;
        }

        // 짧게 누르면 일반 점프. 파츠를 끼면 톡 눌러도 최소 힘(12)으로 7m 를 뛰어 일반 점프가 사라졌다.
        if (superJumpChargeTime < SuperJumpTapThreshold)
        {
            isChargingSuperJump = false;
            superJumpChargeTime = 0f;
            TryJump();
            return;
        }

        AetherFootsteps.SuppressNextJump();
        AetherAudio.Play2D("superjump_release", Mathf.Lerp(0.6f, 1f, superJumpMaxChargeTime > 0f ? superJumpChargeTime / superJumpMaxChargeTime : 1f), 1f, 0.03f);

        float normalizedCharge = superJumpMaxChargeTime > 0f
            ? Mathf.Clamp01((superJumpChargeTime - SuperJumpTapThreshold) / Mathf.Max(0.01f, superJumpMaxChargeTime - SuperJumpTapThreshold))
            : 1f;

        float configuredMinForce = Mathf.Max(0.1f, superJumpMinForce);
        float configuredMaxForce = Mathf.Max(configuredMinForce, superJumpMaxForce);
        float finalJumpForce = Mathf.Lerp(configuredMinForce, configuredMaxForce, normalizedCharge)
            * Mathf.Max(0.05f, jumpForceMultiplier);

        isChargingSuperJump = false;
        superJumpChargeTime = 0f;
        isGrounded = false;
        pendingSuperJumpLandingAlert = normalizedCharge >= superJumpAlertChargeThreshold;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * finalJumpForce, ForceMode.Impulse);

        if (anim != null)
            anim.SetTrigger("JumpStart");
    }

    private void UpdateGrounded()
    {
        wasGrounded = isGrounded;

        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }
        else if (rb != null)
        {
            isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.05f;
        }

        if (!wasGrounded && isGrounded)
        {
            jumpCooldownTimer = jumpCooldown;

            if (pendingSuperJumpLandingAlert)
            {
                pendingSuperJumpLandingAlert = false;
                if (SimpleAlertLevelManager.Instance != null)
                    SimpleAlertLevelManager.Instance.AddAlert(superJumpLandingAlertAmount);
            }

            if (anim != null)
                anim.SetTrigger("JumpLand");
        }
    }

    private void UpdateAnimator(bool canMove)
    {
        if (anim == null)
            return;

        float inputMagnitude = canMove ? moveInput.magnitude : 0f;
        float horizontalSpeed = lastMoveInputMagnitude;
        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            horizontalSpeed = Mathf.Max(horizontalSpeed, velocity.magnitude);
        }

        float targetParam = 0f;
        if (inputMagnitude > 0.05f && horizontalSpeed > 0.02f)
        {
            if (isCrouching)
                targetParam = crouchAnimatorSpeed;
            else if (isSprinting)
                targetParam = runAnimatorSpeed;
            else
                targetParam = walkAnimatorSpeed;
        }

        anim.SetFloat("Speed", targetParam, Mathf.Max(0.001f, animatorDampTime), Time.deltaTime);
        anim.SetBool("IsCrouching", isCrouching);
        anim.SetBool("IsInAir", !isGrounded);
    }

    private void ToggleCrouch()
    {
        isCrouching = !isCrouching;
        AetherAudio.Play2D("crouch", 0.35f, isCrouching ? 0.95f : 1.05f, 0.05f); // 옷·장비 스치는 소리
        UpdateCrouchCollider();
    }

    private void UpdateCrouchCollider()
    {
        if (capsule == null)
            return;

        if (isCrouching)
        {
            capsule.height = originalHeight * crouchHeightRatio;
            capsule.center = new Vector3(originalCenter.x, originalCenter.y * crouchHeightRatio, originalCenter.z);
        }
        else
        {
            capsule.height = originalHeight;
            capsule.center = originalCenter;
        }
    }

    private void UpdateCameraStance()
    {
        if (!adjustCameraForStance || camTransform == null)
            return;

        Vector3 basePosition = hasCameraDefaultLocalPosition ? cameraDefaultLocalPosition : standingCameraLocalPosition;
        Vector3 target = isCrouching ? crouchingCameraLocalPosition : (isSprinting && moveInput.sqrMagnitude > 0.01f ? sprintingCameraLocalPosition : standingCameraLocalPosition);

        if (target == Vector3.zero)
            target = basePosition;

        target = GetSafeCameraLocalPosition(target, isCrouching);

        camTransform.localPosition = Vector3.Lerp(
            camTransform.localPosition,
            target,
            1f - Mathf.Exp(-cameraStanceLerpSpeed * Time.deltaTime)
        );
    }

    private void ApplyCameraClipSettings()
    {
        if (camTransform == null)
            return;

        Camera cameraComponent = camTransform.GetComponent<Camera>();
        if (cameraComponent == null)
            return;

        cameraComponent.nearClipPlane = Mathf.Min(cameraComponent.nearClipPlane, Mathf.Max(0.005f, viewModelNearClip));

        if (!cameraComponent.CompareTag("MainCamera"))
            cameraComponent.tag = "MainCamera";

        if (hideOwnBodyFromFirstPersonCamera)
        {
            int bodyLayer = LayerMask.NameToLayer(ownBodyLayerName);
            int viewModelLayer = LayerMask.NameToLayer(viewModelLayerName);

            if (bodyLayer >= 0)
                cameraComponent.cullingMask &= ~(1 << bodyLayer);

            if (viewModelLayer >= 0)
                cameraComponent.cullingMask |= 1 << viewModelLayer;
        }
    }

    private void ConfigureFirstPersonSelfVisibility()
    {
        if (!hideOwnBodyFromFirstPersonCamera)
            return;

        int bodyLayer = LayerMask.NameToLayer(ownBodyLayerName);
        int viewModelLayer = LayerMask.NameToLayer(viewModelLayerName);
        if (bodyLayer < 0)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool viewModel = viewModelLayer >= 0 && IsViewModelRenderer(renderer.transform);
            renderer.gameObject.layer = viewModel ? viewModelLayer : bodyLayer;
        }
    }

    private static bool IsViewModelRenderer(Transform rendererTransform)
    {
        string path = rendererTransform != null ? rendererTransform.name : string.Empty;
        Transform cursor = rendererTransform != null ? rendererTransform.parent : null;
        while (cursor != null)
        {
            path = cursor.name + "/" + path;
            cursor = cursor.parent;
        }

        return ContainsIgnoreCase(path, "hand")
            || ContainsIgnoreCase(path, "glove")
            || ContainsIgnoreCase(path, "gun")
            || ContainsIgnoreCase(path, "weapon")
            || ContainsIgnoreCase(path, "lowerarm")
            || ContainsIgnoreCase(path, "upperarm");
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(value)
            && source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Vector3 GetSafeCameraLocalPosition(Vector3 localPosition, bool crouched = false)
    {
        float height = capsule != null ? capsule.height : originalHeight;
        if (height <= 0.01f)
            height = 1.85f;

        float minY = height * (crouched ? Mathf.Clamp(crouchHeightRatio + 0.08f, 0.5f, 0.8f) : minCameraHeightRatio);
        localPosition.y = Mathf.Max(localPosition.y, minY);
        localPosition.z = Mathf.Max(localPosition.z, minCameraForwardOffset);
        return localPosition;
    }

    public void ResetPlayerControlState()
    {
        allowInput = true;
        allowMove = true;
        allowLook = true;
        ClearTransientInputState();
        SuppressInputBriefly();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (playerInput != null && !playerInput.inputIsActive)
            playerInput.ActivateInput();

        LockCursor();
    }

    public void SetSuperJumpEnabled(bool enabled)
    {
        canSuperJump = enabled;
        if (!enabled)
        {
            isChargingSuperJump = false;
            superJumpChargeTime = 0f;
            pendingSuperJumpLandingAlert = false;
            StopSuperJumpChargeSound();
        }
    }

    public void ConfigureSuperJump(float minJumpForce, float maxJumpForce, float maxChargeTime)
    {
        superJumpMinForce = Mathf.Max(0.1f, minJumpForce);
        superJumpMaxForce = Mathf.Max(superJumpMinForce, maxJumpForce);
        superJumpMaxChargeTime = Mathf.Max(0.05f, maxChargeTime);

        if (jumpForce > 0.01f)
            superJumpMaxMultiplier = Mathf.Max(1f, superJumpMaxForce / jumpForce);
    }

    public bool IsSuperJumpEnabled()
    {
        return canSuperJump;
    }

    public float GetSuperJumpCharge01()
    {
        return superJumpMaxChargeTime > 0f ? Mathf.Clamp01(superJumpChargeTime / superJumpMaxChargeTime) : 0f;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncLookStateFromCurrentTransforms();
        ClearTransientInputState();
        SuppressInputBriefly();
    }

    private void SyncLookStateFromCurrentTransforms()
    {
        ResolveOwnedControlReferences();

        currentYaw = orientationTransform.eulerAngles.y;
        targetYaw = currentYaw;
        yawSmoothVelocity = 0f;

        if (camTransform != null)
        {
            xRotation = Mathf.Clamp(NormalizeAngle(camTransform.localEulerAngles.x), minLookAngle, Mathf.Min(maxLookAngle, AetherCameraFeel.LookDownLimit));
            targetXRotation = xRotation;
            pitchSmoothVelocity = 0f;
        }
    }

    private void SuppressInputBriefly()
    {
        inputResumeTime = Time.unscaledTime + Mathf.Max(0f, sceneLoadInputGraceTime);
        ClearTransientInputState();
    }

    private void ClearTransientInputState()
    {
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        isSprinting = false;
        isChargingSuperJump = false;
        superJumpChargeTime = 0f;
        // 충전 중에 일시정지·채팅·쓰러짐으로 입력이 끊기면 충전음이 계속 남았다.
        StopSuperJumpChargeSound();
    }

    private void StopSuperJumpChargeSound()
    {
        if (superJumpChargeLoop != null) superJumpChargeLoop.Stop();
        superJumpChargeLoop = null;
    }

    private void DisableDuplicateLookControllers()
    {
        duplicateAetherCameraController = GetComponent<AetherCameraController>();
        if (duplicateAetherCameraController == null)
            duplicateAetherCameraController = GetComponentInChildren<AetherCameraController>(true);

        if (duplicateAetherCameraController != null)
            duplicateAetherCameraController.enabled = false;

        // PlayerMouseLook이 같이 붙어있으면 이중 회전 처리 방지
        PlayerMouseLook mouseLook = GetComponent<PlayerMouseLook>();
        if (mouseLook == null)
            mouseLook = GetComponentInChildren<PlayerMouseLook>(true);
        if (mouseLook != null)
            mouseLook.enabled = false;
    }

    private void ResolveOwnedControlReferences()
    {
        // Movement must always use Hankyul's own look rig. Scene overrides can point
        // to another prefab, which makes WASD move in the wrong direction.
        if (!IsOwnedTransform(orientationTransform))
            orientationTransform = transform;

        if (!IsOwnedTransform(camTransform))
        {
            Transform ownedCamera = FindOwnedCameraTransform();
            if (ownedCamera != null)
                camTransform = ownedCamera;
        }
    }

    private bool IsOwnedTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        if (candidate == transform || candidate.IsChildOf(transform))
            return true;

        Move_Hankyul owner = candidate.GetComponentInParent<Move_Hankyul>();
        return owner == this;
    }

    private Transform FindOwnedCameraTransform()
    {
        Camera[] ownedCameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < ownedCameras.Length; i++)
        {
            if (ownedCameras[i] != null && ownedCameras[i].CompareTag("MainCamera"))
                return ownedCameras[i].transform;
        }

        if (ownedCameras.Length > 0 && ownedCameras[0] != null)
            return ownedCameras[0].transform;

        return null;
    }

    public void SnapLookToWorldRotation(Quaternion worldRotation, float pitch = 0f)
    {
        ResolveOwnedControlReferences();

        lookInput = Vector2.zero;
        yawSmoothVelocity = 0f;
        pitchSmoothVelocity = 0f;
        currentYaw = worldRotation.eulerAngles.y;
        targetYaw = currentYaw;
        xRotation = Mathf.Clamp(NormalizeAngle(pitch), minLookAngle, Mathf.Min(maxLookAngle, AetherCameraFeel.LookDownLimit));
        targetXRotation = xRotation;
        orientationTransform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public void OnStep_L()
    {
    }

    public void OnStep_R()
    {
    }

    public void Jump()
    {
    }

    public void Land()
    {
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
