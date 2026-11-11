using UnityEngine;
using UnityEngine.InputSystem;

public class Hankyul_Battle : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Hankyul_Move move;
    [SerializeField] private Animator anim;

    [Header("Weapon")]
    [SerializeField] private GameObject rifleObject;
    [SerializeField] private Hankyul_Fire gunShoot;

    [Header("Aim Zoom")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float aimFieldOfView = 32f;
    [SerializeField] private float aimZoomSpeed = 18f;
    [SerializeField] private bool allowRightMouseHoldAim = true;

    [Header("Animation Event Fallback")]
    [SerializeField] private float pendingTimeout = 0.2f;

    private bool isBattle;
    private bool isAimHeld;

    /// <summary>오른쪽 버튼으로 조준(ADS) 중인지. 조준 감도(설정)를 적용할 때 쓴다.</summary>
    public bool IsAiming => isAimHeld && isActiveAndEnabled;
    private bool pendingShoot;
    private float pendingShootExpireTime;
    private float defaultFieldOfView;
    private bool hasDefaultFieldOfView;

    private static readonly int HashIsBattle = Animator.StringToHash("IsBattle");
    private static readonly int HashIsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int HashFire = Animator.StringToHash("Fire");
    private static readonly int HashCrouchFire = Animator.StringToHash("CrouchFire");

    private void Awake()
    {
        if (move == null) move = GetComponent<Hankyul_Move>();
        if (anim == null) anim = GetComponent<Animator>();

        if (gunShoot == null)
        {
            if (rifleObject != null) gunShoot = rifleObject.GetComponentInChildren<Hankyul_Fire>(true);
            if (gunShoot == null) gunShoot = GetComponentInChildren<Hankyul_Fire>(true);
        }

        if (gunShoot != null)
            gunShoot.SetOwnerRoot(transform);

        EnsureAimCamera();

        SetRifleVisible(false);
        RefreshCombatPresentation();

        if (anim != null)
            anim.SetBool(HashIsBattle, false);
    }

    private void Update()
    {
        HandleBattleToggle();
        HandleAimInput();
        ApplyAimZoom();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && gunShoot != null)
            gunShoot.TryReload();

        if (!IsCombatActive()) return;

        if (pendingShoot && Time.time > pendingShootExpireTime)
            ShootPendingRound();

        HandleFireInput();
    }

    private void HandleBattleToggle()
    {
        if (Keyboard.current == null) return;

        if (!Keyboard.current.digit1Key.wasPressedThisFrame) return;

        isBattle = !isBattle;
        AetherAudio.Play2D("weapon_draw", isBattle ? 0.55f : 0.4f, isBattle ? 1f : 0.92f, 0.03f); // 총을 들고/내리는 철컥

        RefreshCombatPresentation();
    }

    private void HandleAimInput()
    {
        if (!allowRightMouseHoldAim || Mouse.current == null)
        {
            if (isAimHeld)
            {
                isAimHeld = false;
                RefreshCombatPresentation();
            }

            return;
        }

        bool newAimHeld = Mouse.current.rightButton.isPressed;
        if (newAimHeld == isAimHeld) return;

        isAimHeld = newAimHeld;
        RefreshCombatPresentation();
    }

    private void HandleFireInput()
    {
        if (Mouse.current == null || anim == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        // 엘리베이터 층 목록·상점처럼 커서를 풀고 고르는 화면에서 클릭하면 총까지 나갔다.
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (pendingShoot) return;

        pendingShoot = true;
        pendingShootExpireTime = Time.time + Mathf.Max(0.05f, pendingTimeout);

        bool isCrouching = anim.GetBool(HashIsCrouching);
        anim.SetTrigger(isCrouching ? HashCrouchFire : HashFire);
    }

    public void AnimEvent_Shoot()
    {
        if (!IsCombatActive())
        {
            pendingShoot = false;
            return;
        }

        if (!pendingShoot) return;

        ShootPendingRound();
    }

    private void ShootPendingRound()
    {
        pendingShoot = false;

        if (gunShoot != null)
            gunShoot.ShootOnce();
    }

    private void SetRifleVisible(bool visible)
    {
        if (rifleObject != null)
            rifleObject.SetActive(visible);
    }

    private bool IsCombatActive()
    {
        return isBattle || isAimHeld;
    }

    private void RefreshCombatPresentation()
    {
        bool active = IsCombatActive();

        if (anim != null)
            anim.SetBool(HashIsBattle, active);

        if (gunShoot != null)
            gunShoot.SetBattleMode(active);

        SetRifleVisible(active);

        if (!active)
            pendingShoot = false;
    }

    private void EnsureAimCamera()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main;

        if (aimCamera != null && !hasDefaultFieldOfView)
        {
            defaultFieldOfView = aimCamera.fieldOfView;
            hasDefaultFieldOfView = true;
        }
    }

    private void ApplyAimZoom()
    {
        EnsureAimCamera();
        if (aimCamera == null || !hasDefaultFieldOfView) return;

        float targetFov = isAimHeld ? aimFieldOfView : defaultFieldOfView;
        aimCamera.fieldOfView = Mathf.Lerp(
            aimCamera.fieldOfView,
            targetFov,
            1f - Mathf.Exp(-aimZoomSpeed * Time.deltaTime)
        );
    }

    private void OnDisable()
    {
        if (aimCamera != null && hasDefaultFieldOfView)
            aimCamera.fieldOfView = defaultFieldOfView;
    }

    public bool IsBattle()
    {
        return IsCombatActive();
    }
}
