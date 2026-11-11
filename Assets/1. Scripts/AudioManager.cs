using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    [Header("오디오 소스")]
    public AudioSource bgmSource;    // 배경음
    public AudioSource sfxSource;    // 효과음

    [Header("사운드 목록 (키 = clip)")]
    public List<SoundData> soundList = new List<SoundData>();
    private Dictionary<string, AudioClip> clipDict = new Dictionary<string, AudioClip>();

    [Header("메인 메뉴 씬 이름")]
    public string mainMenuSceneName = "MainMenu"; // 프로젝트에 맞게 변경

    private void Awake()
    {
        EnsureSources();

        foreach (var s in soundList)
        {
            if (s != null && !string.IsNullOrEmpty(s.key) && !clipDict.ContainsKey(s.key))
                clipDict.Add(s.key, s.clip);
        }
    }

    private void EnsureSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    // 씬이 바뀔 때 호출됨
    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // 새로 활성화된 씬이 메인 메뉴가 아니라면 BGM 정지
        if (newScene.name != mainMenuSceneName)
        {
            if (bgmSource != null && bgmSource.isPlaying)
                bgmSource.Stop();
        }
    }

    // ----------------------------------------------------
    // BGM 재생
    // ----------------------------------------------------
    public void PlayBGM(string key, float volume = 1f)
    {
        EnsureSources();

        if (!clipDict.TryGetValue(key, out AudioClip bgmClip) || bgmClip == null)
            return;

        bgmSource.clip = bgmClip;
        bgmSource.volume = volume * AetherAudio.Bgm; // settings BGM slider (was overwritten to 1)
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // ----------------------------------------------------
    // SFX 재생 (키 기반)
    // ----------------------------------------------------
    public void PlaySFX(string key, float volume = 1f)
    {
        EnsureSources();

        if (!clipDict.TryGetValue(key, out AudioClip sfxClip) || sfxClip == null)
        {
            // Fallback: when this manager was created at runtime with an empty list (scene launched directly),
            // play the same key from Resources/AETHERAudio instead of staying silent.
            AetherAudio.Play2D(key, volume, 1f, 0f);
            return;
        }

        sfxSource.PlayOneShot(sfxClip, volume);
    }

    // ----------------------------------------------------
    // SFX 재생 (직접 클립 전달)
    // ----------------------------------------------------
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;

        EnsureSources();
        sfxSource.PlayOneShot(clip, volume);
    }
}

[System.Serializable]
public class SoundData
{
    public string key;
    public AudioClip clip;
}
