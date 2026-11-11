using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ModernInventorySkin
{
    private const string BackgroundResource = "UI/InventoryModern/InventoryFrame";
    private const string LayoutName = "ModernInventoryLayout";
    private const float ReferenceAspect = 1679f / 936f;

    public static void Apply(InventoryUI inventoryUI)
    {
        if (inventoryUI == null || inventoryUI.inventoryPanel == null)
            return;

        RectTransform inventoryRoot = inventoryUI.transform as RectTransform;
        if (inventoryRoot != null)
            Stretch(inventoryRoot);

        RectTransform window = inventoryUI.inventoryPanel.GetComponent<RectTransform>();
        if (window == null)
            return;

        Stretch(window);
        DisableLegacyWindowArtwork(window);

        RectTransform layout = FindOrCreateLayout(window);
        CreateFullscreenBackdrop(window);
        CreateBackground(layout);
        ConfigureCloseButton(window, layout);
        ConfigureSlots(inventoryUI, layout);
        ConfigureItemInfo(inventoryUI, layout);
        ConfigureFonts(layout);
    }

    private static RectTransform FindOrCreateLayout(RectTransform window)
    {
        Transform existing = window.Find(LayoutName);
        RectTransform layout;
        if (existing != null)
        {
            layout = existing as RectTransform;
        }
        else
        {
            GameObject go = new GameObject(LayoutName, typeof(RectTransform), typeof(AspectRatioFitter));
            layout = go.GetComponent<RectTransform>();
            layout.SetParent(window, false);
        }

        Stretch(layout);
        AspectRatioFitter fitter = layout.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = ReferenceAspect;
        layout.SetAsFirstSibling();
        return layout;
    }

    private static void CreateFullscreenBackdrop(RectTransform window)
    {
        const string backdropName = "ModernInventoryBackdrop";
        Transform existing = window.Find(backdropName);
        Image image;

        if (existing == null)
        {
            GameObject go = new GameObject(backdropName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(window, false);
            Stretch(rect);
            image = go.GetComponent<Image>();
        }
        else
        {
            RectTransform rect = existing as RectTransform;
            if (rect != null)
                Stretch(rect);
            image = existing.GetComponent<Image>();
        }

        image.sprite = null;
        image.color = new Color(0.003f, 0.012f, 0.02f, 1f);
        image.raycastTarget = true;
        image.transform.SetAsFirstSibling();
    }

    private static void CreateBackground(RectTransform layout)
    {
        Transform existing = layout.Find("InventoryFrame");
        RawImage image;
        if (existing == null)
        {
            GameObject go = new GameObject("InventoryFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(layout, false);
            Stretch(rect);
            image = go.GetComponent<RawImage>();
        }
        else
        {
            image = existing.GetComponent<RawImage>();
        }

        image.texture = Resources.Load<Texture2D>(BackgroundResource);
        image.color = Color.white;
        image.raycastTarget = false;
        image.transform.SetAsFirstSibling();
    }

    private static void DisableLegacyWindowArtwork(RectTransform window)
    {
        Image windowImage = window.GetComponent<Image>();
        if (windowImage != null)
            windowImage.enabled = false;

        string[] artworkNames = { "Bg", "Gradient", "Frame", "Effects" };
        for (int i = 0; i < artworkNames.Length; i++)
        {
            Transform child = window.Find(artworkNames[i]);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        Transform contents = window.Find("Contents");
        if (contents != null)
        {
            Transform title = contents.Find("Text (TMP)WindowTitle");
            if (title != null)
                title.gameObject.SetActive(false);

            Transform bottomLine = contents.Find("LineBottom");
            if (bottomLine != null)
                bottomLine.gameObject.SetActive(false);
        }
    }

    private static void ConfigureCloseButton(RectTransform window, RectTransform layout)
    {
        Transform close = window.Find("ButtonX");
        if (close == null)
            return;

        RectTransform rect = close as RectTransform;
        rect.SetParent(layout, false);
        SetNormalizedRect(rect, new Vector2(0.862f, 0.884f), new Vector2(0.949f, 0.969f));

        Image image = close.GetComponent<Image>();
        if (image != null)
            image.color = new Color(1f, 1f, 1f, 0.001f);

        for (int i = 0; i < close.childCount; i++)
            close.GetChild(i).gameObject.SetActive(false);
    }

    private static void ConfigureSlots(InventoryUI inventoryUI, RectTransform layout)
    {
        if (inventoryUI.slotsParent == null)
            return;

        RectTransform content = inventoryUI.slotsParent as RectTransform;
        if (content == null)
            return;

        Transform scrollTransform = content;
        while (scrollTransform.parent != null && scrollTransform.GetComponent<ScrollRect>() == null)
            scrollTransform = scrollTransform.parent;

        RectTransform scroll = scrollTransform as RectTransform;
        if (scroll != null && scroll.GetComponent<ScrollRect>() != null)
        {
            scroll.SetParent(layout, false);
            SetNormalizedRect(scroll, new Vector2(0.076f, 0.116f), new Vector2(0.648f, 0.754f));

            Transform viewport = scroll.Find("Viewport");
            if (viewport != null)
            {
                Image viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                    viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            }

            Transform scrollbar = scroll.Find("Scrollbar Vertical");
            if (scrollbar != null)
                scrollbar.gameObject.SetActive(false);
        }

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.cellSize = new Vector2(146f, 139f);
            grid.spacing = new Vector2(15f, 13f);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        InventorySlotUI[] slots = content.GetComponentsInChildren<InventorySlotUI>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlotUI slot = slots[i];
            Image hitArea = slot.GetComponent<Image>();
            if (hitArea != null)
            {
                hitArea.color = new Color(1f, 1f, 1f, 0.001f);
                hitArea.raycastTarget = true;
            }

            slot.normalCheckColor = new Color(1f, 1f, 1f, 0f);
            slot.selectedCheckColor = new Color(0f, 0.85f, 1f, 0.9f);

            if (slot.iconImage != null)
            {
                RectTransform icon = slot.iconImage.rectTransform;
                icon.anchorMin = new Vector2(0.15f, 0.15f);
                icon.anchorMax = new Vector2(0.85f, 0.85f);
                icon.offsetMin = Vector2.zero;
                icon.offsetMax = Vector2.zero;
                slot.iconImage.preserveAspect = true;
                slot.iconImage.raycastTarget = false;
            }

            if (slot.countText != null)
            {
                slot.countText.fontSize = 18f;
                slot.countText.alignment = TextAlignmentOptions.BottomRight;
                slot.countText.color = Color.white;
            }
        }
    }

    private static void ConfigureItemInfo(InventoryUI inventoryUI, RectTransform layout)
    {
        if (inventoryUI.detailUI == null)
            return;

        RectTransform info = inventoryUI.detailUI.transform as RectTransform;
        if (info == null)
            return;

        info.SetParent(layout, false);
        SetNormalizedRect(info, new Vector2(0.675f, 0.105f), new Vector2(0.936f, 0.775f));

        if (inventoryUI.detailUI.itemImage != null)
        {
            inventoryUI.detailUI.itemImage.preserveAspect = true;
            inventoryUI.detailUI.itemImage.color = Color.white;
        }

        StyleText(inventoryUI.detailUI.nameText, 24f, Color.white);
        StyleText(inventoryUI.detailUI.descriptionText, 17f, new Color(0.75f, 0.82f, 0.88f, 1f));
    }

    private static void ConfigureFonts(RectTransform root)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");
        if (font == null)
            return;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
            texts[i].font = font;
    }

    private static void StyleText(TextMeshProUGUI text, float size, Color color)
    {
        if (text == null)
            return;

        text.fontSize = size;
        text.color = color;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetNormalizedRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
