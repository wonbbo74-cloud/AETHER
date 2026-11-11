using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 총알이 맞았을 때의 연출을 한곳에서 만든다.
///
/// 적 총알은 벽에 맞아도 그냥 사라졌고, 플레이어가 맞아도 체력 숫자만 줄어서
/// 어디서 맞는지·맞았는지조차 읽히지 않았다.
///   표면 피격 : 불꽃 튐 + 먼지 연기 + 순간 불빛 + 탄흔 + 짧은 소리
///   플레이어 피격 : 화면 가장자리 번쩍임(체력=붉은색, 실드=푸른색) + 카메라 흔들림 + 둔탁한 소리
///   스침 : 총알이 귀 옆을 지나가면 '휙'
/// 프리팹 없이 코드로 만들어 Resources·씬 어디에도 의존하지 않는다. 오브젝트는 돌려 쓴다.
/// </summary>
public static class AetherHitFX
{
    private const int ImpactPoolSize = 16;
    private const int DecalPoolSize = 48;
    private const float DecalLifetime = 12f;

    private static readonly Color LaserSpark = new Color(1f, 0.25f, 0.18f, 1f);
    private static readonly Color BallisticSpark = new Color(1f, 0.72f, 0.35f, 1f);

    private static GameObject root;
    private static Impact[] impacts;
    private static int nextImpact;
    private static Decal[] decals;
    private static int nextDecal;
    private static Material sparkMaterial;
    private static Material smokeMaterial;
    private static Material decalMaterial;
    private static Mesh quadMesh;

    private static AudioClip impactClip;
    private static AudioClip[] whizClips;
    private static bool audioLoaded;
    private static AudioSource uiVoice;

    private class Impact
    {
        public GameObject go;
        public ParticleSystem sparks;
        public ParticleSystem smoke;
        public Light flash;
        public float flashUntil;
    }

    private class Decal
    {
        public GameObject go;
        public MeshRenderer renderer;
        public MaterialPropertyBlock block;
        public float bornAt;
    }

    // ───────────────────────── 표면 피격 ─────────────────────────

    /// <summary>적 총알이 벽·바닥 등에 맞았을 때.</summary>
    public static void SurfaceImpact(Vector3 point, Vector3 normal, bool laser, Collider surface)
    {
        if (!EnsureBuilt())
            return;

        if (normal.sqrMagnitude < 0.01f)
            normal = Vector3.up;

        Impact fx = impacts[nextImpact];
        nextImpact = (nextImpact + 1) % impacts.Length;

        fx.go.transform.SetPositionAndRotation(point + normal * 0.03f, Quaternion.LookRotation(normal));
        fx.go.SetActive(true);

        ParticleSystem.MainModule sparkMain = fx.sparks.main;
        sparkMain.startColor = laser ? LaserSpark : BallisticSpark;
        fx.sparks.Clear();
        fx.sparks.Play();
        fx.smoke.Clear();
        fx.smoke.Play();

        fx.flash.color = laser ? LaserSpark : BallisticSpark;
        fx.flash.intensity = 2.2f;
        fx.flash.enabled = true;
        fx.flashUntil = Time.time + 0.06f;
        Runner.Ensure();

        if (surface != null && IsStaticSurface(surface))
            PlaceDecal(point, normal);

        PlayImpactSound(point);
    }

