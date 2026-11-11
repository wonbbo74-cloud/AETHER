using UnityEngine;

/// <summary>
/// 낮은 턱·잔해 모서리를 걸어서 넘게 해 준다(계단 보조).
///
/// Meka(Move_Hankyul)에만 있던 것을 Shiri(Move_Yunseo)도 쓰도록 떼어 냈다. Shiri 는 이 보조가 없어
/// 05 잔해 사이·06 문턱 같은 몇 cm 짜리 턱에도 몸이 걸려 길이 좁게 느껴졌다.
/// 높이는 0.45m 까지(보통 FPS 의 걸어 넘는 높이). 4m 였을 때 벽·상자를 걸어 올라간 적이 있어 그보다 높이지 않는다.
/// </summary>
public static class AetherStepAssist
{
    public const float MaxStepHeight = 0.45f;
    private const float CheckDistance = 0.45f;
    private const float ClimbSpeed = 6f;

    private static readonly Collider[] roomHits = new Collider[16];
    private static int mask;

    private static int Mask()
    {
        if (mask == 0)
        {
            int ignore = 1 << 2;
            foreach (string layer in new[] { "Player", "Enemy", "ViewModel", "UI" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0) ignore |= 1 << index;
            }
            mask = ~ignore;
        }
        return mask;
    }

    /// <summary>앞에 넘을 수 있는 턱이 있으면 올라선 위치를 돌려준다.</summary>
    public static bool TryStep(Rigidbody rb, CapsuleCollider capsule, Vector3 moveDir, Vector3 nextPosition, out Vector3 adjusted)
    {
        adjusted = nextPosition;
        if (rb == null || capsule == null || moveDir.sqrMagnitude <= 0.001f)
            return false;

        Transform t = capsule.transform;
        Vector3 direction = new Vector3(moveDir.x, 0f, moveDir.z).normalized;
        float radius = Mathf.Max(0.05f, capsule.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z));
        float forward = radius + CheckDistance;
        float halfHeight = capsule.height * t.lossyScale.y * 0.5f;

        Vector3 bottom = t.TransformPoint(capsule.center) + Vector3.down * (halfHeight - radius);
        int m = Mask();

        // 발바닥 바로 위에서 쏜다. 아래 구의 중심에서 쏘면 조금만 올라가도 턱 위로 빗나가 다시 떨어졌다.
        Vector3 feet = bottom + Vector3.down * radius;
        if (!Physics.Raycast(feet + Vector3.up * 0.05f, direction, out RaycastHit low, forward, m, QueryTriggerInteraction.Ignore))
            return false;
        if (low.normal.y > 0.5f)                  // 걸을 수 있는 경사는 턱이 아니다
            return false;
        if (Physics.Raycast(feet + Vector3.up * (MaxStepHeight + 0.08f), direction, forward, m, QueryTriggerInteraction.Ignore))
            return false;                         // 위까지 막혀 있으면 벽

        Vector3 probe = feet + direction * forward + Vector3.up * (MaxStepHeight + 0.35f);
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, MaxStepHeight + 0.55f, m, QueryTriggerInteraction.Ignore)
            || top.normal.y < 0.35f)
            return false;

        float rise = top.point.y - feet.y;
        if (rise <= 0.01f || rise > MaxStepHeight + 0.05f)
            return false;

        float targetY = rb.position.y + rise + 0.04f;
        if (!HasRoom(capsule, new Vector3(nextPosition.x, targetY, nextPosition.z), radius, halfHeight))
            return false;

        float y = Mathf.MoveTowards(rb.position.y, targetY, ClimbSpeed * Time.fixedDeltaTime);
        adjusted = new Vector3(nextPosition.x, y, nextPosition.z);
        return true;
    }

    private static bool HasRoom(CapsuleCollider capsule, Vector3 bodyPosition, float radius, float halfHeight)
    {
        Transform t = capsule.transform;
        Vector3 center = bodyPosition + (t.TransformPoint(capsule.center) - t.position);
        float half = Mathf.Max(radius, halfHeight);
        Vector3 a = center + Vector3.down * (half - radius) + Vector3.up * 0.04f;
        Vector3 b = center + Vector3.up * (half - radius);

        int count = Physics.OverlapCapsuleNonAlloc(a, b, radius * 0.92f, roomHits, Mask(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (roomHits[i] != null && !roomHits[i].transform.IsChildOf(t.root))
                return false;
        return true;
    }
}
