using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

[RequireComponent(typeof(Move_Hankyul))]
public class HangyeolPartAbilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Move_Hankyul moveHankyul;
    [SerializeField] private PlayerStatus playerStatus;

    [Header("Super Jump")]
    [SerializeField] private float minJumpForce = 12f;
    [SerializeField] private float maxJumpForce = 42f;
    [SerializeField] private float maxChargeTime = 1.2f;

    [Header("Shield")]
    [SerializeField] private ShieldController shieldPrefab;
    [SerializeField] private Transform shieldAnchor;
    [SerializeField] private float shieldPowerPerSecond = 18f;
    [SerializeField] private Vector3 runtimeShieldLocalPosition = new Vector3(0f, 1.1f, 1.0f);
    [SerializeField] private Vector3 runtimeShieldLocalScale = new Vector3(1.4f, 1.4f, 0.12f);

    [Header("Breaker")]
    [SerializeField] private KeyCode breakerKey = KeyCode.Q;
    [SerializeField] private float breakRange = 2.4f;
    [SerializeField] private float breakRadius = 0.55f;
    [SerializeField] private float breakForce = 1f;
    [SerializeField] private float breakCooldown = 0.7f;
    [SerializeField] private LayerMask breakableMask = ~0;

    [Header("Fallback Power")]
    [SerializeField] private float maxPower = 100f;
    [SerializeField] private float currentPower = 100f;

    private ShieldController shieldInstance;
    private float breakerCooldownTimer;

    public float CurrentPowerRatio => maxPower > 0f ? currentPower / maxPower : 0f;

    private void Awake()
    {
        ResolveReferences();
        ApplyTuning();
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe(true);
        ApplyTuning();
        RefreshSuperJumpState();
    }

    private void OnDisable()
    {
        Subscribe(false);
        AetherAudio.Shield(this, false); // 실드를 든 채 채팅 등으로 꺼지면 웅웅거림이 남았다
    }

    private void OnDestroy()
    {
        Subscribe(false);
    }

    private void OnValidate()
    {
        minJumpForce = Mathf.Max(0.1f, minJumpForce);
        maxJumpForce = Mathf.Max(minJumpForce, maxJumpForce);
        maxChargeTime = Mathf.Max(0.05f, maxChargeTime);
        shieldPowerPerSecond = Mathf.Max(0f, shieldPowerPerSecond);
        breakRange = Mathf.Max(0.05f, breakRange);
        breakRadius = Mathf.Max(0.01f, breakRadius);
        breakForce = Mathf.Max(0.1f, breakForce);
        breakCooldown = Mathf.Max(0f, breakCooldown);
        maxPower = Mathf.Max(1f, maxPower);
        currentPower = Mathf.Clamp(currentPower, 0f, maxPower);

        if (Application.isPlaying)
            ApplyTuning();
    }

    private void Update()
    {
        if (breakerCooldownTimer > 0f)
            breakerCooldownTimer -= Time.deltaTime;

        HandleShieldInput();
        HandleBreakerInput();
    }

    private void ResolveReferences()
    {
        if (moveHankyul == null)
            moveHankyul = GetComponent<Move_Hankyul>();

        if (equipmentManager == null)
            equipmentManager = GetComponent<EquipmentManager>();

        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();
    }

    private void Subscribe(bool subscribe)
    {
        if (equipmentManager == null)
            return;

        equipmentManager.OnEquipmentChanged -= HandleEquipmentChanged;
        equipmentManager.OnPartEquipped -= HandlePartEquipped;
        equipmentManager.OnPartUnequipped -= HandlePartUnequipped;

        if (!subscribe)
            return;

        equipmentManager.OnEquipmentChanged += HandleEquipmentChanged;
        equipmentManager.OnPartEquipped += HandlePartEquipped;
        equipmentManager.OnPartUnequipped += HandlePartUnequipped;
    }

    private void HandleEquipmentChanged(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part == null || part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    private void HandlePartEquipped(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part != null && part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    private void HandlePartUnequipped(PartSlotType slot, PartData part)
    {
        if (slot == PartSlotType.Leg || part != null && part.partType == PartType.SuperJump)
            RefreshSuperJumpState();
    }

    /// <summary>
    /// 씬의 배치 배율이 클 때 슈퍼점프 '도달 높이'를 같은 비율로 키운다.
    ///
    /// 점프 힘은 속도라서 높이와 제곱 관계다(h = v² / 2g). 배율을 그대로 곱하면
    /// 높이가 배율의 제곱만큼 뛴다. 2.6배 세계에서 힘을 2.6배 하면 높이는 6.8배가 되어
    /// 완충 시 600m를 솟구쳤다. 그래서 힘에는 배율의 제곱근만 곱한다.
    /// </summary>
    public void ScaleSuperJump(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        float forceMultiplier = Mathf.Sqrt(multiplier);

        minJumpForce *= forceMultiplier;
        maxJumpForce *= forceMultiplier;
        breakRange *= multiplier;   // 거리는 그대로 배율만큼
        ApplyTuning();
    }

    /// <summary>
    /// 슈퍼점프의 도달 높이를 미터로 직접 지정한다. 점프 코스를 설계할 때 쓴다.
    /// 필요한 속도는 v = √(2gh)로 구한다.
    /// </summary>
    public void SetSuperJumpApexHeights(float minApexMeters, float maxApexMeters, float chargeTime)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity < 0.01f)
            gravity = 9.81f;

        minJumpForce = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, minApexMeters));
        maxJumpForce = Mathf.Sqrt(2f * gravity * Mathf.Max(minApexMeters, maxApexMeters));
        maxChargeTime = Mathf.Max(0.05f, chargeTime);
        ApplyTuning();
    }

    /// <summary>지금 설정으로 완충했을 때 올라가는 높이(m).</summary>
    public float MaxApexHeight
    {
        get
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            if (gravity < 0.01f)
                gravity = 9.81f;

            return maxJumpForce * maxJumpForce / (2f * gravity);
        }
    }

    public void RefreshSuperJumpState()
    {
        ResolveReferences();
        ApplyTuning();

        if (moveHankyul == null || equipmentManager == null)
            return;

        PartData legPart = equipmentManager.GetEquipped(PartSlotType.Leg);
        bool directLegSuperJump = legPart != null && legPart.partType == PartType.SuperJump;
        bool externalSuperJump = equipmentManager.HasPart(PartType.SuperJump);

        moveHankyul.SetSuperJumpEnabled(directLegSuperJump || externalSuperJump);
    }

    /// <summary>슈퍼 점프 최대 힘. 실측 42 → 58m, 높이는 힘의 제곱에 비례하므로 25 ≈ 20m.</summary>
    public const float SuperJumpMaxForceCap = 25f;

    private void ApplyTuning()
    {
        if (moveHankyul == null)
            return;

        // 최대 힘 42 는 풀충전 58m 로 맵 밖까지 넘어갔다. 약 20m 가 되게 한 곳에서 묶는다.
        // 장면마다 저장된 값(07 은 42)이 달라도 이 값을 넘지 않는다.
        moveHankyul.ConfigureSuperJump(Mathf.Min(minJumpForce, SuperJumpMaxForceCap), Mathf.Min(maxJumpForce, SuperJumpMaxForceCap), maxChargeTime);
    }

    private void HandleShieldInput()
    {
        bool wantsShield = Mouse.current != null
            ? Mouse.current.rightButton.isPressed
            : Input.GetMouseButton(1);

        bool shouldShield = HasShieldEquipped() && wantsShield && HasPower();
        EnsureShield();

        if (shieldInstance != null)
            shieldInstance.SetActive(shouldShield);

        if (!shouldShield)
        {
            AetherAudio.Shield(this, false);
            return;
        }

        bool powered = ConsumePower(shieldPowerPerSecond * Time.deltaTime);
        if (!powered && shieldInstance != null)
            shieldInstance.SetActive(false);

        AetherAudio.Shield(this, powered);
    }

    private void HandleBreakerInput()
    {
        if (!HasBreakerEquipped() || breakerCooldownTimer > 0f)
            return;

        bool pressed;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && breakerKey == KeyCode.Q)
            pressed = keyboard.qKey.wasPressedThisFrame;
        else if (keyboard != null && breakerKey == KeyCode.E)
            pressed = keyboard.eKey.wasPressedThisFrame;
        else
            pressed = Input.GetKeyDown(breakerKey);

        if (!pressed)
            return;

        breakerCooldownTimer = breakCooldown;
        AetherAudio.Play2D("breaker_hit", 0.85f, 1f, 0.04f); // 팔 파츠가 내리치는 육중한 충격음
        TryBreakForwardTarget();
    }

    private bool HasShieldEquipped()
    {
        if (equipmentManager == null)
            return false;

        PartData armPart = equipmentManager.GetEquipped(PartSlotType.Arm);
        return armPart != null && armPart.partType == PartType.Shield
            || equipmentManager.HasPart(PartType.Shield);
    }

    private bool HasBreakerEquipped()
    {
        if (equipmentManager == null)
            return false;

        PartData armPart = equipmentManager.GetEquipped(PartSlotType.Arm);
        PartData utilityPart = equipmentManager.GetEquipped(PartSlotType.Utility);
        return armPart != null && armPart.partType == PartType.Breaker
            || utilityPart != null && utilityPart.partType == PartType.Breaker
            || equipmentManager.HasPart(PartType.Breaker);
    }

    private bool HasPower()
    {
        if (TryReadPlayerStatusPower(out float statusPower))
            return statusPower > 0f;

        return currentPower > 0f;
    }

    private bool ConsumePower(float amount)
    {
        if (amount <= 0f)
            return true;

        if (TryReadPlayerStatusPower(out float statusPower))
        {
            if (statusPower <= 0f)
                return false;

            TryWritePlayerStatusPower(Mathf.Max(0f, statusPower - amount));
            return statusPower - amount > 0f;
        }

        if (currentPower <= 0f)
            return false;

        currentPower = Mathf.Max(0f, currentPower - amount);
        return currentPower > 0f;
    }

    private void EnsureShield()
    {
        if (shieldInstance != null)
            return;

        Transform parent = shieldAnchor != null ? shieldAnchor : transform;
        if (shieldPrefab != null)
        {
            shieldInstance = Instantiate(shieldPrefab, parent);
            shieldInstance.transform.localPosition = runtimeShieldLocalPosition;
            shieldInstance.transform.localRotation = Quaternion.identity;
            shieldInstance.SetActive(false);
            return;
        }

        GameObject shieldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shieldObject.name = "Runtime Shield";
        shieldObject.transform.SetParent(parent, false);
        shieldObject.transform.localPosition = runtimeShieldLocalPosition;
        shieldObject.transform.localRotation = Quaternion.identity;
        shieldObject.transform.localScale = runtimeShieldLocalScale;

        Collider shieldCollider = shieldObject.GetComponent<Collider>();
        if (shieldCollider != null)
            shieldCollider.isTrigger = true;

        shieldObject.AddComponent<ShieldHitbox>();
        shieldInstance = shieldObject.AddComponent<ShieldController>();
        shieldInstance.SetActive(false);
    }

    private void TryBreakForwardTarget()
    {
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Vector3 direction = transform.forward;

        if (!Physics.SphereCast(origin, breakRadius, direction, out RaycastHit hit, breakRange, breakableMask, QueryTriggerInteraction.Collide))
            return;

        IBreakable breakable = hit.collider.GetComponentInParent<IBreakable>();
        if (breakable != null)
        {
            breakable.TakeBreakDamage(breakForce);
            return;
        }

        hit.collider.SendMessageUpwards("TakeBreakDamage", breakForce, SendMessageOptions.DontRequireReceiver);
        hit.collider.SendMessageUpwards("Break", SendMessageOptions.DontRequireReceiver);
    }

    private bool TryReadPlayerStatusPower(out float value)
    {
        value = 0f;
        if (playerStatus == null)
            return false;

        FieldInfo field = FindPowerField();
        if (field != null && field.FieldType == typeof(float))
        {
            value = (float)field.GetValue(playerStatus);
            return true;
        }

        PropertyInfo property = FindPowerProperty();
        if (property != null && property.PropertyType == typeof(float) && property.CanRead)
        {
            value = (float)property.GetValue(playerStatus, null);
            return true;
        }

        return false;
    }

    private bool TryWritePlayerStatusPower(float value)
    {
        if (playerStatus == null)
            return false;

        FieldInfo field = FindPowerField();
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(playerStatus, Mathf.Max(0f, value));
            return true;
        }

        PropertyInfo property = FindPowerProperty();
        if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
        {
            property.SetValue(playerStatus, Mathf.Max(0f, value), null);
            return true;
        }

        return false;
    }

    private FieldInfo FindPowerField()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "currentPWR", "currentPwr", "currentPower", "pwr", "PWR", "power" };
        for (int i = 0; i < names.Length; i++)
        {
            FieldInfo field = typeof(PlayerStatus).GetField(names[i], flags);
            if (field != null)
                return field;
        }

        return null;
    }

    private PropertyInfo FindPowerProperty()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "CurrentPWR", "CurrentPwr", "CurrentPower", "PWR", "Power" };
        for (int i = 0; i < names.Length; i++)
        {
            PropertyInfo property = typeof(PlayerStatus).GetProperty(names[i], flags);
            if (property != null)
                return property;
        }

        return null;
    }
}
