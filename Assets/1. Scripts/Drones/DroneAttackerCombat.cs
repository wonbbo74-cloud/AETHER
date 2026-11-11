using UnityEngine;

public class DroneAttackerCombat : DroneCombatBase
{
    [Header("Attack")]
    [SerializeField] private float fireRange = 16f;
    [SerializeField] private float preferredDistance = 10f;
    [SerializeField] private float fireInterval = 0.8f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 18f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletLifeTime = 4f;

    [Header("Individual Variation")]
    [SerializeField] private Vector2 fireIntervalMultiplierRange = new Vector2(0.82f, 1.18f);

    private float fireTimer;
    private float individualFireInterval;

    public override void Initialize(Drones drone)
    {
        base.Initialize(drone);

        // FirePoint 가 몸체에서 1.5m 떨어진 허공에 찍혀 있어서, 총알이 드론과 상관없는
        // 자리에서 튀어나왔다. 드론은 총 모델이 따로 없으니 몸체 앞 끝을 총구로 삼는다.
        AetherMuzzleFit.SnapToNose(firePoint, transform);

        individualFireInterval = fireInterval * Random.Range(
            Mathf.Min(fireIntervalMultiplierRange.x, fireIntervalMultiplierRange.y),
            Mathf.Max(fireIntervalMultiplierRange.x, fireIntervalMultiplierRange.y));
    }

    public void ConfigureLegacy(GameObject nextBulletPrefab, Transform nextFirePoint, float nextAttackRange, float nextPreferredDistance, float nextFireInterval, float nextBulletSpeed, int nextBulletDamage, float nextBulletLifeTime)
    {
        bulletPrefab = nextBulletPrefab;
        firePoint = nextFirePoint;
        fireRange = nextAttackRange;
        preferredDistance = nextPreferredDistance;
        fireInterval = nextFireInterval;
        bulletSpeed = nextBulletSpeed;
        bulletDamage = nextBulletDamage;
        bulletLifeTime = nextBulletLifeTime;
    }

    public override void OnEnterCombat()
    {
        float interval = Mathf.Max(0.1f, individualFireInterval);
        fireTimer = -Random.Range(0f, interval * 0.75f);
    }

    public override void OnExitCombat()
    {
        fireTimer = 0f;
    }

    public override void TickCombat()
    {
        if (owner == null || owner.Player == null) return;

        float distanceToTarget = owner.DistanceToPlayerXZ();
        Vector3 toPlayer = owner.DirectionToPlayerXZ();

        if (distanceToTarget > fireRange)
        {
            Vector3 approachTarget = owner.Player.position;
            if (toPlayer.sqrMagnitude > 0.001f)
                approachTarget = owner.Player.position - toPlayer * preferredDistance;

            owner.MoveToPoint(approachTarget, owner.Player.position, 1.1f);
            return;
        }

        owner.FaceTo(owner.Player.position);

        // 건물 옆에 떠서 벽에 대고 쏘면 총알이 0.4m 에서 벽에 박혀 사라졌다.
        // 막혀 있으면 쏘지 말고 플레이어 쪽으로 다가가 트인 자리를 찾는다.
        if (!HasClearShot())
        {
            owner.MoveToPoint(owner.Player.position, owner.Player.position, 1.1f);
            return;
        }

        fireTimer += Time.deltaTime;
        float interval = individualFireInterval > 0f ? individualFireInterval : fireInterval;
        if (fireTimer >= Mathf.Max(0.1f, interval))
        {
            FireSingleShot();
        }
    }

    private bool HasClearShot()
    {
        Vector3 muzzle = firePoint != null ? firePoint.position : owner.transform.position + owner.transform.forward * 1f;
        return AetherLineOfFire.IsClear(owner.transform.position, muzzle, owner.Player.position + Vector3.up, owner.transform, owner.Player);
    }

    private void FireSingleShot()
    {
        if (bulletPrefab == null || owner == null || owner.Player == null) return;

        // 적 전체 발사 수 상한. 차례가 아니면 조금 뒤에 다시 물어본다.
        if (!AetherFireDirector.TryShoot(AetherFireDirector.Kind.Drone, this))
        {
            fireTimer -= 0.12f;
            return;
        }

        Vector3 spawnPos = firePoint != null
            ? firePoint.position
            : owner.transform.position + owner.transform.forward * 1f;

        Vector3 targetPos = owner.Player.position + Vector3.up;
        Vector3 toTarget = targetPos - spawnPos;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        Vector3 dir = toTarget.normalized;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        // 총구 화염·섬광을 띄우고, 드론을 반동으로 살짝 밀어낸다. 총알도 탄두 모양으로 바꾼다.
        AetherGunFX.ShapeBullet(bulletObj, dir);
        AetherGunFX.Flash(spawnPos, dir, 0.16f);
        AetherShotOrigin.Mark(spawnPos, dir, name);
        AetherShotAudio.PlayBallistic(spawnPos, 0.45f);
        AetherGunFX.Kick(firePoint, dir, 0.04f, 3f);

        fireTimer = 0f;

        EnemyBullet bullet = bulletObj.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            bullet.Initialize(dir, bulletSpeed, AetherCombatBalance.ScaleEnemyDamage(bulletDamage), bulletLifeTime, owner.transform);
            return;
        }

        // 'EnemyBullet.prefab' 에는 이름과 달리 RobotBullet 이 붙어 있다. 이걸 못 받아서
        // 총구 섬광·총성만 나고 총알은 그 자리에서 지워졌다(드론 탄 전부, 0m).
        RobotBullet robotBullet = bulletObj.GetComponent<RobotBullet>();
        if (robotBullet != null)
        {
            robotBullet.Initialize(dir, bulletSpeed, AetherCombatBalance.ScaleEnemyDamage(bulletDamage), bulletLifeTime, owner.transform);
            return;
        }

        Destroy(bulletObj);
    }
}
