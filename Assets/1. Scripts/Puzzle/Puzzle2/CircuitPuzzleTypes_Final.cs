using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitPuzzle
{
    public enum SignalType_Final
    {
        Blue = 0,
        Red = 1,
        Green = 2
    }

    [Serializable]
    public class ColumnConfig_Final
    {
        public int gateCount = 3;
    }

    [Serializable]
    public class CP_ConnData_Final
    {
        public string id;
        public string fromPortId;
        public string toPortId;
        public LineRenderer line;
        public GameObject root;
        public List<GameObject> hitAreas = new List<GameObject>();
        public bool isActive = true;
        public SignalType_Final lineColor;
    }

    public class CP_Gate_Final : MonoBehaviour
    {
        public string gateId;
        public string gateType;
        public int columnIndex;
        public int gateIndexInColumn;
        public bool isCorrectGate;
        public bool isActivatedByCorrectColor;
        public bool isDDADisabled;
        public SignalType_Final gateColor;
        public Image gateImage;
        public RectTransform gateRT;

        public bool IsDDADisabled => isDDADisabled;

        public void SetDDADisabled(bool active)
        {
            isDDADisabled = active;

            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.blocksRaycasts = !active;
            canvasGroup.alpha = active ? 0.35f : 1f;

            if (gateImage != null)
                gateImage.raycastTarget = !active;
        }
    }

    public class CP_Port_Final : MonoBehaviour
    {
        public string portId;
        public string portType;
        public CP_Gate_Final parentGate;
    }
}
