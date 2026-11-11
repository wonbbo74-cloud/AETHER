using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 05 폐허된 도시 하늘에 드론을 많이, 역동적으로 띄운다(따라다니는 드론, 휙휙 날아다니는 드론).
///
/// - 추적조(<see cref="SkyOrbiter"/>) 4대: 플레이어 머리 위 9~15m 를 원을 그리며 돌고, 오르내리며 따라다닌다.
/// - 비행조(<see cref="SkySwooper"/>) 10대: 도시 위 8~30m 를 빠르게(16~28m/s) 가로지르며 크게 선회하고(기울며 돈다),
///   가끔 플레이어 옆을 낮게 스쳐 지나간다.
/// - 공격조 2대: 원래 드론 AI 그대로(보이면 쏜다).
/// 추적조·비행조는 공격하지 않는 연출용이지만 맞으면 떨어진다(체력·보상 그대로).
/// 적 무리 편성이 끝난 뒤(4초)에 띄운다.
/// </summary>
public class AetherSkyDrones : MonoBehaviour
{
    private const int OrbiterCount = 4;
    private const int SwooperCount = 10;
    private const int AttackerCount = 2;
    // 눈높이로 지나가는 드론, 천천히 따라오는 드론, 레이저로 플레이어 주변을 훑는 드론.
    private const int LowFlyerCount = 4;
    private const int SlowFollowerCount = 3;
    private const int LaserCount = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("05.") || FindFirstObjectByType<AetherSkyDrones>() != null)
            return;

        GameObject go = new GameObject("AETHER_SkyDrones");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherSkyDrones>();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(4f);

        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null)
            yield break;

        Drones observer = null, attacker = null;
        foreach (Drones d in FindObjectsByType<Drones>(FindObjectsSortMode.None))
        {
            string n = d.name.ToLowerInvariant();
            if (observer == null && n.Contains("observer")) observer = d;
            else if (attacker == null && n.Contains("attacker")) attacker = d;
        }
        if (observer == null && attacker == null)
            yield break;
        observer ??= attacker;
        attacker ??= observer;

        // 공격조: 원래 AI 그대로, 높은 곳에.
        for (int i = 0; i < AttackerCount; i++)
        {
            Vector3 p = AroundPlayer(player, 18f, 28f, 12f, 16f);
            Drones clone = Instantiate(attacker, p, Quaternion.identity);
            clone.name = attacker.name + "_SkyAttack" + i;
            clone.gameObject.SetActive(true);
            yield return null;
        }

        // 추적조: 머리 위를 돈다.
        for (int i = 0; i < OrbiterCount; i++)
        {
            GameObject g = MakeFlyer(i % 2 == 0 ? observer : attacker, AroundPlayer(player, 8f, 14f, 9f, 15f), "_SkyOrbit" + i);
            SkyOrbiter o = g.AddComponent<SkyOrbiter>();
            o.Init(i * (360f / OrbiterCount));
            yield return null;
        }

        // 비행조: 도시 위를 휙휙.
        for (int i = 0; i < SwooperCount; i++)
        {
            GameObject g = MakeFlyer(i % 3 == 0 ? attacker : observer, AroundPlayer(player, 25f, 60f, 10f, 28f), "_SkySwoop" + i);
            g.AddComponent<SkySwooper>();
            yield return null;
        }

        // 눈높이 비행조: 플레이어 시선 높이로 앞을 가로지른다.
        for (int i = 0; i < LowFlyerCount; i++)
        {
            GameObject g = MakeFlyer(i % 2 == 0 ? observer : attacker, AroundPlayer(player, 15f, 30f, 2.5f, 4.5f), "_SkyLow" + i);
            g.AddComponent<SkyLowFlyer>();
            yield return null;
        }

        // 천천히 따라오는 드론: 뒤·옆에서 느릿하게 쫓아온다.
        for (int i = 0; i < SlowFollowerCount; i++)
        {
            GameObject g = MakeFlyer(observer, AroundPlayer(player, 10f, 16f, 4f, 6f), "_SkySlow" + i);
            g.AddComponent<SkySlowFollower>().Init(i * (360f / SlowFollowerCount) + 150f);
            yield return null;
        }

        // 레이저 드론: 따라오며 붉은 레이저로 플레이어 주변 바닥을 훑는다(피해 없음).
        for (int i = 0; i < LaserCount; i++)
        {
            GameObject g = MakeFlyer(i % 2 == 0 ? attacker : observer, AroundPlayer(player, 10f, 14f, 6f, 9f), "_SkyLaser" + i);
            g.AddComponent<SkyLaserScanner>().Init(i * (360f / LaserCount));
            yield return null;
        }
    }

    private static Vector3 AroundPlayer(Transform player, float minR, float maxR, float minH, float maxH)
    {
        Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(minR, maxR);
        return new Vector3(player.position.x + ring.x, player.position.y + Random.Range(minH, maxH), player.position.z + ring.y);
    }

    /// <summary>드론 모양만 복제한다(움직임은 우리가 맡는다). 맞으면 떨어지도록 체력은 남긴다.</summary>
    private static GameObject MakeFlyer(Drones source, Vector3 position, string suffix)
    {
        // 꺼 둔 그릇 안에 복제해서 AI 가 깨어나기(Awake) 전에 떼어 낸다.
        GameObject holder = new GameObject("FlyerHolder");
        holder.SetActive(false);
        GameObject g = Instantiate(source.gameObject, position, Quaternion.identity, holder.transform);

        g.name = source.name + suffix;
        foreach (MonoBehaviour mb in g.GetComponents<MonoBehaviour>())
        {
            if (mb is Drones || mb.GetType().Name.EndsWith("Combat"))
                DestroyImmediate(mb);
        }
        foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;

        g.SetActive(true);
        g.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(g, SceneManager.GetActiveScene());
        Destroy(holder);
        AetherDroneHighlight.Apply(g.transform);
        return g;
    }
}

