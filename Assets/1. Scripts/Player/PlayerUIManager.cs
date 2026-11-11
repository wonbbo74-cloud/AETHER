using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player HUD(상호작용 UI) 전담:
/// - 크로스헤어(hover 시 색/크기/스프라이트 보간)
/// - 픽업 패널(F : 아이템명 + 아이콘) 표시/숨김
/// 
/// 주의:
/// - 이 스크립트는 "표시만" 담당한다.
/// - Interact 호출/거리 판정/대상 탐색은 PlayerInteractor가 담당한다.
/// </summary>
public class PlayerUIManager : MonoBehaviour
{
    [Header("Interactor (필수)")]
    public PlayerInteractor interactor;

    [Header("Pickup UI")]
    public GameObject pickupPanel;           // "F : [아이콘] [이름]" 패널
    public TextMeshProUGUI pickupText;       // "F : 아이템명"
    public Image pickupIcon;                 // 아이콘

    [Header("Crosshair UI")]
    public Image crosshairImage;             // 중앙 십자선
    public Color normalColor = Color.white;
    public Color hoverColor = Color.cyan;
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = Vector3.one * 1.25f;
    public Sprite normalSprite;              // 기본 스프라이트(없으면 현재 스프라이트 유지)
    public Sprite hoverSprite;               // 호버 시 스프라이트(없으면 교체 안 함)
    [Range(0.01f, 30f)]
    public float transitionSpeed = 12f;

    [Header("Item Name Text (선택)")]
    public Text itemNameText;                // 중앙 상/하단 표시용(원하면 사용)

    [Header("Quick Slot UIs")]
    public QuickSlotManager quickSlotManager;
    public QuickSlotUI[] quickSlotUIs; // 0~4 순서로 넣기

    // 내부 상태(보간용)
    private Color currentColor;
    private Vector3 currentScale;

    private void Awake()
    {
        RefreshInteractionUI();

        if (quickSlotManager != null && quickSlotUIs != null && quickSlotUIs.Length > 0)
        {
            // UI들이 manager를 알게 하고
            foreach (var ui in quickSlotUIs)
                if (ui != null) ui.Bind(quickSlotManager);

            // manager가 UI 배열을 들고 갱신하도록 주입
            quickSlotManager.BindUI(quickSlotUIs);
        }

        StylePickupPanel();

        // 초기 UI 상태
        if (pickupPanel != null)
            pickupPanel.SetActive(false);

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

        currentColor = normalColor;
        currentScale = normalScale;

        ApplyCrosshairImmediate();
    }

    /// <summary>
    /// 줍기 안내("F : 아이템")를 HUD 와 같은 어두운 반투명 상자로 바꾼다.
    ///
    /// 원래 배경은 두 줄짜리 디자인 그림(301×137)을 430×96 으로 납작하게 늘린 것이라,
    /// 글자가 두 줄 경계에 걸려 아래가 잘리고 회색 바탕에 검은 글씨라 어두운 화면에서 잘 안 읽혔다.
    /// </summary>
    private void HideStrayPlaceholderTexts()
    {
        // Meka 프리팹(Hankyul_찐)의 Player_UI 에 이름 없는 견본 글자 'Text (TMP)'(흰 "0")·'Text (TMP) (1)'(빨간 "-25")가
        // 남아 화면 오른쪽 위에 항상 떠 있었다. 어느 스크립트도 쓰지 않는 견본이라 끈다.
        // 줍기 패널은 실행 중 새로 만들어지므로(처음엔 비어 있음) 패널과 상관없이 캐릭터 안의 'Player_UI' 바로 아래에서 찾는다.
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
        {
            Transform child = t.transform;
            if (child.parent == null || child.parent.name != "Player_UI") continue;
            if (!child.name.StartsWith("Text (TMP)") || t == pickupText) continue;
            child.gameObject.SetActive(false);
        }
    }

    private void StylePickupPanel()
    {
        HideStrayPlaceholderTexts();

        if (pickupPanel == null)
            return;

        RectTransform panel = pickupPanel.transform as RectTransform;
        if (panel != null)
        {
            panel.sizeDelta = new Vector2(220f, 42f);
            // 장면마다 자리가 달랐다(04 는 조준점 아래, 05~07 은 조준점 오른쪽). 캔버스 바로 아래에 있으면 조준점 아래 가운데로 맞춘다.
            if (panel.parent != null && panel.parent.GetComponent<Canvas>() != null)
            {
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 1f);
                panel.anchoredPosition = new Vector2(0f, -90f);
            }
        }

