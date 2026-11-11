using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFadeController : MonoBehaviour
{
    public static SceneFadeController Instance;

    [Header("페이드 이미지")]
    [SerializeField] private Image fadeImage;

    [Header("씬 시작 시 페이드 인 시간")]
    [SerializeField] private float defaultFadeInDuration = 1.5f;

    private bool isTransitioning = false;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 04 에서는 장면의 최상위가 아니라 유지되지 않는다(경고만 뜸). 최상위일 때만 유지하고, 아니면 전환 끝에서 AetherSceneFade 가 이어받는다.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새 씬이 열리면 하얀 화면에서 천천히 사라지게 — 단, 이 장치가 시작한 전환(04→05)일 때만.
        // 예전엔 04 이후 모든 씬(05→06 검은 페이드, 다시 시도, 타이틀)에서도 흰 화면이 번쩍였다.
        StopAllCoroutines();
        if (isTransitioning)
            StartCoroutine(FadeInRoutine(defaultFadeInDuration));
        else if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float waitAfterFullWhite)
    {
        if (isTransitioning)
            return;

        StartCoroutine(FadeOutAndLoadRoutine(sceneName, fadeOutDuration, waitAfterFullWhite));
    }

    private IEnumerator FadeOutAndLoadRoutine(string sceneName, float fadeOutDuration, float waitAfterFullWhite)
    {
        if (fadeImage == null)
        {
            Debug.LogError("[SceneFadeController] fadeImage가 연결되지 않았습니다.");
            yield break;
        }

        isTransitioning = true;

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(t / fadeOutDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 1f;
        fadeImage.color = c;

        yield return new WaitForSecondsRealtime(waitAfterFullWhite);

        // 이 장치는 장면의 최상위가 아니어서 DontDestroyOnLoad 가 먹지 않고 장면과 함께 사라진다.
        // 그래서 도착하자마자 흰 화면이 한 번에 꺼졌다. 흰 화면을 이어받아 새 장면에서 천천히 걷어내게 넘긴다.
        AetherSceneFade.LoadSceneFromCovered(sceneName, fadeImage.color, defaultFadeInDuration);
    }

    private IEnumerator FadeInRoutine(float duration)
    {
        if (fadeImage == null)
            yield break;

        Color c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;

        isTransitioning = false;
    }
}