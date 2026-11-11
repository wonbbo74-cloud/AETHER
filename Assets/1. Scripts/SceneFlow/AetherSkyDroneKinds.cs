using UnityEngine;

/// <summary>
/// 눈높이로 날아다니는 드론. 플레이어 시선 높이(땅 위 2.5~4.5m)로 앞쪽 10~30m 를 가로지른다.
/// 땅·건물에 닿지 않게 아래·앞을 살피며 높이를 고친다.
/// </summary>
public class SkyLowFlyer : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 target;
    private float speed;
    private float nextRetarget;

    private void Start()
    {
        speed = Random.Range(9f, 15f);
        velocity = transform.forward * speed;
        PickTarget();
    }

    private void PickTarget()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        Camera cam = Camera.main;
        if (player == null) { target = transform.position; return; }

        // 주로 시선 앞쪽(±70°)을 지나가게 한다.
        Vector3 fwd = cam != null ? cam.transform.forward : player.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * fwd.normalized;
        target = player.position + dir * Random.Range(10f, 30f) + Vector3.up * Random.Range(2.5f, 4.5f);
        nextRetarget = Time.time + Random.Range(3f, 6f);
    }

    private void Update()
    {
        Vector3 to = target - transform.position;
        if (to.magnitude < 3f || Time.time > nextRetarget)
        {
            PickTarget();
            to = target - transform.position;
        }

        Vector3 desired = to.normalized * speed;
        Vector3 oldDir = velocity;
        velocity = Vector3.ClampMagnitude(velocity + Vector3.ClampMagnitude(desired - velocity, 18f * Time.deltaTime), speed);

        // 앞이 막히면 위로, 땅에 너무 가까우면 띄운다.
        if (Physics.Raycast(transform.position, velocity.normalized, 6f, ~0, QueryTriggerInteraction.Ignore))
            velocity += Vector3.up * 25f * Time.deltaTime;
        Vector3 next = transform.position + velocity * Time.deltaTime;
        if (Physics.Raycast(next + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 20f, ~0, QueryTriggerInteraction.Ignore)
            && !ground.collider.transform.IsChildOf(transform) && next.y < ground.point.y + 2f)
            next.y = Mathf.Lerp(next.y, ground.point.y + 2f, 8f * Time.deltaTime);
        transform.position = next;

        float turn = Vector3.SignedAngle(new Vector3(oldDir.x, 0f, oldDir.z), new Vector3(velocity.x, 0f, velocity.z), Vector3.up) / Mathf.Max(0.0001f, Time.deltaTime);
        float bank = Mathf.Clamp(-turn * 0.4f, -35f, 35f);
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, 0f, bank), 6f * Time.deltaTime);
    }
}

/// <summary>천천히 따라오는 드론. 플레이어 뒤·옆 7~11m, 땅 위 4~6m 에서 느릿하게(최대 4m/s) 쫓아오며 플레이어를 바라본다.</summary>
public class SkySlowFollower : MonoBehaviour
{
    private float offsetAngle;
    private float distance;
    private float height;
    private float phase;
    private Vector3 velocity;

    public void Init(float angle)
    {
        offsetAngle = angle;
        distance = Random.Range(7f, 11f);
        height = Random.Range(4f, 6f);
        phase = Random.value * 10f;
    }

    private void Update()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null) return;

        float a = offsetAngle + Mathf.Sin(Time.time * 0.25f + phase) * 25f;
        Vector3 target = player.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * distance
                         + Vector3.up * (height + Mathf.Sin(Time.time * 1.1f + phase) * 0.6f);
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, 2.2f, 4f);

        Vector3 look = player.position + Vector3.up * 1.4f - transform.position;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 2f * Time.deltaTime);
    }
}

/// <summary>
/// 레이저 드론. 플레이어를 6~9m 위에서 따라오며 붉은 레이저를 플레이어 주변 바닥(1.5~5m)에 쏘아 이리저리 훑는다.
/// 수색하는 느낌만 준다(피해 없음). 레이저 끝에 붉은 점과 빛.
/// </summary>
public class SkyLaserScanner : MonoBehaviour
{
    private float angle;
    private float phase;
    private Vector3 velocity;
    private LineRenderer line;
    private Light dot;
    private Transform spot;
    private static Material laserMaterial;

    public void Init(float startAngle)
    {
        angle = startAngle;
        phase = Random.value * 10f;
    }

    private void Start()
    {
        if (laserMaterial == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            laserMaterial = new Material(shader) { name = "AETHER_DroneLaser" };
            if (laserMaterial.HasProperty("_TintColor")) laserMaterial.SetColor("_TintColor", new Color(1f, 0.1f, 0.08f, 0.9f));
            laserMaterial.color = new Color(1f, 0.15f, 0.1f, 1f);
        }

        GameObject lg = new GameObject("Laser");
        lg.transform.SetParent(transform, false);
        line = lg.AddComponent<LineRenderer>();
        line.sharedMaterial = laserMaterial;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.06f;
        line.endWidth = 0.03f;
        line.startColor = new Color(1f, 0.25f, 0.2f, 1f);
        line.endColor = new Color(1f, 0.1f, 0.05f, 0.9f);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        GameObject dg = new GameObject("LaserDot");
        dg.transform.SetParent(transform, false);
        dot = dg.AddComponent<Light>();
        dot.type = LightType.Point;
        dot.color = new Color(1f, 0.15f, 0.1f);
        dot.range = 2.5f;
        dot.intensity = 5f;
        dot.shadows = LightShadows.None;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "LaserSpot";
        quad.transform.SetParent(transform, false);
        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = laserMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spot = quad.transform;
    }

    private void Update()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null || line == null) return;

        // 플레이어 위 6~9m, 4~7m 옆에서 천천히 돌며 따라온다.
        angle += 18f * Time.deltaTime;
        Vector3 hover = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (5.5f + Mathf.Sin(Time.time * 0.5f + phase) * 1.5f)
                        + Vector3.up * (7.5f + Mathf.Sin(Time.time * 0.8f + phase) * 1.5f);
        transform.position = Vector3.SmoothDamp(transform.position, hover, ref velocity, 0.9f, 12f);

        // 레이저 끝: 플레이어 둘레 1.5~5m 를 리사주 곡선처럼 훑는다.
        float t = Time.time + phase;
        Vector3 sweep = new Vector3(Mathf.Sin(t * 1.3f) * 4f, 0f, Mathf.Sin(t * 0.9f + 1.2f) * 4f);
        if (sweep.magnitude < 1.5f) sweep = sweep.normalized * 1.5f;
        Vector3 aim = player.position + sweep;

        Vector3 from = transform.position + Vector3.down * 0.3f;
        Vector3 dir = (aim - from).normalized;
        Vector3 end = aim;
        if (Physics.Raycast(from, dir, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            end = hit.point;
        else if (Physics.Raycast(aim + Vector3.up * 3f, Vector3.down, out RaycastHit g, 10f, ~0, QueryTriggerInteraction.Ignore))
            end = g.point;

        line.SetPosition(0, from);
        line.SetPosition(1, end);
        dot.transform.position = end + Vector3.up * 0.15f;
        dot.intensity = 4f + Mathf.Sin(Time.time * 20f);
        spot.position = end + Vector3.up * 0.03f;
        spot.rotation = Quaternion.Euler(90f, 0f, 0f);
        spot.localScale = Vector3.one * 0.35f / Mathf.Max(0.01f, transform.lossyScale.x);

        Vector3 look = end - transform.position;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look) * Quaternion.Euler(-60f, 0f, 0f), 3f * Time.deltaTime);
    }
}
