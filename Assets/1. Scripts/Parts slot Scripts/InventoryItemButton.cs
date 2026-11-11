using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("이 슬롯이 나타내는 아이템")]
    public EquipmentItemData item;

    [Header("슬롯에 보일 아이콘(UI Image)")]
    public Image iconImage;

    private Button _button;
    private CanvasGroup _canvasGroup;
    private EquipmentUIController _controller;

    // 드래그 시 따라다니는 고스트(아이콘)
    private GameObject _dragGhostGO;
    private RectTransform _dragGhostRect;
    private Image _dragGhostImage;

    private Canvas _rootCanvas;
    private bool _sourceIconWasEnabled;

    private void Awake()
    {
        _button = GetComponent<Button>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Setup(EquipmentUIController controller)
    {
        _controller = controller;

        RefreshIcon();
        RefreshInteractable();

        // 클릭하면 우측 정보 표시
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() =>
            {
                if (_controller != null && item != null)
                    _controller.SelectItem(item, this);
            });
        }
    }

    public void RefreshIcon()
    {
        if (iconImage == null) return;

        Sprite icon = GetDisplayIcon();
        if (item != null && icon != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
        }
        else
        {
            // ✅ 아이템이 없으면 아이콘 “완전히” 숨김
            iconImage.enabled = false;
            iconImage.sprite = null;
        }
    }

    public void RefreshInteractable()
    {
        if (_button == null) return;
        _button.interactable = (item != null);
    }

    // 컨트롤러가 “소비(제거)”할 때 호출
    public void Consume()
    {
        item = null;
        RefreshIcon();
        RefreshInteractable();
    }

    // =========================
    // 드래그 시작
    // =========================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item == null || GetDisplayIcon() == null) return;
        if (_controller == null) return;

        // 드래그 시작 시에도 선택 처리(우측 정보 갱신)
        _controller.SelectItem(item, this);

        _sourceIconWasEnabled = iconImage != null && iconImage.enabled;
        if (iconImage != null)
            iconImage.enabled = false;

        CreateDragGhost();
        UpdateDragGhostPosition(eventData);

        // 드래그 중엔 이 슬롯이 레이캐스트 막지 않게
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhostGO == null) return;
        UpdateDragGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragGhost();

        // 원상복구
        if (_canvasGroup != null)
            _canvasGroup.blocksRaycasts = true;

        if (item != null && iconImage != null)
            iconImage.enabled = _sourceIconWasEnabled;
    }

    private void CreateDragGhost()
    {
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null) return;

        _dragGhostGO = new GameObject("DragGhost_Icon");
        _dragGhostGO.transform.SetParent(_rootCanvas.transform, false);

        _dragGhostRect = _dragGhostGO.AddComponent<RectTransform>();
        _dragGhostImage = _dragGhostGO.AddComponent<Image>();
        var cg = _dragGhostGO.AddComponent<CanvasGroup>();

        // 드롭 판정 방해 안 하게
        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0.9f;

        _dragGhostImage.sprite = GetDisplayIcon();
        _dragGhostImage.preserveAspect = true;

        // 원본 아이콘 크기 따라가기
        if (iconImage != null)
        {
            RectTransform iconRect = (RectTransform)iconImage.transform;
            Vector2 size = iconRect.rect.size;
            if (size.x <= 1f || size.y <= 1f)
                size = iconRect.sizeDelta;

            _dragGhostRect.sizeDelta = size;
        }
        else
            _dragGhostRect.sizeDelta = new Vector2(80, 80);
    }

    private void UpdateDragGhostPosition(PointerEventData eventData)
    {
        if (_rootCanvas == null || _dragGhostRect == null) return;

        RectTransform canvasRect = _rootCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPos))
        {
            _dragGhostRect.anchoredPosition = localPos;
        }
    }

    private void DestroyDragGhost()
    {
        if (_dragGhostGO != null)
            Destroy(_dragGhostGO);

        _dragGhostGO = null;
        _dragGhostRect = null;
        _dragGhostImage = null;
    }

    private Sprite GetDisplayIcon()
    {
        if (item == null)
            return null;

        if (item.icon != null)
            return item.icon;

        return item.sourceCraftedPart != null ? item.sourceCraftedPart.itemSprite : null;
    }
}
