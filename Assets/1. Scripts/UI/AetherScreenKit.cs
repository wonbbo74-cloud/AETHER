using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 디자인 이미지(3840 x 2160) 위에 조작 요소를 얹기 위한 공용 도구.
///
/// 이미지에서 잰 픽셀 좌표를 그대로 넣으면 앵커로 바꿔 준다. 좌표는 이미지 왼쪽 위가 (0,0)이고,
/// 디자인 시안에서 자로 잰 값을 변환 없이 쓸 수 있다.
/// </summary>
public static class AetherScreenKit
{
    public const float DesignWidth = 3840f;
    public const float DesignHeight = 2160f;

    public static readonly Color TextBright = new Color(0.92f, 0.96f, 1f, 1f);
    public static readonly Color TextDim = new Color(0.62f, 0.72f, 0.80f, 1f);
    public static readonly Color Accent = new Color(0.45f, 0.83f, 1f, 1f);

    /// <summary>이미지 픽셀 좌표(왼쪽 위 기준)로 자식 RectTransform을 만든다.</summary>
    public static RectTransform CreateRect(RectTransform parent, string name, float x0, float y0, float x1, float y1,
        params System.Type[] components)
    {
        GameObject go = components != null && components.Length > 0
            ? new GameObject(name, components)
            : new GameObject(name, typeof(RectTransform));

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
            rect = go.AddComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x0 / DesignWidth, 1f - y1 / DesignHeight);
        rect.anchorMax = new Vector2(x1 / DesignWidth, 1f - y0 / DesignHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    /// <summary>디자인 이미지를 화면에 꽉 차게 깔고, 그 위에 요소를 올릴 기준 RectTransform을 돌려준다.</summary>
    /// <summary>
    /// 배경 그림에 박힌 글자를 같은 그림의 옆 빈 곳(srcOffsetX 만큼 옆)을 잘라 덮는다.
    /// 단색으로 덮으면 선형 색 공간·후처리 때문에 색이 미묘하게 달라 네모가 보였다(10/12 최종 점검).
    /// </summary>
    public static void CreatePatch(RectTransform design, string name, float x0, float y0, float x1, float y1, float srcOffsetX)
    {
        Image backdrop = design != null ? design.GetComponent<Image>() : null;
        if (backdrop == null || backdrop.sprite == null)
            return;

        Sprite src = backdrop.sprite;
        Rect sr = src.rect;
        float sx = sr.width / DesignWidth;
        float sy = sr.height / DesignHeight;
        Rect crop = new Rect(sr.x + (x0 + srcOffsetX) * sx, sr.y + (DesignHeight - y1) * sy, (x1 - x0) * sx, (y1 - y0) * sy);

        RectTransform rect = CreateRect(design, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image));
        Image image = rect.GetComponent<Image>();
        image.sprite = Sprite.Create(src.texture, crop, new Vector2(0.5f, 0.5f), 100f);
        image.color = backdrop.color;
        image.raycastTarget = false;
    }

    public static RectTransform CreateBackdrop(Transform parent, string spriteResourcePath, out Image image)
    {
        GameObject go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        image = go.GetComponent<Image>();
        image.raycastTarget = false;

        Sprite sprite = Resources.Load<Sprite>(spriteResourcePath);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            rect.sizeDelta = new Vector2(1920f, 1080f);
        }
        else
        {
            image.color = new Color(0.02f, 0.04f, 0.06f, 0.98f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            Debug.LogWarning($"[AetherScreenKit] Resources/{spriteResourcePath} 스프라이트를 찾지 못했습니다.");
        }

        return rect;
    }

    /// <summary>
    /// 화면을 덮는 캔버스.
    ///
    /// ScreenMatchMode.Expand를 쓰면 1920x1080 기준 영역이 어떤 해상도·화면비에서도
    /// 통째로 화면 안에 들어간다(모자란 쪽은 여백). 16:10이나 울트라와이드에서
    /// UI 가장자리가 잘려 나가는 것을 막고, 글자 크기도 이미지와 같은 비율로 커지고 작아진다.
    /// </summary>
    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // 크기 정규화 대상에서 제외(이미 화면비에 맞춰져 있다).
        go.AddComponent<AetherDesignCanvasMarker>();

        return canvas;
    }

    /// <summary>
    /// 디자인 이미지(3840 폭) 기준으로 잰 글자 크기를 화면(1920 폭) 크기로 바꾼다.
    /// 시안에서 잰 픽셀값을 그대로 넣으면 이미지에 인쇄된 글자와 같은 크기로 나온다.
    /// </summary>
    public static float Font(float designPixels)
    {
        return designPixels * 1920f / DesignWidth;
    }

    public static TMP_Text CreateText(RectTransform parent, string name, float x0, float y0, float x1, float y1,
        string content, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(TextMeshProUGUI));

        TMP_Text text = rect.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        return text;
    }

    /// <summary>이미지 위에 얹는 투명 버튼. 그림이 디자인을 담당하므로 강조만 살짝 준다.</summary>
    public static Button CreateHitButton(RectTransform parent, string name, float x0, float y0, float x1, float y1,
        UnityEngine.Events.UnityAction action, out Image highlight)
    {
        RectTransform rect = CreateRect(parent, name, x0, y0, x1, y1,
            typeof(RectTransform), typeof(Image), typeof(Button));

        Image hit = rect.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        RectTransform highlightRect = CreateRect(rect, "Highlight", 0f, 0f, 0f, 0f);
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = Vector2.zero;
        highlightRect.offsetMax = Vector2.zero;

        highlight = highlightRect.gameObject.AddComponent<Image>();
        highlight.color = new Color(Accent.r, Accent.g, Accent.b, 0f);
        highlight.raycastTarget = false;

        Button button = rect.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (action != null)
            button.onClick.AddListener(action);

        AetherHoverGlow glow = rect.gameObject.AddComponent<AetherHoverGlow>();
        glow.Setup(highlight, 0.10f);

        return button;
    }
}

/// <summary>마우스를 올렸을 때 지정한 이미지를 옅게 밝히는 작은 도우미.</summary>
public class AetherHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image target;
    private float hoverAlpha = 0.1f;
    private float selectedAlpha;
    private bool hovering;

    public void Setup(Image image, float alpha)
    {
        target = image;
        hoverAlpha = alpha;
        ApplyAlpha();
    }

    /// <summary>선택 상태(클릭으로 고정된 강조)를 따로 유지한다.</summary>
    public void SetSelected(bool selected)
    {
        selectedAlpha = selected ? 0.22f : 0f;
        ApplyAlpha();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        ApplyAlpha();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        ApplyAlpha();
    }

    private void OnDisable()
    {
        hovering = false;
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        if (target == null)
            return;

        Color color = target.color;
        color.a = Mathf.Max(selectedAlpha, hovering ? hoverAlpha : 0f);
        target.color = color;
    }
}
