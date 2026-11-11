using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면이 툭 끊기며 넘어가던 장면 전환(06→07 문, 캐릭터 선택→로딩, 다시 시도, 처음으로)을
/// 검게 어두워졌다가(0.6초) 새 장면에서 밝아지는(0.7초) 전환으로 바꾼다.
///
/// 시간이 멈춘 상태(작전 실패 화면 등)에서도 움직이도록 실제 시간으로 돈다.
/// 전환 중에는 같은 요청이 또 와도 무시한다(두 번 로드되지 않게).
/// </summary>
public class AetherSceneFade : MonoBehaviour
{
    private static AetherSceneFade instance;
    private Image overlay;

    public static bool Busy { get; private set; }

    public static void LoadScene(string sceneName, float fadeOut = 0.6f, float fadeIn = 0.7f, float holdBlack = 0f)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (Busy)
            return;

        Ensure().StartCoroutine(instance.Run(sceneName, -1, fadeOut, fadeIn, holdBlack));
    }

    public static void LoadScene(int buildIndex, float fadeOut = 0.6f, float fadeIn = 0.7f)
    {
        if (Busy)
            return;

        Ensure().StartCoroutine(instance.Run(null, buildIndex, fadeOut, fadeIn));
    }

    /// <summary>
    /// 다른 장치가 이미 화면을 다 덮은 상태(예: 04 출구의 흰 화면)에서 넘겨받아, 같은 색으로 덮은 채 로드하고 새 장면에서 걷어낸다.
    /// 04 의 흰 페이드 장치는 장면과 함께 사라져서 도착 순간 흰 화면이 한 번에 꺼졌다.
    /// </summary>
    public static void LoadSceneFromCovered(string sceneName, Color color, float fadeIn)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || Busy)
            return;

        Ensure().StartCoroutine(instance.RunCovered(sceneName, color, fadeIn));
    }

    private IEnumerator RunCovered(string sceneName, Color color, float fadeIn)
    {
        Busy = true;
        overlay.raycastTarget = true;
        overlay.color = new Color(color.r, color.g, color.b, 1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(AetherCinematics.Redirect(sceneName));
        while (op != null && !op.isDone)
            yield return null;
        yield return null;
        yield return null;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeIn)));
            overlay.color = new Color(color.r, color.g, color.b, 1f - k);
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = false;
        Busy = false;
    }

    private static AetherSceneFade Ensure()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("AETHER_SceneFade", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;

        var img = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        instance = go.AddComponent<AetherSceneFade>();
        instance.overlay = img.GetComponent<Image>();
        instance.overlay.color = new Color(0f, 0f, 0f, 0f);
        instance.overlay.raycastTarget = false;
        return instance;
    }

    private IEnumerator Run(string sceneName, int buildIndex, float fadeOut, float fadeIn, float holdBlack = 0f)
    {
        Busy = true;
        overlay.raycastTarget = true; // 전환 중엔 버튼이 눌리지 않게

        yield return Fade(0f, 1f, fadeOut);

        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(AetherCinematics.Redirect(sceneName)) : SceneManager.LoadSceneAsync(buildIndex);
        // 03 로딩 화면은 열리자마자 04 를 미리 부르기 시작하는데, 그동안 앞선 로드의 isDone 이 늦게 와서
        // 로딩 화면이 2.3초 동안 검게 남았다. 장면이 실제로 바뀌면 바로 다음으로 간다.
        while (op != null && !op.isDone && !IsActive(sceneName, buildIndex))
            yield return null;

        // 캐릭터·HUD 가 새 장면에서 자리 잡을 때까지 잠깐 검은 화면을 유지한다(첫 프레임 튀는 모습이 안 보이게).
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f + Mathf.Max(0f, holdBlack));

        yield return Fade(1f, 0f, fadeIn);

        overlay.raycastTarget = false;
        Busy = false;
    }

    /// <summary>
    /// 검게 덮고, 다른 누군가(2인의 호스트 네트워크 로드)가 장면을 바꿀 때까지 기다렸다가 새 장면에서 밝힌다.
    /// timeout 안에 장면이 안 바뀌면 fallbackScene 을 직접 부른다(호스트가 끊긴 경우).
    /// </summary>
    public static void CoverUntilNextScene(float fadeOut, float fadeIn, float timeout, string fallbackScene)
    {
        if (Busy)
            return;

        Ensure().StartCoroutine(instance.RunCoverUntil(fadeOut, fadeIn, timeout, fallbackScene));
    }

    private IEnumerator RunCoverUntil(float fadeOut, float fadeIn, float timeout, string fallbackScene)
    {
        Busy = true;
        overlay.raycastTarget = true;
        Scene from = SceneManager.GetActiveScene();

        yield return Fade(overlay.color.a, 1f, fadeOut);

        float waited = 0f;
        while (SceneManager.GetActiveScene() == from)
        {
            waited += Time.unscaledDeltaTime;
            if (waited > timeout && !string.IsNullOrEmpty(fallbackScene))
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(fallbackScene);
                while (op != null && !op.isDone)
                    yield return null;
                break;
            }
            yield return null;
        }

        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        yield return Fade(1f, 0f, fadeIn);

        overlay.raycastTarget = false;
        Busy = false;
    }

    private static bool IsActive(string sceneName, int buildIndex)
    {
        Scene s = SceneManager.GetActiveScene();
        return sceneName != null ? s.name == sceneName : s.buildIndex == buildIndex;
    }

    /// <summary>
    /// 미리 불러 둔 장면(allowSceneActivation = false)을 검게 덮은 뒤 켜고, 새 장면에서 밝힌다.
    /// 03 로딩 영상이 끝나는 순간 04 로 화면이 툭 끊기던 것을 부드럽게 한다.
    /// </summary>
    public static void ActivateWithFade(AsyncOperation preloaded, float fadeOut = 0.6f, float fadeIn = 0.8f)
    {
        if (preloaded == null)
            return;

        if (Busy)
        {
            preloaded.allowSceneActivation = true;
            return;
        }

        Ensure().StartCoroutine(instance.RunActivate(preloaded, fadeOut, fadeIn));
    }

    private IEnumerator RunActivate(AsyncOperation op, float fadeOut, float fadeIn)
    {
        Busy = true;
        overlay.raycastTarget = true;
        yield return Fade(0f, 1f, fadeOut);

        Scene before = SceneManager.GetActiveScene();
        op.allowSceneActivation = true;
        while (!op.isDone && SceneManager.GetActiveScene() == before)
            yield return null;

        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        yield return Fade(1f, 0f, fadeIn);

        overlay.raycastTarget = false;
        Busy = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duration)));
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, to);
    }
}
