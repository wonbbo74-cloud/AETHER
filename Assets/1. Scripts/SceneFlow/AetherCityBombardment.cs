using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 05 폐허된 도시의 전쟁터 분위기: 도시 곳곳에서 폭탄이 터지고, 터진 자리에 연막탄 같은 짙은 연기가 한동안 남는다.
/// 몇 군데 잔해는 계속 타오르며 연기를 피운다.
///
/// - 폭발은 보이는 쪽(시선 앞 ±70°)에 주로 떨어뜨려 놓치지 않게 하고, 가끔은 가까이(18~30m) 터뜨려 긴장을 준다.
/// - 피해는 없다(연출만). 카메라를 흔들지 않는다 — 시점은 흔들지 않는다는 원칙([[camera-feel]]). 대신 빛과 소리로 때린다.
/// - 효과 원본은 Resources/AETHERFX/AetherFxLibrary 가 가리키는 Unity Particle Pack·Vefects 프리팹.
/// </summary>
public class AetherCityBombardment : MonoBehaviour
{
    private const float FirstBlastDelay = 4f;
    // 사용자 요청(10/11): 폭탄 소리를 더 자주, 실제로 터지는 불덩이가 보이게.
    private const float MinInterval = 4f;
    private const float MaxInterval = 9f;
    private const float CloseChance = 0.45f;
    private const float RumbleMinInterval = 2.5f;
    private const float RumbleMaxInterval = 6f;
    private const int MaxSmokeColumns = 4;
    private const int BurningWrecks = 4;

