using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CompassTickBuilder : MonoBehaviour
{
    [Header("Build Target")]
    [SerializeField] private RectTransform content;

    [Header("Prefabs")]
    [SerializeField] private RectTransform tickLinePrefab;   // UI Image(선) 프리팹의 RectTransform
    [SerializeField] private TextMeshProUGUI labelPrefab;    // TMP 라벨 프리팹

    [Header("Layout (px)")]
    [Tooltip("1도당 UI에서 몇 px로 표현할지 (Scroller/Marker와 반드시 동일)")]
    [SerializeField] private float pixelsPerDegree = 8f;

    [Tooltip("눈금 간격(도). 5도 추천")]
    [SerializeField] private int stepDegree = 5;

    [Tooltip("숫자/방향 라벨 간격(도). 15도 추천")]
    [SerializeField] private int numberLabelStep = 15;

    [Tooltip("라인이 배치될 Y")]
    [SerializeField] private float yLine = 12f;

    [Tooltip("라벨이 배치될 Y")]
    [SerializeField] private float yLabel = -18f;

    [Header("Tick Heights (px)")]
    [SerializeField] private float majorTickHeight = 18f; // 15도 단위(라벨 있는) 큰 눈금
    [SerializeField] private float minorTickHeight = 10f; // 그 외 작은 눈금
    [SerializeField] private float tickWidth = 2f;        // 선 두께

    [Header("Repeat")]
    [SerializeField] private int repeatCount = 3;     // -1,0,1 = 3세트
    [SerializeField] private int startCycleIndex = -1;

    [ContextMenu("Build Compass Ticks")]
    public void Build()
    {
        Debug.Log("Build() called!");

        if (content == null || tickLinePrefab == null || labelPrefab == null)
        {
            Debug.LogError($"References missing! content:{content}, line:{tickLinePrefab}, label:{labelPrefab}");
            return;
        }

        if (stepDegree <= 0 || numberLabelStep <= 0 || repeatCount <= 0)
        {
            Debug.LogError($"Invalid values! stepDegree:{stepDegree}, numberLabelStep:{numberLabelStep}, repeatCount:{repeatCount}");
            return;
        }

        // 1) 기존 자식 제거 (중요)
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(content.GetChild(i).gameObject);
        }

        float cycleWidth = 360f * pixelsPerDegree;

        // 2) 세트 반복(-1,0,1)
        for (int r = 0; r < repeatCount; r++)
        {
            int cycleIndex = startCycleIndex + r;         // -1,0,1
            float cycleOffsetX = cycleIndex * cycleWidth; // 세트 시작 오프셋

            for (int deg = 0; deg < 360; deg += stepDegree)
            {
                float x = cycleOffsetX + deg * pixelsPerDegree;

                bool isMajor = (deg % numberLabelStep == 0);

                // --- Tick Line 생성 ---
                RectTransform line = Instantiate(tickLinePrefab, content);

                // 프리팹 설정이 꼬여도 위치가 안정적이도록 강제 세팅(추천)
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                line.pivot = new Vector2(0.5f, 0f); // 아래 기준으로 위로 자라게
                line.anchoredPosition = new Vector2(x, yLine);

                float height = isMajor ? majorTickHeight : minorTickHeight;
                line.sizeDelta = new Vector2(tickWidth, height);

                // Image 색이 투명해서 안 보이는 경우 방지 (원하면 지워도 됨)
                var img = line.GetComponent<Image>();
                if (img != null && img.color.a <= 0.01f)
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

                // --- Label 생성 (큰 눈금일 때만) ---
                if (isMajor)
                {
                    var label = Instantiate(labelPrefab, content);

                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    label.rectTransform.anchoredPosition = new Vector2(x, yLabel);

                    string dir = DegreeToDirection(deg);
                    label.text = !string.IsNullOrEmpty(dir) ? dir : deg.ToString();
                }
            }
        }

        // 3) Content 폭 설정(마스크/스크롤 안정성을 위해 넉넉히)
        Vector2 size = content.sizeDelta;
        size.x = cycleWidth * repeatCount;
        //if (size.y <= 0f) size.y = 60f; // 혹시 0이면 기본 높이
        size.y = Mathf.Max(size.y, 60f);
        content.sizeDelta = size;

        Debug.Log($"Build done. childCount = {content.childCount}, cycleWidth={cycleWidth}");
    }

    private string DegreeToDirection(int deg)
    {
        // 배그 스타일(0=N, 90=E...)
        switch (deg)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return "";
        }
    }

    // (선택) Content 아래 눈금들만 삭제하고 싶을 때
    [ContextMenu("Clear Only TickLine & TickLabel")]
    public void ClearTicksOnly()
    {
        if (content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            string n = child.name;

            if (n.Contains("TickLine") || n.Contains("TickLabel"))
                DestroyImmediate(child.gameObject);
        }

        Debug.Log("TickLine/TickLabel만 삭제 완료!");
    }
}


