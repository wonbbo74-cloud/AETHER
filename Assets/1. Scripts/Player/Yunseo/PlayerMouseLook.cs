using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 안전한 마우스 룩 스크립트
/// - 플레이어 몸체는 Y축 회전만 수행
/// - 카메라 피벗은 X축(피치)만 로컬 회전(Quaternion.Euler)으로 수행
/// - 수직 회전은 float 변수(xRotation)에 보관하고 -80..80로 클램프
/// - 마우스 감도와 deltaTime을 사용, 입력 방식에 따라 적절히 처리
/// - null 체크 및 안전 초기화 포함
/// </summary>
public class PlayerMouseLook : MonoBehaviour
{
    [Header("References (필수)")]
    [Tooltip("Y축 회전을 적용할 Transform (보통 플레이어 루트/몸체). 비어있으면 이 오브젝트 사용")]
    public Transform playerBody;

    [Tooltip("X축 회전을 적용할 카메라 피벗 (카메라의 부모 또는 카메라 자체). 비어있으면 자식 Camera를 찾아 자동 설정")]
    public Transform cameraPivot;

    [Header("Settings")]
    [Tooltip("마우스 감도")]
    public float mouseSensitivity = 200f;
    public bool invertLookY = false;
    public bool invertLookX = false;

    [Tooltip("수직 회전 최소 각도")]
    public float minVerticalAngle = -80f;

    [Tooltip("수직 회전 최대 각도")]
    public float maxVerticalAngle = 80f;

    [Tooltip("시작 시 커서 잠금/숨김")]
    public bool lockCursorOnStart = true;

    // 내부 상태
    private float xRotation = 0f; // 수직(피치) 각도 저장 (안정적으로 누적)
    private bool initialized = false;

    private void Awake()
    {
        SafeInit();
    }

    private void OnEnable()
    {
        SafeInit();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SafeInit()
    {
        if (initialized) return;

        if (playerBody == null)
            playerBody = transform; // 기본: 이 오브젝트를 몸체로 사용

        if (cameraPivot == null)
        {
            // 자식에 Camera가 있으면 그 부모(피벗)를 cameraPivot으로 사용
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                // 카메라의 부모가 있으면 피벗으로, 없으면 카메라 자체를 피벗으로 사용
                cameraPivot = cam.transform.parent != null ? cam.transform.parent : cam.transform;
            }
        }

        // 최종 null 체크
        if (cameraPivot == null)
            Debug.LogWarning("[PlayerMouseLook] cameraPivot이 설정되지 않았습니다. 카메라 피벗을 수동으로 할당하세요.", this);

        initialized = true;
    }

    private void Update()
    {
        // 안전성: 참조가 없으면 동작하지 않음
        if (playerBody == null || cameraPivot == null)
            return;

        // 입력 읽기 (새 InputSystem 우선, 없으면 레거시 Input 사용)
        Vector2 rawDelta = Vector2.zero;
        bool usedNewInputMouse = false;

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            rawDelta = mouse.delta.ReadValue();
            // 아주 작은 노이즈 무시
            if (rawDelta.sqrMagnitude > 0.000001f)
                usedNewInputMouse = true;
        }
#endif

        if (!usedNewInputMouse)
        {
            // 레거시 입력(또는 게임패드 스틱이 바인딩된 경우)
            // Input.GetAxis 계열은 프레임 보정 필요 -> Time.deltaTime 사용
            rawDelta.x = Input.GetAxis("Mouse X");
            rawDelta.y = Input.GetAxis("Mouse Y");
        }

        // 장치 유형에 따라 프레임 스케일 결정
        // - 새 InputSystem의 Mouse.delta: 프레임 델타값(픽셀 단위) -> Time.deltaTime 곱하면 과소/튀는 현상 발생하므로 곱하지 않음
        // - 레거시 GetAxis: 프레임 독립적으로 Time.deltaTime을 곱해줌
        float frameScale = usedNewInputMouse ? 1f : Time.deltaTime;

        float mouseX = rawDelta.x * mouseSensitivity * frameScale * (invertLookX ? -1f : 1f);
        float mouseY = rawDelta.y * mouseSensitivity * frameScale * (invertLookY ? -1f : 1f);

        // Y축(수평) 회전: 플레이어 몸체에 적용 (transform.Rotate 사용)
        // Space.Self로 로컬 Y 회전(보통 상향 y) 적용
        playerBody.Rotate(Vector3.up * mouseX, Space.Self);

        // X축(수직) 회전: xRotation 누적 -> Clamp -> cameraPivot.localRotation = Quaternion.Euler(...)
        xRotation -= mouseY; // 마우스 위로 올리면 시선이 내려감(일반적)
        xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    /// <summary>
    /// 외부에서 커서 잠금 상태를 설정할 수 있음
    /// </summary>
    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}