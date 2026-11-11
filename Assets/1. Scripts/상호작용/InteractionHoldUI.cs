using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionHoldUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image radialFillImage;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private string fallbackTitle = "상호작용";
    [SerializeField] private string fallbackActionText = InteractionPromptTextUtility.HoldPrefix;

    [Header("크기 맞추기")]
    [Tooltip("씬에 배치된 상호작용 안내를 다른 씬과 같은 크기로 맞춥니다. 끄면 씬에 있는 그대로 씁니다.")]
    [SerializeField] private bool normalizeLayout = true;

    // 기준값은 ProximityHoldPrompt(파츠 제작실 등에서 쓰는 안내)와 같다.
    // 05.폐허된 도시에 배치된 것은 링이 100px, 글자 상자가 520x80이라
    // 화면 가운데를 가릴 만큼 크고 링과 글자가 따로 떨어져 보였다.
    private const float CircleDiameter = 58f;
    private const float CircleOffsetY = -132f;
    private const float LabelWidth = 420f;
    private const float LabelHeight = 34f;
    private const float LabelOffsetY = -180f;
    private const float LabelFontSize = 19f;

    private Object currentOwner;

    private void Awake()
    {
        ResolveTextReference();
        NormalizeLayout();
        ForceHide();
    }

    /// <summary>
    /// 링과 글자를 다른 씬의 안내와 같은 크기·자리로 맞춘다.
    ///
    /// 씬마다 손으로 배치해 둔 크기가 제각각이라, 어떤 씬에서는 화면 한가운데에
    /// 커다란 원과 글자 덩어리가 뜨고 어떤 씬에서는 작게 뜬다.
    /// 값을 한곳에 모아 두고 실행할 때 맞춰 주면 어느 씬에서나 같아 보인다.
    /// </summary>
    private void NormalizeLayout()
    {
        if (!normalizeLayout)
            return;

        // 안내 전체를 감싸는 판이 화면에 꽉 차 있어야 가운데 기준 배치가 뜻대로 된다.
        if (transform is RectTransform root && root.parent is RectTransform)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
        }

        PlaceCircle();
        PlaceLabel();
    }

    /// <summary>
    /// 링은 보통 배경과 채움 두 장으로 돼 있다. 둘 다 같은 크기·자리로 맞춘다.
    /// (채움이 배경 안에 들어 있는 구조면 배경만 옮기면 따라온다.)
    /// </summary>
    private void PlaceCircle()
    {
        if (radialFillImage == null)
            return;

        RectTransform fill = radialFillImage.rectTransform;
        bool fillFollowsParent = fill.anchorMin != fill.anchorMax;

        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image == null)
                continue;

            RectTransform rect = image.rectTransform;

            // 부모에 맞춰 늘어나는 채움은 부모만 옮기면 되므로 건드리지 않는다.
            if (rect == fill && fillFollowsParent)
                continue;

            // 링과 상관없는 그림(아이콘 등)까지 옮기지 않도록, 링과 비슷한 크기만 고른다.
            bool looksLikeRing = rect == fill
                                 || rect == fill.parent as RectTransform
                                 || Mathf.Abs(rect.rect.width - fill.rect.width) < 1f
                                 && Mathf.Abs(rect.rect.height - fill.rect.height) < 1f;

            if (!looksLikeRing)
                continue;

            CenterAt(rect, new Vector2(CircleDiameter, CircleDiameter), CircleOffsetY);
        }
    }

    private void PlaceLabel()
    {
        if (interactionText == null)
            return;

        CenterAt(interactionText.rectTransform, new Vector2(LabelWidth, LabelHeight), LabelOffsetY);

        interactionText.fontSize = LabelFontSize;
        interactionText.alignment = TextAlignmentOptions.Center;
        interactionText.enableWordWrapping = false;
        interactionText.overflowMode = TextOverflowModes.Overflow;
    }

    private static void CenterAt(RectTransform rect, Vector2 size, float offsetY)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, offsetY);
        rect.localScale = Vector3.one;
    }

    public void Show()
    {
        ShowFor(null, string.Empty, string.Empty);
    }

    public void Show(string title, string actionText)
    {
        ShowFor(null, title, actionText);
    }

    public void ShowFor(Object owner, string title, string actionText)
    {
        ShowFor(owner, title, actionText, false);
    }

    public void ShowFor(Object owner, string title, string actionText, bool suppressText)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        currentOwner = owner;
        ResolveTextReference();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (radialFillImage != null)
            radialFillImage.fillAmount = 0f;

        if (suppressText)
            ClearText();
        else
            SetText(title, actionText);
    }

    public void UpdateProgress(float value)
    {
        UpdateProgressFor(null, value);
    }

    public void UpdateProgressFor(Object owner, float value)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        if (radialFillImage != null)
            radialFillImage.fillAmount = Mathf.Clamp01(value);

        AetherAudio.HoldProgress(Mathf.Clamp01(value)); // 누르는 동안 차오르는 소리
    }

    public void HideImmediate()
    {
        ForceHide();
    }

    public void HideFor(Object owner)
    {
        if (currentOwner != null && owner != null && currentOwner != owner)
            return;

        ForceHide();
    }

    private void ForceHide()
    {
        currentOwner = null;
        AetherAudio.HoldEnd();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (radialFillImage != null)
            radialFillImage.fillAmount = 0f;
    }

    private void SetText(string title, string actionText)
    {
        ResolveTextReference();

        if (interactionText == null)
            return;

        interactionText.enabled = true;

        title = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim();
        actionText = string.IsNullOrWhiteSpace(actionText) ? fallbackActionText : actionText.Trim();
        interactionText.text = InteractionPromptTextUtility.FormatHoldPrompt(title, actionText);
    }

    private void ClearText()
    {
        ResolveTextReference();

        if (interactionText == null)
            return;

        interactionText.text = string.Empty;
        interactionText.enabled = false;
    }

    private void ResolveTextReference()
    {
        if (interactionText != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null)
                continue;

            string objectName = candidate.name.ToLowerInvariant();
            if (objectName.Contains("key"))
                continue;

            if (objectName.Contains("prompt")
                || objectName.Contains("label")
                || objectName.Contains("interaction")
                || objectName.Contains("action"))
            {
                interactionText = candidate;
                return;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || candidate.name.ToLowerInvariant().Contains("key"))
                continue;

            interactionText = candidate;
            return;
        }

        CreateFallbackInteractionText();
    }

    private void CreateFallbackInteractionText()
    {
        GameObject textObject = new GameObject("Interaction Action Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        interactionText = textObject.GetComponent<TMP_Text>();
        interactionText.alignment = TextAlignmentOptions.Center;
        interactionText.color = Color.white;
        interactionText.raycastTarget = false;

        // 자리와 크기는 다른 씬과 같은 기준을 따른다.
        CenterAt(textObject.GetComponent<RectTransform>(), new Vector2(LabelWidth, LabelHeight), LabelOffsetY);
        interactionText.fontSize = LabelFontSize;
    }
}

