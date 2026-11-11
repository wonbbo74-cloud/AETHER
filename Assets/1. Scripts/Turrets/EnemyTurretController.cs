using UnityEngine;

public class EnemyTurretController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform leftMuzzle;
    [SerializeField] private Transform rightMuzzle;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Targeting")]
    [SerializeField] private float detectionRange = 34f;
    [SerializeField] private float turnSpeed = 150f;
    [SerializeField] private float fireAngle = 7f;
    [SerializeField] private LayerMask obstacleMask = 1;

    [Header("Weapon")]
    [SerializeField] private float initialFireDelay = 1.2f;
    [SerializeField] private float fireInterval = 0.65f;
    [SerializeField] private float bulletSpeed = 22f;
    [SerializeField] private int bulletDamage = 12;
    [SerializeField] private float bulletLifeTime = 4f;

    private Transform player;
    private float nextPlayerLookup;
    private RobotHealth health;
    private float nextFireTime;
    private bool useLeftMuzzle = true;
    [SerializeField, HideInInspector] private int shotsFired;

    public int ShotsFired => shotsFired;

    public void Configure(GameObject projectilePrefab)
    {
        bulletPrefab = projectilePrefab;
    }

    private void Awake()
    {
        ResolveVisualReferences();
        FindPlayer();

        health = GetComponent<RobotHealth>();
        if (health != null)
        {
            health.Configure(160, 1.5f);
            health.Died += HandleDeath;
        }

        // 보이지도 않는 거리에서 쏘지 않게, 그리고 한 발씩 띄엄띄엄 쏘게 맞춘다.
        detectionRange = AetherCombatBalance.TurretDetectionRange;
        fireInterval = Mathf.Max(fireInterval, AetherCombatBalance.TurretFireInterval);

        // 터렛 넷이 같은 박자로 쏘면 또 겹친다. 시작 시점을 넓게 흩뿌린다.
        nextFireTime = Time.time + initialFireDelay + Random.Range(0f, fireInterval);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        AetherPlayerCharacter.RefreshTarget(ref player, ref nextPlayerLookup);
        if (player == null || AetherEncounterDirector.IsDormant(this))   // 05 웨이브: 깨기 전엔 겨누지 않는다
            return;

        Vector3 targetPosition = player.position + Vector3.up;
        Vector3 targetDirection = targetPosition - AimOrigin();
        if (targetDirection.sqrMagnitude > detectionRange * detectionRange)
            return;

        RotateToward(targetPosition);

        Vector3 flatTargetDirection = targetPosition - yawPivot.position;
        float angle = Vector3.Angle(yawPivot.forward, flatTargetDirection.normalized);
        if (angle <= fireAngle && Time.time >= nextFireTime && HasClearShot(targetPosition))
            Fire(targetPosition);
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void ResolveVisualReferences()
    {
        if (yawPivot == null)
            yawPivot = FindChildContaining(transform, "turret_head1") ?? transform;

        if (leftMuzzle == null)
            leftMuzzle = CreateMuzzle("TurretMuzzleLeft", FindChildContaining(yawPivot, "barrel_l"), -0.45f);

        if (rightMuzzle == null)
            rightMuzzle = CreateMuzzle("TurretMuzzleRight", FindChildContaining(yawPivot, "barrel_r"), 0.45f);
    }

    private Transform CreateMuzzle(string muzzleName, Transform barrel, float fallbackX)
    {
        GameObject muzzleObject = new GameObject(muzzleName);
        Transform muzzle = muzzleObject.transform;

        if (barrel != null)
        {
            muzzle.SetParent(barrel, false);

            // 포신 길이를 1.25 로 박아 두었더니 총알이 포신 한가운데에서 튀어나왔다.
            // (실제로 재 보니 보이는 포신 끝은 1.3m 더 앞이었다.)
            // 모델마다 다르므로 포신 메시의 앞 끝을 재서 그 자리에 둔다.
            muzzle.localPosition = new Vector3(0f, 0f, AetherMuzzleFit.ForwardReach(barrel, 1.25f) + 0.1f);
        }
        else
        {
            muzzle.SetParent(yawPivot, false);
            muzzle.localPosition = new Vector3(fallbackX, 0f, 2.1f);
        }

        muzzle.localRotation = Quaternion.identity;
        return muzzle;
    }

    private static Transform FindChildContaining(Transform root, string token)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return children[i];
        }

        return null;
    }

    private Vector3 AimOrigin()
    {
        if (useLeftMuzzle && leftMuzzle != null)
            return leftMuzzle.position;
        if (!useLeftMuzzle && rightMuzzle != null)
            return rightMuzzle.position;
        return yawPivot.position + yawPivot.forward * 1.5f;
    }

    private void RotateToward(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - yawPivot.position;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        yawPivot.rotation = Quaternion.RotateTowards(
            yawPivot.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }

    private bool HasClearShot(Vector3 targetPosition)
    {
        Vector3 origin = AimOrigin();
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return false;

        return !Physics.Raycast(
            origin,
            direction / distance,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void Fire(Vector3 targetPosition)
    {
        nextFireTime = Time.time + fireInterval + Random.Range(-0.25f, 0.45f);

        if (bulletPrefab == null)
            return;

        // 터렛은 한 발 한 발이 들려야 하므로 전체 상한에서 빼 준다.
        AetherFireDirector.TakeImportantShot();

        Transform muzzle = useLeftMuzzle ? leftMuzzle : rightMuzzle;
        useLeftMuzzle = !useLeftMuzzle;
        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : yawPivot.position + yawPivot.forward * 1.5f;
        Vector3 direction = (targetPosition - spawnPosition).normalized;

        GameObject bulletObject = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(direction, Vector3.up));
        shotsFired++;

        // 총구 화염·섬광을 띄우고, 쏜 포신을 뒤로 민다. 총알도 탄두 모양으로 바꾼다.
        AetherGunFX.ShapeBullet(bulletObject, direction);
        AetherGunFX.Flash(spawnPosition, direction, 0.26f);
        AetherShotAudio.PlayTurretBoom(spawnPosition);
        AetherGunFX.Kick(muzzle != null ? muzzle.parent : yawPivot, direction, 0.06f, 3f);

        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(direction, bulletSpeed, AetherCombatBalance.ScaleEnemyDamage(bulletDamage), bulletLifeTime, transform);
            return;
        }

        RobotBullet robotBullet = bulletObject.GetComponent<RobotBullet>();
        if (robotBullet != null)
            robotBullet.Initialize(direction, bulletSpeed, AetherCombatBalance.ScaleEnemyDamage(bulletDamage), bulletLifeTime, transform);
    }

    private void HandleDeath(RobotHealth deadHealth)
    {
        // 합선 불꽃 + 전원이 꺼지는 소리
        AetherAudio.PlayAt("turret_death", transform.position + Vector3.up * 0.8f, 1f, 3f, 45f, 0.05f, 1f, 60);
        enabled = false;
        Destroy(gameObject, deadHealth.DeathDespawnDelay);
    }
}
