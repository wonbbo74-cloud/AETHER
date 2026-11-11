using UnityEngine;

/// <summary>
/// 적의 총격 소리를 낸다.
///
/// 지금까지 적 총격에는 소리가 전혀 없어서, 어디서 맞고 있는지 알 수가 없었다.
/// 적이 스무 명 넘게 쉬지 않고 쏘기 때문에 한 발마다 오브젝트를 만들면 금방 쌓인다.
/// 소리통 몇 개를 미리 만들어 돌려 쓴다.
/// </summary>
public static class AetherShotAudio
{
    /// <summary>동시에 울릴 수 있는 소리 수. 넘치면 가장 오래된 것을 덮어쓴다.</summary>
    private const int VoiceCount = 10;

    /// <summary>이 거리 밖에서 난 총성은 아예 재생하지 않는다.</summary>
    private const float HearingRange = 55f;

    private const string LaserResourceFormat = "AETHERRuntime/AetherEnemyLaser{0}";
    private const string BallisticResource = "AETHERRuntime/AetherGunshot";

    private static AudioSource[] voices;
    private static int nextVoice;

    private static AudioClip[] laserClips;
    private static AudioClip ballisticClip;
    private static bool loaded;

    /// <summary>손에 든 레이저 총의 발사음.</summary>
    public static void PlayLaser(Vector3 position, float volume = 0.55f)
    {
        Load();

        if (laserClips == null || laserClips.Length == 0)
            return;

        Play(position, laserClips[Random.Range(0, laserClips.Length)], volume, Random.Range(1.18f, 1.34f));

        // 레이저음만으로는 가볍고 '삐용'처럼 들려서, 낮게 깐 실탄음을 몸통으로 겹친다.
        if (ballisticClip != null)
            Play(position, ballisticClip, volume * 0.45f, Random.Range(0.7f, 0.8f), HearingRange, important: true);
    }

    /// <summary>드론처럼 실탄을 쏘는 쪽의 발사음.</summary>
    public static void PlayBallistic(Vector3 position, float volume = 0.45f)
    {
        Load();

        if (ballisticClip == null)
            return;

        Play(position, ballisticClip, volume, Random.Range(0.82f, 0.95f));
    }

    /// <summary>
    /// 터렛의 묵직한 '뻥' 소리.
    ///
    /// 거치형 중화기 한 발은 또렷하게 들려야 한다. 음을 크게 낮춰 길고 둔탁하게 만들고,
    /// 소리통 하나를 통째로 비워서 다른 총성에 묻히지 않게 한다.
    /// </summary>
    public static void PlayTurretBoom(Vector3 position, float volume = 0.95f)
    {
        Load();

        if (ballisticClip == null)
            return;

        Play(position, ballisticClip, volume, Random.Range(0.52f, 0.62f), 70f, important: true);
    }

    /// <summary>같은 소리가 너무 촘촘히 겹치면 빗소리가 된다. 이보다 가까우면 거른다.</summary>
    private const float MinimumGap = 0.07f;

    private static float lastPlayTime = -99f;

    private static void Play(Vector3 position, AudioClip clip, float volume, float pitch,
        float range = HearingRange, bool important = false)
    {
        if (clip == null || !IsWithinHearing(position, range))
            return;

        // 터렛의 '뻥'은 무슨 일이 있어도 들려야 한다. 나머지는 간격을 지킨다.
        if (!important && Time.unscaledTime - lastPlayTime < MinimumGap)
            return;

        AudioSource source = TakeVoice();
        if (source == null)
            return;

        lastPlayTime = Time.unscaledTime;

        source.transform.position = position;
        source.clip = clip;
        source.volume = volume * AetherAudio.Sfx; // 설정의 효과음 음량
        source.pitch = pitch;
        source.maxDistance = range;
        source.Play();
    }

    private static bool IsWithinHearing(Vector3 position, float range)
    {
        AudioListener listener = Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Exclude);
        Vector3 ear = listener != null
            ? listener.transform.position
            : (Camera.main != null ? Camera.main.transform.position : position);

        return (ear - position).sqrMagnitude < range * range;
    }

    /// <summary>다음 차례의 소리통. 돌아가며 쓴다.</summary>
    private static AudioSource TakeVoice()
    {
        if (voices == null || voices.Length == 0 || voices[0] == null)
            Build();

        if (voices == null)
            return null;

        // 지금 쉬고 있는 통을 먼저 찾는다.
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource candidate = voices[(nextVoice + i) % voices.Length];
            if (candidate != null && !candidate.isPlaying)
            {
                nextVoice = (nextVoice + i + 1) % voices.Length;
                return candidate;
            }
        }

        // 전부 울리는 중이면 가장 오래된 것을 덮어쓴다.
        AudioSource oldest = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        return oldest;
    }

    private static void Build()
    {
        GameObject host = new GameObject("AETHER_ShotAudio");
        Object.DontDestroyOnLoad(host);

        voices = new AudioSource[VoiceCount];

        for (int i = 0; i < VoiceCount; i++)
        {
            GameObject child = new GameObject("Voice " + i);
            child.transform.SetParent(host.transform, false);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // 소리가 나는 자리를 알아야 어디서 쏘는지 알 수 있다. 완전한 3D 로 둔다.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = HearingRange;
            source.dopplerLevel = 0f;

            voices[i] = source;
        }
    }

    private static void Load()
    {
        if (loaded)
            return;

        loaded = true;

        laserClips = new AudioClip[4];
        int found = 0;

        for (int i = 0; i < 4; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(string.Format(LaserResourceFormat, i + 1));
            if (clip != null)
                laserClips[found++] = clip;
        }

        if (found < laserClips.Length)
            System.Array.Resize(ref laserClips, found);

        ballisticClip = Resources.Load<AudioClip>(BallisticResource);

        Debug.Log($"[총성] 레이저 발사음 {found}개, 실탄 발사음 {(ballisticClip != null ? "있음" : "★ 없음")}");
    }
}
