using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AETHER 효과음 중심.
///
/// - 소리는 Resources/AETHERAudio 의 "이름" 또는 "이름_1, 이름_2 …" 묶음으로 부른다. 묶음은 같은 변형이 연달아 나오지 않게 고른다.
/// - 모든 소리에 설정 화면의 효과음 음량(AetherSettings.SfxVolume)을 곱한다. (마스터는 AudioListener 가 맡는다)
/// - 3D 소리는 거리에 따라 고음이 깎이고(공기 흡수), 벽에 가리면 먹먹해진다(차폐). 실내 울림은 AetherAtmosphere 의 ReverbZone.
///
/// 소리를 만드는 곳은 Editor/AetherSoundForge.cs. 소리를 붙이는 곳은 이 파일 아래쪽의 발걸음·로봇·환경음 부품과 각 게임 스크립트.
/// </summary>
public static class AetherAudio
{
    private const string Folder = "AETHERAudio/";

    private static readonly Dictionary<string, AudioClip[]> banks = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();

    /// <summary>설정의 효과음 음량(0~1).</summary>
    public static float Sfx
    {
        get { AetherSettings.Load(); return Mathf.Clamp01(AetherSettings.SfxVolume / 100f); }
    }

    /// <summary>설정의 배경음 음량(0~1).</summary>
    public static float Bgm
    {
        get { AetherSettings.Load(); return Mathf.Clamp01(AetherSettings.BgmVolume / 100f); }
    }

    public static AudioClip[] Bank(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (banks.TryGetValue(name, out AudioClip[] found))
            return found;

        var list = new List<AudioClip>();
        AudioClip single = Resources.Load<AudioClip>(Folder + name);
        if (single != null)
            list.Add(single);

        for (int i = 1; i <= 16; i++)
        {
            AudioClip c = Resources.Load<AudioClip>($"{Folder}{name}_{i}");
            if (c == null)
                break;
            list.Add(c);
        }

        AudioClip[] arr = list.Count > 0 ? list.ToArray() : null;
        banks[name] = arr;
        return arr;
    }

    /// <summary>묶음에서 하나를 고른다. 바로 전에 쓴 것은 피한다.</summary>
    public static AudioClip Clip(string name)
    {
        AudioClip[] b = Bank(name);
        if (b == null || b.Length == 0)
            return null;
        if (b.Length == 1)
            return b[0];

        lastPick.TryGetValue(name, out int last);
        int i = Random.Range(0, b.Length - 1);
        if (i >= last) i++;
        lastPick[name] = i;
        return b[i];
    }

    /// <summary>화면(2D) 소리: UI, 내 발소리, 내 숨소리 같은 것.</summary>
    public static void Play2D(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0.03f)
    {
        AudioClip c = Clip(name);
        if (c == null) return;
        AetherAudioRunner.Ensure().Play(c, Vector3.zero, volume, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)), 0f, 1f, 30f, false, 64);
    }

    /// <summary>자리에서 나는 3D 소리.</summary>
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float minDistance = 1.5f, float maxDistance = 35f,
        float pitchJitter = 0.05f, float pitch = 1f, int priority = 128)
    {
        AudioClip c = Clip(name);
        if (c == null) return;
        AetherAudioRunner.Ensure().Play(c, position, volume, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)), 1f, minDistance, maxDistance, true, priority);
    }

    /// <summary>계속 도는 소리(드론, 불, 형광등). 따라다닐 대상 아래에 붙는다.</summary>
    public static AetherAudioLoop StartLoop(string name, Transform parent, float volume, float minDistance = 1.5f, float maxDistance = 25f, bool spatial = true, bool music = false)
    {
        AudioClip c = Clip(name);
        if (c == null || parent == null) return null;

        var go = new GameObject("AETHER_Loop_" + name);
        go.transform.SetParent(parent, false);
        var loop = go.AddComponent<AetherAudioLoop>();
        loop.Init(c, volume, minDistance, maxDistance, spatial);
        loop.Music = music;
        return loop;
    }

    private static readonly Dictionary<int, AetherAudioLoop> shieldLoops = new Dictionary<int, AetherAudioLoop>();
    private static readonly Dictionary<int, bool> shieldStates = new Dictionary<int, bool>();

    /// <summary>
    /// 실드를 켜고 끌 때 '위잉' 소리, 켜져 있는 동안 낮은 웅웅거림. 매 프레임 불러도 되고 바뀔 때만 소리가 난다.
    /// </summary>
    public static void Shield(Component owner, bool active)
    {
        if (owner == null) return;
        int id = owner.GetInstanceID();
        shieldStates.TryGetValue(id, out bool was);
        if (was == active) return;
        shieldStates[id] = active;

        Play2D(active ? "shield_on" : "shield_off", 0.5f, 1f, 0.03f);
        if (shieldLoops.TryGetValue(id, out AetherAudioLoop loop) && loop != null) loop.Stop();
        shieldLoops.Remove(id);
        if (active)
            shieldLoops[id] = StartLoop("shield_hum", owner.transform, 0.3f, 1f, 10f, false);
    }

    private static AetherAudioLoop holdLoop;
    private static bool holdDone;

    /// <summary>
    /// 길게 누르는 상호작용(종이 읽기·엘리베이터·문)의 진행 소리. 누르는 동안 음이 올라가고, 다 차면 '딸깍' 한 번.
    /// </summary>
    public static void HoldProgress(float progress)
    {
        if (progress <= 0.01f) { HoldEnd(); return; }
        if (holdDone) { if (progress < 0.5f) holdDone = false; else return; }

        if (holdLoop == null)
            holdLoop = StartLoop("hold_loop", AetherAudioRunner.Ensure().transform, 0.28f, 1f, 10f, false);
        if (holdLoop != null)
            holdLoop.Pitch = Mathf.Lerp(0.8f, 1.6f, progress);

        if (progress >= 0.995f)
        {
            HoldEnd();
            holdDone = true;
            Play2D("ui_confirm", 0.45f, 1f, 0f);
        }
    }

    /// <summary>손을 떼거나 안내가 사라지면 진행 소리를 멈춘다.</summary>
    public static void HoldEnd()
    {
        if (holdLoop != null) holdLoop.Stop();
        holdLoop = null;
    }

    /// <summary>바닥 재질. 발걸음·착지 소리 이름에 붙는다.</summary>
    public static string SurfaceAt(Collider col)
    {
        return AetherSurface.Classify(col);
    }
}

