using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Hankyul_Fire : MonoBehaviour
{
    public enum FireMode
    {
        Single,
        Auto
    }

    [Header("References")]
    [SerializeField] private Camera playerCam;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private Vector3 fallbackMuzzleLocalOffset = new Vector3(0f, 0f, 0.65f);

    [Header("Battle")]
    [SerializeField] private bool isBattleMode = true;
    [SerializeField] private bool allowDirectInput = false;

    [Header("Shoot - Common")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float range = 200f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float bulletSpeed = 80f;
    [SerializeField] private float bulletLifeTime = 3f;
    [SerializeField] private LayerMask hitMask;

    [Header("Shoot - Single / Auto")]
    [SerializeField] private FireMode currentFireMode = FireMode.Single;
    [SerializeField] private float singleFireRate = 4f;
    [SerializeField] private float autoFireRate = 10f;

    [Header("Shoot - Shotgun")]
    [SerializeField] private int shotgunPelletCount = 5;
    [SerializeField] private float shotgunFireRate = 1.2f;
    [SerializeField] private float shotgunSpreadAngle = 15f;

    [Header("Ammo")]
    [SerializeField] private int maxTotalAmmo = 150;
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int currentMagazineAmmo = 30;
    [SerializeField] private int currentReserveAmmo = 120;
    [SerializeField] private float reloadTime = 1.5f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitImpactPrefab;

    [Header("Tracer")]
    [SerializeField] private LineRenderer tracerPrefab;
    [SerializeField] private float tracerTime = 0.05f;

    [Header("반동")]
    [SerializeField] private bool enableRecoil = true;
    [Tooltip("한 발에 총구가 위로 튀는 각도(도).")]
    [SerializeField] private float recoilPitch = 1.6f;
    [Tooltip("좌우로 밀리는 각도의 최대치(도). 매번 방향이 바뀝니다.")]
    [SerializeField] private float recoilYaw = 0.5f;
    [Tooltip("화면이 기우는 각도의 최대치(도).")]
    [SerializeField] private float recoilRoll = 0.6f;
    [Tooltip("산탄총일 때 반동 배수.")]
    [SerializeField] private float shotgunRecoilScale = 2.4f;

    [Header("탄피 배출")]
    [Tooltip("탄피가 튀어나오는 자리. 비우면 총구를 씁니다.")]
    [SerializeField] private Transform shellEjectPoint;
    [Tooltip("탄피 배출 연출의 월드 기준 크기(m).")]
    [SerializeField] private float shellEjectSize = 0.35f;

    private ParticleSystem shellEjectEffect;

    [Header("총구 화염")]
    [Tooltip("총구 화염의 월드 기준 크기(m). 크면 총구가 아니라 화염만 보입니다.")]
    [SerializeField] private float muzzleFlashSize = 0.18f;

    [Header("총 반동 동작")]
    [Tooltip("쏠 때 총이 뒤로 밀렸다가 제자리로 돌아옵니다.")]
    [SerializeField] private bool enableWeaponKick = true;
    [Tooltip("뒤로 밀리는 거리(m).")]
    [SerializeField] private float kickBack = 0.06f;
    [Tooltip("위로 들리는 거리(m).")]
    [SerializeField] private float kickUp = 0.018f;
    [Tooltip("총구가 들리는 각도(도).")]
    [SerializeField] private float kickPitch = 7f;
    [Tooltip("제자리로 돌아오는 속도. 클수록 빨리 돌아옵니다.")]
    [SerializeField] private float kickRecover = 16f;
    [Tooltip("연사해도 총이 끝없이 밀리지 않게 하는 한계(m).")]
    [SerializeField] private float kickMax = 0.14f;

    private Vector3 kickOffset;
    private float kickAngle;
    private bool kickWritten;
    private Vector3 kickRestPosition;
    private Vector3 kickWrotePosition;
    private Quaternion kickRestRotation;
    private Quaternion kickWroteRotation;

    [Header("총구 섬광")]
    [Tooltip("쏠 때 총구가 순간적으로 환해집니다. 어두운 맵에서 효과가 큽니다.")]
    [SerializeField] private bool enableMuzzleLight = true;
    [SerializeField] private float muzzleLightIntensity = 6f;
    [SerializeField] private float muzzleLightRange = 7f;
    [SerializeField] private float muzzleLightSeconds = 0.055f;

    private Light muzzleLight;
    private float muzzleLightTimer;


    [Header("화면 흔들림")]
    [Tooltip("쏠 때 화면을 살짝 흔듭니다. 프로젝트에 이미 있는 CameraShake를 씁니다.")]
    [SerializeField] private bool shakeCameraOnFire = true;
    [SerializeField] private float shakeDuration = 0.06f;
    [SerializeField] private float shakeAmplitude = 0.035f;
    [SerializeField] private float shakeFrequency = 30f;

    // 어느 씬의 어떤 플레이어든 쓸 수 있도록 Resources에 둔 사격 연출 에셋.
    private const string MuzzleFlashResource = "AETHERRuntime/AetherMuzzleFlash";
    private const string BulletImpactResource = "AETHERRuntime/AetherBulletImpact";
    private const string GunshotResource = "AETHERRuntime/AetherGunshot";
    private const string CartridgeEjectResource = "AETHERRuntime/AetherCartridgeEject";

    // 총 전용 재장전음/빈 탄창 소리가 프로젝트에 없어서 가장 비슷한 기계음으로 대신한다.
    // 나중에 진짜 총 소리를 구하면 인스펙터에서 바꿔 끼우면 된다.
    private const string DryFireResource = "AETHERRuntime/AetherDryFire";
    private const string ReloadResource = "AETHERRuntime/AetherReload";

    private Move_Hankyul recoilTarget;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip shotgunClip;
    [SerializeField] private AudioClip reloadClip;
    [SerializeField] private AudioClip emptyClip;

    private float nextFireTime;
    private bool isReloading;
    private GameObject runtimeFallbackBulletPrefab;

    public FireMode CurrentFireMode => currentFireMode;
    public int CurrentMagazineAmmo => currentMagazineAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MaxTotalAmmo => maxTotalAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    public bool IsBattleMode => isBattleMode;

    private void Reset()
    {
        playerCam = Camera.main;
        audioSource = GetComponent<AudioSource>();
        ownerRoot = transform.root;

        EnsureMuzzleReference();
    }

    private void Awake()
    {
        if (playerCam == null)
        {
            playerCam = GetComponentInParent<Camera>();
            if (playerCam == null) playerCam = Camera.main;
        }

        EnsureMuzzleReference();

        EnsureAudioSource();
        EnsureMuzzleFlash();
        EnsureShellEject();
        EnsureShotFeedbackAssets();
        if (ownerRoot == null) ownerRoot = transform.root;
        EnsureBulletPrefab();

        ClampAmmo();
    }

    private void Update()
    {
        if (!allowDirectInput || Mouse.current == null || Keyboard.current == null) return;

        if (Keyboard.current.vKey.wasPressedThisFrame)
            ToggleFireMode();

        if (!isBattleMode || isReloading) return;
        if (Cursor.lockState != CursorLockMode.Locked) return; // 메뉴를 고르는 클릭으로 쏘지 않는다

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ShootShotgun();
            return;
        }

        if (currentFireMode == FireMode.Single)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                ShootSingle();
        }
        else
        {
            if (Mouse.current.leftButton.isPressed)
                ShootAuto();
        }
    }

    public void SetBattleMode(bool value)
    {
        isBattleMode = value;
    }

    public void ToggleFireMode()
    {
        currentFireMode = currentFireMode == FireMode.Single ? FireMode.Auto : FireMode.Single;
        AetherAudio.Play2D("firemode", 0.5f, currentFireMode == FireMode.Auto ? 1.08f : 0.95f, 0.02f); // 조정간 딸깍
        Debug.Log($"[Hankyul_Fire] FireMode Changed: {currentFireMode}");
    }

    public bool ShootOnce()
    {
        return ShootSingle();
    }

    public bool ShootSingle()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, singleFireRate), 1))
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();

        FireProjectile(spawnPos, aimPoint, damage);
        ConsumeAmmo(1);
        PlayShootFX(false);
        CheckAutoReload();

        return true;
    }

    public bool ShootAuto()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, autoFireRate), 1))
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();

        FireProjectile(spawnPos, aimPoint, damage);
        ConsumeAmmo(1);
        PlayShootFX(false);
        CheckAutoReload();

        return true;
    }

    public bool ShootShotgun()
    {
        if (!CanShoot(1f / Mathf.Max(0.01f, shotgunFireRate), 1))
            return false;

        int pelletCountToFire = Mathf.Min(currentMagazineAmmo, shotgunPelletCount);
        if (pelletCountToFire <= 0)
        {
            PlayEmptySound();
            return false;
        }

        Vector3 aimPoint = GetAimPoint();
        Vector3 spawnPos = GetMuzzlePosition();
        Vector3 baseDir = (aimPoint - spawnPos).normalized;

        if (baseDir.sqrMagnitude < 0.0001f)
            baseDir = transform.forward;

        for (int i = 0; i < pelletCountToFire; i++)
        {
            Vector3 spreadDir = GetSpreadDirection(baseDir, shotgunSpreadAngle);
            Vector3 pelletAimPoint = spawnPos + spreadDir * range;
            FireProjectile(spawnPos, pelletAimPoint, damage);
        }

        ConsumeAmmo(pelletCountToFire);
        PlayShootFX(true);
        CheckAutoReload();

        return true;
    }

    private bool CanShoot(float interval, int ammoNeeded)
    {
        if (!isBattleMode) return false;
        if (isReloading) return false;
        EnsureRuntimeReferences();
        if (!IsWeaponActuallyEquipped()) return false;
        if (playerCam == null) return false;
        if (Time.time < nextFireTime) return false;

        if (currentMagazineAmmo < ammoNeeded)
        {
            PlayEmptySound();
            CheckAutoReload();
            if (currentReserveAmmo <= 0)
                HintOutOfAmmo();
            return false;
        }

        nextFireTime = Time.time + interval;
        return true;
    }

    private float nextOutOfAmmoHint;

    /// <summary>
    /// 탄이 바닥나면 "0 0"만 보이고 철컥 소리만 났다. 에너지팩이 곧 탄약이라는 걸 알 길이 없어
    /// 연구소에서 팩을 들고도 빈총으로 맞기만 했다. 무엇을 하면 되는지 알려 준다.
    /// </summary>
    private void HintOutOfAmmo()
    {
        if (Time.unscaledTime < nextOutOfAmmoHint)
            return;
        nextOutOfAmmoHint = Time.unscaledTime + 8f;

        PlayerInventory inventory = ownerRoot != null ? ownerRoot.GetComponentInChildren<PlayerInventory>() : GetComponentInParent<PlayerInventory>();
        bool hasPack = false;
        if (inventory != null)
        {
            for (int i = 0; i < inventory.SlotCount && !hasPack; i++)
            {
                PlayerInventory.Slot slot = inventory.GetSlot(i);
                if (slot == null || slot.item == null || slot.item.effects == null) continue;
                foreach (var effect in slot.item.effects)
                    if (effect.type == EffectType.EnergyPack) { hasPack = true; break; }
            }
        }

        AetherObjectiveToast.Show(hasPack
            ? "탄약이 떨어졌다!\n퀵슬롯의 에너지팩으로 충전하라."
            : "탄약이 떨어졌다!\n상점으로 가서 에너지팩을 받아라.", 5f);
    }

    private bool IsWeaponActuallyEquipped()
    {
        if (!isBattleMode)
            return false;

        Transform owner = ownerRoot != null ? ownerRoot : transform.root;
        Hankyul_Battle battle = owner != null ? owner.GetComponentInChildren<Hankyul_Battle>(true) : null;
        if (battle != null && !battle.IsBattle())
            return false;

        if (!gameObject.activeInHierarchy)
            return false;

        if (muzzle != null && !muzzle.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private void CheckAutoReload()
    {
        if (currentMagazineAmmo <= 0 && currentReserveAmmo > 0 && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (audioSource != null && reloadClip != null)
            audioSource.PlayOneShot(reloadClip, AetherAudio.Sfx);

        yield return new WaitForSeconds(reloadTime);

        int needAmmo = magazineSize - currentMagazineAmmo;
        int loadAmmo = Mathf.Min(needAmmo, currentReserveAmmo);

        currentMagazineAmmo += loadAmmo;
        currentReserveAmmo -= loadAmmo;

        isReloading = false;
    }

    public bool TryReload()
    {
        if (!CanReload())
            return false;

        StartCoroutine(ReloadRoutine());
        return true;
    }

    private void ConsumeAmmo(int amount)
    {
        currentMagazineAmmo -= amount;
        if (currentMagazineAmmo < 0)
            currentMagazineAmmo = 0;
    }

    private void ClampAmmo()
    {
        currentMagazineAmmo = Mathf.Clamp(currentMagazineAmmo, 0, magazineSize);
        currentReserveAmmo = Mathf.Clamp(currentReserveAmmo, 0, maxTotalAmmo);

        int total = currentMagazineAmmo + currentReserveAmmo;
        if (total > maxTotalAmmo)
        {
            int overflow = total - maxTotalAmmo;
            currentReserveAmmo = Mathf.Max(0, currentReserveAmmo - overflow);
        }
    }

    private Vector3 GetAimPoint()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        Vector3 hitPoint = ray.origin + ray.direction * range;

        // 조준점은 '크로스헤어가 실제로 가리키는 곳'이어야 한다. 예전엔 적 레이어만 봐서
        // 문 표적·벽을 겨누면 조준점이 허공 끝으로 잡혀, 총구(카메라 아래 옆)에서 나란히 날아간 총알이
        // 작은 표적 옆을 스쳐 지나갔다(실측: 05 연구소 문 표적 51발 정조준 → 0발 명중).
        if (Physics.Raycast(ray, out RaycastHit hit, range, AimMask(), QueryTriggerInteraction.Ignore))
            hitPoint = hit.point;

        return hitPoint;
    }

    private int aimMask;

    private int AimMask()
    {
        if (aimMask == 0)
        {
            int ignore = 0;
            foreach (string layer in new[] { "Player", "ViewModel", "Ignore Raycast", "UI" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0) ignore |= 1 << index;
            }
            aimMask = ~ignore | hitMask.value;
        }
        return aimMask;
    }

    private Vector3 GetMuzzlePosition()
    {
        EnsureMuzzleReference();

        if (muzzle != null)
            return muzzle.position;

        if (TryGetRendererMuzzlePosition(out Vector3 rendererMuzzlePosition))
            return rendererMuzzlePosition;

        return transform.TransformPoint(fallbackMuzzleLocalOffset);
    }

    private void EnsureMuzzleReference()
    {
        Transform fireBase = FindFireBaseTransform();
        if (fireBase != null)
        {
            muzzle = fireBase;
            return;
        }

        if (muzzle == null)
            muzzle = FindMuzzleTransform();

        if (muzzle != null && !muzzle.name.Equals("firebase", System.StringComparison.OrdinalIgnoreCase))
            muzzle = CreateRuntimeFireBaseFrom(muzzle);
    }

    private void EnsureRuntimeReferences()
    {
        if (playerCam == null)
        {
            playerCam = GetComponentInParent<Camera>(true);
            if (playerCam == null) playerCam = Camera.main;
        }

        EnsureMuzzleReference();
        EnsureBulletPrefab();

        if (ownerRoot == null)
            ownerRoot = transform.root;
    }

    private void EnsureBulletPrefab()
    {
        if (bulletPrefab != null)
            return;

        bulletPrefab = Resources.Load<GameObject>("AETHERRuntime/Bullet");
        if (bulletPrefab != null)
            return;

        if (runtimeFallbackBulletPrefab != null)
        {
            bulletPrefab = runtimeFallbackBulletPrefab;
            return;
        }

        runtimeFallbackBulletPrefab = CreateRuntimeFallbackBulletPrefab();
        bulletPrefab = runtimeFallbackBulletPrefab;
    }

    private GameObject CreateRuntimeFallbackBulletPrefab()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        prefab.name = "Runtime_PlayerBullet_Fallback";
        prefab.transform.localScale = Vector3.one * 0.08f;

        Collider collider = prefab.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;

        Rigidbody rigidbody = prefab.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        prefab.AddComponent<PlayerBullet>();
        prefab.SetActive(false);
        DontDestroyOnLoad(prefab);
        return prefab;
    }

    private Transform FindMuzzleTransform()
    {
        Transform fireBase = FindFireBaseTransform();
        if (fireBase != null) return fireBase;

        Transform found = FindChildByNameContains(transform, "muzzle");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "firepoint");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "fire_point");
        if (found != null) return found;

        found = FindChildByNameContains(transform, "barrel");
        if (found != null) return found;

        if (muzzleFlash != null)
            return muzzleFlash.transform;

        return null;
    }

    private Transform FindFireBaseTransform()
    {
        Transform found = FindChildByNameEquals(transform, "firebase");
        if (found != null) return found;

        found = FindChildByNameEquals(transform, "fire base");
        if (found != null) return found;

        found = FindChildByNameEquals(transform, "fire_base");
        if (found != null) return found;

        return null;
    }

    private Transform CreateRuntimeFireBaseFrom(Transform source)
    {
        if (source == null)
            return null;

        GameObject fireBaseObject = new GameObject("firebase");
        Transform fireBase = fireBaseObject.transform;
        fireBase.SetParent(transform, false);
        fireBase.position = source.position;
        fireBase.rotation = source.rotation;
        fireBase.localScale = Vector3.one;
        return fireBase;
    }

    private bool TryGetRendererMuzzlePosition(out Vector3 position)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            position = default(Vector3);
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 forward = transform.forward;
        Vector3 center = bounds.center;
        float forwardExtent =
            Mathf.Abs(forward.x) * bounds.extents.x +
            Mathf.Abs(forward.y) * bounds.extents.y +
            Mathf.Abs(forward.z) * bounds.extents.z;
        position = center + forward * Mathf.Max(0.2f, forwardExtent + 0.08f);
        return true;
    }

    private static Transform FindChildByNameContains(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        string normalizedToken = token.ToLowerInvariant();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == root || child == null)
                continue;

            if (child.name.ToLowerInvariant().Contains(normalizedToken))
                return child;
        }

        return null;
    }

    private static Transform FindChildByNameEquals(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        string normalizedName = targetName.ToLowerInvariant();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == root || child == null)
                continue;

            if (child.name.ToLowerInvariant() == normalizedName)
                return child;
        }

        return null;
    }

    private void FireProjectile(Vector3 spawnPos, Vector3 aimPoint, float appliedDamage)
    {
        EnsureRuntimeReferences();

        Vector3 shootDir = (aimPoint - spawnPos).normalized;
        if (shootDir.sqrMagnitude < 0.0001f)
            shootDir = transform.forward;

        Vector3 tracerEndPoint = spawnPos + shootDir * range;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(spawnPos, shootDir, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (hasHit)
            tracerEndPoint = hit.point;

        if (bulletPrefab != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(shootDir));
            bulletObj.SetActive(true);

            // 동그란 공이 아니라 길쭉한 탄두로 보이게 한다.
            AetherGunFX.ShapeBullet(bulletObj, shootDir);

            PlayerBullet playerBullet = bulletObj.GetComponent<PlayerBullet>();
            if (playerBullet != null)
            {
                playerBullet.Initialize(
                    shootDir,
                    bulletSpeed,
                    appliedDamage,
                    bulletLifeTime,
                    ownerRoot != null ? ownerRoot : transform.root,
                    hitImpactPrefab
                );
            }
            else
            {
                Rigidbody bulletRb = bulletObj.GetComponent<Rigidbody>();
                if (bulletRb != null)
                    bulletRb.linearVelocity = shootDir * bulletSpeed;

                Destroy(bulletObj, bulletLifeTime);
            }
        }
        else
        {
            if (hasHit)
            {
                Lab_DoorPointer doorPointer = hit.collider.GetComponent<Lab_DoorPointer>();
                if (doorPointer == null)
                    doorPointer = hit.collider.GetComponentInParent<Lab_DoorPointer>();

                if (doorPointer != null)
                    doorPointer.Hit();

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    DamageInfo damageInfo = new DamageInfo(
                        appliedDamage,
                        DamageType.Bullet,
                        gameObject,
                        ownerRoot != null ? ownerRoot.gameObject : gameObject,
                        hit.point
                    );

                    damageable.TakeDamage(damageInfo);
                }

                // 착탄음: 적이면 장갑을 때리는 금속음, 아니면 바닥 재질에 맞는 소리(예전엔 불꽃만 보이고 무음)
                if (damageable != null)
                    AetherAudio.PlayAt("robot_hit", hit.point, 0.7f, 2f, 40f, 0.06f, 1f, 110);
                else
                    AetherAudio.PlayAt(AetherSurface.Classify(hit.collider) == "metal" ? "bullet_impact_metal" : "bullet_impact_concrete",
                        hit.point, 0.5f, 1.5f, 30f, 0.08f, 1f, 150);

                if (hitImpactPrefab != null)
                {
                    Quaternion rot = Quaternion.LookRotation(hit.normal);
                    GameObject impact = Instantiate(hitImpactPrefab, hit.point, rot);
                    Destroy(impact, 2f);
                }
            }
        }

        ShowTracerInstance(spawnPos, tracerEndPoint);
    }

    private void ShowTracerInstance(Vector3 start, Vector3 end)
    {
        if (tracerPrefab == null) return;

        LineRenderer tracerInstance = Instantiate(tracerPrefab, Vector3.zero, Quaternion.identity);
        tracerInstance.positionCount = 2;
        tracerInstance.SetPosition(0, start);
        tracerInstance.SetPosition(1, end);

        StartCoroutine(DestroyTracerAfter(tracerInstance.gameObject, tracerTime));
    }

    private IEnumerator DestroyTracerAfter(GameObject tracerObj, float time)
    {
        yield return new WaitForSeconds(time);

        if (tracerObj != null)
            Destroy(tracerObj);
    }

    private Vector3 GetSpreadDirection(Vector3 baseDir, float spreadAngle)
    {
        Vector3 right = Vector3.Cross(Vector3.up, baseDir).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = transform.right;

        Vector3 up = Vector3.Cross(baseDir, right).normalized;

        float yaw = Random.Range(-spreadAngle, spreadAngle);
        float pitch = Random.Range(-spreadAngle, spreadAngle);

        Vector3 spreadDir =
            baseDir +
            right * Mathf.Tan(yaw * Mathf.Deg2Rad) +
            up * Mathf.Tan(pitch * Mathf.Deg2Rad);

        return spreadDir.normalized;
    }

    private void PlayShootFX(bool isShotgun)
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (audioSource != null)
        {
            if (isShotgun && shotgunClip != null)
                audioSource.PlayOneShot(shotgunClip, AetherAudio.Sfx);
            else if (!isShotgun && fireClip != null)
                audioSource.PlayOneShot(fireClip, AetherAudio.Sfx);
        }


        FlashMuzzleLight(isShotgun);
        KickWeapon(isShotgun);
        ApplyRecoil(isShotgun);
        ShakeCamera(isShotgun);
        EjectShell();
    }

    /// <summary>
    /// 총이 뒤로 밀렸다가 제자리로 돌아오게 한다.
    ///
    /// 화면만 흔들리고 총은 가만히 있으면 '쏜 느낌'이 나지 않는다.
    /// 총구 방향 반대로 살짝 밀고 총구를 들었다가 스프링처럼 되돌린다.
    /// 되돌리는 일은 LateUpdate에서 한다. 애니메이터가 뼈를 갱신한 뒤라야 우리 값이 남는다.
    /// </summary>
    private void KickWeapon(bool isShotgun)
    {
        if (!enableWeaponKick)
            return;

        float scale = isShotgun ? shotgunRecoilScale : 1f;

        Vector3 back = ToLocalDirection(-FiringForward());
        Vector3 up = ToLocalDirection(Vector3.up);

        kickOffset += back * (kickBack * scale) + up * (kickUp * scale);
        kickOffset = Vector3.ClampMagnitude(kickOffset, kickMax);

        kickAngle = Mathf.Min(kickAngle + kickPitch * scale, kickPitch * 2.5f);
    }

    private Vector3 ToLocalDirection(Vector3 worldDirection)
    {
        // localPosition 은 부모 기준이라 방향도 부모 기준으로 바꿔야 한다.
        return transform.parent != null
            ? transform.parent.InverseTransformDirection(worldDirection)
            : worldDirection;
    }

    private Vector3 FiringForward()
    {
        return muzzle != null ? muzzle.forward : transform.forward;
    }

    private void LateUpdate()
    {
        UpdateWeaponKick();
        UpdateMuzzleLight();

    }

    /// <summary>
    /// 반동을 '애니메이션 위에 더하는' 방식으로 적용한다.
    ///
    /// 처음에는 총의 제자리를 한 번 기억해 두고 거기에 더했는데, 그러면 안 된다.
    /// 총은 손 뼈에 매달려 애니메이션으로 매 프레임 자리가 바뀌기 때문에,
    /// 기억해 둔 옛 자리로 되돌려 버려서 <b>총이 손에서 떨어져 나왔다.</b>
    /// LateUpdate 는 애니메이터가 끝난 뒤라, 지금 자리에 치우침만 얹으면 된다.
    /// </summary>
    private void UpdateWeaponKick()
    {
        if (!enableWeaponKick)
            return;

        // 이 총(Gun)은 애니메이션 커브가 없어 애니메이터가 자리를 다시 잡아 주지 않는다.
        // 그래서 매 프레임 더한 반동이 그대로 쌓여, 쏠수록 총이 손에서 멀어졌다
        // (실측: 한 번 교전 뒤 손에서 6.8m 위 — 총이 안 보이고 총알이 하늘에서 나가 적을 못 맞혔다).
        // 지난 프레임에 우리가 써 넣은 값이 그대로 남아 있으면 먼저 원래 자리로 되돌린다.
        // (애니메이션이 덮어썼다면 값이 달라져 있으니 건드리지 않는다.)
        if (kickWritten)
        {
            if (transform.localPosition == kickWrotePosition)
                transform.localPosition = kickRestPosition;
            if (transform.localRotation == kickWroteRotation)
                transform.localRotation = kickRestRotation;
            kickWritten = false;
        }

        if (kickOffset.sqrMagnitude < 0.0000001f && Mathf.Abs(kickAngle) < 0.01f)
            return;

        Vector3 pitchAxis = ToLocalDirection(muzzle != null ? muzzle.right : transform.right);

        kickRestPosition = transform.localPosition;
        kickRestRotation = transform.localRotation;

        transform.localPosition += kickOffset;
        transform.localRotation = Quaternion.AngleAxis(-kickAngle, pitchAxis) * transform.localRotation;

        kickWrotePosition = transform.localPosition;
        kickWroteRotation = transform.localRotation;
        kickWritten = true;

        float t = 1f - Mathf.Exp(-kickRecover * Time.deltaTime);
        kickOffset = Vector3.Lerp(kickOffset, Vector3.zero, t);
        kickAngle = Mathf.Lerp(kickAngle, 0f, t);
    }

    /// <summary>총구를 순간적으로 밝힌다. 어두운 곳에서 '뻥' 하는 느낌을 가장 크게 만든다.</summary>
    private void FlashMuzzleLight(bool isShotgun)
    {
        if (!enableMuzzleLight)
            return;

        EnsureMuzzleLight();

        if (muzzleLight == null)
            return;

        muzzleLightTimer = muzzleLightSeconds * (isShotgun ? 1.4f : 1f);
        muzzleLight.intensity = muzzleLightIntensity * (isShotgun ? 1.6f : 1f);
        muzzleLight.enabled = true;
    }

    private void UpdateMuzzleLight()
    {
        if (muzzleLight == null || !muzzleLight.enabled)
            return;

        muzzleLightTimer -= Time.deltaTime;

        if (muzzleLightTimer <= 0f)
        {
            muzzleLight.enabled = false;
            return;
        }

        // 남은 시간에 비례해 빠르게 꺼진다.
        float ratio = muzzleLightTimer / Mathf.Max(0.0001f, muzzleLightSeconds);
        muzzleLight.intensity = muzzleLightIntensity * ratio;
    }

    private void EnsureMuzzleLight()
    {
        if (muzzleLight != null)
            return;

        Transform origin = muzzle != null ? muzzle : transform;

        GameObject host = new GameObject("Muzzle Light");
        host.transform.SetParent(origin, false);
        host.transform.localPosition = Vector3.zero;

        muzzleLight = host.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = new Color(1f, 0.82f, 0.45f);
        muzzleLight.range = muzzleLightRange;
        muzzleLight.intensity = 0f;
        muzzleLight.shadows = LightShadows.None;
        muzzleLight.enabled = false;
    }

    /// <summary>
    /// 쏠 때 화면을 짧게 흔든다.
    /// 드론이 터질 때 쓰던 CameraShake가 이미 있는데 총에는 연결돼 있지 않았다.
    /// </summary>
    private void ShakeCamera(bool isShotgun)
    {
        if (!shakeCameraOnFire || CameraShake.Instance == null)
            return;

        float scale = isShotgun ? shotgunRecoilScale : 1f;
        CameraShake.Instance.Shake(shakeDuration, shakeAmplitude * scale, shakeFrequency);
    }

    /// <summary>
    /// 소리를 낼 곳을 보장한다.
    ///
    /// 플레이어 프리팹에는 AudioSource가 아예 없었다. 그래서 발사음 칸에 무엇을 넣어도
    /// audioSource가 null이라 아무 소리도 나지 않았다.
    /// 내 총소리는 귀 바로 옆에서 나는 것이므로 거리 감쇠 없이(2D) 재생한다.
    /// </summary>
    private void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            return;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
    }

    /// <summary>
    /// 피격 이펙트와 발사음이 비어 있으면 채운다.
    ///
    /// 플레이어가 씬마다 다른 프리팹이라(05는 Hankyul_찐, 구역은 Hankyul_Player),
    /// 프리팹마다 손으로 꽂으면 어디 하나는 반드시 빠진다.
    /// 비어 있을 때만 채우므로, 따로 지정해 둔 것이 있으면 그대로 둔다.
    /// </summary>
    private void EnsureShotFeedbackAssets()
    {
        if (hitImpactPrefab == null)
            hitImpactPrefab = Resources.Load<GameObject>(BulletImpactResource);

        if (fireClip == null)
            fireClip = Resources.Load<AudioClip>(GunshotResource);

        // 산탄총 전용 소리가 없으면 일반 발사음을 쓴다. 없는 것보다 낫다.
        if (shotgunClip == null)
            shotgunClip = fireClip;

        if (emptyClip == null)
            emptyClip = Resources.Load<AudioClip>(DryFireResource);

        if (reloadClip == null)
            reloadClip = Resources.Load<AudioClip>(ReloadResource);
    }

    /// <summary>
    /// 총구 화염을 총구에 달아 둔다.
    ///
    /// 이 칸은 '씬에 살아 있는 파티클'을 가리켜야 해서 프리팹 에셋을 그대로 꽂을 수 없다.
    /// (에셋을 가리키면 Play()를 불러도 화면에는 아무것도 안 나온다.)
    /// 그래서 총구 아래에 하나 만들어 붙인 뒤 그것을 쓴다.
    /// </summary>
    private void EnsureMuzzleFlash()
    {
        if (muzzleFlash != null)
            return;

        Transform origin = muzzle != null ? muzzle : transform;

        GameObject prefab = Resources.Load<GameObject>(MuzzleFlashResource);
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, origin.position, origin.rotation, origin);
        instance.name = "Muzzle Flash";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // 프리팹은 큼직한 총을 기준으로 만들어져 있어 그대로 달면 총만큼 커진다.
        // 손에 든 총 크기에 맞게 월드 기준으로 줄인다.
        Vector3 parentScale = origin.lossyScale;
        instance.transform.localScale = new Vector3(
            muzzleFlashSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            muzzleFlashSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            muzzleFlashSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        muzzleFlash = instance.GetComponent<ParticleSystem>();
        if (muzzleFlash == null)
            muzzleFlash = instance.GetComponentInChildren<ParticleSystem>(true);

        if (muzzleFlash == null)
        {
            Destroy(instance);
            return;
        }

        // 쏠 때만 터져야 한다. 켜자마자 혼자 뿜으면 총을 안 쏴도 불이 난 것처럼 보인다.
        foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>총구가 튀어 오르는 느낌. 산탄총은 더 크게 밀린다.</summary>
    private void ApplyRecoil(bool isShotgun)
    {
        if (!enableRecoil)
            return;

        if (recoilTarget == null)
        {
            recoilTarget = GetComponentInParent<Move_Hankyul>();
            if (recoilTarget == null)
                return;
        }

        float scale = isShotgun ? shotgunRecoilScale : 1f;

        // 좌우는 매번 다른 쪽으로 밀려야 총이 살아 있는 것처럼 느껴진다.
        float yaw = Random.Range(-recoilYaw, recoilYaw) * scale;
        float roll = Random.Range(-recoilRoll, recoilRoll) * scale;

        recoilTarget.AddRecoil(recoilPitch * scale, yaw, roll);
    }

    /// <summary>
    /// 탄피 배출 연출을 총에 달아 둔다.
    ///
    /// 프로젝트에 굴러다니는 탄피 프리팹은 없고 파티클로 된 배출 이펙트만 있다.
    /// 총구 화염과 같은 방식으로 한 번 달아 두고 쏠 때마다 재생한다.
    /// </summary>
    private void EnsureShellEject()
    {
        if (shellEjectEffect != null)
            return;

        Transform origin = shellEjectPoint != null ? shellEjectPoint : (muzzle != null ? muzzle : transform);

        GameObject prefab = Resources.Load<GameObject>(CartridgeEjectResource);
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, origin.position, origin.rotation, origin);
        instance.name = "Shell Eject";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Vector3 parentScale = origin.lossyScale;
        instance.transform.localScale = new Vector3(
            shellEjectSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            shellEjectSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            shellEjectSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        shellEjectEffect = instance.GetComponent<ParticleSystem>();
        if (shellEjectEffect == null)
            shellEjectEffect = instance.GetComponentInChildren<ParticleSystem>(true);

        if (shellEjectEffect == null)
        {
            Destroy(instance);
            return;
        }

        foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>한 발마다 탄피가 튀어나오는 연출.</summary>
    private void EjectShell()
    {
        if (shellEjectEffect != null)
            shellEjectEffect.Play(true);
    }

    private void PlayEmptySound()
    {
        if (audioSource != null && emptyClip != null)
            audioSource.PlayOneShot(emptyClip, AetherAudio.Sfx);
    }

    public void SetOwnerRoot(Transform rootTransform)
    {
        ownerRoot = rootTransform;
    }

    public string GetAmmoText()
    {
        return $"{currentMagazineAmmo} / {GetTotalCurrentAmmo()}";
    }

    public int GetTotalCurrentAmmo()
    {
        return currentMagazineAmmo + currentReserveAmmo;
    }

    public bool IsMagazineEmpty()
    {
        return currentMagazineAmmo <= 0;
    }

    public bool CanReload()
    {
        return !isReloading && currentMagazineAmmo < magazineSize && currentReserveAmmo > 0;
    }

    public void AddReserveAmmo(int amount)
    {
        currentReserveAmmo += amount;
        ClampAmmo();
    }

    public void SetAmmo(int magAmmo, int reserveAmmo)
    {
        currentMagazineAmmo = magAmmo;
        currentReserveAmmo = reserveAmmo;
        ClampAmmo();
    }

    public bool RefillAmmoByShop(int amount)
    {
        if (amount <= 0) return false;

        int totalAmmo = currentMagazineAmmo + currentReserveAmmo;
        if (totalAmmo >= maxTotalAmmo) return false;

        int addableAmount = Mathf.Min(amount, maxTotalAmmo - totalAmmo);
        currentReserveAmmo += addableAmount;

        ClampAmmo();
        return addableAmount > 0;
    }
    public bool RefillAmmoBattery()
    {
        return RefillAmmoByShop(30);
    }

    public bool TryBuyEnergyCharge(out string message)
    {
        message = "";

        bool reserveFull = currentReserveAmmo >= 120;
        bool magazineFull = currentMagazineAmmo >= 30;

        if (reserveFull && magazineFull)
        {
            message = "에너지가 가득 찼습니다.";
            return false;
        }

        if (reserveFull)
        {
            currentMagazineAmmo = 30;
            ClampAmmo();
            return true;
        }

        currentReserveAmmo += 30;
        ClampAmmo();
        return true;
    }

    public bool TryBuyEnergyPack(out string message)
    {
        return UseEnergyPack(out message);
    }

    public bool UseEnergyPack(out string message)
    {
        message = "";

        bool magazineFull = currentMagazineAmmo >= magazineSize;
        bool reserveFull = currentReserveAmmo >= (maxTotalAmmo - magazineSize);

        if (magazineFull && reserveFull)
        {
            message = "에너지가 가득 찼습니다.";
            return false;
        }

        currentMagazineAmmo = magazineSize;
        currentReserveAmmo = maxTotalAmmo - magazineSize;
        ClampAmmo();
        AetherAudio.Play2D("energy_pack", 0.6f, 1f, 0.02f); // 배터리 끼우고 충전되는 소리
        return true;
    }
}
