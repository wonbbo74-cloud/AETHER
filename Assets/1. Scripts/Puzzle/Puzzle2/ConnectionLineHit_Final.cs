using UnityEngine;
using UnityEngine.EventSystems;

namespace CircuitPuzzle
{
    public class ConnectionLineHit_Final : MonoBehaviour, IPointerClickHandler
    {
        public CircuitPuzzleManager_Final manager;
        public string connectionId;

        float lastClickTime;
        const float DoubleClickWindow = 0.35f;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.time - lastClickTime < DoubleClickWindow)
                manager?.RemoveConnectionById(connectionId);

            lastClickTime = Time.time;
        }
    }
}