// =========================================================================
// 재생 장치: 미리 만든 소리통을 돌려 쓴다.
// =========================================================================
public class AetherAudioRunner : MonoBehaviour
{
    private const int VoiceCount = 32;

    private static AetherAudioRunner instance;

    private class Voice
    {
        public AudioSource src;
        public AudioLowPassFilter lpf;
        public float baseVolume;
        public float startTime;
        public int priority;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private static AudioListener listener;

    public static AetherAudioRunner Ensure()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("AETHER_Audio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AetherAudioRunner>();
        instance.Build();
        return instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        Ensure();
    }

    private void Build()
    {
        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var v = new Voice
            {
                src = go.AddComponent<AudioSource>(),
                lpf = go.AddComponent<AudioLowPassFilter>()
            };
            v.src.playOnAwake = false;
            v.src.dopplerLevel = 0f;
            v.src.rolloffMode = AudioRolloffMode.Logarithmic;
            v.lpf.cutoffFrequency = 22000f;
            voices.Add(v);
        }

        // 일시정지 메뉴에서 처음으로 등으로 씬을 떠나면 Resume 이 안 불려 소리가 멈춘 채 남을 수 있다. 새 씬에선 항상 푼다.
        SceneManager.sceneLoaded += (s, m) => { AudioListener.pause = false; listener = null; AetherAmbience.OnSceneLoaded(s); AetherAudioAttach.OnSceneLoaded(s); };
        AetherAmbience.OnSceneLoaded(SceneManager.GetActiveScene());
        AetherAudioAttach.OnSceneLoaded(SceneManager.GetActiveScene());
    }

    public static Transform Listener
    {
        get
        {
            if (listener == null || !listener.isActiveAndEnabled)
            {
                listener = null;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (l.isActiveAndEnabled) { listener = l; break; }
            }
            return listener != null ? listener.transform : null;
        }
    }

    public void Play(AudioClip clip, Vector3 pos, float volume, float pitch, float spatial, float minD, float maxD, bool occlude, int priority)
    {
        // 너무 멀어서 들리지 않을 소리는 아예 내지 않는다(소리통 낭비 방지).
        Transform lis = Listener;
        float dist = 0f;
        if (spatial > 0.5f && lis != null)
        {
            dist = Vector3.Distance(lis.position, pos);
            if (dist > maxD * 1.05f) return;
        }

        Voice v = Pick(priority);
        if (v == null) return;

        v.src.Stop();
        v.src.clip = clip;
        v.src.transform.position = pos;
        v.src.spatialBlend = spatial;
        v.src.minDistance = minD;
        v.src.maxDistance = maxD;
        v.src.pitch = pitch;
        v.src.priority = priority;
        v.baseVolume = volume;
        v.priority = priority;
        v.startTime = Time.unscaledTime;
        v.src.ignoreListenerPause = spatial < 0.5f;

        float occ = 1f;
        float cutoff = 22000f;
        if (spatial > 0.5f && lis != null)
        {
            cutoff = AetherOcclusion.AirCutoff(dist, maxD);
            if (occlude && AetherOcclusion.Blocked(lis.position, pos))
            {
                cutoff = Mathf.Min(cutoff, 1100f);
                occ = 0.55f;
            }
        }
        v.lpf.cutoffFrequency = cutoff;
        v.src.volume = Mathf.Clamp01(volume * occ * AetherAudio.Sfx);
        v.src.Play();
    }

