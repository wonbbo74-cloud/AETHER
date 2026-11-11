using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 팀이 만든 시네마틱 3편(Cinemachine 3 + Timeline, "0. Scenes/Cinematic")을 진행 사이에 끼운다(10/12 사용자 요청).
///
///  1. Cine_01_벙커대화  — 로비에서 새 게임(혼자 하기) → 시네마틱 → 02 플레이어 선택
///  2. Cine_02_로봇앞     — 04 벙커 출구 → 시네마틱 → 05 폐허된 도시
///  3. Cine_03_연구소앞   — 05 연구소 문 → 시네마틱 → 06 연구소
///
/// 장면을 부르는 쪽(AetherSceneFade, SceneFader, 혼자 하기)이 <see cref="Redirect"/> 로 목적지를 물어,
/// 위 세 경우에만 시네마틱 장면으로 돌린다. 이어하기·퍼즐 복귀·다시 시도처럼 같은 목적지라도 출발지가 다르면 그대로 간다.
/// 시네마틱 장면에서는 타임라인이 끝나면(또는 건너뛰면) 검은 화면으로 덮고 원래 목적지로 넘어간다.
/// </summary>
public class AetherCinematics : MonoBehaviour
{
    public const string ScenePrefix = "Cine_";

    private struct Route
    {
        public string fromPrefix, toPrefix, cinematic, next;
        public Route(string fromPrefix, string toPrefix, string cinematic, string next)
        { this.fromPrefix = fromPrefix; this.toPrefix = toPrefix; this.cinematic = cinematic; this.next = next; }
    }

    private static readonly Route[] Routes =
    {
        new Route("LobbyScene", "02.", "Cine_01_벙커대화", "02.플레이어선택"),
        new Route("04.", "05.", "Cine_02_로봇앞", "05.폐허된 도시"),
        new Route("05.", "06.", "Cine_03_연구소앞", "06.연구소 메인 장소"),
    };

    public static bool IsCinematicScene(string sceneName) =>
        !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith(ScenePrefix, System.StringComparison.Ordinal);

    public static bool IsPlaying => IsCinematicScene(SceneManager.GetActiveScene().name);

    /// <summary>지금 장면에서 target 으로 갈 때 시네마틱을 먼저 보여야 하면 시네마틱 장면 이름을, 아니면 target 을 그대로 돌려준다.</summary>
    public static string Redirect(string target)
    {
        if (string.IsNullOrEmpty(target)) return target;
        string from = SceneManager.GetActiveScene().name;
        foreach (Route r in Routes)
        {
            if (from.StartsWith(r.fromPrefix, System.StringComparison.Ordinal) && target.StartsWith(r.toPrefix, System.StringComparison.Ordinal)
                && Application.CanStreamedLevelBeLoaded(r.cinematic))
                return r.cinematic;
        }
        return target;
    }

    private static string NextAfter(string cinematic)
    {
        foreach (Route r in Routes)
            if (r.cinematic == cinematic) return r.next;
        return null;
    }

