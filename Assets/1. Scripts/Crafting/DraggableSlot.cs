using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))] // Raycast 보장
public class DraggableSlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("필수 연결")]
    public InventorySlot slot;      // 드래그 출발 슬롯
    public Canvas rootCanvas;       // 최상위 Canvas

    [HideInInspector]
    public bool dropSucceeded;      // Drop 성공 여부

    private CanvasGroup canvasGroup;

    // 드래그 고스트
    private GameObject ghost;
    private RectTransform ghostRect;
    private RawImage ghostImage;
    private RectTransform canvasRect;
    private bool sourceIconWasEnabled;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        canvasRect = rootCanvas.transform as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (slot == null || slot.IsEmpty()) return;
        if (slot.iconRawImage == null || slot.itemData == null) return;

        dropSucceeded = false;
        canvasGroup.blocksRaycasts = false;

        sourceIconWasEnabled = slot.iconRawImage.enabled;
        slot.iconRawImage.enabled = false;

        // 커서에 붙어서 따라다니는 실제 아이템 아이콘
        ghost = new GameObject("DragGhost");
        ghost.transform.SetParent(rootCanvas.transform);
        ghost.transform.SetAsLastSibling();

        ghostRect = ghost.AddComponent<RectTransform>();
        ghostImage = ghost.AddComponent<RawImage>();

        ghostImage.texture = slot.itemData.iconTexture;
        ghostImage.color = Color.white;
        ghostImage.raycastTarget = false;

        ghostRect.anchorMin = ghostRect.anchorMax = ghostRect.pivot =
            new Vector2(0.5f, 0.5f);

        RectTransform sourceRect = slot.iconRawImage.rectTransform;
        Vector2 sourceSize = sourceRect.rect.size;
        if (sourceSize.x <= 1f || sourceSize.y <= 1f)
            sourceSize = sourceRect.sizeDelta;

        ghostRect.sizeDelta = sourceSize;

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect == null) return;
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (ghost != null)
            Destroy(ghost);

        // 드롭 실패 시에는 원래 슬롯 아이콘을 다시 보여준다.
        // 드롭 성공 시에는 DropSlot이 source slot을 비우거나 이동 처리를 끝낸다.
        if (!dropSucceeded && slot != null && slot.iconRawImage != null && !slot.IsEmpty())
            slot.iconRawImage.enabled = sourceIconWasEnabled;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera,
            out localPoint))
        {
            ghostRect.anchoredPosition = localPoint;
        }
    }
}