    private Voice Pick(int priority)
    {
        Voice oldest = null;
        foreach (var v in voices)
        {
            if (!v.src.isPlaying) return v;
            // 중요도가 같거나 낮은(숫자 큰) 것 중 가장 오래된 것을 뺏는다.
            if (v.priority >= priority && (oldest == null || v.startTime < oldest.startTime))
                oldest = v;
        }
        return oldest;
    }

    /// <summary>설정에서 효과음 음량을 바꾸면 이미 울리고 있는 소리도 따라간다.</summary>
    public static void OnVolumeChanged()
    {
        if (instance == null) return;
        foreach (var v in instance.voices)
            if (v.src.isPlaying)
                v.src.volume = Mathf.Clamp01(v.baseVolume * AetherAudio.Sfx);
    }

    private float nextWire;

    private void Update()
    {
        AetherAmbience.Tick();
        AetherHeartbeat.Tick();

        // 화면(상점·제작·일시정지 …)이 실행 중에 만들어지므로 가끔 새 버튼을 찾아 소리를 붙인다.
        if (Time.unscaledTime >= nextWire)
        {
            nextWire = Time.unscaledTime + 1f;
            AetherUiButtonSound.WireAll();
        }
    }
}

// =========================================================================
// 차폐·공기 흡수
// =========================================================================
public static class AetherOcclusion
{
    private static readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>멀수록 높은 소리가 먼저 사라진다(실제 공기 흡수의 단순화).</summary>
    public static float AirCutoff(float distance, float maxDistance)
    {
        float k = Mathf.Clamp01(distance / Mathf.Max(1f, maxDistance));
        return Mathf.Lerp(22000f, 4200f, Mathf.Pow(k, 0.75f));
    }

    /// <summary>듣는 사람과 소리 사이를 벽·건물이 가리는가. 사람·로봇·트리거는 무시한다.</summary>
    public static bool Blocked(Vector3 listener, Vector3 source)
    {
        Vector3 d = source - listener;
        float len = d.magnitude;
        if (len < 2.5f) return false;
        // 소리 나는 물체 자신의 콜라이더(터렛 몸통, 문짝, 등갓)에 막히지 않게 소리 쪽 1.5m 는 보지 않는다.
        int n = Physics.RaycastNonAlloc(listener, d / len, hits, len - 1.5f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c == null) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // 움직이는 물체(사람·로봇·파편)
            if (c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
            if (c.GetComponentInParent<PlayerStatus>() != null) continue;
            return true;
        }
        return false;
    }
}

// =========================================================================
// 반복 소리
// =========================================================================
public class AetherAudioLoop : MonoBehaviour
{
    private AudioSource src;
    private AudioLowPassFilter lpf;
    private float baseVolume;
    private float fade = 1f;
    private float targetFade = 1f;
    private float nextOcclusion;
    private float occ = 1f;
    private float occSmooth = 1f;
    private float cutoff = 22000f;
    private bool spatial;

    public float Volume { get => baseVolume; set => baseVolume = value; }
    /// <summary>true 면 효과음 대신 배경음 음량을 따른다.</summary>
    public bool Music { get; set; }
    /// <summary>일시정지(AudioListener.pause) 중에도 계속 재생(음악).</summary>
    public bool ignorePause { set { if (src != null) src.ignoreListenerPause = value; } }
    public float Pitch { get => src != null ? src.pitch : 1f; set { if (src != null) src.pitch = value; } }

    public void Init(AudioClip clip, float volume, float minD, float maxD, bool isSpatial)
    {
        spatial = isSpatial;
        src = gameObject.AddComponent<AudioSource>();
        lpf = gameObject.AddComponent<AudioLowPassFilter>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.dopplerLevel = 0f;
        src.spatialBlend = isSpatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = minD;
        src.maxDistance = maxD;
        src.priority = isSpatial ? 160 : 40;
        baseVolume = volume;
        src.volume = 0f;
        // 같은 소리 여러 개가 같은 박자로 맞물리지 않게 시작점을 흩뜨린다.
        src.time = Random.Range(0f, Mathf.Max(0f, clip.length - 0.05f));
        src.Play();
    }

    public void FadeTo(float target) { targetFade = Mathf.Clamp01(target); }

    public void Stop(bool destroy = true)
    {
        targetFade = 0f;
        if (destroy) Destroy(gameObject, 0.6f);
    }

