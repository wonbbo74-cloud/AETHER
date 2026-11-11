using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬마다 제각각인 환경광·안개·후처리를 한 분위기로 맞춘다.
///
/// 04~07(사용자가 실제로 쓰는 구간)은 '어둡고 채도 낮은, 살짝 따뜻한 회색' 하나의 톤으로 묶는다.
///   05.폐허된 도시 : 실외. 회갈색 안개(Exponential), 흐린 하늘빛 환경광
///   04·06·07       : 실내. 같은 색 계열의 짙은 안개와 낮은 환경광, 조명은 씬에 배치된 불빛이 주연
/// 색 보정·블룸·AO·비네트·필름 그레인은 씬별 PostProcessProfile(Resources/AETHERRuntime/Mood)에 있다.
///
/// 플레이어 카메라가 씬마다 다르고 캐릭터 선택에 따라 런타임에 생기기도 해서,
/// 실행 중에 메인 카메라를 찾아 PostProcessLayer 를 붙이고 전역 볼륨을 깐다.
/// 같은 값을 에디터에서 ApplyToOpenScene() 으로 씬에 구워 두면 편집 화면도 똑같이 보인다.
///
/// 08~11 은 예전 값(Exp2 0.048)을 그대로 쓴다.
/// </summary>
public class AetherAtmosphere : MonoBehaviour
{
    public struct Mood
    {
        public FogMode fogMode;
        public float fogDensity;
        public Color fogColor;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public float ambientIntensity;
        public AudioReverbPreset reverb;
    }

    private const string MoodAssetsPath = "AETHERRuntime/Mood/AetherMoodAssets";

    // 실내 공통 색. 05 안개색을 어둡게 내린 같은 계열이다.
    private static readonly Color IndoorFog = new Color(0.13f, 0.125f, 0.115f, 1f);
    private static readonly Color IndoorSky = new Color(0.30f, 0.29f, 0.27f, 1f);
    private static readonly Color IndoorEquator = new Color(0.20f, 0.19f, 0.175f, 1f);
    private static readonly Color IndoorGround = new Color(0.08f, 0.075f, 0.07f, 1f);

    public static bool TryGetMood(string sceneName, out Mood mood)
    {
        mood = default;
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (sceneName.StartsWith("05.", System.StringComparison.Ordinal))
        {
            mood = new Mood
            {
                fogMode = FogMode.Exponential,
                fogDensity = 0.028f, // 0.038 은 60m 너머 고층 건물 실루엣을 다 지워 도시가 작아 보였다
                fogColor = new Color(0.30f, 0.295f, 0.28f, 1f),
                ambientSky = new Color(0.40f, 0.39f, 0.37f, 1f),
                ambientEquator = new Color(0.27f, 0.26f, 0.24f, 1f),
                ambientGround = new Color(0.12f, 0.11f, 0.10f, 1f),
                ambientIntensity = 0.95f,
                reverb = AudioReverbPreset.City,
            };
            return true;
        }

        if (sceneName.StartsWith("04.", System.StringComparison.Ordinal))
        {
            // 2026-10-08 사용자 "벙커는 좀 더 밝게": 안개를 걷고 환경광을 올린다(노출·lift 는 04 프로필).
            mood = Indoor(0.020f, 1.05f);
            mood.reverb = AudioReverbPreset.Room;
            return true;
        }

        if (sceneName.StartsWith("06.", System.StringComparison.Ordinal))
        {
            // 배치된 불빛이 셋뿐이라 환경광을 조금 더 남긴다.
            mood = Indoor(0.028f, 1.25f);
            mood.reverb = AudioReverbPreset.Hallway;
            return true;
        }

        if (sceneName.StartsWith("07.", System.StringComparison.Ordinal))
        {
            mood = Indoor(0.030f, 1.1f);
            mood.reverb = AudioReverbPreset.Room;
            return true;
        }

        if (sceneName.StartsWith("08.", System.StringComparison.Ordinal) ||
            sceneName.StartsWith("09.", System.StringComparison.Ordinal) ||
            sceneName.StartsWith("10.", System.StringComparison.Ordinal) ||
            sceneName.StartsWith("11.", System.StringComparison.Ordinal))
        {
            mood = new Mood
            {
                fogMode = FogMode.ExponentialSquared,
                fogDensity = 0.048f,
                fogColor = new Color(0.5f, 0.5f, 0.5f, 1f),
                ambientSky = new Color(0.212f, 0.227f, 0.259f, 1f),
                ambientEquator = new Color(0.114f, 0.125f, 0.133f, 1f),
                ambientGround = new Color(0.047f, 0.043f, 0.035f, 1f),
                ambientIntensity = 1f,
                reverb = AudioReverbPreset.Off,
            };
            return true;
        }

        return false;
    }

    private static Mood Indoor(float fogDensity, float ambientIntensity)
    {
        return new Mood
        {
            fogMode = FogMode.Exponential,
            fogDensity = fogDensity,
            fogColor = IndoorFog,
            ambientSky = IndoorSky,
            ambientEquator = IndoorEquator,
            ambientGround = IndoorGround,
            ambientIntensity = ambientIntensity,
        };
    }

    private static bool subscribed;
    private static AetherMoodAssets assets;
    private static bool assetsLoaded;

    private string appliedScene;
    private Camera lastCamera;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;

