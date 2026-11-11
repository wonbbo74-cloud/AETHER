using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 06 연구소 메인 장소: 바닥을 따라 낮게 깔린 연기가 계속 흘러 다닌다. 버려진 연구소의 삭막한 분위기.
///
/// 플레이어가 걸어 다닐 수 있는 곳(길찾기 바닥, NavMesh)만 덮는다. 06 은 폭 19m 남짓의 긴 복도형이라
/// 8m 칸마다 걸을 수 있는 바닥이 플레이어 발 높이에 있는 칸에만 연기 상자를 둔다(위층·천장에는 두지 않는다).
/// 연기는 무릎 높이에서 천천히 흐르고 커지며 옅어진다. 시점이나 이동에는 영향이 없다.
/// </summary>
public class AetherGroundSmoke : MonoBehaviour
{
    private const float Cell = 8f;
    private const int MaxEmitters = 70;

    private bool changedSoftParticles;
    private bool previousSoftParticles;

    private static Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.StartsWith("06.") || FindFirstObjectByType<AetherGroundSmoke>() != null)
            return;

        GameObject go = new GameObject("AETHER_GroundSmoke");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherGroundSmoke>();
    }

    private IEnumerator Start()
    {
        // 플레이어가 자리 잡을 때까지 기다린다.
        Transform player = null;
        for (int i = 0; i < 60 && player == null; i++)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            if (player == null) yield return null;
        }

        if (player == null || !EnsureMaterial())
            yield break;

        // 연기가 바닥에 닿는 곳이 칼로 자른 듯한 직선으로 보이지 않게(부드러운 입자). 이 씬에서만 켠다.
        previousSoftParticles = QualitySettings.softParticles;
        QualitySettings.softParticles = true;
        changedSoftParticles = true;
        if (Camera.main != null)
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        float feetY = player.position.y;
        Bounds walkable = WalkableBounds(player.position);
        int count = 0;
        for (float x = walkable.min.x + Cell * 0.5f; x <= walkable.max.x && count < MaxEmitters; x += Cell)
        {
            for (float z = walkable.min.z + Cell * 0.5f; z <= walkable.max.z && count < MaxEmitters; z += Cell)
            {
                // 이 칸 가까이에 걸을 수 있는 바닥이 플레이어 발 높이(±1.2m)에 있어야 한다.
                if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(x, feetY, z), out UnityEngine.AI.NavMeshHit hit, Cell * 0.6f, UnityEngine.AI.NavMesh.AllAreas))
                    continue;
                if (Mathf.Abs(hit.position.y - feetY) > 1.2f)
                    continue;

                CreateEmitter(hit.position);
                count++;
            }
        }
    }

    private void OnDestroy()
    {
        if (changedSoftParticles)
            QualitySettings.softParticles = previousSoftParticles;
    }

    /// <summary>길찾기 바닥(NavMesh) 전체의 범위. 없으면 플레이어 주변 60m.</summary>
    private static Bounds WalkableBounds(Vector3 around)
    {
        UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0)
            return new Bounds(around, new Vector3(120f, 4f, 120f));

        Bounds bounds = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (Vector3 v in tri.vertices)
            bounds.Encapsulate(v);
        return bounds;
    }

    private void CreateEmitter(Vector3 floor)
    {
        GameObject go = new GameObject("FloorSmoke");
        go.transform.SetParent(transform, false);
        go.transform.position = floor + Vector3.up * 0.45f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.prewarm = true;                       // 들어서는 순간부터 깔려 있게
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 5.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.72f, 0.74f, 0.74f, 0.55f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 2.4f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Cell, Cell, 0.4f);   // 아래 회전으로 바닥에 눕는다
        shape.rotation = new Vector3(90f, 0f, 0f);   // 위로 뿜지 않고 옆으로 흐르게

        // 한쪽으로 느리게 흘러가며 위로 조금 들뜬다(무릎 높이 이상은 안 올라간다).
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.03f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.1f);

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f));

        // 천천히 나타났다가 천천히 사라진다.
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;   // 눕힌 판은 눈높이에서 보면 실처럼 얇아져 안 보였다. 카메라를 보는 판을 무릎 높이에 둔다
        renderer.sortingFudge = 10f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
    }

    private static bool EnsureMaterial()
    {
        if (material != null)
            return true;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return false;

        AetherFxLibrary library = AetherFxLibrary.Load();
        Texture2D texture = library != null && library.smokeTexture != null ? library.smokeTexture : SoftCircle();
        material = new Material(shader) { name = "AETHER_FloorSmoke", mainTexture = texture };
        if (material.HasProperty("_InvFade"))
            material.SetFloat("_InvFade", 0.6f);   // 바닥과 닿는 곳을 넓게 흐린다
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        return true;
    }

    private static Texture2D SoftCircle()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f) * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