    private void Update()
    {
        if (src == null) return;
        fade = Mathf.MoveTowards(fade, targetFade, Time.unscaledDeltaTime * 2f);

        if (spatial && Time.unscaledTime >= nextOcclusion)
        {
            nextOcclusion = Time.unscaledTime + 0.25f;
            Transform lis = AetherAudioRunner.Listener;
            if (lis != null)
            {
                float dist = Vector3.Distance(lis.position, transform.position);
                float c = AetherOcclusion.AirCutoff(dist, src.maxDistance);
                bool blocked = dist < src.maxDistance && AetherOcclusion.Blocked(lis.position, transform.position);
                cutoff = blocked ? Mathf.Min(c, 1100f) : c;
                occ = blocked ? 0.55f : 1f;
                // 로그 감쇠는 maxDistance 에서 멈춰(약 0.1) 맵 어디서나 불·형광등이 희미하게 깔렸다. 끝 15% 에서 0 으로 줄인다.
                occ *= Mathf.Clamp01(Mathf.InverseLerp(src.maxDistance, src.maxDistance * 0.85f, dist));
            }
        }

        // 벽 뒤로 돌아갈 때 소리가 툭 끊기지 않게 부드럽게 바꾼다.
        lpf.cutoffFrequency = Mathf.Lerp(lpf.cutoffFrequency, cutoff, Time.unscaledDeltaTime * 6f);
        occSmooth = Mathf.Lerp(occSmooth, occ, Time.unscaledDeltaTime * 4f);
        src.volume = Mathf.Clamp01(baseVolume * fade * occSmooth * (Music ? AetherAudio.Bgm : AetherAudio.Sfx));
    }
}

// =========================================================================
// 바닥 재질 판별
// =========================================================================
public static class AetherSurface
{
    private static readonly Dictionary<int, string> cache = new Dictionary<int, string>();
    private static readonly string[] MetalWords = { "metal", "steel", "iron", "grate", "grid", "plate", "alumin", "tread", "catwalk", "scifi", "sci-fi", "panel" };
    private static readonly string[] GravelWords = { "gravel", "rubble", "debris", "dirt", "sand", "rock", "stone", "mud", "soil", "ash", "ground", "pile", "ruin" };
    private static readonly string[] ConcreteWords = { "concrete", "asphalt", "road", "tile", "brick", "cement", "floor", "pavement", "sidewalk" };

    public static string Classify(Collider col)
    {
        string fallback = SceneDefault();
        // 07 제작실은 바닥 재질 이름(panel·scifi)이 철판으로 읽혀 금속 발소리가 났는데, 울리는 소리가 '딩동'처럼 들렸다(사용자 지적).
        // 07 은 바닥과 상관없이 일반 발소리를 쓴다.
        if (SceneManager.GetActiveScene().name.StartsWith("07.")) return "concrete";
        if (col == null) return fallback;

        int id = col.GetInstanceID();
        if (cache.TryGetValue(id, out string s)) return s;

        if (col is TerrainCollider)
            s = "gravel";
        else
        {
            var names = new System.Text.StringBuilder();
            if (col.sharedMaterial != null) names.Append(col.sharedMaterial.name).Append(' ');
            names.Append(col.gameObject.name).Append(' ');
            Renderer r = col.GetComponent<Renderer>();
            if (r != null)
                foreach (Material m in r.sharedMaterials)
                    if (m != null) names.Append(m.name).Append(' ');
            string n = names.ToString().ToLowerInvariant();

            if (Has(n, MetalWords)) s = "metal";
            else if (Has(n, GravelWords)) s = "gravel";
            else if (Has(n, ConcreteWords)) s = "concrete";
            else s = fallback;
        }

        cache[id] = s;
        return s;
    }

    private static bool Has(string n, string[] words)
    {
        foreach (string w in words) if (n.Contains(w)) return true;
        return false;
    }

    /// <summary>재질 이름으로 알 수 없을 때: 연구소(06)·제작실(07)은 철판 바닥, 나머지는 콘크리트.</summary>
    public static string SceneDefault()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene.StartsWith("06.")) return "metal";
        return "concrete";
    }
}

