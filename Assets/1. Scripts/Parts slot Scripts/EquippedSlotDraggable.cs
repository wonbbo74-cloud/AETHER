using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquippedSlotDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("어느 장착 슬롯인지")]
    public EquipmentUIController.EquipSlot slot;

    [Header("컨트롤러")]
    public EquipmentUIController controller;

    [Header("이 슬롯에서 실제로 보여주는 장착 아이콘 이미지")]
    public Image equippedIconImage;

    private CanvasGroup _canvasGroup;
    private Canvas _rootCanvas;

    // 드래그 고스트(아이콘)
    private GameObject _dragGhostGO;
    private RectTransform _dragGhostRect;
    private Image _dragGhostImage;

    private EquipmentItemData _draggingItem;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null) return;

        // ✅ 현재 슬롯에 장착된 아이템 가져오기
        _draggingItem = controller.GetEquippedItem(slot);
        if (_draggingItem == null || _draggingItem.icon == null) return;

        CreateDragGhost(_draggingItem.icon);
        UpdateGhostPos(eventData);

        // 드롭 판정 되게 레이캐스트 막지 않기
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhostGO == null) return;
        UpdateGhostPos(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyGhost();
        _canvasGroup.blocksRaycasts = true;
        _draggingItem = null;
    }

    private void CreateDragGhost(Sprite sprite)
    {
        if (_rootCanvas == null) _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null) return;

        _dragGhostGO = new GameObject("DragGhost_Equipped");
        _dragGhostGO.transform.SetParent(_rootCanvas.transform, false);

        _dragGhostRect = _dragGhostGO.AddComponent<RectTransform>();
        _dragGhostImage = _dragGhostGO.AddComponent<Image>();
        var cg = _dragGhostGO.AddComponent<CanvasGroup>();

        cg.blocksRaycasts = false;
        cg.interactable = false;
        cg.alpha = 0.9f;

        _dragGhostImage.sprite = sprite;
        _dragGhostImage.preserveAspect = true;

        // 크기: 기존 EquippedIcon 크기 따라가기
        if (equippedIconImage != null)
            _dragGhostRect.sizeDelta = ((RectTransform)equippedIconImage.transform).sizeDelta;
        else
            _dragGhostRect.sizeDelta = new Vector2(80, 80);
    }

    private void UpdateGhostPos(PointerEventData eventData)
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

    private void DestroyGhost()
    {
        if (_dragGhostGO != null) Destroy(_dragGhostGO);
        _dragGhostGO = null;
        _dragGhostRect = null;
        _dragGhostImage = null;
    }
}