            GameObject host = new GameObject("AetherAtmosphere_Runtime");
            DontDestroyOnLoad(host);
            host.AddComponent<AetherAtmosphere>();
        }

        ApplyRenderSettings(SceneManager.GetActiveScene().name);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyRenderSettings(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        // 캐릭터 선택에 따라 카메라가 늦게 생기거나 바뀌므로 잠깐씩 다시 본다.
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + 0.5f;

        string sceneName = SceneManager.GetActiveScene().name;
        Camera cam = Camera.main;
        if (sceneName == appliedScene && cam == lastCamera)
            return;

        if (!TryGetMood(sceneName, out Mood mood))
        {
            appliedScene = sceneName;
            lastCamera = cam;
            return;
        }

        PostProcessProfile profile = LoadAssets()?.FindProfile(sceneName);
        if (profile != null)
            EnsureGlobalVolume(profile);

        if (cam != null)
            PrepareCamera(cam, mood, profile != null);

        ApplyReverb(mood.reverb);

        appliedScene = sceneName;
        lastCamera = cam;
    }

    private static AetherMoodAssets LoadAssets()
    {
        if (!assetsLoaded)
        {
            assets = Resources.Load<AetherMoodAssets>(MoodAssetsPath);
            assetsLoaded = true;
        }

        return assets;
    }

    public static void ApplyRenderSettings(string sceneName)
    {
        if (!TryGetMood(sceneName, out Mood mood))
            return;

        RenderSettings.fog = true;
        RenderSettings.fogMode = mood.fogMode;
        RenderSettings.fogDensity = mood.fogDensity;
        RenderSettings.fogColor = mood.fogColor;

        // Trilight로 두면 스카이박스가 무엇이든 하늘/수평/바닥 색을 직접 지정할 수 있어
        // 씬마다 밝기가 튀지 않는다.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = mood.ambientSky;
        RenderSettings.ambientEquatorColor = mood.ambientEquator;
        RenderSettings.ambientGroundColor = mood.ambientGround;
        RenderSettings.ambientIntensity = mood.ambientIntensity;

        LiftDarkLightProbes(mood);
    }

    /// <summary>
    /// 06 은 불을 다 끈 채 구운 라이트 프로브(밝기 0.02~0.04)가 남아 있어서, 라이트맵이 없는
    /// 사무 기기·플레이어·적이 새까맣게 나왔다. 너무 어두운 프로브에만 환경광만큼 더해 준다.
    /// 씬 파일은 그대로고 실행 중 값만 바뀐다(씬을 다시 열면 원래대로).
    /// </summary>
    private static void LiftDarkLightProbes(Mood mood)
    {
        LightProbes probes = LightmapSettings.lightProbes;
        if (probes == null || probes.count == 0)
            return;

        Color fill = mood.ambientEquator * mood.ambientIntensity;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            fill = fill.linear;

        UnityEngine.Rendering.SphericalHarmonicsL2[] baked = probes.bakedProbes;
        Vector3[] up = { Vector3.up };
        Color[] sample = new Color[1];
        bool changed = false;

        for (int i = 0; i < baked.Length; i++)
        {
            baked[i].Evaluate(up, sample);
            if (sample[0].grayscale >= 0.08f)
                continue;

            baked[i].AddAmbientLight(fill);
            changed = true;
        }

        if (changed)
            probes.bakedProbes = baked;
    }

    /// <summary>
    /// 카메라 배경을 안개색으로 칠하고(하늘이 안개를 안 받아 납작하게 뜨는 것 방지) 후처리를 붙인다.
    /// </summary>
    public static void PrepareCamera(Camera cam, Mood mood, bool withPostProcess)
    {
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = mood.fogColor;
        cam.allowHDR = true;

        if (!withPostProcess)
            return;

        PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            AetherMoodAssets moodAssets = LoadAssets();
            if (moodAssets == null || moodAssets.postProcessResources == null)
                return;

            layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(moodAssets.postProcessResources);
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
        }

        layer.volumeTrigger = cam.transform;
        layer.volumeLayer |= 1 << 0; // Default
        layer.enabled = true;
    }

    /// <summary>
    /// 듣는 귀(AudioListener)에 잔향을 붙인다. 총성이 실외에선 건물에 메아리치고
    /// 실내에선 방 안에서 울려야 공간이 느껴진다. 귀를 따라다니므로 늘 범위 안이다.
    /// </summary>
    private static void ApplyReverb(AudioReverbPreset preset)
    {
        AudioListener listener = FindFirstObjectByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listener == null)
            return;

        AudioReverbZone zone = listener.GetComponent<AudioReverbZone>();
        if (preset == AudioReverbPreset.Off)
        {
            if (zone != null) zone.enabled = false;
            return;
        }

        if (zone == null)
            zone = listener.gameObject.AddComponent<AudioReverbZone>();

        zone.enabled = true;
        zone.minDistance = 500f;
        zone.maxDistance = 600f;
        zone.reverbPreset = preset;
    }

    /// <summary>씬에 이 프로필을 쓰는 전역 볼륨이 없으면 하나 깐다.</summary>
    public static PostProcessVolume EnsureGlobalVolume(PostProcessProfile profile)
    {
        PostProcessVolume[] volumes = FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i].isGlobal && volumes[i].sharedProfile == profile && volumes[i].gameObject.layer == 0)
                return volumes[i];
        }

        GameObject go = new GameObject("AETHER_MoodVolume");
        go.layer = 0;
        PostProcessVolume volume = go.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        return volume;
    }
}
