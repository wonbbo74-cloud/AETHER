using UnityEngine;
using UnityEngine.InputSystem;

public class Hankyul_Move : MonoBehaviour
{
    #region public 변수들
    [Header("이동 속도")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
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
    public bool allowMove = true;   // WASD 이동 허용 여부
    public bool allowLook = true;   // 마우스 회전/시야 허용 여부
    #endregion

    #region private 변수들
    private float xRotation = 0f;            // 카메라 상하 누적 회전값
    private Vector2 lookInput;               // 마우스 입력 저장

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Animator anim;

    private Vector2 moveInput;               // WASD 입력 (x: 좌우, y: 전후)
    private bool isSprinting = false;
    private bool isCrouching = false;

    private float originalHeight;
    private Vector3 originalCenter;

    private bool isGrounded = true;
    private bool wasGrounded = true;
    private float jumpCooldownTimer = 0f;    // 남은 쿨타임

    public bool allowInput = true;
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        anim = GetComponent<Animator>();

        // 회전 고정 (회전은 마우스 로직에서만 처리)
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        originalHeight = capsule.height;
        originalCenter = capsule.center;

        // orientationTransform이 비어 있으면 자기 자신을 기준으로 사용
        if (orientationTransform == null)
            orientationTransform = transform;

        // 마우스 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        UpdateGrounded();

        // 점프 쿨타임 감소
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        if (Keyboard.current != null)
        {
            // Shift: 달리기
            isSprinting = Keyboard.current.leftShiftKey.isPressed;

            // LeftCtrl: 웅크리기 토글
            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                isCrouching = !isCrouching;
                UpdateCrouchCollider();
            }

            // Space: 점프
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryJump();
            }

            UpdateAnimator();
        }
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    // 새 Input System: PlayerInput 컴포넌트의 "Move" 액션에 연결
    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();   // x: A/D, y: W/S
    }

    // 새 Input System: PlayerInput 컴포넌트의 "Look" 액션에 연결
    private void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();   // x: Mouse X, y: Mouse Y
    }

    // 이동 처리
    private void HandleMovement()
    {
        // WASD 입력 (정규화해서 8방향 이동)
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        // 현재 속도 결정
        float currentSpeed;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else
        {
            currentSpeed = isSprinting ? runSpeed : walkSpeed;
        }

        // 마우스로 회전된 기준(orientationTransform)을 기준으로 이동 방향 계산
        Transform basis = orientationTransform;
        Vector3 moveDir =
            basis.forward * inputDir.z +
            basis.right * inputDir.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        Vector3 nextPos = rb.position + moveDir * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);
    }

    // 카메라 회전 처리
    private void HandleLook()
    {

        // 튜토리얼
        if (!allowInput) return; // 튜토리얼 중이면 입력 무시

        // lookInput은 Mouse Delta에 매핑되어 있다고 가정
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // 좌우 회전: 플레이어(orientationTransform)를 기준으로 회전
        orientationTransform.Rotate(Vector3.up * mouseX);

        // 상하 회전: 카메라 pitch
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -60f, 60f);

        camTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // 점프 시도
    private void TryJump()
    {
        // 공중이거나 웅크린 상태이거나 쿨타임 중이면 점프하지 않음
        if (!isGrounded || isCrouching || jumpCooldownTimer > 0f) return;

        isGrounded = false;

        // 기존 수직 속도 제거 후 점프 힘 적용
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        // 점프 시작 애니메이션 트리거
        if (anim != null)
        {
            anim.SetTrigger("JumpStart");   // jumpstart 클립용 트리거
        }
    }

    // 지상 체크
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
            // groundCheck가 없으면 단순히 y 속도로 대체 (임시용)
            isGrounded = Mathf.Abs(rb.linearVelocity.y) < 0.01f;
        }

        // 공중 → 지상으로 전환되는 순간
        if (!wasGrounded && isGrounded)
        {
            // 착지 후 점프 쿨타임 시작
            jumpCooldownTimer = jumpCooldown;

            if (anim != null)
            {
                anim.SetTrigger("JumpLand");   // jumpland 클립용 트리거
            }
        }
    }

    // 애니메이터 파라미터 갱신
    private void UpdateAnimator()
    {
        if (anim == null) return;

        float inputMagnitude = moveInput.magnitude; // 0 ~ 1
        float speedParam = 0f;

        if (isCrouching)
        {
            // 웅크린 상태: Crouching Idle / Crouched Walking (0 ~ 0.5 정도)
            speedParam = inputMagnitude * 0.5f;
        }
        else
        {
            if (isSprinting)
            {
                // 일반 달리기: Running (0.6 ~ 0.9 정도)
                speedParam = inputMagnitude * 0.6f;
            }
            else
            {
                // 걷기: Walking (0.3 ~ 0.5 정도)
                speedParam = inputMagnitude * 0.3f;
            }
        }

        anim.SetFloat("Speed", speedParam);       // 이동용 Blend Tree 파라미터
        anim.SetBool("IsCrouching", isCrouching);
        anim.SetBool("IsInAir", !isGrounded);
    }

    // 웅크리기 시 캡슐 콜라이더 조정
    private void UpdateCrouchCollider()
    {
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

    // 디버그용: 에디터에서 지면 체크 영역 표시
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
