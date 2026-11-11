using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Move_Yunseo))]
public class YunseoPartAbilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private ShieldController shieldPrefab;
    [SerializeField] private LayerMask breakableMask = ~0;

    [Header("Power")]
    [SerializeField] private float maxPower = 100f;
    [SerializeField] private float currentPower = 100f;
    [SerializeField] private float shieldPowerPerSecond = 18f;
    // 파워는 어디서도 채워지지 않아서 슈퍼점프 10번이면 끝이었다. 1구역 점프 코스는 떨어지면
    // 다시 뛰어야 하므로 한결처럼 비용 없이 쓴다. 방패를 안 쓸 때는 파워가 천천히 찬다.
    [SerializeField] private float superJumpPowerCost = 0f;
    [SerializeField] private float powerRegenPerSecond = 10f;

    [Header("Super Jump")]
    // 1구역 점프 코스(최대 14m 높이, 14m 간격)는 한결 값(12~42)에 맞춰 설계됐다. 5~13 으로는 8.6m 밖에 안 떠서 못 건넌다.
    [SerializeField] private float maxChargeTime = 1.5f;
    [SerializeField] private float minJumpForce = 12f;
    [SerializeField] private float maxJumpForce = 42f;

    [Header("Breaker")]
    [SerializeField] private KeyCode breakerKey = KeyCode.Q;
    [SerializeField] private float breakRange = 2.4f;
    [SerializeField] private float breakRadius = 0.55f;
    [SerializeField] private float breakForce = 1f;
    [SerializeField] private float breakCooldown = 0.7f;

    private Move_Yunseo move;
    private ShieldController shieldInstance;
    private float chargeTime;
    private bool chargingSuperJump;
    private float breakerCooldownTimer;

    public float CurrentPowerRatio => maxPower > 0f ? currentPower / maxPower : 0f;

    private void Awake()
    {
        move = GetComponent<Move_Yunseo>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (equipmentManager == null)
            equipmentManager = GetComponent<EquipmentManager>();
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);
    }

    private void OnDisable()
    {
        AetherAudio.Shield(this, false); // 실드를 든 채 꺼지면 웅웅거림이 남지 않게
    }

    private void Update()
    {
        if (breakerCooldownTimer > 0f)
            breakerCooldownTimer -= Time.deltaTime;

        HandleShield();
        HandleBreaker();
    }

    public bool HandleJumpInput(Keyboard keyboard)
    {
        if (keyboard == null || !HasPart(PartType.SuperJump))
            return false;

        if (keyboard.spaceKey.wasPressedThisFrame && IsGrounded() && currentPower >= superJumpPowerCost)
        {
            chargingSuperJump = true;
            chargeTime = 0f;
            return true;
        }

        if (!chargingSuperJump)
            return false;

        if (keyboard.spaceKey.isPressed)
        {
            chargeTime = Mathf.Min(maxChargeTime, chargeTime + Time.deltaTime);
            return true;
        }

        if (keyboard.spaceKey.wasReleasedThisFrame)
        {
            ExecuteSuperJump();
            chargingSuperJump = false;
            chargeTime = 0f;
            return true;
        }

        return true;
    }

    private void ExecuteSuperJump()
    {
        if (rb == null || !IsGrounded())
            return;

        currentPower = Mathf.Max(0f, currentPower - superJumpPowerCost);
        float ratio = maxChargeTime > 0f ? Mathf.Clamp01(chargeTime / maxChargeTime) : 0f;
        float jumpForce = Mathf.Lerp(minJumpForce, maxJumpForce, ratio);

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        AetherFootsteps.SuppressNextJump();
        AetherAudio.Play2D("superjump_release", Mathf.Lerp(0.6f, 1f, ratio), 1f, 0.03f);
    }

    private void HandleShield()
    {
        bool wantsShield = Mouse.current != null
            ? Mouse.current.rightButton.isPressed
            : Input.GetMouseButton(1);

        bool active = HasPart(PartType.Shield) && wantsShield && currentPower > 0f;
        EnsureShield();

        if (shieldInstance != null)
            shieldInstance.SetActive(active);
        AetherAudio.Shield(this, active);

        if (active)
            currentPower = Mathf.Max(0f, currentPower - shieldPowerPerSecond * Time.deltaTime);
        else
            currentPower = Mathf.Min(maxPower, currentPower + powerRegenPerSecond * Time.deltaTime);
    }

    private void HandleBreaker()
    {
        if (!HasPart(PartType.Breaker) || breakerCooldownTimer > 0f)
            return;

        Keyboard keyboard = Keyboard.current;
        bool pressed = keyboard != null
            ? keyboard.qKey.wasPressedThisFrame
            : Input.GetKeyDown(breakerKey);

        if (!pressed)
            return;

        breakerCooldownTimer = breakCooldown;
        AetherAudio.Play2D("breaker_hit", 0.85f, 1f, 0.04f);
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 direction = transform.forward;

        if (Physics.SphereCast(origin, breakRadius, direction, out RaycastHit hit, breakRange, breakableMask, QueryTriggerInteraction.Collide))
        {
            IBreakable breakable = hit.collider.GetComponentInParent<IBreakable>();
            if (breakable != null)
                breakable.TakeBreakDamage(breakForce);
        }
    }

    private bool IsGrounded()
    {
        // 이동 스크립트와 같은 판정을 쓴다(groundMask 가 비어 있어도 맞게 판정한다).
        if (move != null)
            return move.IsGroundedNow;

        if (move != null && move.groundCheck != null)
        {
            return Physics.CheckSphere(
                move.groundCheck.position,
                move.groundCheckRadius,
                move.groundMask,
                QueryTriggerInteraction.Ignore);
        }

        return rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.05f;
    }

    private bool HasPart(PartType type)
    {
        return equipmentManager != null && equipmentManager.HasPart(type);
    }

    private void EnsureShield()
    {
        if (shieldInstance != null)
            return;

        Transform parent = shieldAnchor != null ? shieldAnchor : transform;
        if (shieldPrefab != null)
        {
            shieldInstance = Instantiate(shieldPrefab, parent);
            shieldInstance.transform.localPosition = new Vector3(0f, 1.1f, 1.0f);
            shieldInstance.transform.localRotation = Quaternion.identity;
            return;
        }

        GameObject shieldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shieldObject.name = "Runtime Shield";
        shieldObject.transform.SetParent(parent, false);
        shieldObject.transform.localPosition = new Vector3(0f, 1.1f, 1.0f);
        shieldObject.transform.localScale = new Vector3(1.4f, 1.4f, 0.12f);
        Collider collider = shieldObject.GetComponent<Collider>();
        collider.isTrigger = true;
        shieldObject.AddComponent<ShieldHitbox>();
        shieldInstance = shieldObject.AddComponent<ShieldController>();
    }
}