/*using TMPro;
using UnityEngine;

public class CompassTickBuilder : MonoBehaviour
{
    [Header("Build Target")]
    [SerializeField] private RectTransform content;

    [Header("Prefabs")]
    [SerializeField] private RectTransform tickLinePrefab; // 얇은 Image(선)
    [SerializeField] private TextMeshProUGUI labelPrefab;  // 숫자/방향 텍스트

    [Header("Layout")]
    [SerializeField] private float pixelsPerDegree = 8f;
    [SerializeField] private int stepDegree = 5;           // 눈금 간격(5도)
    [SerializeField] private int numberLabelStep = 15;     // 숫자 표시 간격(15도)
    [SerializeField] private float yLine = 0f;
    [SerializeField] private float yLabel = -18f;

    [Header("Repeat")]
    [SerializeField] private int repeatCount = 3;          // 3세트
    [SerializeField] private int startCycleIndex = -1;     // -1,0,1 (가운데가 0)

    [ContextMenu("Build Compass Ticks")]
    public void Build()
    {
        if (content == null || tickLinePrefab == null || labelPrefab == null)
        {
            Debug.LogError("References missing!");
            return;
        }

        // 기존 자식 제거
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(content.GetChild(i).gameObject);
        }

        float cycleWidth = 360f * pixelsPerDegree;

        for (int r = 0; r < repeatCount; r++)
        {
            int cycleIndex = startCycleIndex + r; // -1,0,1
            float cycleOffsetX = cycleIndex * cycleWidth;

            for (int deg = 0; deg <= 360; deg += stepDegree)
            {
                float x = cycleOffsetX + deg * pixelsPerDegree;

                // 1) tick line
                RectTransform line = Instantiate(tickLinePrefab, content);
                line.anchoredPosition = new Vector2(x, yLine);

                // 길이: 큰 눈금/작은 눈금 구분
                float height = (deg % numberLabelStep == 0) ? 18f : 10f;
                line.sizeDelta = new Vector2(line.sizeDelta.x, height);

                // 2) label
                if (deg % numberLabelStep == 0)
                {
                    var label = Instantiate(labelPrefab, content);
                    label.rectTransform.anchoredPosition = new Vector2(x, yLabel);

                    string dir = DegreeToDirection(deg);
                    if (!string.IsNullOrEmpty(dir))
                        label.text = dir;         // N/E/S/W/NE...
                    else
                        label.text = deg.ToString(); // 15, 30, 45...
                }
            }
        }

        // Content 폭 대충 설정(반복 수만큼)
        Vector2 size = content.sizeDelta;
        size.x = (360f * pixelsPerDegree) * repeatCount;
        content.sizeDelta = size;

        Debug.Log("Compass ticks built!");
    }

    private string DegreeToDirection(int deg)
    {
        // 배그 스타일: 0=N, 90=E, 180=S, 270=W
        // 45 단위로 NE, SE...
        switch (deg)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            case 360: return "N"; // 마지막도 N으로
            default: return "";
        }
    }
}
*/