using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 체험판이 끝나는 지점에서 "TO BE CONTINUED" 를 띄우고 시작 화면으로 돌려보낸다.
///
/// 1구역 이후는 이번 빌드에 넣지 않기로 했다. 그래서 엘리베이터에서 1구역을 고르면
/// 씬을 불러오는 대신 이 연출을 보여 주고 처음 화면으로 자연스럽게 되돌린다.
///
/// 화면을 통째로 덮어야 하므로 다른 UI보다 위에 그리고, 중간에 조작이 끼어들지 못하게 막는다.
/// 일시정지로 Time.timeScale 이 0이어도 흘러가야 해서 전부 unscaled 시간을 쓴다.
/// </summary>
public class AetherToBeContinued : MonoBehaviour
{
    private const string StartSceneName = "01.스타트씬";

    private const string Message = "TO BE CONTINUED...";

    private const float FadeInSeconds = 1.2f;
    private const float HoldSeconds = 3.2f;
    private const float FadeOutSeconds = 1.4f;

    private static bool running;

    private CanvasGroup group;
    private TMP_Text label;

    /// <summary>
    /// 엔딩 연출을 시작한다. 이미 돌고 있으면 무시한다.
    /// </summary>
    public static void Play()
    {
        if (running)
            return;

        running = true;

        GameObject host = new GameObject("AETHER_ToBeContinued");
        DontDestroyOnLoad(host);
        host.AddComponent<AetherToBeContinued>();
    }

    private void Start()
    {
        Build();
        StartCoroutine(Run());
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 일시정지 메뉴(300)나 안내 토스트(400)보다도 위에 와야 한다.
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // 연출 중에는 뒤쪽 버튼이 눌리지 않아야 한다.
        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = true;

        GameObject background = new GameObject("Black", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(transform, false);
        Stretch(background.GetComponent<RectTransform>());

        Image image = background.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        GameObject textObject = new GameObject("Message", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        Stretch(textObject.GetComponent<RectTransform>());

        // HUD 와 같은 글꼴(나눔고딕코딩)과 청록 포인트로 게임 화면과 결을 맞춘다. 처음엔 숨겨 두고 검은 화면이 다 덮인 뒤 나타난다.
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");

        label = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = Message;
        label.fontSize = 84f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.93f, 0.95f, 1f, 0f);
        label.characterSpacing = 14f;
        label.raycastTarget = false;

        // 제목 아래 가는 청록 선. 나타날 때 가운데서 양옆으로 그어진다.
        GameObject dividerObject = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerObject.transform.SetParent(transform, false);
        divider = dividerObject.GetComponent<RectTransform>();
        divider.anchorMin = divider.anchorMax = divider.pivot = new Vector2(0.5f, 0.5f);
        divider.anchoredPosition = new Vector2(0f, -66f);
        divider.sizeDelta = new Vector2(0f, 2f);
        dividerImage = dividerObject.GetComponent<Image>();
        dividerImage.color = new Color(0.31f, 0.77f, 0.91f, 0f);
        dividerImage.raycastTarget = false;

        // 이번 판의 에테르 레벨을 제목 아래에 남긴다. 제목과 함께 사라진다.
        GameObject summaryObject = new GameObject("AetherSummary", typeof(RectTransform));
        summaryObject.transform.SetParent(transform, false);
        RectTransform summaryRect = summaryObject.GetComponent<RectTransform>();
        summaryRect.anchorMin = summaryRect.anchorMax = summaryRect.pivot = new Vector2(0.5f, 0.5f);
        summaryRect.anchoredPosition = new Vector2(0f, -110f);
        summaryRect.sizeDelta = new Vector2(1200f, 50f);
        summary = summaryObject.AddComponent<TextMeshProUGUI>();
        if (font != null) summary.font = font;
        summary.text = AetherLevel.Summary();
        summary.fontSize = 28f;
        summary.alignment = TextAlignmentOptions.Center;
        summary.color = new Color(0.31f, 0.77f, 0.91f, 0f);
        summary.characterSpacing = 4f;
        summary.raycastTarget = false;
    }

    private TextMeshProUGUI summary;
    private RectTransform divider;
    private Image dividerImage;

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator Run()
    {
        // 조작을 멈춘다. 연출 중에 플레이어가 움직이면 몰입이 깨진다.
        FreezePlayer();

        // 한 판을 끝냈다. 이어하기 저장을 지운다(끝 화면 도중에 다시 저장되지도 않게 막는다).
        AetherSave.DeleteSave();

        // 엘리베이터가 움직이는 소리와 함께 화면이 검게 덮이고, 도착음과 함께 글자가 떠오른다.
        AetherAudio.Play2D("elevator_move", 0.55f, 1f, 0f);
        yield return Fade(0f, 1f, FadeInSeconds);
        AetherAudio.Play2D("elevator_ding", 0.6f, 1f, 0f);
        yield return FadeLabel(0f, 1f, 1.1f);
        yield return new WaitForSecondsRealtime(HoldSeconds);

        // 글자만 먼저 지우고 검은 화면을 남겨, 다음 씬으로 자연스럽게 넘어가게 한다.
        yield return FadeLabel(1f, 0f, FadeOutSeconds);

        // 돌아가면 새 판이므로 지난 판의 인벤토리·장비 기록을 비운다.
        PartSystemRuntimeState.ResetForNewRun();
        AetherLevel.ResetRun();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(StartSceneName);

        // 새 씬이 자리를 잡은 뒤에 검은 화면을 걷어낸다.
        yield return null;
        yield return Fade(1f, 0f, 0.8f);

        running = false;
        Destroy(gameObject);
    }

    /// <summary>연출이 시작되면 플레이어가 움직이거나 쏘지 못하게 한다.</summary>
    private static void FreezePlayer()
    {
        foreach (Move_Hankyul move in FindObjectsByType<Move_Hankyul>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            move.allowInput = false;
            move.allowMove = false;
            move.allowLook = false;
        }

        foreach (Move_Yunseo move in FindObjectsByType<Move_Yunseo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            move.enabled = false;

        // 쏘기·근접 공격도 막는다. (예전에는 이동만 막아서 연출 중에도 공격할 수 있었다.)
        foreach (Hankyul_Fire fire in FindObjectsByType<Hankyul_Fire>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            fire.enabled = false;

        foreach (Yunseo_Battle battle in FindObjectsByType<Yunseo_Battle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            battle.enabled = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / seconds);
            yield return null;
        }

        group.alpha = to;
    }

    private IEnumerator FadeLabel(float from, float to, float seconds)
    {
        Color color = label.color;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(from, to, elapsed / seconds);
            label.color = color;
            if (summary != null) summary.alpha = color.a * 0.9f;
            SetDivider(color.a, to > from ? Mathf.Clamp01(elapsed / seconds) : 1f);
            yield return null;
        }

        color.a = to;
        label.color = color;
        if (summary != null) summary.alpha = to * 0.9f;
        SetDivider(to, 1f);
    }

    // 가운데서 양옆으로 그어지는 선(최대 560px).
    private void SetDivider(float alpha, float grow)
    {
        if (divider == null) return;
        divider.sizeDelta = new Vector2(560f * Mathf.SmoothStep(0f, 1f, grow), 2f);
        Color c = dividerImage.color;
        c.a = alpha * 0.8f;
        dividerImage.color = c;
    }
}
