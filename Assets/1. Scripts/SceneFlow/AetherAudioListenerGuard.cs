using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 어느 씬에서든 활성화된 AudioListener가 <b>정확히 하나</b> 있게 만든다.
///
/// 리스너가 없으면 "There are no audio listeners in the scene"가,
/// 둘 이상이면 "There are 2 audio listeners in the scene"가 매번 뜬다.
/// 05.폐허된 도시처럼 AetherZoneBootstrap이 돌지 않는 씬에는 챙겨 주는 쪽이 아무도 없었다.
///
/// 플레이어가 Start에서 만들어지는 씬도 있으므로 한 번만 확인하지 않고
/// 잠깐 지켜보다가 자리를 잡으면 멈춘다.
/// </summary>
public sealed class AetherAudioListenerGuard : MonoBehaviour
{
    private static AetherAudioListenerGuard instance;
    private static bool warnedNoHost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (instance == null)
        {
            GameObject runner = new GameObject("AETHER_AudioListenerGuard");
            instance = runner.AddComponent<AetherAudioListenerGuard>();
            DontDestroyOnLoad(runner);
        }

        instance.Restart();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Restart();
    }

    private void Restart()
    {
        StopAllCoroutines();
        StartCoroutine(KeepExactlyOne());
    }

    private IEnumerator KeepExactlyOne()
    {
        // 플레이어·카메라가 자리를 잡을 때까지 지켜본다.
        float endTime = Time.unscaledTime + 10f;

        while (Time.unscaledTime < endTime)
        {
            Resolve();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // 마지막으로 한 번 더 정리하고 손을 뗀다.
        Resolve();
    }

    private static void Resolve()
    {
        // FindObjectsInactive.Exclude는 '꺼진 오브젝트'만 걸러낸다. 오브젝트는 켜져 있는데
        // AudioListener 컴포넌트만 꺼져 있는 경우가 있어서, enabled까지 직접 확인해야
        // Unity가 세는 개수와 같아진다. (이걸 빠뜨려서 경고가 그대로 나고 있었다.)
        AudioListener[] all = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<AudioListener> listeners = new List<AudioListener>();

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled)
                listeners.Add(all[i]);
        }

        if (listeners.Count == 1)
            return;

        if (listeners.Count == 0)
        {
            GameObject host = PickListenerHost();
            if (host == null)
            {
                if (!warnedNoHost)
                {
                    warnedNoHost = true;
                    Debug.LogWarning("[AudioListenerGuard] 리스너를 붙일 카메라를 찾지 못했습니다.");
                }

                return;
            }

            AudioListener existing = host.GetComponent<AudioListener>();
            if (existing != null)
                existing.enabled = true;
            else
                host.AddComponent<AudioListener>();

            Debug.Log($"[AudioListenerGuard] '{host.name}'에 오디오 리스너를 붙였습니다.");
            return;
        }

        // 둘 이상이면 메인 카메라에 붙은 것만 남긴다.
        AudioListener keep = null;
        Camera main = Camera.main;

        if (main != null)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].gameObject == main.gameObject)
                {
                    keep = listeners[i];
                    break;
                }
            }
        }

        if (keep == null)
            keep = listeners[0];

        for (int i = 0; i < listeners.Count; i++)
        {
            if (listeners[i] != keep)
                listeners[i].enabled = false;
        }
    }

    /// <summary>
    /// 리스너를 붙일 곳: 메인 카메라 → 실제로 그리고 있는 카메라 → 꺼져 있는 카메라 순.
    /// 소리가 나는 위치가 달라지므로 되도록 화면을 그리는 카메라에 붙인다.
    /// </summary>
    private static GameObject PickListenerHost()
    {
        Camera main = Camera.main;
        if (main != null)
            return main.gameObject;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].isActiveAndEnabled)
                return cameras[i].gameObject;
        }

        return cameras.Length > 0 ? cameras[0].gameObject : null;
    }
}
