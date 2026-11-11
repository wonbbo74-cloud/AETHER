using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Yunseo_Move : MonoBehaviour
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

    [Header("점프")]
    public float jumpForce = 5f;             // 점프 힘
    public Transform groundCheck;            // 발 위치 정도의 빈 오브젝트
    public float groundCheckRadius = 0.2f;   // 지면 체크 반경
    public LayerMask groundMask;             // Ground 레이어 마스크
    public float jumpCooldown = 0.5f;        // 착지 후 다음 점프까지 딜레이(초)

    [Header("입력 허용")]
    public bool allowMove = true;            // WASD 이동 허용 여부
    public bool allowLook = true;            // 마우스 회전/시야 허용 여부

    [Header("애니메이션 연동 SFX 키")]
    public string step_LSfxKey = "step_L";       // 걷기/뛰기/웅크리기 발소리
    public string step_RSfxKey = "step_R";
    public string jumpSfxKey = "jump";       // 점프
    public string landSfxKey = "land";       // 착지
    #endregion

    #region private 변수들
    private float xRotation = 0f;            // 카메라 상하 누적 회전값
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
            rb.constraints = RigidbodyConstraints.FreezeRotation;

        originalHeight = capsule != null ? capsule.height : 2f;
        originalCenter = capsule != null ? capsule.center : Vector3.up * 1f;

        if (orientationTransform == null)
            orientationTransform = transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
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
                UpdateCrouchCollider();

                if (isCrouching)
                {
                    sprintHoldTime = 0f;
                    isBoosting = false;
                }
            }

            // 점프
            if (kb.spaceKey.wasPressedThisFrame)
            {
                TryJump();
            }
        }

        UpdateSprintBoostState();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
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
            Vector3 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(nextPos);
        }
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        if (orientationTransform != null)
            orientationTransform.Rotate(Vector3.up * mouseX);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -60f, 60f);

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
            isGrounded = Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
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

    // ---- 애니메이션 이벤트용 SFX 메서드 ----
    // 걷기/뛰기/웅크린 이동 애니메이션에서 발이 바닥에 닿는 프레임에 호출
    public void OnStep_L()
    {
        if (!allowMove) return;
        if (!isGrounded) return;
        if (moveInput.magnitude < 0.1f) return;

        if (!string.IsNullOrEmpty(step_LSfxKey))
        {
            GlobalManager.Audio.PlaySFX(step_LSfxKey);
        }
    }

    public void OnStep_R()
    {
        if (!allowMove) return;
        if (!isGrounded) return;
        if (moveInput.magnitude < 0.1f) return;

        if (!string.IsNullOrEmpty(step_RSfxKey))
        {
            GlobalManager.Audio.PlaySFX(step_RSfxKey);
        }
    }

    public void Jump()
    {
        if (!string.IsNullOrEmpty(jumpSfxKey))
        {
            GlobalManager.Audio.PlaySFX(jumpSfxKey);
        }
    }

    public void Land()
    {
        if (!string.IsNullOrEmpty(landSfxKey))
        {
            GlobalManager.Audio.PlaySFX(landSfxKey);
        }
    }


    // ---- 선택: 강제 스프린트 기능(튜토리얼과 무관하게 일반 기능으로 사용 가능) ----
    public void ForceSprint(float seconds)
    {
        if (forceSprintRoutine != null)
            StopCoroutine(forceSprintRoutine);

        forceSprintRoutine = StartCoroutine(ForceSprintCoroutine(seconds));
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
