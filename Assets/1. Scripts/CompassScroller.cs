using UnityEngine;

public class CompassScroller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform content; // 움직일 Content (Tick들이 들어있는 RectTransform)
    [SerializeField] private Transform yawSource;   // 보통 Main Camera (시야가 도는 Transform)

    [Header("Tuning")]
    [Tooltip("1도당 UI에서 몇 px 이동할지 (Builder/Marker와 반드시 동일)")]
    [SerializeField] private float pixelsPerDegree = 8f;

    private float cycleWidth; // 0~360 한 세트 폭(px)

    private void Reset()
    {
        if (Camera.main) yawSource = Camera.main.transform;
    }

    private void Awake()
    {
        cycleWidth = 360f * pixelsPerDegree;
    }

    private void LateUpdate()
    {
        if (content == null || yawSource == null) return;

        float yaw = yawSource.eulerAngles.y; // 0~360

        // yaw만큼 왼쪽으로 스크롤 (현재 바라보는 각도가 중앙에 오도록)
        float x = -(yaw * pixelsPerDegree);

        // 3세트(-1,0,1) 중 "가운데 세트(0세트)"를 기준으로 보이게 오프셋을 한 세트만큼 적용
        // (startCycleIndex=-1로 만들었으니 가운데 세트는 content 내부의 +cycleWidth 구간)
        x -= cycleWidth;

        Vector2 pos = content.anchoredPosition;
        pos.x = x;
        content.anchoredPosition = pos;
    }

    [ContextMenu("Log Cycle Width")]
    private void LogCycleWidth()
    {
        Debug.Log($"cycleWidth = {360f * pixelsPerDegree} (pixelsPerDegree={pixelsPerDegree})");
    }
}