    // ------------------------------------------------------------------ 시네마틱 장면 진행

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    // 시네마틱이 끝나고 넘어간 장면: 검은 화면을 잠깐 유지했다가 천천히 밝히고, 도착 직후엔 무적(10/12 사용자 요청).
    // 05 는 도착하자마자 총격이 시작돼 시네마틱에서 막 넘어온 플레이어가 15초에 HP 의 절반을 잃었다.
    private const float ArrivalHoldBlack = 0.6f;
    private const float ArrivalFadeIn = 1.6f;
    private const float ArrivalInvulnerableSeconds = 6f;
    private static bool protectNextArrival;

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsCinematicScene(scene.name) && protectNextArrival)
        {
            protectNextArrival = false;
            GameObject guard = new GameObject("AETHER_ArrivalGuard");
            SceneManager.MoveGameObjectToScene(guard, scene);
            guard.AddComponent<AetherArrivalGuard>().Seconds = ArrivalInvulnerableSeconds + ArrivalHoldBlack + ArrivalFadeIn;
            return;
        }

        if (!IsCinematicScene(scene.name) || FindFirstObjectByType<AetherCinematics>() != null)
            return;
        GameObject go = new GameObject("AETHER_Cinematics");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherCinematics>();
    }

    private const float SkipHoldSeconds = 1.0f;
    private const string FontPath = "UI/Fonts/NanumGothicCoding-Bold SDF";

    private PlayableDirector director;
    private string next;
    private bool leaving;
    private float skipHeld;
    private float hintVisibleUntil;
    private CanvasGroup hintGroup;
    private Image hintFill;

    private IEnumerator Start()
    {
        next = NextAfter(SceneManager.GetActiveScene().name);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
        BuildSkipHint();
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a.GetComponent<AetherCinematicStepReceiver>() == null) a.gameObject.AddComponent<AetherCinematicStepReceiver>();

        yield return null;
        director = PickDirector();
        if (director != null && director.state != PlayState.Playing)
        {
            director.time = 0;
            director.Play();
        }
        if (CanSkip) hintVisibleUntil = Time.unscaledTime + 3f;   // 처음 3초는 건너뛰기 안내를 보여 준다(2인 손님은 못 넘기니 안 보임)
    }

    /// <summary>켜져 있는 감독 중 가장 긴 타임라인을 가진 것.</summary>
    private static PlayableDirector PickDirector()
    {
        PlayableDirector best = null;
        foreach (PlayableDirector d in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (d == null || !d.enabled || d.playableAsset == null) continue;
            if (best == null || d.playableAsset.duration > best.playableAsset.duration) best = d;
        }
        return best;
    }

    private void Update()
    {
        if (leaving) return;

        // 끝까지 봤으면 다음 장면으로.
        if (director != null && director.playableAsset != null)
        {
            double end = director.duration;
            if (director.time >= end - 0.05 || (director.state != PlayState.Playing && director.time > 0.5))
            {
                Leave(0.9f);
                return;
            }
        }

        // 건너뛰기: Space · Enter · ESC 를 1초 누르고 있기.
        Keyboard kb = CanSkip ? Keyboard.current : null;
        bool held = kb != null &&(kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.escapeKey.isPressed);
        bool anyPress = kb != null && kb.anyKey.wasPressedThisFrame;
        if (anyPress) hintVisibleUntil = Time.unscaledTime + 2.5f;
        skipHeld = held ? skipHeld + Time.unscaledDeltaTime : Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2f);
        if (hintFill != null) hintFill.fillAmount = Mathf.Clamp01(skipHeld / SkipHoldSeconds);
        if (hintGroup != null)
        {
            float target = (Time.unscaledTime < hintVisibleUntil || skipHeld > 0f) ? 1f : 0f;
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, target, Time.unscaledDeltaTime * 3f);
        }
        if (skipHeld >= SkipHoldSeconds)
            Leave(0.6f);
    }

    private void Leave(float fadeOut)
    {
        if (leaving) return;
        leaving = true;
        protectNextArrival = true;
        if (hintGroup != null) hintGroup.alpha = 0f;
        StartCoroutine(FadeAudioAndGo(fadeOut));
    }

    private IEnumerator FadeAudioAndGo(float fadeOut)
    {
        // 시네마틱 소리(대사·배경음)를 화면이 어두워지는 동안 같이 줄인다.
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        float[] start = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++) start[i] = sources[i] != null ? sources[i].volume : 0f;

        string target = string.IsNullOrEmpty(next) ? "01.스타트씬" : next;

        // 들어올 때의 페이드(04 흰 화면 걷어내기 등)가 아직 돌고 있으면 전환 요청이 무시되어
        // 시네마틱에 갇힌다. 끝날 때까지 기다렸다가 부른다.
        for (float w = 0f; AetherSceneFade.Busy && w < 5f; w += Time.unscaledDeltaTime)
            yield return null;

        if (Networked)
        {
            // 2인: 호스트가 두 사람을 함께 다음 장면으로 보낸다. 손님은 검은 화면에서 기다린다.
            AetherSceneFade.CoverUntilNextScene(fadeOut, ArrivalFadeIn, 15f, target);
            if (NetworkManager.Singleton.IsServer)
                StartCoroutine(NetworkLoadAfter(target, fadeOut + 0.1f));
        }
        else
        {
            AetherSceneFade.LoadScene(target, fadeOut, ArrivalFadeIn, ArrivalHoldBlack);
        }
        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / fadeOut;
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null) sources[i].volume = start[i] * k;
            yield return null;
        }
    }

    /// <summary>방(호스트·손님)에 들어가 있는가. 혼자 하기는 네트워크를 켜지 않는다.</summary>
    private static bool Networked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    /// <summary>건너뛰기는 혼자일 때와 호스트만. 손님이 넘기면 두 사람 화면이 갈라진다.</summary>
    private static bool CanSkip => !Networked || NetworkManager.Singleton.IsServer;

    private IEnumerator NetworkLoadAfter(string target, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (Networked && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(target, LoadSceneMode.Single);
    }

    private void BuildSkipHint()
    {
        GameObject root = new GameObject("SkipHint", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        hintGroup = root.GetComponent<CanvasGroup>();
        hintGroup.alpha = 0f;
        hintGroup.blocksRaycasts = false;

        // 오른쪽 아래: 원형 진행 + "길게 눌러 건너뛰기"
        GameObject ring = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ring.transform.SetParent(root.transform, false);
        RectTransform rr = (RectTransform)ring.transform;
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(1f, 0f);
        rr.anchoredPosition = new Vector2(-322f, 48f);
        rr.sizeDelta = new Vector2(30f, 30f);
        Image bg = ring.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.18f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(ring.transform, false);
        RectTransform fr = (RectTransform)fill.transform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
        hintFill = fill.GetComponent<Image>();
        hintFill.sprite = MakeCircleSprite();
        bg.sprite = hintFill.sprite;
        hintFill.type = Image.Type.Filled;
        hintFill.fillMethod = Image.FillMethod.Radial360;
        hintFill.fillOrigin = (int)Image.Origin360.Top;
        hintFill.fillAmount = 0f;
        hintFill.color = new Color(0.3f, 0.9f, 1f, 0.95f);

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(root.transform, false);
        TextMeshProUGUI t = label.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        t.text = "Space 길게 눌러 건너뛰기";
        t.fontSize = 22f;
        t.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.textWrappingMode = TextWrappingModes.NoWrap;   // 250 폭에서 마지막 '기'가 다음 줄로 떨어졌다
        RectTransform lr = t.rectTransform;
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(1f, 0f);
        lr.anchoredPosition = new Vector2(-40f, 48f);
        lr.sizeDelta = new Vector2(320f, 30f);
    }

    private static Sprite circle;

    private static Sprite MakeCircleSprite()
    {
        if (circle != null) return circle;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(n / 2f - r) * Mathf.Clamp01(r - (n / 2f - 7f));   // 고리
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return circle;
    }
}

/// <summary>시네마틱 인물에서 플레이어 스크립트를 뺐더니 걷기 애니메이션의 발소리 이벤트가 받을 곳이 없어 오류가 났다. 받기만 한다(발소리는 타임라인 소리에 들어 있다).</summary>
public class AetherCinematicStepReceiver : MonoBehaviour
{
    public void OnStep_L() { }
    public void OnStep_R() { }
    public void OnStep() { }
    public void FootL() { }
    public void FootR() { }
}

/// <summary>시네마틱 다음 장면에서 플레이어를 찾아(캐릭터 선택이 끝날 때까지 몇 프레임 걸린다) 정해진 시간 동안 피해를 받지 않게 한다.</summary>
public class AetherArrivalGuard : MonoBehaviour
{
    public float Seconds = 8f;

    private System.Collections.IEnumerator Start()
    {
        float until = Time.time + Seconds;
        // 플레이어가 늦게 켜지거나(캐릭터 가드) 다른 장치가 값을 덮어도 끝날 때까지 계속 건다.
        while (Time.time < until)
        {
            Transform p = AetherPlayerCharacter.FindActiveTransform();
            PlayerStatus status = p != null ? (p.GetComponentInParent<PlayerStatus>() ?? p.GetComponentInChildren<PlayerStatus>()) : null;
            if (status != null && status.InvulnerableUntil < until)
                status.InvulnerableUntil = until;
            yield return new WaitForSeconds(0.25f);
        }
        Destroy(gameObject);
    }
}