/// <summary>플레이어 머리 위를 원을 그리며 돌고, 오르내리며 따라다닌다. 돌아가는 방향으로 기운다.</summary>
public class SkyOrbiter : MonoBehaviour
{
    private float angle;
    private float radius;
    private float height;
    private float angularSpeed;
    private float bobPhase;
    private Vector3 velocity;

    public void Init(float startAngle)
    {
        angle = startAngle;
        radius = Random.Range(7f, 13f);
        height = Random.Range(9f, 15f);
        angularSpeed = Random.Range(35f, 60f) * (Random.value < 0.5f ? 1f : -1f);
        bobPhase = Random.value * 10f;
    }

    private void Update()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null) return;

        // 반경·높이도 천천히 바뀌어 같은 원만 돌지 않는다.
        angle += angularSpeed * Time.deltaTime;
        float r = radius + Mathf.Sin(Time.time * 0.6f + bobPhase) * 3f;
        float h = height + Mathf.Sin(Time.time * 0.9f + bobPhase * 2f) * 2.5f;
        Vector3 target = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * r + Vector3.up * h;

        Vector3 next = Vector3.SmoothDamp(transform.position, target, ref velocity, 0.6f, 30f);
        Vector3 move = next - transform.position;
        transform.position = next;

        if (move.sqrMagnitude > 0.0001f)
        {
            Vector3 flat = new Vector3(move.x, 0f, move.z);
            float bank = -Mathf.Sign(angularSpeed) * Mathf.Clamp(flat.magnitude / Time.deltaTime, 0f, 20f) * 1.4f;
            Quaternion look = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0f, 0f, bank), 6f * Time.deltaTime);
        }
    }
}

/// <summary>
/// 도시 위를 빠르게 가로지르는 드론. 먼 곳을 골라 날아가며 크게 선회하고(기울며), 도착하면 다음 곳으로.
/// 다섯 번에 한 번쯤은 플레이어 옆을 낮게 스쳐 지나간다.
/// </summary>
public class SkySwooper : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 target;
    private float speed;
    private float nextRetarget;

    private void Start()
    {
        speed = Random.Range(16f, 28f);
        velocity = transform.forward * speed;
        PickTarget();
    }

    private void PickTarget()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        Vector3 center = player != null ? player.position : transform.position;

        if (player != null && Random.value < 0.2f)
        {
            // 플레이어 옆 6~10m 를 낮게(5~7m) 스쳐 지나가는 지점. 그 너머로 빠져나간다.
            Vector3 side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(6f, 10f);
            target = center + side + Vector3.up * Random.Range(5f, 7f);
        }
        else
        {
            Vector2 ring = Random.insideUnitCircle * 70f;
            target = new Vector3(center.x + ring.x, center.y + Random.Range(10f, 30f), center.z + ring.y);
        }
        nextRetarget = Time.time + Random.Range(4f, 8f);
    }

    private void Update()
    {
        Vector3 to = target - transform.position;
        if (to.magnitude < 6f || Time.time > nextRetarget)
        {
            PickTarget();
            to = target - transform.position;
        }

        // 원하는 방향으로 서서히 꺾는다(급회전 대신 크게 도는 궤적).
        Vector3 desired = to.normalized * speed;
        Vector3 steer = Vector3.ClampMagnitude(desired - velocity, 22f * Time.deltaTime);
        Vector3 oldDir = velocity;
        velocity = Vector3.ClampMagnitude(velocity + steer, speed);

        Vector3 next = transform.position + velocity * Time.deltaTime;
        // 건물에 박지 않게: 앞이 막히면 위로 솟는다.
        if (Physics.Raycast(transform.position, velocity.normalized, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
            velocity += Vector3.up * 30f * Time.deltaTime;
        transform.position = next;

        // 도는 쪽으로 기운다.
        float turn = Vector3.SignedAngle(new Vector3(oldDir.x, 0f, oldDir.z), new Vector3(velocity.x, 0f, velocity.z), Vector3.up) / Mathf.Max(0.0001f, Time.deltaTime);
        float bank = Mathf.Clamp(-turn * 0.5f, -45f, 45f);
        if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, 0f, bank), 5f * Time.deltaTime);
    }
}
