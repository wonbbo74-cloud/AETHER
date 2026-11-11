using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [Header("페이드 이미지")]
    [SerializeField] private Image fadeImage;

    [Header("기본 시간")]
    [SerializeField] private float defaultFadeOutTime = 1.0f;
    [SerializeField] private float defaultFadeInTime = 1.0f;

    private bool isFading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeImage == null)
        {
            Debug.LogError("[SceneFader] Fade Image가 연결되지 않았습니다.");
            return;
        }

        SetAlpha(0f);
    }

    public void FadeOutAndLoadScene(string sceneName)
    {
        FadeOutAndLoadScene(sceneName, defaultFadeOutTime, defaultFadeInTime);
    }

    public void FadeOutAndLoadScene(string sceneName, float fadeOutTime, float fadeInTime)
    {
        if (isFading)
            return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneFader] sceneName이 비어 있습니다.");
            return;
        }

        StartCoroutine(FadeOutLoadFadeInRoutine(sceneName, fadeOutTime, fadeInTime));
    }

    private IEnumerator FadeOutLoadFadeInRoutine(string sceneName, float fadeOutTime, float fadeInTime)
    {
        isFading = true;

        yield return StartCoroutine(Fade(0f, 1f, fadeOutTime));

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(AetherCinematics.Redirect(sceneName));   // cinematic between 05 and 06
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;

        yield return StartCoroutine(Fade(1f, 0f, fadeInTime));

        isFading = false;
    }

    public void FadeInOnly(float fadeInTime)
    {
        if (isFading)
            return;

        StartCoroutine(FadeInOnlyRoutine(fadeInTime));
    }

    private IEnumerator FadeInOnlyRoutine(float fadeInTime)
    {
        isFading = true;
        yield return StartCoroutine(Fade(1f, 0f, fadeInTime));
        isFading = false;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float time = 0f;

        SetAlpha(startAlpha);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime; // keep fading even when timeScale is 0
            float t = Mathf.Clamp01(time / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(endAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}