// =========================================================================
// 플레이어 발걸음·점프·착지 (걸은 거리로 박자를 맞춘다 — 애니메이션 이벤트가 섞임 블렌드에서 두 번씩 울려서)
// =========================================================================
public class AetherFootsteps : MonoBehaviour
{
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private float standHeight;
    private Vector3 lastPos;
    private float distance;
    private bool grounded = true;
    private float airTime;
    private float maxFallSpeed;
    private float lastY;
    private float yVel;
    private bool leftFoot;
    private Collider groundCol;
    private float lastStepTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        standHeight = capsule != null ? capsule.height : 1.8f;
        lastPos = transform.position;
        lastY = transform.position.y;
    }

    private float stillTime;
    private static float suppressJumpUntil;

    /// <summary>슈퍼점프처럼 자기 소리가 따로 있는 도약에서는 일반 점프 소리를 한 번 건너뛴다.</summary>
    public static void SuppressNextJump() { suppressJumpUntil = Time.time + 0.4f; }

    private void OnEnable()
    {
        lastPos = Pos();
        lastY = lastPos.y;
        distance = 0f;
    }

    private Vector3 Pos() => rb != null ? rb.position : transform.position;

    // 물리 단계마다 잰다. Shiri 프리팹은 보간(Interpolate)이 꺼져 있어 화면 프레임마다 재면
    // 대부분의 프레임에서 위치가 그대로라 걸은 거리가 쌓이지 않았다(실측: 7m 걸어도 발소리 0).
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        Vector3 p = Pos();
        float measuredY = (p.y - lastY) / dt;
        yVel = rb != null && !rb.isKinematic ? rb.linearVelocity.y : measuredY;
        lastY = p.y;

        bool nowGrounded = CheckGround(out groundCol);

        if (!nowGrounded)
        {
            airTime += dt;
            maxFallSpeed = Mathf.Max(maxFallSpeed, -yVel);
            if (grounded && yVel > 2.2f && Time.time >= suppressJumpUntil)
                AetherAudio.Play2D("jump", 0.45f, 1f, 0.05f);
        }
        else if (!grounded)
        {
            // 착지: 떨어진 속도만큼 크게
            if (airTime > 0.22f && maxFallSpeed > 1.5f)
            {
                float k = Mathf.InverseLerp(1.5f, 11f, maxFallSpeed);
                AetherAudio.Play2D("land_" + AetherAudio.SurfaceAt(groundCol), Mathf.Lerp(0.35f, 1f, k), Mathf.Lerp(1.05f, 0.9f, k), 0.04f);
            }
            airTime = 0f;
            maxFallSpeed = 0f;
            distance = 0f;
        }
        grounded = nowGrounded;

        Vector3 flat = p - lastPos; flat.y = 0f;
        lastPos = p;
        float speed = flat.magnitude / dt;
        if (speed < 0.4f) stillTime += dt; else stillTime = 0f;
        if (!grounded || speed < 0.4f || speed > 25f)
        {
            // 확실히 멈췄을 때만(0.25초) 다음 첫 발이 빨리 나오게 당겨 둔다.
            if (stillTime > 0.25f) distance = Mathf.Min(distance, 0.35f);
            return;
        }

        bool crouch = capsule != null && capsule.height < standHeight * 0.8f;
        float stride, vol;
        if (crouch) { stride = 0.55f; vol = 0.32f; }   // 10/11 사용자 요청: 발소리 전체 1.6배
        else if (speed < 3.4f) { stride = 0.78f; vol = 0.6f; } // 실측: 05 배경음 속에서 묻혀 0.30 → 0.38
        else { stride = 1.15f; vol = 0.9f; }

        distance += flat.magnitude;
        if (distance < stride || Time.time - lastStepTime < 0.16f) return;
        distance = 0f;
        lastStepTime = Time.time;
        leftFoot = !leftFoot;

        string surface = AetherAudio.SurfaceAt(groundCol);
        // 왼발·오른발이 똑같이 들리지 않게 음높이를 아주 조금 다르게
        AetherAudio.Play2D("step_" + surface, vol * Random.Range(0.85f, 1.05f), leftFoot ? 0.98f : 1.02f, 0.04f);
    }

    private bool CheckGround(out Collider col)
    {
        col = null;
        float half = capsule != null ? capsule.height * 0.5f * Mathf.Max(transform.lossyScale.y, 0.01f) : 0.9f;
        float radius = capsule != null ? capsule.radius * 0.8f : 0.25f;
        Vector3 center = capsule != null ? transform.TransformPoint(capsule.center) : transform.position + Vector3.up * half;
        RaycastHit[] all = Physics.SphereCastAll(center, radius, Vector3.down, half + 0.2f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in all)
        {
            if (h.collider.transform.IsChildOf(transform) || (rb != null && h.collider.attachedRigidbody == rb)) continue;
            if (h.distance < best) { best = h.distance; col = h.collider; }
        }
        return col != null;
    }
}

// =========================================================================
// 로봇: 무거운 금속 발걸음, 제자리에서 몸을 돌릴 때 관절 모터음
// =========================================================================
public class AetherRobotAudio : MonoBehaviour
{
    private const int MaxConcurrentSteps = 6;
    private static readonly float[] stepTimes = new float[MaxConcurrentSteps];
    private static int stepCursor;

    /// <summary>최근 0.5초 안에 울린 로봇 발소리 수.</summary>
    private static int StepsPlaying()
    {
        int n = 0;
        foreach (float t in stepTimes) if (Time.time - t < 0.5f) n++;
        return n;
    }

    private static bool IsNearListener(Vector3 p, float range)
    {
        Transform lis = AetherAudioRunner.Listener;
        return lis != null && (lis.position - p).sqrMagnitude < range * range;
    }

    private RobotHealth health;
    private Vector3 lastPos;
    private float lastYaw;
    private float distance;
    private float nextServo;

