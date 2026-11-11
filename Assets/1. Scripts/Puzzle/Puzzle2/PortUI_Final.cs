using UnityEngine;
using UnityEngine.EventSystems;

namespace CircuitPuzzle
{
    public class PortUI_Final : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public CircuitPuzzleManager_Final manager;
        public CP_Port_Final port;

        public void OnPointerDown(PointerEventData eventData) => manager?.BeginDragFromPort(port, eventData);
        public void OnDrag(PointerEventData eventData) => manager?.DragTo(eventData);
        public void OnPointerUp(PointerEventData eventData) => manager?.EndDrag(eventData);
    }
}
