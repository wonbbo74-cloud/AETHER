using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 설정 화면의 값을 보관하고 실제 게임에 적용한다.
/// 값은 PlayerPrefs에 저장되고, 씬을 넘어가면 새 플레이어에게 다시 적용된다.
/// </summary>
public static class AetherSettings
{
    private const string MasterKey = "AETHER_VOL_MASTER";
    private const string BgmKey = "AETHER_VOL_BGM";
    private const string SfxKey = "AETHER_VOL_SFX";
    private const string SensitivityKey = "AETHER_MOUSE_SENSITIVITY";
    private const string AdsSensitivityKey = "AETHER_ADS_SENSITIVITY";
    private const string InvertYKey = "AETHER_INVERT_Y";
    private const string InvertXKey = "AETHER_INVERT_X";
    private const string VibrationKey = "AETHER_VIBRATION";

    /// <summary>마우스 감도 100(최대)일 때의 실제 감도 값.</summary>
    private const float SensitivityAtMax = 400f;

    private static bool loaded;
    private static bool subscribed;

    // 0~100 범위로 다룬다(설정 화면 표기와 동일).
    public static float MasterVolume { get; private set; } = 80f;
    public static float BgmVolume { get; private set; } = 70f;
    public static float SfxVolume { get; private set; } = 90f;
    public static float MouseSensitivity { get; private set; } = 50f;
    public static float AdsSensitivity { get; private set; } = 35f;
    public static bool InvertY { get; private set; }
    public static bool InvertX { get; private set; }
    public static bool Vibration { get; private set; } = true;

    /// <summary>조준(ADS) 중에 곱할 감도 배율. ADS 구현 시 이 값을 쓰면 된다.</summary>
    public static float AdsSensitivityMultiplier =>
        MouseSensitivity <= 0.01f ? 1f : Mathf.Clamp(AdsSensitivity / MouseSensitivity, 0.05f, 4f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        Load();

        if (!subscribed)
        {
            SceneManager.sceneLoaded += (scene, mode) => Apply();
            subscribed = true;
        }

        Apply();
    }

    public static void Load()
    {
        if (loaded)
            return;

        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 80f);
        BgmVolume = PlayerPrefs.GetFloat(BgmKey, 70f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 90f);
        MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 50f);
        AdsSensitivity = PlayerPrefs.GetFloat(AdsSensitivityKey, 35f);
        InvertY = PlayerPrefs.GetInt(InvertYKey, 0) == 1;
        InvertX = PlayerPrefs.GetInt(InvertXKey, 0) == 1;
        Vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;

        loaded = true;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        PlayerPrefs.SetFloat(BgmKey, BgmVolume);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
        PlayerPrefs.SetFloat(AdsSensitivityKey, AdsSensitivity);
        PlayerPrefs.SetInt(InvertYKey, InvertY ? 1 : 0);
        PlayerPrefs.SetInt(InvertXKey, InvertX ? 1 : 0);
        PlayerPrefs.SetInt(VibrationKey, Vibration ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp(value, 0f, 100f); Apply(); }
    public static void SetBgmVolume(float value) { BgmVolume = Mathf.Clamp(value, 0f, 100f); Apply(); }
    public static void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp(value, 0f, 100f); Apply(); }
    public static void SetMouseSensitivity(float value) { MouseSensitivity = Mathf.Clamp(value, 1f, 100f); Apply(); }
    public static void SetAdsSensitivity(float value) { AdsSensitivity = Mathf.Clamp(value, 1f, 100f); Apply(); }
    public static void SetInvertY(bool value) { InvertY = value; Apply(); }
    public static void SetInvertX(bool value) { InvertX = value; Apply(); }
    public static void SetVibration(bool value) { Vibration = value; Apply(); }

    /// <summary>현재 값을 실제 게임 오브젝트에 반영한다.</summary>
    public static void Apply()
    {
        Load();

        AudioListener.volume = MasterVolume / 100f;

        AudioManager audio = GlobalManager.HasInstance ? GlobalManager.Audio : null;
        if (audio != null)
        {
            if (audio.bgmSource != null)
                audio.bgmSource.volume = BgmVolume / 100f;

            if (audio.sfxSource != null)
                audio.sfxSource.volume = SfxVolume / 100f;
        }

        // 발걸음·총성·환경음 등 AetherAudio 로 나는 소리도 효과음 음량을 따른다.
        AetherAudioRunner.OnVolumeChanged();

        float sensitivity = Mathf.Max(1f, SensitivityAtMax * (MouseSensitivity / 100f));

        Move_Hankyul[] hankyul = Object.FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < hankyul.Length; i++)
        {
            if (hankyul[i] == null)
                continue;

            hankyul[i].mouseSensitivity = sensitivity;
            hankyul[i].invertLookY = InvertY;
            hankyul[i].invertLookX = InvertX;
        }

        // Shiri uses the same formula (input × sensitivity × AetherCameraFeel.MouseScale), so she gets the same value. 상하·좌우 반전은 윤서 쪽에 없다.
        Move_Yunseo[] yunseo = Object.FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < yunseo.Length; i++)
        {
            if (yunseo[i] == null)
                continue;

            yunseo[i].mouseSensitivity = sensitivity;
            yunseo[i].invertLookY = InvertY;
            yunseo[i].invertLookX = InvertX;
        }

        PlayerMouseLook[] looks = Object.FindObjectsByType<PlayerMouseLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < looks.Length; i++)
        {
            if (looks[i] == null)
                continue;

            looks[i].mouseSensitivity = sensitivity;
            looks[i].invertLookY = InvertY;
            looks[i].invertLookX = InvertX;
        }
    }
}
