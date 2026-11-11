using UnityEngine;

/// <summary>
/// 적이 총을 쏠 때의 연출을 한곳에 모아 둔다.
///
/// 로봇·드론·터렛이 저마다 다른 코드로 총알만 뱉고 있어서,
/// 플레이어 쪽에는 있는 총구 화염·반동이 적에게는 전혀 없었다.
/// 세 곳에서 같은 함수를 부르게 해 연출을 맞춘다.
/// </summary>
public static class AetherGunFX
{
    private const string MuzzleFlashResource = "AETHERRuntime/AetherMuzzleFlash";

    private static GameObject muzzleFlashPrefab;
    private static bool muzzleFlashLoaded;

    /// <summary>
    /// 총구에 화염과 섬광을 띄운다. 짧게 떴다가 스스로 사라진다.
    /// </summary>
    /// <param name="position">총구 자리.</param>
    /// <param name="direction">총알이 나가는 방향.</param>
    /// <param name="size">화염의 월드 기준 크기(m).</param>
    public static void Flash(Vector3 position, Vector3 direction, float size = 0.22f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        // 적이 스무 명 넘게 쉬지 않고 쏘기 때문에, 한 발마다 오브젝트를 만들면 금방 쌓인다.
        // 어차피 보이지도 않는 먼 곳의 발사는 건너뛴다.
        if (!IsWorthShowing(position))
            return;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject host = new GameObject("Enemy Muzzle FX");
        host.transform.SetPositionAndRotation(position, rotation);

        SpawnFlashParticle(host.transform, size);
        SpawnFlashLight(host.transform);

        Object.Destroy(host, 0.3f);
    }

    /// <summary>
    /// 레이저 총의 총구 섬광. 불덩이 대신 작고 짧은 빛만 띄운다.
    ///
    /// 손에 든 총까지 터렛처럼 화염을 뿜으면 화면이 불바다가 된다.
    /// 화염은 확 줄이고 붉은 빛으로 '에너지 무기' 느낌만 남긴다.
    /// </summary>
    public static void FlashLaser(Vector3 position, Vector3 direction, float size = 0.08f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        if (!IsWorthShowing(position))
            return;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject host = new GameObject("Enemy Laser Muzzle");
        host.transform.SetPositionAndRotation(position, rotation);

        SpawnFlashParticle(host.transform, size);
        SpawnFlashLight(host.transform, new Color(1f, 0.32f, 0.26f), 3.5f, 2.5f);

        Object.Destroy(host, 0.2f);
    }