    private void Awake()
    {
        health = GetComponent<RobotHealth>() ?? GetComponentInParent<RobotHealth>();
        lastPos = transform.position;
        lastYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (health != null && health.IsDead) { enabled = false; return; }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 p = transform.position;
        Vector3 flat = p - lastPos; flat.y = 0f;
        lastPos = p;
        float speed = flat.magnitude / dt;

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt;
        lastYaw = yaw;

        if (speed > 0.3f && speed < 20f)
        {
            float stride = speed > 3f ? 1.25f : 0.95f;
            distance += flat.magnitude;
            if (distance >= stride)
            {
                distance = 0f;
                // 실측: 05 의 로봇 19대가 34m 안에서 동시에 걸어 5초에 발소리 50번 — 금속음이 벽처럼 깔렸다.
                // 들리는 거리를 줄이고, 동시에 울리는 로봇 발소리는 6개까지만(가까운 것이 우선) 낸다.
                if (IsNearListener(p, 24f) && (StepsPlaying() < MaxConcurrentSteps || IsNearListener(p, 10f)))
                {
                    stepTimes[stepCursor] = Time.time;
                    stepCursor = (stepCursor + 1) % MaxConcurrentSteps;
                    AetherAudio.PlayAt("robot_step", p, speed > 3f ? 0.85f : 0.6f, 2.5f, 24f, 0.06f, 1f, 140);
                }
            }
        }
        else if (yawRate > 70f && Time.time >= nextServo)
        {
            nextServo = Time.time + Random.Range(2.5f, 4.5f);
            if (IsNearListener(p, 14f))
                AetherAudio.PlayAt("robot_servo", p + Vector3.up * 1.3f, 0.4f, 1.5f, 14f, 0.08f, 1f, 170);
        }
    }
}

// =========================================================================
// 장면을 읽고 소리 부품을 붙인다
// =========================================================================
public static class AetherAudioAttach
{
    public static void OnSceneLoaded(Scene scene)
    {
        AetherAudioRunner.Ensure().StartCoroutine(AttachSoon());
    }

    private static System.Collections.IEnumerator AttachSoon()
    {
        // 캐릭터·로봇이 씬 시작 직후 만들어지는 경우가 있어 몇 번 나눠서 붙인다.
        for (int pass = 0; pass < 4; pass++)
        {
            AttachAll();
            yield return new WaitForSecondsRealtime(pass == 0 ? 0.2f : 1.5f);
        }
    }

    public static void AttachAll()
    {
        foreach (var m in Object.FindObjectsByType<Move_Hankyul>(FindObjectsSortMode.None)) Add<AetherFootsteps>(m.gameObject);
        foreach (var m in Object.FindObjectsByType<Move_Yunseo>(FindObjectsSortMode.None)) Add<AetherFootsteps>(m.gameObject);
        foreach (var r in Object.FindObjectsByType<Robots>(FindObjectsSortMode.None)) Add<AetherRobotAudio>(r.gameObject);
        ScaleScenePlacedLoops();
        AttachFires();
    }

