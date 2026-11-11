using UnityEngine;

/// <summary>
/// 적이 지금 쏘면 총알이 플레이어까지 갈 수 있는지 본다.
///
/// 적이 벽에 바짝 붙어 서면 총구가 벽 속에 파묻히거나 바로 앞에 벽이 있어서,
/// 쏘자마자 총알이 0.2~0.4m 에서 사라졌다. 섬광과 총성만 나고 총알은 안 보이니
/// '총알이 안 나오는 적'이 됐다. 쏘기 전에 이걸로 걸러 내고, 막혔으면 자리를 옮긴다.
///
/// 총알(RobotBullet/EnemyBullet)과 같은 규칙을 쓴다: 자기 몸·적·트리거는 통과한다.
/// </summary>
public static class AetherLineOfFire
{
    /// <summary>총알 굵기보다 조금 넉넉하게. 모서리를 스치는 것까지 잡는다.</summary>
    private const float Radius = 0.05f;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];
    private static readonly Collider[] Overlaps = new Collider[8];

    /// <param name="body">쏘는 쪽 몸 안의 한 점. 총구와 같은 높이가 좋다.</param>
    /// <param name="muzzle">총알이 생기는 자리.</param>
    /// <param name="aimPoint">총알이 향하는 자리.</param>
    /// <param name="self">쏘는 쪽의 뿌리. 자기 몸은 막는 것으로 치지 않는다.</param>
    /// <param name="target">맞힐 대상. 대상의 몸도 막는 것으로 치지 않는다.</param>
    public static bool IsClear(Vector3 body, Vector3 muzzle, Vector3 aimPoint, Transform self, Transform target)
    {
        Transform targetRoot = target != null ? target.root : null;

        // 1) 총구가 벽 너머로 삐져나갔는지. 건물은 메시 충돌체라 안에 파묻힌 점은
        //    겹침 검사로도 광선으로도 안 잡힌다. 몸 중심에서 총구로 쏘면 벽의 바깥 면에 걸린다.
        //    여기서는 터렛 같은 적도 막는 것으로 본다(총이 터렛 속에 들어가 있는 모습이 된다).
        if (Blocked(body, muzzle, 0f, self, targetRoot, enemiesBlock: true))
            return false;

        int inside = Physics.OverlapSphereNonAlloc(muzzle, Radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < inside; i++)
        {
            Collider other = Overlaps[i];
            if (other != null && !Ignored(other.transform, self, targetRoot, enemiesBlock: true))
                return false;
        }

        // 2) 총구에서 대상까지. 적은 총알이 통과하므로 막는 것으로 치지 않는다.
        return !Blocked(muzzle, aimPoint, Radius, self, targetRoot, enemiesBlock: false);
    }

    private static bool Blocked(Vector3 from, Vector3 to, float radius, Transform self, Transform targetRoot, bool enemiesBlock)
    {
        Vector3 offset = to - from;
        float distance = offset.magnitude;
        if (distance < 0.01f)
            return false;

        Vector3 direction = offset / distance;
        int count = radius > 0f
            ? Physics.SphereCastNonAlloc(from, radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(from, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = Hits[i].collider;
            if (other != null && !Ignored(other.transform, self, targetRoot, enemiesBlock))
                return true;
        }

        return false;
    }

    private static bool Ignored(Transform other, Transform self, Transform targetRoot, bool enemiesBlock)
    {
        if (self != null && other.IsChildOf(self))
            return true;

        if (targetRoot != null && other.IsChildOf(targetRoot))
            return true;

        return !enemiesBlock && BelongsToEnemy(other);
    }

    private static bool BelongsToEnemy(Transform other)
    {
        for (Transform current = other; current != null; current = current.parent)
        {
            if (current.CompareTag("Enemy"))
                return true;
        }

        return false;
    }
}