    /// <summary>
    /// 총구에서 쏘는 방향으로 짧은 빛줄기를 뻗는다.
    ///
    /// 총알만 날려 보내면 "총구에서 나온 것"으로 안 읽힌다. 총알은 이미 저만치 가 있고
    /// 총구에는 아무 일도 일어나지 않기 때문이다. 총구에 붙은 빛줄기가 둘을 이어 준다.
    /// 레이저 무기라는 느낌도 이게 가장 크다.
    /// </summary>
    /// <param name="from">총구 자리.</param>
    /// <param name="direction">쏘는 방향.</param>
    /// <param name="length">빛줄기 길이(m).</param>
    public static void LaserMuzzleBeam(Vector3 from, Vector3 direction, float length = 1.6f)
    {
        if (direction.sqrMagnitude < 0.0001f || !IsWorthShowing(from))
            return;

        Vector3 forward = direction.normalized;

        GameObject host = new GameObject("Laser Muzzle Beam");
        host.transform.position = from;

        LineRenderer beam = host.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.SetPosition(0, from);
        beam.SetPosition(1, from + forward * length);

        // 총구 쪽이 굵고 끝으로 갈수록 가늘어진다.
        beam.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.075f),
            new Keyframe(0.25f, 0.04f),
            new Keyframe(1f, 0.008f));

        beam.numCapVertices = 0;
        beam.alignment = LineAlignment.View;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", new Color(1f, 0.35f, 0.25f, 1f));

            beam.material = material;
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.86f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0.2f), 0.3f),
                new GradientColorKey(new Color(0.85f, 0.12f, 0.08f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });

        beam.colorGradient = gradient;

        host.AddComponent<AetherLaserBeamFade>();
        Object.Destroy(host, 0.12f);
    }

    /// <summary>화면에 보일 만한 거리인지. 카메라에서 50m 밖이면 연출을 만들지 않는다.</summary>
    private static bool IsWorthShowing(Vector3 position)
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            // 태그가 붙은 카메라가 없으면 아무 카메라나 기준으로 본다.
            Camera[] cameras = Camera.allCameras;
            if (cameras.Length == 0)
                return true;

            camera = cameras[0];
        }

        return (camera.transform.position - position).sqrMagnitude < 50f * 50f;
    }

    private static void SpawnFlashParticle(Transform parent, float size)
    {
        if (!muzzleFlashLoaded)
        {
            muzzleFlashPrefab = Resources.Load<GameObject>(MuzzleFlashResource);
            muzzleFlashLoaded = true;
        }

        if (muzzleFlashPrefab == null)
            return;

        GameObject flash = Object.Instantiate(muzzleFlashPrefab, parent.position, parent.rotation, parent);
        flash.transform.localScale = Vector3.one * size;

        foreach (ParticleSystem system in flash.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            system.Play();
        }
    }

    private static void SpawnFlashLight(Transform parent)
    {
        SpawnFlashLight(parent, new Color(1f, 0.78f, 0.42f), 6f, 4.5f);
    }

    private static void SpawnFlashLight(Transform parent, Color color, float range, float intensity)
    {
        GameObject lightObject = new GameObject("Flash Light");
        lightObject.transform.SetParent(parent, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;

        lightObject.AddComponent<AetherFlashLightFade>();
    }

    /// <summary>
    /// 총을 쏜 쪽을 뒤로 밀었다가 되돌린다.
    /// 뼈 애니메이션 위에 얹어야 해서 전용 부품을 붙여 LateUpdate에서 처리한다.
    /// </summary>
    public static void Kick(Transform target, Vector3 worldDirection, float distance = 0.05f, float degrees = 4f)
    {
        if (target == null)
            return;

        AetherRecoilKick kick = target.GetComponent<AetherRecoilKick>();
        if (kick == null)
            kick = target.gameObject.AddComponent<AetherRecoilKick>();

        kick.Add(worldDirection, distance, degrees);
    }

    /// <summary>
    /// 총알을 '동그란 공'에서 길쭉하고 뾰족한 탄두 모양으로 바꾼다.
    ///
    /// 적 총알은 지름 0.05m 짜리 구(球)라 콩알이 날아오는 것처럼 보였다.
    /// 메시를 갈아 끼우는 대신 날아가는 방향으로 늘이고 옆을 좁혀 탄두처럼 만든다.
    /// (구를 늘이면 양 끝이 뾰족해진다.)
    /// </summary>
    public static void ShapeBullet(GameObject bullet, Vector3 direction)
    {
        if (bullet == null)
            return;

        // 방향은 총알 스크립트(EnemyBullet/RobotBullet)가 직접 맞춘다.
        // 여기서 또 돌리면 그쪽 보정값과 싸우게 되므로 모양만 손댄다.
        foreach (MeshFilter filter in bullet.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            // 어느 축이 총알의 '길이'인지는 메시가 정한다.
            // 적 총알은 캡슐이라 길이가 로컬 +Y 다. 여기를 +Z 로 넘겨짚었다가
            // 총알이 날아가는 쪽이 아니라 하늘 쪽으로 늘어나 종잇장처럼 보였다.
            Vector3 meshSize = filter.sharedMesh.bounds.size;
            int lengthAxis = LongestAxis(meshSize);

            Vector3 scale = filter.transform.localScale;
            float currentLength = meshSize[lengthAxis] * Mathf.Abs(scale[lengthAxis]);
            float currentThickness = ShortestWorldWidth(meshSize, scale, lengthAxis);

            if (currentLength <= 0.0001f || currentThickness <= 0.0001f)
                continue;

            // 이미 가늘고 길면 또 손대지 않는다.
            if (currentLength > currentThickness * 3.5f)
                continue;

            // 지름 5cm 짜리 알맹이는 총알치고 너무 굵다. 가늘고 조금 더 긴 탄두로 잡는다.
            // (0.05 x 0.10 → 0.022 x 0.11)
            float thickness = currentThickness * 0.45f;
            float length = currentLength * 1.1f;

            Vector3 next = scale;
            for (int axis = 0; axis < 3; axis++)
            {
                if (meshSize[axis] <= 0.0001f)
                    continue;

                next[axis] = axis == lengthAxis
                    ? length / meshSize[axis]
                    : thickness / meshSize[axis];
            }

            KeepColliderSize(filter.transform, scale, next, lengthAxis);
            filter.transform.localScale = next;

            MakeGlow(filter.GetComponent<Renderer>());
        }

        AddTracerTrail(bullet.transform);
    }

    /// <summary>
    /// 총알을 가늘고 긴 레이저 탄으로 바꾼다.
    ///
    /// 손에 든 총은 에너지 무기로 보이게 한다. 탄두보다 훨씬 길고 가늘게 늘이고,
    /// 빛을 받지 않는 새빨간 색으로 칠해 어디서 보든 선처럼 읽히게 한다.
    /// </summary>
    /// <returns>탄의 절반 길이(m). 이만큼 앞으로 밀어야 꼬리가 총구에서 시작한다.</returns>
    public static float ShapeLaserBolt(GameObject bullet, Vector3 direction)
    {
        if (bullet == null)
            return 0f;

        float halfLength = 0f;

        foreach (MeshFilter filter in bullet.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            Vector3 meshSize = filter.sharedMesh.bounds.size;
            int lengthAxis = LongestAxis(meshSize);

            Vector3 scale = filter.transform.localScale;
            float currentLength = meshSize[lengthAxis] * Mathf.Abs(scale[lengthAxis]);
            float currentThickness = ShortestWorldWidth(meshSize, scale, lengthAxis);

            if (currentLength <= 0.0001f || currentThickness <= 0.0001f)
                continue;

            // 이미 레이저 모양(아주 길쭉)이면 또 늘이지 않는다.
            if (currentLength > currentThickness * 10f)
                continue;

            // 0.05 x 0.10 짜리 알맹이 → 굵기 0.025m, 길이 0.45m 의 빛줄기.
            // 너무 가늘면 20m 밖에서 아예 안 보인다.
            float thickness = currentThickness * 0.5f;
            float length = currentLength * 4.5f;

            Vector3 next = scale;
            for (int axis = 0; axis < 3; axis++)
            {
                if (meshSize[axis] <= 0.0001f)
                    continue;

                next[axis] = axis == lengthAxis
                    ? length / meshSize[axis]
                    : thickness / meshSize[axis];
            }

            KeepColliderSize(filter.transform, scale, next, lengthAxis);
            filter.transform.localScale = next;
            halfLength = Mathf.Max(halfLength, length * 0.5f);

            MakeGlow(filter.GetComponent<Renderer>(), new Color(1f, 0.22f, 0.16f, 1f));
        }

        AddLaserTrail(bullet.transform);
        return halfLength;
    }

    /// <summary>레이저 탄의 꼬리. 아주 가늘고 길어서 빛줄기처럼 보인다.</summary>
    private static void AddLaserTrail(Transform bullet)
    {
        if (bullet.GetComponentInChildren<TrailRenderer>(true) != null)
            return;

        GameObject host = new GameObject("LaserTrail");
        host.transform.SetParent(bullet, false);
        host.transform.localPosition = Vector3.zero;
        host.transform.localScale = InverseScale(bullet.lossyScale);

        TrailRenderer trail = host.AddComponent<TrailRenderer>();
        trail.time = 0.06f;
        trail.widthMultiplier = 1f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.042f),
            new Keyframe(1f, 0.004f));
        trail.minVertexDistance = 0.05f;
        trail.autodestruct = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.numCapVertices = 0;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", new Color(1f, 0.3f, 0.22f, 1f));

            trail.material = material;
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.66f), 0f),
                new GradientColorKey(new Color(1f, 0.22f, 0.14f), 0.4f),
                new GradientColorKey(new Color(0.7f, 0.08f, 0.05f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });

        trail.colorGradient = gradient;
    }

    private static int LongestAxis(Vector3 size)
    {
        if (size.y >= size.x && size.y >= size.z)
            return 1;

        return size.z > size.x ? 2 : 0;
    }

    /// <summary>길이 축을 뺀 나머지 두 축 중 더 가는 쪽의 월드 두께.</summary>
    private static float ShortestWorldWidth(Vector3 meshSize, Vector3 scale, int lengthAxis)
    {
        float smallest = float.MaxValue;

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == lengthAxis)
                continue;

            smallest = Mathf.Min(smallest, meshSize[axis] * Mathf.Abs(scale[axis]));
        }

        return smallest == float.MaxValue ? 0f : smallest;
    }

    /// <summary>
    /// 총알이 스스로 빛나게 한다.
    ///
    /// 회색 쇠구슬이 날아오면 총알인지 돌멩이인지 알 수 없다.
    /// 빛을 받지 않는 밝은 주황으로 칠해서, 그늘에서도 예광탄처럼 보이게 한다.
    /// </summary>
    private static void MakeGlow(Renderer renderer)
    {
        MakeGlow(renderer, new Color(1f, 0.72f, 0.3f, 1f));
    }

    private static void MakeGlow(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            return;

        Material material = new Material(shader)
        {
            color = color,
        };

        renderer.material = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// 총알 뒤에 짧은 불꼬리를 붙인다. 날아오는 게 눈에 보이게 하는 핵심이다.
    ///
    /// 꼬리는 항상 카메라를 보고 서는 띠라서, 굵고 길면 종잇장을 붙여 놓은 것처럼 납작해 보인다.
    /// (0.09m 굵기에 1.2m 길이로 두었더니 바로 그렇게 보였다.)
    /// 총알 쪽은 조금 굵고 끝은 뾰족하게, 길이는 짧게 잡아 불꽃 모양을 만든다.
    /// </summary>
    private static void AddTracerTrail(Transform bullet)
    {
        if (bullet.GetComponentInChildren<TrailRenderer>(true) != null)
            return;

        GameObject host = new GameObject("Tracer");
        host.transform.SetParent(bullet, false);
        host.transform.localPosition = Vector3.zero;

        // 총알 몸통을 가늘게 눌러 놓았으므로, 그대로 자식이 되면 꼬리까지 같이 눌린다.
        // 반대로 늘려서 꼬리는 제 크기로 그려지게 한다.
        host.transform.localScale = InverseScale(bullet.lossyScale);

        TrailRenderer trail = host.AddComponent<TrailRenderer>();
        trail.time = 0.035f;
        trail.widthMultiplier = 1f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.055f),
            new Keyframe(0.35f, 0.028f),
            new Keyframe(1f, 0f));
        trail.minVertexDistance = 0.04f;
        trail.autodestruct = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.numCapVertices = 0;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", new Color(1f, 0.62f, 0.22f, 1f));

            trail.material = material;
        }

        // 총알 쪽은 하얗게 달아오르고 뒤로 갈수록 주황에서 붉게 식는다.
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.78f), 0f),
                new GradientColorKey(new Color(1f, 0.6f, 0.18f), 0.35f),
                new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.4f),
                new GradientAlphaKey(0f, 1f),
            });

        trail.colorGradient = gradient;
    }

    /// <summary>축마다 뒤집은 크기. 부모가 눌려 있어도 자식은 제 크기로 보이게 한다.</summary>
    private static Vector3 InverseScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x) < 0.0001f ? 1f : 1f / scale.x,
            Mathf.Abs(scale.y) < 0.0001f ? 1f : 1f / scale.y,
            Mathf.Abs(scale.z) < 0.0001f ? 1f : 1f / scale.z);
    }

    /// <summary>
    /// 보이는 모양만 바꾸고 맞는 범위는 원래대로 둔다.
    ///
    /// 콜라이더는 트랜스폼 크기를 따라 같이 줄고 늘어난다. 총알을 가늘게 만들면
    /// 맞는 범위까지 절반 이하로 줄어 잘 안 맞게 되고, 길게 늘이면 빗나간 총알에도 맞는다.
    /// 바뀐 배율만큼 반대로 곱해 두어 실제 판정은 그대로 유지한다.
    /// </summary>
    private static void KeepColliderSize(Transform target, Vector3 before, Vector3 after, int lengthAxis)
    {
        float widthBefore = 0f;
        float widthAfter = 0f;

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == lengthAxis)
                continue;

            widthBefore = Mathf.Max(widthBefore, Mathf.Abs(before[axis]));
            widthAfter = Mathf.Max(widthAfter, Mathf.Abs(after[axis]));
        }

        if (widthAfter <= 0.0001f)
            return;

        float widthRatio = widthBefore / widthAfter;
        float lengthRatio = Mathf.Abs(after[lengthAxis]) > 0.0001f
            ? Mathf.Abs(before[lengthAxis]) / Mathf.Abs(after[lengthAxis])
            : 1f;

        foreach (SphereCollider sphere in target.GetComponents<SphereCollider>())
        {
            if (sphere != null)
                sphere.radius *= Mathf.Max(widthRatio, lengthRatio);
        }

        foreach (CapsuleCollider capsule in target.GetComponents<CapsuleCollider>())
        {
            if (capsule == null)
                continue;

            // 캡슐 콜라이더의 높이 축이 총알 길이 축과 다르면 보정이 뒤바뀐다. 그때는 손대지 않는다.
            if (capsule.direction != lengthAxis)
                continue;

            capsule.radius *= widthRatio;
            capsule.height *= lengthRatio;
        }
    }
}