    /// <summary>
    /// 씬에 처음부터 놓여 스스로 도는 반복 소리(불 연출 프리팹의 불 소리 등)도 효과음 음량을 따르게 한다.
    /// 실측: 05 불 11곳이 음량 1.0 으로 설정을 무시하고 울렸다. 드론처럼 스크립트가 음량을 직접 맞추는 것은 건드리지 않는다.
    /// </summary>
    private static void ScaleScenePlacedLoops()
    {
        // 반복 소리뿐 아니라 씬에 놓인 모든 소리(엘리베이터·가스·불꽃 분출구·퍼즐·문 환경음 등)가 효과음 설정을 따르게 한다.
        // 예전엔 '반복 + 자동 재생' 만 맞춰서, PlayOneShot·Play 로 내는 소리는 설정을 0 으로 해도 들렸다.
        // 이미 직접 음량을 곱하는 것(AetherAudio 재생기·피격음 등 씬을 넘어 살아있는 것, 드론)은 건드리지 않는다.
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (src == null || !src.gameObject.scene.IsValid() || src.gameObject.scene.name == "DontDestroyOnLoad") continue;
            if (src.GetComponent<AetherSfxScaler>() != null || src.GetComponent<AetherAudioLoop>() != null) continue;
            if (src.GetComponent<UnityEngine.Video.VideoPlayer>() != null) continue;
            // 소리 낼 때 이미 효과음 배율을 곱하는 곳(한결의 총, 드론 계열, 우리 연출 스크립트)은 두 번 줄이지 않게 뺀다.
            if (src.GetComponentInParent<Hankyul_Fire>() != null || src.GetComponentInParent<DroneHealth>() != null) continue;
            if (HasAetherScript(src.gameObject)) continue;
            if (src.GetComponentInParent<Drones>() != null || src.GetComponent<AudioManager>() != null) continue;
            src.gameObject.AddComponent<AetherSfxScaler>().Bind(src);
        }
    }

    private static bool HasAetherScript(GameObject go)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name.StartsWith("Aether") && !(mb is AetherSfxScaler))
                return true;
        return false;
    }

    private static void Add<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() == null) go.AddComponent<T>();
    }

    private static readonly List<Vector3> fireSpots = new List<Vector3>();

    /// <summary>불 연출(파티클) 자리에 타닥거리는 불 소리를 붙인다. 가까이 모인 불은 하나로 친다.</summary>
    private static void AttachFires()
    {
        fireSpots.Clear();
        foreach (var l in Object.FindObjectsByType<AetherAudioLoop>(FindObjectsSortMode.None))
            if (l.name == "AETHER_Loop_fire_loop") fireSpots.Add(l.transform.position);

        int added = 0;
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (added >= 16 || fireSpots.Count >= 16) break;
            if (ps.transform.parent != null && ps.transform.parent.GetComponentInParent<ParticleSystem>() != null) continue; // 최상위 파티클만
            string n = ps.name.ToLowerInvariant();
            if (!(n.Contains("fire") || n.Contains("flame") || n.Contains("burn") || n.Contains("불"))) continue;
            if (n.Contains("muzzle") || n.Contains("spark") || n.Contains("smoke") || n.Contains("hit")) continue;
            // 불 연출 프리팹에 자체 불 소리가 있으면 그대로 쓴다(겹치면 두 겹으로 들렸다).
            AudioSource own = ps.GetComponentInChildren<AudioSource>();
            if (own != null && own.clip != null) { fireSpots.Add(ps.transform.position); continue; }
            Vector3 p = ps.transform.position;
            bool near = false;
            foreach (var q in fireSpots) if ((q - p).sqrMagnitude < 25f) { near = true; break; }
            if (near) continue;
            fireSpots.Add(p);
            AetherAudio.StartLoop("fire_loop", ps.transform, 0.45f, 1.5f, 14f);
            added++;
        }
    }
}

// =========================================================================
// 씬에 놓인 소리의 음량을 효과음 설정에 맞춘다(원래 음량 × 효과음)
// =========================================================================
public class AetherSfxScaler : MonoBehaviour
{
    private AudioSource src;
    private float baseVolume;

    public void Bind(AudioSource s)
    {
        src = s;
        baseVolume = s.volume;
        // 불 연출 프리팹의 불 소리는 음량 1.0 이라 출발 지점 불 옆에서 다른 소리를 덮었다. 다른 환경음과 맞춘다.
        if (s.clip != null && s.clip.name.IndexOf("Fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
            baseVolume = Mathf.Min(baseVolume, 0.55f);
        Apply();
    }

    private float lastApplied = -1f;

    private void LateUpdate() { Apply(); }

    private void Apply()
    {
        if (src == null)
            return;

        // 다른 스크립트가 음량을 바꿨으면(서서히 키우기·줄이기 등) 그 값을 새 기준으로 삼고 설정 배율만 곱한다.
        if (lastApplied >= 0f && Mathf.Abs(src.volume - lastApplied) > 0.0005f)
            baseVolume = src.volume;

        lastApplied = baseVolume * AetherAudio.Sfx;
        src.volume = lastApplied;
    }
}

// =========================================================================
// 모든 UI 버튼: 마우스를 올리면 '틱', 누르면 '딸깍'
// =========================================================================
public class AetherUiButtonSound : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerClickHandler
{
    private static float lastHover;
    private UnityEngine.UI.Selectable selectable;

    private void Awake() { selectable = GetComponent<UnityEngine.UI.Selectable>(); }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (selectable != null && !selectable.IsInteractable()) return;
        if (Time.unscaledTime - lastHover < 0.06f) return;
        lastHover = Time.unscaledTime;
        AetherAudio.Play2D("ui_hover", 0.35f, 1f, 0.02f);
    }

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
    {
        if (e.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;
        if (selectable != null && !selectable.IsInteractable()) return;
        AetherAudio.Play2D("ui_click", 0.55f, 1f, 0.02f);
    }

    /// <summary>씬의 버튼에 붙인다. 시작 화면 버튼(StartButtonController)은 자기 소리가 있어 건너뛴다.</summary>
    public static void WireAll()
    {
        foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || b.GetComponent<AetherUiButtonSound>() != null) continue;
            if (b.GetComponent<StartButtonController>() != null || b.GetComponentInParent<StartButtonController>() != null) continue;
            // 예전 인벤토리(InventoryUI)는 버튼마다 자기 클릭음(btn_Click·select)을 내서 겹친다.
            if (b.GetComponentInParent<InventoryUI>(true) != null) continue;
            b.gameObject.AddComponent<AetherUiButtonSound>();
        }
    }
}

