using UnityEngine;

public class UIFlyNoise : MonoBehaviour
{
    [Header("Range (start position 기준)")]
    public Vector2 wanderRange = new Vector2(60f, 40f);

    [Header("Motion")]
    [Tooltip("움직임 속도(노이즈 진행 속도)")]
    public float frequency = 0.25f;

    [Tooltip("따라가는 부드러움(클수록 빨리 따라감)")]
    public float smooth = 6f;

    [Header("Extra Flutter (선택)")]
    [Tooltip("가벼운 잔떨림 크기")]
    public float flutterAmplitude = 2f;

    [Tooltip("잔떨림 속도")]
    public float flutterFrequency = 2f;

    private RectTransform rect;
    private Vector2 startPos;
    private float seedX;
    private float seedY;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        startPos = rect.anchoredPosition;

        // 오브젝트마다 다른 궤적을 가지도록 시드 부여
        seedX = UnityEngine.Random.Range(0f, 1000f);
        seedY = UnityEngine.Random.Range(0f, 1000f);
    }

    void Update()
    {
        float t = Time.time * frequency;

        // PerlinNoise는 0~1이라 -1~1로 변환
        float nx = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;

        Vector2 noiseOffset = new Vector2(nx * wanderRange.x, ny * wanderRange.y);

        // 가벼운 날갯짓 같은 잔떨림(원형)
        float fx = Mathf.Sin(Time.time * flutterFrequency) * flutterAmplitude;
        float fy = Mathf.Cos(Time.time * flutterFrequency * 1.13f) * flutterAmplitude;
        Vector2 flutterOffset = new Vector2(fx, fy);

        Vector2 desired = startPos + noiseOffset + flutterOffset;

        // 현재 위치가 목표를 "부드럽게" 따라가게 (각짐 방지)
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}