/// <summary>
/// 총구의 빛줄기를 순식간에 지운다.
/// 가늘어지면서 사라져야 '쏘고 나간' 느낌이 난다. 남아 있으면 레이저 포인터처럼 보인다.
/// </summary>
public class AetherLaserBeamFade : MonoBehaviour
{
    private LineRenderer beam;
    private float life = 0.09f;
    private float startWidth = 1f;

    private void Awake()
    {
        beam = GetComponent<LineRenderer>();
        if (beam != null)
            startWidth = beam.widthMultiplier;
    }

    private void Update()
    {
        if (beam == null)
            return;

        life -= Time.deltaTime;

        if (life <= 0f)
        {
            beam.enabled = false;
            enabled = false;
            return;
        }

        float t = Mathf.Clamp01(life / 0.09f);
        beam.widthMultiplier = startWidth * t;
    }
}

/// <summary>총구 섬광을 빠르게 꺼지게 한다.</summary>
public class AetherFlashLightFade : MonoBehaviour
{
    private Light target;
    private float life = 0.06f;
    private float startIntensity;

    private void Awake()
    {
        target = GetComponent<Light>();
        if (target != null)
            startIntensity = target.intensity;
    }

    private void Update()
    {
        if (target == null)
            return;

        life -= Time.deltaTime;

        if (life <= 0f)
        {
            target.enabled = false;
            enabled = false;
            return;
        }

        target.intensity = startIntensity * Mathf.Clamp01(life / 0.06f);
    }
}