// =========================================================================
// 씬별 환경음: 바탕 반복음 + 가끔 들리는 먼 소리
// =========================================================================
public static class AetherAmbience
{
    private static AetherAudioLoop bed;
    private static string scenePrefix = "";
    private static float nextEvent;

    private static string lastScene = "";

    public static void OnSceneLoaded(Scene scene)
    {
        // 다른 장소로 넘어왔을 때 화면이 밝아지며 '쉭' (같은 씬 다시 시작·첫 실행·게임 오버로 타이틀 가기는 제외)
        bool toGameplay = scene.name.StartsWith("04.") || scene.name.StartsWith("05.") || scene.name.StartsWith("06.") || scene.name.StartsWith("07.");
        if (lastScene != "" && lastScene != scene.name && toGameplay)
            AetherAudio.Play2D("transition", 0.4f, 1f, 0.03f);
        lastScene = scene.name;

        if (bed != null) bed.Stop();
        bed = null;
        scenePrefix = scene.name.Length >= 3 ? scene.name.Substring(0, 3) : scene.name;
        string clip = null;
        float vol = 0.5f;
        switch (scenePrefix)
        {
            case "04.": clip = "amb_bunker"; vol = 0.55f; break;
            case "05.": clip = "amb_city"; vol = 0.5f; break;
            case "06.": clip = "amb_lab"; vol = 0.5f; break;
            case "07.": clip = "amb_parts"; vol = 0.45f; break;
        }
        if (clip != null)
            bed = AetherAudio.StartLoop(clip, AetherAudioRunner.Ensure().transform, vol, 1f, 100f, false);
        nextEvent = Time.time + Random.Range(4f, 8f);
    }

    public static void Tick()
    {
        if (Time.time < nextEvent) return;
        Transform lis = AetherAudioRunner.Listener;
        if (lis == null) { nextEvent = Time.time + 2f; return; }

        Vector2 dir = Random.insideUnitCircle.normalized;
        switch (scenePrefix)
        {
            case "04.":
                // 벙커: 천장에서 떨어지는 물방울, 가끔 배관 삐걱
                AetherAudio.PlayAt("drip", lis.position + new Vector3(dir.x, 1.5f, dir.y) * Random.Range(3f, 8f), 0.5f, 1f, 14f, 0.08f);
                if (Random.value < 0.18f)
                    AetherAudio.PlayAt("creak", lis.position + new Vector3(dir.y, 2f, -dir.x) * Random.Range(6f, 12f), 0.35f, 3f, 25f, 0.1f, 0.8f);
                nextEvent = Time.time + Random.Range(3.5f, 9f);
                break;
            case "05.":
                // 폐허: 멀리서 무너지는 잔해, 바람에 철골이 삐걱
                if (Random.value < 0.45f)
                    AetherAudio.PlayAt("distant_debris", lis.position + new Vector3(dir.x, 4f, dir.y) * Random.Range(30f, 50f), 0.7f, 10f, 90f, 0.1f);
                else
                    AetherAudio.PlayAt("creak", lis.position + new Vector3(dir.x, 6f, dir.y) * Random.Range(14f, 24f), 0.45f, 6f, 45f, 0.12f);
                nextEvent = Time.time + Random.Range(9f, 20f);
                break;
            case "06.":
                AetherAudio.PlayAt("creak", lis.position + new Vector3(dir.x, 3f, dir.y) * Random.Range(8f, 16f), 0.25f, 3f, 30f, 0.1f, 1.3f);
                nextEvent = Time.time + Random.Range(14f, 28f);
                break;
            default:
                nextEvent = Time.time + 10f;
                break;
        }
    }
}

// =========================================================================
// 체력이 낮으면 심장 소리
// =========================================================================
public static class AetherHeartbeat
{
    private static AetherAudioLoop loop;
    private static PlayerStatus player;
    private static float nextFind;

    public static void Tick()
    {
        if ((player == null || !player.isActiveAndEnabled) && Time.unscaledTime >= nextFind)
        {
            nextFind = Time.unscaledTime + 1f;
            player = null;
            foreach (var s in Object.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None))
                if (s.isActiveAndEnabled) { player = s; break; }
        }

        float ratio = player != null && player.maxHP > 0f ? player.currentHP / player.maxHP : 1f;
        bool want = player != null && ratio > 0f && ratio < 0.3f;

        if (want && loop == null)
            loop = AetherAudio.StartLoop("heartbeat", AetherAudioRunner.Ensure().transform, 0f, 1f, 10f, false);

        if (loop != null)
        {
            if (!want) { loop.Stop(); loop = null; return; } // 서서히 사라진다
            float k = Mathf.InverseLerp(0.3f, 0.05f, ratio);
            loop.Volume = Mathf.Lerp(0.35f, 0.85f, k);
            loop.Pitch = Mathf.Lerp(1.0f, 1.45f, k); // 위험할수록 빨라진다
        }
    }
}
