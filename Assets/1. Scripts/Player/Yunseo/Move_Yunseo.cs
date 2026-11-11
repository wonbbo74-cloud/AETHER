using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Move_Yunseo : MonoBehaviour
{
    #region public 변수들
    [Header("이동 속도")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float boostSpeed = 9f;
    public float boostDelay = 1.0f;
    public float crouchSpeed = 1.5f;

    [Header("웅크리기 콜라이더 설정")]
    [Range(0.3f, 0.8f)]
    public float crouchHeightRatio = 0.6f;

    [Header("이동 기준(보통 Player 루트)")]
    public Transform orientationTransform;   // 마우스로 회전시키는 기준 Transform

    [Header("카메라")]
    public Transform camTransform;           // 실제 카메라 Transform (머리 위치 자식 오브젝트)
    public float mouseSensitivity = 200f;    // 마우스 감도
    [HideInInspector] public bool invertLookY;   // 설정의 상하·좌우 반전(AetherSettings)
    [HideInInspector] public bool invertLookX;

    [Header("Fall Safety")]
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("점프")]
    public float jumpForce = 5f;             // 점프 힘
    public Transform groundCheck;            // 발 위치 정도의 빈 오브젝트
    public float groundCheckRadius = 0.2f;   // 지면 체크 반경
    public LayerMask groundMask;             // Ground 레이어 마스크
    public float jumpCooldown = 0.5f;        // 착지 후 다음 점프까지 딜레이(초)

    [Header("입력 허용")]
    public bool allowMove = true;            // WASD 이동 허용 여부
    public bool allowLook = true;            // 마우스 회전/시야 허용 여부
    [SerializeField] private bool useDirectInputFallback = true;

    [Header("애니메이션 연동 SFX 키")]
    public string step_LSfxKey = "step_L";       // 걷기/뛰기/웅크리기 발소리
    public string step_RSfxKey = "step_R";
    public string jumpSfxKey = "jump";       // 점프
    public string landSfxKey = "land";       // 착지
    #endregion

    #region private 변수들
    private float xRotation = 0f;            // 카메라 상하 누적 회전값
    private YunseoPartAbilityController partAbility;
    private Vector2 lookInput;               // 마우스 입력 저장

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator anim;

    private bool isBoosting = false;
    private float sprintHoldTime = 0f;

    private Vector2 moveInput;       // WASD 입력 (x: 좌우, y: 전후)
    private bool isSprinting = false;
    private bool isCrouching = false;

    private float originalHeight;
    private Vector3 originalCenter;

    private bool isGrounded = true;
    private bool wasGrounded = true;
    private float jumpCooldownTimer = 0f;    // 남은 쿨타임

    // 강제 스프린트 관련 (튜토리얼 제거 후에도 일반 기능으로 유지 가능)
    private bool overrideSprint = false;
    private Coroutine forceSprintRoutine = null;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        anim = GetComponent<Animator>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        originalHeight = capsule != null ? capsule.height : 2f;
        originalCenter = capsule != null ? capsule.center : Vector3.up * 1f;

        if (orientationTransform == null)
            orientationTransform = transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 중복 시야 처리 스크립트 비활성화
        PlayerMouseLook mouseLook = GetComponent<PlayerMouseLook>();
        if (mouseLook != null) mouseLook.enabled = false;

        // Yunseo_Move가 같이 붙어있으면 중복 입력 방지
        Yunseo_Move duplicate = GetComponent<Yunseo_Move>();
        if (duplicate != null) duplicate.enabled = false;
    }

    private void Update()
    {
        ReadDirectInputFallback();

        // 시야 처리
        if (allowLook)
        {
            HandleLook();
        }
        else
        {
            lookInput = Vector2.zero;
        }

        UpdateGrounded();

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // 이동 잠금 상태면 이동 관련 입력 처리 X
        if (!allowMove)
            return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            // 스프린트
            if (!overrideSprint)
                isSprinting = kb.leftShiftKey.isPressed;

            // 웅크리기 토글
            if (kb.leftCtrlKey.wasPressedThisFrame)
            {
                isCrouching = !isCrouching;
                AetherAudio.Play2D("crouch", 0.35f, isCrouching ? 0.95f : 1.05f, 0.05f);
                UpdateCrouchCollider();

                if (isCrouching)
                {
                    sprintHoldTime = 0f;
                    isBoosting = false;
                }
            }

            // 점프. 슈퍼점프 파츠가 있으면 스페이스를 모았다가 놓을 때 높이 뛴다.
            // (YunseoPartAbilityController 에 기능은 있었지만 아무도 불러 주지 않아 윤서는 슈퍼점프를 못 했다.)
            if (partAbility == null)
                partAbility = GetComponent<YunseoPartAbilityController>();

            bool superJumpHandled = partAbility != null && partAbility.isActiveAndEnabled && partAbility.HandleJumpInput(kb);
            if (!superJumpHandled && kb.spaceKey.wasPressedThisFrame)
            {
                TryJump();
            }
        }

        UpdateSprintBoostState();
        UpdateAnimator();
    }

    private void ReadDirectInputFallback()
    {
        if (!useDirectInputFallback)
            return;

        var kb = Keyboard.current;
        if (allowMove && kb != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
                horizontal -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
                horizontal += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
                vertical -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
                vertical += 1f;

            Vector2 directMove = new Vector2(horizontal, vertical);
            moveInput = directMove.sqrMagnitude > 1f ? directMove.normalized : directMove;
        }

        var mouse = Mouse.current;
        if (allowLook && mouse != null)
            lookInput = AetherLookGuard.ShouldIgnoreLook() ? Vector2.zero : mouse.delta.ReadValue();
    }

    private void FixedUpdate()
    {
        // While an elevator or cutscene holds the body kinematic, touching velocity only spams warnings.
        if (rb != null && rb.isKinematic)
            return;

        if (allowMove)
        {
            HandleMovement();
        }
        else
        {
            // 이동 잠금 시 수평속도 0으로 고정 (퍼즐 중 등)
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity;
                v.x = 0f;
                v.z = 0f;
                rb.linearVelocity = v;
            }
        }

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

    // 새 Input System: PlayerInput의 Move 액션에 연결
    private void OnMove(InputValue value)
    {
        if (!allowMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = value.Get<Vector2>();
    }

    // Look 액션
    private void OnLook(InputValue value)
    {
        if (!allowLook)
        {
            lookInput = Vector2.zero;
            return;
        }

        lookInput = value.Get<Vector2>();
    }

    private void HandleMovement()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        float currentSpeed;

        if (isCrouching)
            currentSpeed = crouchSpeed;
        else
        {
            if (isBoosting)
                currentSpeed = boostSpeed;
            else if (isSprinting)
                currentSpeed = runSpeed;
            else
                currentSpeed = walkSpeed;
        }

        Transform basis = orientationTransform;
        Vector3 moveDir = basis.forward * inputDir.z + basis.right * inputDir.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (rb != null)
        {
            Vector3 delta = moveDir * currentSpeed * Time.fixedDeltaTime;
            // Check the path first. Running at the 9 m/s boost went straight through thin doors and gates.
            delta = AetherMoveSweep.ClampHorizontal(rb, capsule, delta, 0.4f);
            Vector3 next = rb.position + delta;
            // 낮은 턱·잔해 모서리를 걸어서 넘는다(Meka 와 같은 보조). 없을 땐 몇 cm 턱에도 걸렸다.
            if (isGrounded && !isCrouching && AetherStepAssist.TryStep(rb, capsule, moveDir, next, out Vector3 stepped))
                next = stepped;
            rb.MovePosition(next);
        }
    }

    // Look state. Tracks the body yaw so turns made by other code (elevator, respawn) are picked up.
    private float targetYaw;
    private float currentYaw;
    private float targetPitch;
    private float lastAppliedYaw = float.NaN;

    private void HandleLook()
    {
        // Mouse movement is already counted per frame, so don't multiply by frame time again (that made the look shift with frame rate).
        float mouseX = lookInput.x * mouseSensitivity * AetherCameraFeel.MouseScale * (invertLookX ? -1f : 1f);
        float mouseY = lookInput.y * mouseSensitivity * AetherCameraFeel.MouseScale * (invertLookY ? -1f : 1f);

        // While Alt is held, only the camera looks around and the body stays put.
        if (AetherCameraFeel.TryCaptureFreeLook(this, mouseX, -mouseY))
        {
            mouseX = 0f;
            mouseY = 0f;
        }

        Transform orientation = orientationTransform != null ? orientationTransform : transform;

        // If other code (elevator, respawn, cutscene) turned the body, start over from that direction.
        float actualYaw = orientation.eulerAngles.y;
        if (float.IsNaN(lastAppliedYaw) || Mathf.Abs(Mathf.DeltaAngle(actualYaw, lastAppliedYaw)) > 0.01f)
        {
            currentYaw = targetYaw = actualYaw;
            targetPitch = xRotation;
        }

        targetYaw += mouseX;
        targetPitch = Mathf.Clamp(targetPitch - mouseY, AetherCameraFeel.LookUpLimit, AetherCameraFeel.LookDownLimit);

        // Follow the mouse 1:1 instantly, as in competitive FPS games. Smoothing only showed up as lag.
        currentYaw = targetYaw;
        xRotation = targetPitch;

        orientation.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        lastAppliedYaw = orientation.eulerAngles.y;

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void UpdateSprintBoostState()
    {
        bool isMoving = moveInput.magnitude > 0.1f;

        if (isSprinting && isMoving && !isCrouching)
        {
            sprintHoldTime += Time.deltaTime;

            if (!isBoosting && sprintHoldTime >= boostDelay)
                isBoosting = true;
        }
        else
        {
            sprintHoldTime = 0f;
            isBoosting = false;
        }
    }

    // 점프 시도
    public void TryJump()
    {
        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f) return;

        isGrounded = false;

        // 수직 속도 초기화 및 점프
        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (anim != null)
            anim.SetTrigger("JumpStart");
    }

    private void UpdateGrounded()
    {
        wasGrounded = isGrounded;

        if (groundCheck != null)
        {
            isGrounded = OverlapsGround();
        }
        else
        {
            if (rb != null)
                isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.01f;
        }

        // 착지 순간
        if (!wasGrounded && isGrounded)
        {
            jumpCooldownTimer = jumpCooldown;

            if (anim != null)
                anim.SetTrigger("JumpLand");
        }
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;

        float inputMagnitude = moveInput.magnitude;
        float speedParam = 0f;

        if (isCrouching)
            speedParam = inputMagnitude * 0.5f;
        else
        {
            if (isBoosting)
                speedParam = 1.0f;
            else if (isSprinting)
                speedParam = inputMagnitude * 0.6f;
            else
                speedParam = inputMagnitude * 0.3f;
        }

        anim.SetFloat("Speed", speedParam);
        anim.SetBool("IsCrouching", isCrouching);
        anim.SetBool("IsBoosting", isBoosting);
        anim.SetBool("IsInAir", !isGrounded);
    }

    private void UpdateCrouchCollider()
    {
        if (capsule == null) return;

        if (isCrouching)
        {
            capsule.height = originalHeight * crouchHeightRatio;
            capsule.center = new Vector3(
                originalCenter.x,
                originalCenter.y * crouchHeightRatio,
                originalCenter.z
            );
        }
        else
        {
            capsule.height = originalHeight;
            capsule.center = originalCenter;
        }
    }

    // ---- 애니메이션 이벤트 받는 곳 ----
    // 발걸음·점프·착지 소리는 AetherFootsteps(SceneFlow/AetherAudio.cs)가 걸은 거리와 낙하 속도로 낸다.
    // 이동 블렌드에서 두 동작의 이벤트가 겹쳐 한 걸음에 두 번씩 울려서, 여기서는 받기만 한다.
    public void OnStep_L() { }
    public void OnStep_R() { }
    public void Jump() { }
    public void Land() { }


    // ---- 선택: 강제 스프린트 기능(튜토리얼과 무관하게 일반 기능으로 사용 가능) ----
    public void ForceSprint(float seconds)
    {
        if (forceSprintRoutine != null)
            StopCoroutine(forceSprintRoutine);

        forceSprintRoutine = StartCoroutine(ForceSprintCoroutine(seconds));
    }

    /// <summary>지금 땅에 서 있는지. 슈퍼점프(YunseoPartAbilityController)가 같은 판정을 쓴다.</summary>
    public bool IsGroundedNow => isGrounded;

    private readonly Collider[] groundHits = new Collider[8];

    /// <summary>
    /// 발밑에 땅이 있는지. 씬의 윤서는 groundMask 가 0(아무 레이어도 안 봄)이라 늘 공중으로
    /// 판정돼 점프를 못 했다. 비어 있으면 모든 레이어를 보되 자기 몸은 뺀다.
    /// </summary>
    private bool OverlapsGround()
    {
        int mask = groundMask.value != 0 ? groundMask.value : ~0;
        int count = Physics.OverlapSphereNonAlloc(groundCheck.position, groundCheckRadius, groundHits, mask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    public void SetMoveEnabled(bool enabled)
    {
        allowMove = enabled;
        if (!enabled)
            moveInput = Vector2.zero;
    }

    public void SetLookEnabled(bool enabled)
    {
        allowLook = enabled;
        if (!enabled)
            lookInput = Vector2.zero;
    }

    /// <summary>
    /// 몸과 시선을 이 방향으로 바로 돌린다. Move_Hankyul 과 같은 이름·동작이다.
    /// 엘리베이터·씬 이동·부활처럼 플레이어를 옮기는 코드가 두 캐릭터를 똑같이 다룰 수 있게 한다.
    /// </summary>
    public void SnapLookToWorldRotation(Quaternion worldRotation, float pitch = 0f)
    {
        lookInput = Vector2.zero;

        Transform orientation = orientationTransform != null ? orientationTransform : transform;
        orientation.rotation = Quaternion.Euler(0f, worldRotation.eulerAngles.y, 0f);

        float normalized = Mathf.Repeat(pitch + 180f, 360f) - 180f;
        xRotation = Mathf.Clamp(normalized, AetherCameraFeel.LookUpLimit, AetherCameraFeel.LookDownLimit);
        targetPitch = xRotation;
        currentYaw = targetYaw = orientation.eulerAngles.y;
        lastAppliedYaw = currentYaw;

        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    /// <summary>조작을 모두 풀고 남아 있던 입력·속도를 지운다. Move_Hankyul 과 같은 이름·동작이다.</summary>
    public void ResetPlayerControlState()
    {
        allowMove = true;
        allowLook = true;
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetLegPartEffect(EquipmentItemData legItem)
    {
        if (legItem == null)
            return;

        jumpForce = (jumpForce + legItem.jumpForceAdd) * Mathf.Max(0.1f, legItem.jumpForceMultiplier);
    }

    public void ClearLegPartEffect()
    {
    }

    private IEnumerator ForceSprintCoroutine(float seconds)
    {
        overrideSprint = true;
        bool prevSprint = isSprinting;
        isSprinting = true;
        sprintHoldTime = boostDelay;

        yield return new WaitForSeconds(seconds);

        isSprinting = prevSprint;
        overrideSprint = false;
        sprintHoldTime = 0f;
        forceSprintRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
