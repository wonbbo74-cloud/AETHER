using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 진행 안내를 화면에 잠깐 띄운다.
///
/// 구역 연출이 "지금 뭘 해야 하는지"를 알려 줘야 하는데, Debug.Log는 플레이어에게 보이지 않는다.
/// HUD의 목표 패널이 있으면 그쪽에 쓰고, 없으면 화면 아래쪽에 직접 띄운다.
/// </summary>
public class AetherObjectiveToast : MonoBehaviour
{
    private const float DefaultSeconds = 6f;

    private static AetherObjectiveToast instance;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private readonly Queue<string> queue = new Queue<string>();
    private bool showing;

    /// <summary>같은 문구를 연달아 띄우지 않기 위해 마지막 문구를 기억한다.</summary>
    private static string lastMessage;
    private static float lastMessageTime;

    private static float lastChimeTime = -10f;

    public static void Show(string message, float seconds = DefaultSeconds)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        // 웨이브 판정처럼 자주 도는 코드에서 불려도 도배되지 않게 한다.
        if (message == lastMessage && Time.unscaledTime - lastMessageTime < 3f)
            return;

        lastMessage = message;
        lastMessageTime = Time.unscaledTime;
        // 새 안내가 떴다는 짧은 알림음. 한꺼번에 여러 개가 들어와도 한 번만.
        if (Time.unscaledTime - lastChimeTime > 1f)
        {
            lastChimeTime = Time.unscaledTime;
            AetherAudio.Play2D("ui_confirm", 0.35f, 1f, 0f);
        }

        // 퍼즐2 화면에는 HUD 가 없어, 레벨업 안내가 아래쪽 대체 칸에 떠 [계속 진행 (F)] 버튼을 덮었다.
        // 퍼즐2 에서 나온 안내는 모아 두었다가 돌아간 장면의 목표 칸에 띄운다.
        // 퍼즐3(숫자 해독)도 HUD 가 없는 화면이라 같이 모아 둔다.
        string activeName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeName == "Puzzle2" || activeName == "Puzzle3")
        {
            HoldUntilNextScene(message, seconds);
            return;
        }

        // HUD 목표 패널이 있으면 그곳이 제자리다.
        if (AetherModernHUD.SetObjective(message, seconds))
            return;

        EnsureInstance();
        instance.Enqueue(message, seconds);
    }

    private static readonly List<KeyValuePair<string, float>> held = new List<KeyValuePair<string, float>>();
    private static bool holdHooked;

    private static void HoldUntilNextScene(string message, float seconds)
    {
        held.Add(new KeyValuePair<string, float>(message, seconds));
        if (holdHooked)
            return;

        holdHooked = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "Puzzle2" || scene.name == "Puzzle3" || held.Count == 0)
                return;
            EnsureInstance();
            instance.StartCoroutine(instance.ReleaseHeld());
        };
    }

    private IEnumerator ReleaseHeld()
    {
        // 돌아간 장면의 HUD 가 만들어진 뒤에 띄운다.
        yield return new WaitForSecondsRealtime(1.2f);
        var pending = new List<KeyValuePair<string, float>>(held);
        held.Clear();
        foreach (var item in pending)
        {
            lastMessage = null;
            Show(item.Key, item.Value);
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AETHER_ObjectiveToast");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherObjectiveToast>();
        instance.Build();
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panel.transform.SetParent(transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 168f);
        rect.sizeDelta = new Vector2(820f, 58f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.03f, 0.05f, 0.07f, 0.72f);
        background.raycastTarget = false;

        group = panel.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 6f);
        textRect.offsetMax = new Vector2(-20f, -6f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.85f, 0.95f, 1f);
        label.raycastTarget = false;
    }

    private void Enqueue(string message, float seconds)
    {
        queue.Enqueue(message);

        if (!showing)
            StartCoroutine(Pump(seconds));
    }

    private IEnumerator Pump(float seconds)
    {
        showing = true;

        while (queue.Count > 0)
        {
            label.text = queue.Dequeue();
            // 여러 줄 안내가 한 줄 높이(58) 칸 밖으로 넘쳤다. 글 높이에 맞춘다.
            RectTransform panelRect = (RectTransform)label.transform.parent;
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, Mathf.Max(58f, label.GetPreferredValues(label.text, 780f, 0f).y + 16f));

            yield return Fade(0f, 1f, 0.18f);
            yield return new WaitForSecondsRealtime(seconds);
            yield return Fade(1f, 0f, 0.35f);
        }

        showing = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }
}