/// <summary>
/// 쏜 쪽을 뒤로 밀었다가 되돌린다.
///
/// 애니메이션이 매 프레임 자리를 다시 잡으므로, 기억해 둔 자리로 되돌리면 안 된다.
/// (플레이어 총에서 그렇게 했다가 총이 손에서 떨어져 나왔다.)
/// LateUpdate에서 지금 자리에 치우침만 얹는다.
/// </summary>
public class AetherRecoilKick : MonoBehaviour
{
    private Vector3 offset;
    private float angle;
    private Vector3 axis = Vector3.right;

    // 지난 프레임에 우리가 써 넣은 값. 그대로 남아 있으면 아무도 손대지 않았다는 뜻이다.
    private bool hasWritten;
    private Vector3 wrotePosition;
    private Quaternion wroteRotation;
    private Vector3 restPosition;
    private Quaternion restRotation;

    private const float Recover = 14f;
    private const float MaxDistance = 0.12f;

    public void Add(Vector3 worldDirection, float distance, float degrees)
    {
        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        Vector3 back = -worldDirection.normalized;

        // localPosition 은 부모 기준이라 방향도 부모 기준으로 바꾼다.
        Vector3 localBack = transform.parent != null
            ? transform.parent.InverseTransformDirection(back)
            : back;

        offset = Vector3.ClampMagnitude(offset + localBack * distance, MaxDistance);

        Vector3 worldAxis = Vector3.Cross(Vector3.up, worldDirection.normalized);
        if (worldAxis.sqrMagnitude < 0.0001f)
            worldAxis = Vector3.right;

        axis = transform.parent != null
            ? transform.parent.InverseTransformDirection(worldAxis.normalized)
            : worldAxis.normalized;

        angle = Mathf.Min(angle + degrees, degrees * 2.5f);
    }