    private AetherFxLibrary library;
    private readonly Queue<GameObject> smokeColumns = new Queue<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("05.") || FindFirstObjectByType<AetherCityBombardment>() != null)
            return;

        // 씬 안에 둔다 — 씬을 떠나면 같이 사라진다.
        GameObject go = new GameObject("AETHER_CityBombardment");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherCityBombardment>();
    }

    private IEnumerator Start()
    {
        library = AetherFxLibrary.Load();
        if (library == null || library.bigExplosion == null)
            yield break;

        // 플레이어가 자리 잡을 때까지 잠깐 기다린다.
        yield return new WaitForSeconds(1f);
        PlaceBurningWrecks();

        yield return new WaitForSeconds(FirstBlastDelay);
        StartCoroutine(DistantRumbles());
        Blast(close: false, inView: true);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(MinInterval, MaxInterval));
            bool close = Random.value < CloseChance;
            Blast(close, inView: Random.value < 0.75f);
        }
    }

    // ------------------------------------------------------------------ 폭발

    private void Blast(bool close, bool inView)
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        Camera cam = Camera.main;
        if (player == null || cam == null)
            return;

        if (!TryPickGroundPoint(cam.transform.position, player.position, cam.transform.forward, close ? 18f : 30f, close ? 30f : 70f, inView, out Vector3 point))
            return;

        float scale = close ? 1.6f : 2.4f;   // 먼 폭발은 크게 해야 읽힌다

        GameObject explosion = Spawn(library.bigExplosion, point, scale);
        if (explosion != null) Destroy(explosion, 7f);

        if (library.dustExplosion != null)
        {
            GameObject dust = Spawn(library.dustExplosion, point + Vector3.up * 0.2f, scale * 1.4f);
            if (dust != null) Destroy(dust, 7f);
        }

        SpawnFireball(point, close ? 1f : 1.5f);
        StartCoroutine(Flash(point + Vector3.up * 3f, close ? 45f : 70f));
        SpawnSmokeColumn(point, close ? 2.6f : 3.4f);

        AetherAudio.PlayAt("explosion", point, close ? 1.4f : 1.1f, close ? 12f : 25f, 260f, 0.12f, Random.Range(0.85f, 1.05f), 40);
        StartCoroutine(Delayed(0.7f, () => AetherAudio.PlayAt("distant_debris", point, 0.8f, 10f, 120f)));
    }

    /// <summary>
    /// 폭발 사이사이 멀리서 들리는 포격음. 터지는 모습은 없이 소리만 — 도시 전체가 폭격당하는 느낌을 준다.
    /// </summary>
    private IEnumerator DistantRumbles()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(RumbleMinInterval, RumbleMaxInterval));
            Transform player = AetherPlayerCharacter.FindActiveTransform();
            if (player == null) continue;

            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(90f, 170f);
            Vector3 at = player.position + new Vector3(ring.x, 10f, ring.y);
            AetherAudio.PlayAt("explosion", at, Random.Range(0.55f, 0.8f), 40f, 320f, 0.1f, Random.Range(0.6f, 0.8f), 90);
            if (Random.value < 0.5f)
                StartCoroutine(Delayed(Random.Range(0.25f, 0.6f), () => AetherAudio.PlayAt("explosion", at + Vector3.right * 12f, 0.45f, 40f, 320f, 0.1f, Random.Range(0.55f, 0.75f), 90)));
        }
    }

    private static Material fireMaterial;

    private Material FireMaterial()
    {
        if (fireMaterial != null)
            return fireMaterial;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null || library == null || library.smokeTexture == null)
            return null;

        fireMaterial = new Material(shader) { name = "AETHER_Fireball", mainTexture = library.smokeTexture };
        if (fireMaterial.HasProperty("_TintColor"))
            fireMaterial.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        return fireMaterial;
    }

    /// <summary>
    /// 눈에 확실히 보이는 불덩이: 밝은 주황 화염이 확 부풀었다 꺼지고, 불똥과 파편이 사방으로 튄다.
    /// 효과 팩 폭발은 멀리서는 안개·어둠에 묻혀 '터졌는지' 알기 어려웠다. 더해지는 빛(Additive)이라 어두운 도시에서도 잘 보인다.
    /// </summary>
    private void SpawnFireball(Vector3 point, float scale)
    {
        Material material = FireMaterial();
        if (material == null)
            return;

        GameObject go = new GameObject("Fireball");
        go.transform.SetParent(transform, false);
        go.transform.position = point + Vector3.up * 1.2f * scale;

        // 1) 화염 덩어리
        ParticleSystem core = go.AddComponent<ParticleSystem>();
        core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = core.main;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 9f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(4f * scale, 8f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.45f, 0.12f, 1f));
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var emission = core.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34), new ParticleSystem.Burst(0.08f, 16) });
        var shape = core.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 1.2f * scale;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var size = core.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.4f)));
        var color = core.colorOverLifetime;
        color.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.3f), new GradientColorKey(new Color(0.55f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.35f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = material;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        // 2) 불똥·파편: 길게 늘어나 날아가며 떨어진다.
        GameObject sparksGo = new GameObject("Sparks");
        sparksGo.transform.SetParent(go.transform, false);
        ParticleSystem sparks = sparksGo.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var sm = sparks.main;
        sm.loop = false;
        sm.duration = 0.2f;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(14f * scale, 30f * scale);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.12f * scale, 0.3f * scale);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.55f, 0.2f, 1f));
        sm.gravityModifier = 1.6f;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.maxParticles = 120;
        var se = sparks.emission;
        se.rateOverTime = 0f;
        se.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
        var ss = sparks.shape;
        ss.shapeType = ParticleSystemShapeType.Hemisphere;
        ss.radius = 0.6f * scale;
        ss.rotation = new Vector3(-90f, 0f, 0f);
        var sr = sparksGo.GetComponent<ParticleSystemRenderer>();
        sr.sharedMaterial = material;
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.06f;
        sr.lengthScale = 2f;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        core.Play(true);
        Destroy(go, 3.5f);
    }

    /// <summary>터진 순간 주변을 주황빛으로 한 번 밝힌다.</summary>
    private IEnumerator Flash(Vector3 position, float range)
    {
        GameObject go = new GameObject("BlastFlash");
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.42f);
        light.range = range;
        light.shadows = LightShadows.None;

        const float duration = 0.7f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - t / duration;
            light.intensity = 9f * k * k;
            yield return null;
        }

        Destroy(go);
    }

    /// <summary>
    /// 연막탄처럼 짙은 흰 연기 기둥. 10초 뿜고 서서히 걷힌다. 동시에 넷까지만.
    /// 효과 팩의 연기는 빛을 받는 재질이라 어두운 도시에서 거의 안 보여서, 직접 만든다.
    /// </summary>
    private void SpawnSmokeColumn(Vector3 point, float scale)
    {
        Material material = SmokeMaterial();
        if (material == null)
            return;

        while (smokeColumns.Count >= MaxSmokeColumns)
        {
            GameObject old = smokeColumns.Dequeue();
            if (old != null) StopEmitting(old);
        }

        GameObject go = new GameObject("SmokeColumn");
        go.transform.SetParent(transform, false);
        go.transform.position = point;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float k = scale / 2.6f;
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * k, 3f * k);
        main.startSize = new ParticleSystem.MinMaxCurve(3.5f * k, 7f * k);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.80f, 0.80f, 0.78f, 0.55f), new Color(0.62f, 0.62f, 0.60f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 9f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });   // 터지는 순간 한 번에 확 퍼진다

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 2f * k;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // 위로

        // 위로 솟다가 바람에 한쪽으로 기운다.
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.4f);

        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1.2f * k;
        limit.dampen = 0.08f;

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.4f));

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.7f), 0f), new GradientColorKey(Color.white, 0.15f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 5f;

        ps.Play();
        smokeColumns.Enqueue(go);
        StartCoroutine(Delayed(10f, () => StopEmitting(go)));
    }

    private static Material smokeMaterial;

    private Material SmokeMaterial()
    {
        if (smokeMaterial != null)
            return smokeMaterial;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null || library == null || library.smokeTexture == null)
            return null;

        smokeMaterial = new Material(shader) { name = "AETHER_SmokeColumn", mainTexture = library.smokeTexture };
        if (smokeMaterial.HasProperty("_TintColor"))
            smokeMaterial.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        return smokeMaterial;
    }

    private static void StopEmitting(GameObject go)
    {
        if (go == null) return;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>())
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        Destroy(go, 15f);   // 남은 연기가 다 걷힌 뒤에
    }

    // ------------------------------------------------------------------ 타는 잔해

    private void PlaceBurningWrecks()
    {
        if (library.burningWreck == null)
            return;

        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null)
            return;

        // 매번 같은 자리에 타오르도록 씨앗을 고정한다.
        Random.State saved = Random.state;
        Random.InitState(5050);
        int placed = 0;
        for (int attempt = 0; attempt < 40 && placed < BurningWrecks; attempt++)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(25f, 75f);
            Vector3 probe = player.position + new Vector3(ring.x, 0f, ring.y);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            GameObject wreck = Spawn(library.burningWreck, hit.position, 1.3f, loop: true);
            if (wreck != null)
                AetherAudio.StartLoop("fire_loop", wreck.transform, 0.55f, 4f, 30f);
            placed++;
        }

        Random.state = saved;
    }

    // ------------------------------------------------------------------ 도움

    private static bool TryPickGroundPoint(Vector3 eye, Vector3 origin, Vector3 forward, float minDistance, float maxDistance, bool inView, out Vector3 point)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 fallback = default;
        bool hasFallback = false;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = inView ? Random.Range(-70f, 70f) : Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 probe = origin + dir * Random.Range(minDistance, maxDistance);

            if (!Physics.Raycast(probe + Vector3.up * 120f, Vector3.down, out RaycastHit hit, 250f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            // 산 위나 지하처럼 엉뚱한 높이는 피한다.
            if (Mathf.Abs(hit.point.y - origin.y) > 30f)
                continue;

            // 보이는 쪽이면 눈에서 폭발 기둥(땅 위 4m)까지 가리는 것이 없어야 한다. 잔해에 가려 안 보이던 문제.
            if (inView)
            {
                Vector3 target = hit.point + Vector3.up * 4f;
                Vector3 toTarget = target - eye;
                if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit block, toTarget.magnitude * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hasFallback) { fallback = hit.point; hasFallback = true; }
                    continue;
                }
            }

            point = hit.point;
            return true;
        }

        // 다 가려져 있으면 가려진 자리라도 쓴다(연기 기둥은 위로 솟아 보인다).
        point = fallback;
        return hasFallback;
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, float scale, bool loop = false)
    {
        if (prefab == null) return null;

        GameObject go = Instantiate(prefab, position, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            // 효과 팩의 몇몇 재질(Dust, SandParticle, ExplosionDistortion)이 불투명 Standard 셰이더로 되어 있어
            // 회색 네모로 보였다. 아지랑이는 끄고, 나머지는 같은 그림으로 반투명 입자 재질을 만들어 바꾼다.
            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null)
            {
                Material mat = psr.sharedMaterial;
                if (mat.name.Contains("Distortion"))
                {
                    ps.gameObject.SetActive(false);
                    continue;
                }

                if (NeedsParticleMaterial(mat.shader))
                    psr.sharedMaterial = ParticleCopy(mat);
            }

            ParticleSystem.MainModule main = ps.main;
            // 크기를 키우면 입자도 같이 커지게. 늘어나는 불똥(Stretch)은 그러면 화면을 가로지르는 막대가 되어 원래 크기로 둔다.
            bool stretched = psr != null && psr.renderMode == ParticleSystemRenderMode.Stretch;
            main.scalingMode = stretched ? ParticleSystemScalingMode.Shape : ParticleSystemScalingMode.Hierarchy;
            if (!loop) main.loop = false;
            ps.Play(false);
        }

        return go;
    }

    private static readonly Dictionary<Material, Material> particleCopies = new Dictionary<Material, Material>();

    private static bool NeedsParticleMaterial(Shader shader)
    {
        if (shader == null || !shader.isSupported) return true;
        string n = shader.name;
        return n == "Standard" || n.StartsWith("Universal Render Pipeline") || n.StartsWith("HDRP");
    }

    private static Material ParticleCopy(Material source)
    {
        if (particleCopies.TryGetValue(source, out Material copy) && copy != null)
            return copy;

        Shader alpha = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (alpha == null) return source;

        copy = new Material(alpha) { name = source.name + " (AETHER particle)" };
        Texture tex = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        if (tex == null && source.HasProperty("_BaseMap")) tex = source.GetTexture("_BaseMap");
        copy.mainTexture = tex;
        if (copy.HasProperty("_TintColor"))
            copy.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        particleCopies[source] = copy;
        return copy;
    }

    private static IEnumerator Delayed(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }
}
