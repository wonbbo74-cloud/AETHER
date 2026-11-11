using UnityEngine;

/// <summary>
/// 적이 든 총이 실제로 쏘는 쪽을 겨누게 한다.
///
/// 총알은 '플레이어 쪽'으로 날아가는데 총은 애니메이션이 잡아 준 자세 그대로였다.
/// 실제로 재 보니 총열과 날아가는 방향이 73~80° 어긋나 있었다.
/// 자리는 총구가 맞는데도 "엉뚱한 데서 총알이 나온다"로 보인 이유다.
///
/// 애니메이션이 자세를 다 잡은 뒤(LateUpdate) 총만 살짝 돌려 둘을 맞춘다.
/// 너무 많이 돌리면 팔이 부러진 것처럼 보이므로 각도를 제한한다.
/// </summary>
public class AetherWeaponAim : MonoBehaviour
{
    /// <summary>애니메이션 자세에서 비틀 수 있는 최대 각도.</summary>
    private const float MaxDegrees = 65f;

    /// <summary>겨눔이 들어오고 빠지는 속도. 뚝뚝 끊기지 않게 한다.</summary>
    private const float BlendSpeed = 9f;

    /// <summary>이 시간 동안 겨누라는 요청이 없으면 원래 자세로 돌아간다.</summary>
    private const float RequestTimeout = 0.25f;

    private Transform muzzle;
    private Vector3 localBarrel = Vector3.forward;
    private bool ready;

    private Vector3 target;
    private float lastRequestTime = -99f;
    private float weight;

    /// <summary>총구가 어디인지 알려 준다. 총 손잡이에서 총구로 향하는 쪽이 총열 방향이다.</summary>
    public void Configure(Transform firePoint)
    {
        if (firePoint == null)
            return;

        muzzle = firePoint;

        Vector3 barrel = firePoint.position - transform.position;
        if (barrel.sqrMagnitude < 0.0001f)
            return;

        localBarrel = transform.InverseTransformDirection(barrel.normalized);
        ready = true;
    }

    /// <summary>이 자리를 겨눠 달라고 매 프레임 알려 준다.</summary>
    public void AimAt(Vector3 worldTarget)
    {
        target = worldTarget;
        lastRequestTime = Time.time;
    }

    private void LateUpdate()
    {
        bool wants = ready && muzzle != null && Time.time - lastRequestTime < RequestTimeout;
        weight = Mathf.MoveTowards(weight, wants ? 1f : 0f, BlendSpeed * Time.deltaTime);

        if (weight <= 0.001f || !ready || muzzle == null)
            return;

        Vector3 current = transform.TransformDirection(localBarrel);
        Vector3 desired = target - muzzle.position;

        if (desired.sqrMagnitude < 0.0001f || current.sqrMagnitude < 0.0001f)
            return;

        Quaternion correction = Quaternion.FromToRotation(current.normalized, desired.normalized);
        correction.ToAngleAxis(out float angle, out Vector3 axis);

        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.0001f)
            return;

        if (angle > 180f)
            angle -= 360f;

        angle = Mathf.Clamp(angle, -MaxDegrees, MaxDegrees) * weight;
        transform.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
    }
}