    /// <summary>트리거로 닿은 총알의 실제 맞은 자리와 면 방향을 찾아 연출한다.</summary>
    public static void BulletHit(Vector3 position, Vector3 direction, Collider other, bool laser)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        Ray ray = new Ray(position - direction * 1f, direction);
        if (other != null && other.Raycast(ray, out RaycastHit hit, 2.5f))
            SurfaceImpact(hit.point, hit.normal, laser, other);
        else
            SurfaceImpact(position, -direction, laser, null);
    }

    /// <summary>
    /// 총알이 귀 옆을 지나쳤는지 본다. 가장 가까웠던 순간이 2.2m 안이면 한 번 '휙'.
    /// </summary>
    public static void TrackWhiz(Vector3 bulletPosition, ref float previousDistance, ref bool done)
    {
        if (done)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        float d = Vector3.Distance(cam.transform.position, bulletPosition);
        if (d > previousDistance && previousDistance < 2.2f)
        {
            done = true;
            PlayWhiz(bulletPosition);
        }
        previousDistance = d;
    }

    private static bool IsStaticSurface(Collider c)
    {
        if (c.isTrigger) return false;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
        for (Transform t = c.transform; t != null; t = t.parent)
        {
            if (t.CompareTag("Enemy") || t.CompareTag("Player"))
                return false;
        }
        return true;
    }

    private static void PlaceDecal(Vector3 point, Vector3 normal)
    {
        Decal d = decals[nextDecal];
        nextDecal = (nextDecal + 1) % decals.Length;

        d.go.transform.SetPositionAndRotation(point + normal * 0.012f,
            Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        d.go.transform.localScale = Vector3.one * Random.Range(0.16f, 0.24f);
        d.bornAt = Time.time;
        d.block.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        d.renderer.SetPropertyBlock(d.block);
        d.go.SetActive(true);
    }

    // ───────────────────────── 플레이어 피격 ─────────────────────────

    private static float flashAlpha;
    private static float rumbleUntil;
    private static Color flashColor;
    private static float shakeAmount;

    /// <summary>플레이어가 피해를 입었을 때. absorbedByShield 면 실드가 다 막은 것.</summary>
    public static void PlayerDamaged(float damage, bool absorbedByShield)
    {
        if (damage <= 0f)
            return;

        float strength = Mathf.Clamp01(damage / 12f);
        flashColor = absorbedByShield ? new Color(0.25f, 0.6f, 1f, 1f) : new Color(0.75f, 0.03f, 0.02f, 1f);
        flashAlpha = Mathf.Max(flashAlpha, Mathf.Lerp(0.35f, 0.75f, strength));
        shakeAmount = Mathf.Max(shakeAmount, Mathf.Lerp(0.5f, 1.6f, strength));

        Runner.Ensure();

        // 설정의 '진동': 게임패드가 있으면 맞을 때 짧게 떨린다.
        if (AetherSettings.Vibration && UnityEngine.InputSystem.Gamepad.current != null)
        {
            UnityEngine.InputSystem.Gamepad.current.SetMotorSpeeds(0.25f + 0.5f * strength, 0.4f + 0.5f * strength);
            rumbleUntil = Time.unscaledTime + 0.18f;
        }

        LoadAudio();
        if (impactClip != null)
        {
            AudioSource voice = UIVoice();
            voice.pitch = absorbedByShield ? Random.Range(1.5f, 1.7f) : Random.Range(0.42f, 0.5f);
            voice.PlayOneShot(impactClip, (absorbedByShield ? 0.25f : 0.4f) * AetherAudio.Sfx);
        }
    }

    /// <summary>총알이 귀 옆을 스쳐 지나갈 때.</summary>
    public static void PlayWhiz(Vector3 position)
    {
        LoadAudio();
        if (whizClips == null || whizClips.Length == 0)
            return;

        AudioSource voice = UIVoice();
        voice.pitch = Random.Range(2.2f, 2.6f);
        voice.PlayOneShot(whizClips[Random.Range(0, whizClips.Length)], 0.18f * AetherAudio.Sfx);
    }

    /// <summary>화면을 흔든다(폭발 등). 0.5 약하게 ~ 3 크게.</summary>
    public static void Shake(float amount)
    {
        shakeAmount = Mathf.Max(shakeAmount, amount);
        Runner.Ensure();
    }

    /// <summary>
    /// 큰 먼지 구름. 문이 터지거나 벽이 무너질 때처럼 넓은 면에서 한꺼번에 피어오른다.
    /// 다 퍼지면 스스로 지워진다.
    /// </summary>
    public static void DustCloud(Vector3 center, Vector3 size, Vector3 direction)
    {
        if (!EnsureBuilt())
            return;

        GameObject go = new GameObject("AETHER_DustCloud");
        go.transform.position = center;
        go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule m = ps.main;
        m.loop = false;
        m.duration = 0.3f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(0.5f, 0.47f, 0.43f, 0.32f);
        m.gravityModifier = -0.02f;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = 80;
        m.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule e = ps.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, 28, 38) });
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(size.x, size.y, Mathf.Max(0.1f, size.z));
        ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.z = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 0.4f;
        lim.dampen = 0.08f;
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2f));
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = smokeMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    // ───────────────────────── 소리 ─────────────────────────

    private static float lastImpactSound = -99f;

    private static void PlayImpactSound(Vector3 point)
    {
        LoadAudio();
        if (impactClip == null || Time.unscaledTime - lastImpactSound < 0.08f)
            return;

        lastImpactSound = Time.unscaledTime;
        AudioSource.PlayClipAtPoint(impactClip, point, 0.22f * AetherAudio.Sfx);
    }

    private static void LoadAudio()
    {
        if (audioLoaded)
            return;
        audioLoaded = true;

        impactClip = Resources.Load<AudioClip>("AETHERRuntime/AetherGunshot");
        List<AudioClip> list = new List<AudioClip>();
        for (int i = 1; i <= 4; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>("AETHERRuntime/AetherEnemyLaser" + i);
            if (clip != null) list.Add(clip);
        }
        whizClips = list.ToArray();
    }

    private static AudioSource UIVoice()
    {
        if (uiVoice == null)
        {
            EnsureRoot();
            uiVoice = root.AddComponent<AudioSource>();
            uiVoice.playOnAwake = false;
            uiVoice.spatialBlend = 0f;
        }
        return uiVoice;
    }

    // ───────────────────────── 만들기 ─────────────────────────

    private static void EnsureRoot()
    {
        if (root != null)
            return;
        root = new GameObject("AETHER_HitFX");
        Object.DontDestroyOnLoad(root);
    }

    private static bool EnsureBuilt()
    {
        if (impacts != null && impacts[0].go != null)
            return true;

        EnsureRoot();
        BuildMaterials();
        if (sparkMaterial == null || smokeMaterial == null)
            return false;

        impacts = new Impact[ImpactPoolSize];
        for (int i = 0; i < impacts.Length; i++)
            impacts[i] = BuildImpact(i);

        quadMesh = BuildQuad();
        decals = new Decal[DecalPoolSize];
        for (int i = 0; i < decals.Length; i++)
        {
            GameObject go = new GameObject("Decal " + i);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = decalMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            decals[i] = new Decal { go = go, renderer = mr, block = new MaterialPropertyBlock() };
        }

        return true;
    }

    private static Impact BuildImpact(int index)
    {
        GameObject go = new GameObject("Impact " + index);
        go.transform.SetParent(root.transform, false);

        // 불꽃: 표면에서 튀어나와 중력에 떨어지는 짧은 선
        ParticleSystem sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = sparks.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 18) });
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.01f;
        ParticleSystem.ColorOverLifetimeModule col = sparks.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystemRenderer sr = go.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.035f;
        sr.lengthScale = 1.5f;
        sr.sharedMaterial = sparkMaterial;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 먼지 연기: 천천히 퍼지며 옅어지는 회색 뭉치
        GameObject smokeGo = new GameObject("Smoke");
        smokeGo.transform.SetParent(go.transform, false);
        ParticleSystem smoke = smokeGo.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule sm = smoke.main;
        sm.playOnAwake = false;
        sm.loop = false;
        sm.duration = 0.2f;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        sm.startColor = new Color(0.55f, 0.52f, 0.48f, 0.45f);
        sm.gravityModifier = -0.05f;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.maxParticles = 10;
        ParticleSystem.EmissionModule se = smoke.emission;
        se.rateOverTime = 0f;
        se.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });
        ParticleSystem.ShapeModule ss = smoke.shape;
        ss.shapeType = ParticleSystemShapeType.Cone;
        ss.angle = 25f;
        ss.radius = 0.03f;
        ParticleSystem.SizeOverLifetimeModule sol = smoke.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
        ParticleSystem.ColorOverLifetimeModule scol = smoke.colorOverLifetime;
        scol.enabled = true;
        Gradient sg = new Gradient();
        sg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        scol.color = sg;
        ParticleSystemRenderer smr = smokeGo.GetComponent<ParticleSystemRenderer>();
        smr.sharedMaterial = smokeMaterial;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 순간 불빛
        GameObject lightGo = new GameObject("Flash");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        Light flash = lightGo.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 2.5f;
        flash.shadows = LightShadows.None;
        flash.renderMode = LightRenderMode.ForceVertex;
        flash.enabled = false;

        return new Impact { go = go, sparks = sparks, smoke = smoke, flash = flash };
    }

    private static void BuildMaterials()
    {
        if (sparkMaterial != null)
            return;

        Texture2D soft = BuildRadialTexture(64, 2.2f, false);
        Texture2D scorch = BuildRadialTexture(64, 1.2f, true);

        Shader additive = FindShader("Legacy Shaders/Particles/Additive", "Legacy Shaders/Particles/Additive (Soft)", "Particles/Standard Unlit");
        Shader alpha = FindShader("Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit");
        if (additive == null || alpha == null)
            return;

        sparkMaterial = new Material(additive) { name = "AETHER_HitSpark", mainTexture = soft };
        smokeMaterial = new Material(alpha) { name = "AETHER_HitSmoke", mainTexture = soft };
        decalMaterial = new Material(alpha) { name = "AETHER_HitDecal", mainTexture = scorch, renderQueue = 2450 };
    }

    private static Shader FindShader(params string[] names)
    {
        foreach (string n in names)
        {
            Shader s = Shader.Find(n);
            if (s != null) return s;
        }
        return null;
    }

    /// <summary>가운데가 진하고 가장자리로 갈수록 투명한 원. scorch 면 검은 그을음 + 가운데 구멍.</summary>
    private static Texture2D BuildRadialTexture(int size, float falloff, bool scorch)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                if (scorch)
                {
                    float noise = Mathf.PerlinNoise(x * 0.23f, y * 0.23f);
                    a = Mathf.Clamp01(a * (0.6f + noise * 0.8f));
                    float hole = d < 0.12f ? 1f : 0f;
                    float c = Mathf.Lerp(0.08f, 0.02f, hole);
                    tex.SetPixel(x, y, new Color(c, c * 0.95f, c * 0.9f, Mathf.Max(a, hole)));
                }
                else
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private static Mesh BuildQuad()
    {
        Mesh m = new Mesh { name = "AETHER_DecalQuad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ───────────────────────── 매 프레임 ─────────────────────────

    /// <summary>불빛 끄기, 탄흔 옅어지기, 화면 번쩍임·흔들림을 맡는다.</summary>
    private class Runner : MonoBehaviour
    {
        private static Runner instance;
        private Texture2D edgeTexture;
        private CameraShake shake;

        public static void Ensure()
        {
            if (instance != null)
                return;
            EnsureRoot();
            instance = root.AddComponent<Runner>();
        }

        private void Update()
        {
            float now = Time.time;

            if (rumbleUntil > 0f && Time.unscaledTime >= rumbleUntil)
            {
                rumbleUntil = 0f;
                if (UnityEngine.InputSystem.Gamepad.current != null)
                    UnityEngine.InputSystem.Gamepad.current.SetMotorSpeeds(0f, 0f);
            }

            if (impacts != null)
            {
                for (int i = 0; i < impacts.Length; i++)
                {
                    Impact fx = impacts[i];
                    if (fx.flash != null && fx.flash.enabled && now >= fx.flashUntil)
                        fx.flash.enabled = false;
                }
            }

            if (decals != null)
            {
                for (int i = 0; i < decals.Length; i++)
                {
                    Decal d = decals[i];
                    if (d.go == null || !d.go.activeSelf) continue;
                    float age = now - d.bornAt;
                    if (age >= DecalLifetime)
                    {
                        d.go.SetActive(false);
                        continue;
                    }
                    if (age > DecalLifetime - 3f)
                    {
                        float a = Mathf.InverseLerp(DecalLifetime, DecalLifetime - 3f, age) * 0.5f;
                        d.block.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, a));
                        d.renderer.SetPropertyBlock(d.block);
                    }
                }
            }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.8f);
            shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.unscaledDeltaTime * 6f);

            Camera cam = Camera.main;
            if (cam != null && (shake == null || shake.gameObject != cam.gameObject))
            {
                shake = cam.GetComponent<CameraShake>();
                if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            }
        }

        private void OnGUI()
        {
            if (flashAlpha <= 0.01f || Event.current.type != EventType.Repaint)
                return;

            if (edgeTexture == null)
                edgeTexture = BuildEdgeTexture();

            Color prev = GUI.color;
            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edgeTexture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>가운데는 비고 가장자리만 진한 테두리.</summary>
        private static Texture2D BuildEdgeTexture()
        {
            const int w = 128, h = 72;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1) * 2f - 1f;
                    float v = y / (float)(h - 1) * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }

    /// <summary>
    /// 그리기 직전에만 살짝 돌렸다가 그린 뒤 되돌린다. 카메라 스크립트가 잡아 둔 자리를 건드리지 않는다.
    /// </summary>
    private class CameraShake : MonoBehaviour
    {
        private Quaternion saved;
        private bool applied;

        private void OnPreCull()
        {
            if (shakeAmount <= 0.01f)
                return;

            saved = transform.localRotation;
            float t = Time.unscaledTime * 38f;
            Vector3 euler = new Vector3(
                (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t, t) - 0.5f)) * shakeAmount;
            transform.localRotation = saved * Quaternion.Euler(euler);
            applied = true;
        }

        private void OnPostRender()
        {
            if (!applied)
                return;
            transform.localRotation = saved;
            applied = false;
        }
    }
}