        Image panelImage = pickupPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = null;
            panelImage.color = new Color(0.02f, 0.06f, 0.1f, 0.78f);
        }

        foreach (Transform child in pickupPanel.transform)
        {
            // 프리팹에 남은 예전 안내 문구("F키를 눌러 주으십시오.", 검은 글씨)가 새 문구 뒤로 겹쳐 비쳤다.
            TMP_Text strayText = child.GetComponent<TMP_Text>();
            if (strayText != null && strayText != pickupText)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            if (child.name == "Bg")
            {
                Image bg = child.GetComponent<Image>();
                if (bg != null)
                {
                    // 왼쪽 가장자리의 얇은 청록 줄
                    bg.sprite = null;
                    bg.color = new Color(0.25f, 0.85f, 0.95f, 0.9f);
                    RectTransform r = bg.rectTransform;
                    r.anchorMin = new Vector2(0f, 0f);
                    r.anchorMax = new Vector2(0f, 1f);
                    r.pivot = new Vector2(0f, 0.5f);
                    r.anchoredPosition = Vector2.zero;
                    r.sizeDelta = new Vector2(4f, 0f);
                }
            }
        }

        if (pickupIcon != null)
        {
            RectTransform r = pickupIcon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(12f, 0f);
            r.sizeDelta = new Vector2(28f, 28f);
            pickupIcon.preserveAspect = true;
        }

        if (pickupText != null)
        {
            RectTransform r = pickupText.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(50f, 2f);
            r.offsetMax = new Vector2(-14f, -4f);
            pickupText.color = new Color(0.92f, 0.96f, 0.98f, 0.95f);
            pickupText.enableAutoSizing = false;
            pickupText.fontSize = 19f;
            pickupText.alignment = TextAlignmentOptions.MidlineLeft;
            pickupText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void OnEnable()
    {
        if (interactor != null)
            interactor.OnTargetChanged += RefreshInteractionUI;
    }

    private void OnDisable()
    {
        if (interactor != null)
            interactor.OnTargetChanged -= RefreshInteractionUI;
    }

    private void Start()
    {
        RefreshInteractionUI();
    }

    private void Update()
    {
        UpdateCrosshairVisuals();
    }

    /// <summary>
    /// PlayerInteractor의 대상/거리 상태가 바뀌었을 때 호출되어
    /// 패널/텍스트/아이콘을 갱신한다.
    /// </summary>
    private void RefreshInteractionUI()
    {
        if (interactor == null)
        {
            HideAllInteractionUI();
            return;
        }

        var target = interactor.CurrentInteractable;
        bool showPanel = interactor.CanShowUI && target is IPickupInteractable;

        // 패널 표시/숨김
        if (pickupPanel != null)
            pickupPanel.SetActive(showPanel);

        // 아이템 이름 텍스트(선택)
        if (itemNameText != null)
        {
            bool showName = showPanel && target != null && !string.IsNullOrEmpty(SafeGetName(target));
            itemNameText.gameObject.SetActive(showName);
            if (showName)
                itemNameText.text = SafeGetName(target);
        }

        if (!showPanel || target == null)
            return;

        // 패널 내부 텍스트
        if (pickupText != null)
        {
            string name = SafeGetName(target);
            // 다른 안내([F] 상점 열기 등)와 같은 모양으로, 키만 청록색으로 강조한다.
            pickupText.text = $"<color=#5FD3F0>[{interactor.interactKey}]</color>  {name}";

            // 글 길이에 맞춰 칸 폭을 맞춘다(짧은 "물"에 420px 칸이 떠 빈 곳이 너무 많았다).
            RectTransform panelRect = pickupPanel != null ? pickupPanel.transform as RectTransform : null;
            if (panelRect != null)
            {
                float textWidth = pickupText.GetPreferredValues(pickupText.text).x;
                panelRect.sizeDelta = new Vector2(Mathf.Clamp(textWidth + 50f + 18f, 150f, 520f), panelRect.sizeDelta.y);
            }
        }

        // 패널 내부 아이콘
        if (pickupIcon != null)
        {
            var icon = target.GetIcon();
            pickupIcon.sprite = icon;
            pickupIcon.enabled = (icon != null);
        }
    }

    /// <summary>
    /// 크로스헤어는 부드러운 전환이 필요하므로 매 프레임 보간 업데이트.
    /// (표시 조건은 "정면에 상호작용 대상이 있는가" 기준)
    /// </summary>
    private void UpdateCrosshairVisuals()
    {
        if (crosshairImage == null) return;

        // ✅ pickupPanel 표시 타이밍과 동일하게 hover
        bool hovering = (interactor != null && interactor.CanShowUI);

        Color targetColor = hovering ? hoverColor : normalColor;
        Vector3 targetScale = hovering ? hoverScale : normalScale;

        Sprite targetSprite = null;
        if (hovering && hoverSprite != null) targetSprite = hoverSprite;
        else if (!hovering && normalSprite != null) targetSprite = normalSprite;

        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);
        currentScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * transitionSpeed);

        crosshairImage.color = currentColor;
        crosshairImage.rectTransform.localScale = currentScale;

        if (targetSprite != null && crosshairImage.sprite != targetSprite)
            crosshairImage.sprite = targetSprite;
    }

    private void ApplyCrosshairImmediate()
    {
        if (crosshairImage == null) return;

        crosshairImage.color = normalColor;
        crosshairImage.rectTransform.localScale = normalScale;

        if (normalSprite != null)
            crosshairImage.sprite = normalSprite;
    }

    private void HideAllInteractionUI()
    {
        if (pickupPanel != null)
            pickupPanel.SetActive(false);

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

        // 크로스헤어는 "숨김"이 아니라 기본 상태로 되돌림
        currentColor = normalColor;
        currentScale = normalScale;
        ApplyCrosshairImmediate();
    }

    private static string SafeGetName(IInteractable interactable)
    {
        try
        {
            return interactable.GetDisplayName();
        }
        catch
        {
            return "Item";
        }
    }
}