    private void LateUpdate()
    {
        // 애니메이션이 자리를 다시 잡아 주는 뼈라면 지난 프레임 값은 이미 지워져 있다.
        // 터렛 포신처럼 아무도 손대지 않는 오브젝트는 우리가 민 만큼 그대로 남아 있어서,
        // 걷어내지 않으면 쏠 때마다 조금씩 영영 밀려난다. (총구도 같이 밀려난다.)
        //
        // 위치와 회전을 따로 본다. 겨눔(AetherWeaponAim)이 회전만 건드리는데
        // 둘을 묶어서 보면 '누가 손댔다'로 판단해 위치를 영영 안 돌려놓는다.
        // 그래서 쏠 때마다 총이 손에서 밀려나 0.22m 가 0.53m 까지 벌어졌다.
        if (hasWritten)
        {
            if (transform.localPosition == wrotePosition)
                transform.localPosition = restPosition;

            if (transform.localRotation == wroteRotation)
                transform.localRotation = restRotation;
        }

        if (offset.sqrMagnitude < 0.0000001f && Mathf.Abs(angle) < 0.01f)
        {
            hasWritten = false;
            return;
        }

        restPosition = transform.localPosition;
        restRotation = transform.localRotation;

        transform.localPosition += offset;
        transform.localRotation = Quaternion.AngleAxis(-angle, axis) * transform.localRotation;

        wrotePosition = transform.localPosition;
        wroteRotation = transform.localRotation;
        hasWritten = true;

        float t = 1f - Mathf.Exp(-Recover * Time.deltaTime);
        offset = Vector3.Lerp(offset, Vector3.zero, t);
        angle = Mathf.Lerp(angle, 0f, t);
    }
}
