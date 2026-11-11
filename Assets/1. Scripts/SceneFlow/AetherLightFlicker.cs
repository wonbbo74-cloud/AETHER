using UnityEngine;

/// <summary>
/// 낡은 형광등처럼 불이 가끔 떨리거나 툭 끊겼다 들어온다.
/// 평소엔 거의 가만히 있고, 몇 초에 한 번 짧게 '지직'거리는 쪽이 자연스럽다.
/// 같은 오브젝트(또는 지정한 렌더러)의 발광(_EmissionColor)도 함께 어둡게 한다.
/// </summary>
[RequireComponent(typeof(Light))]
public class AetherLightFlicker : MonoBehaviour
{
    [SerializeField, Tooltip("지직거림 사이의 쉬는 시간(초)")]
    private Vector2 calmSeconds = new Vector2(2.5f, 9f);

    [SerializeField, Tooltip("한 번 지직거리는 길이(초)")]
    private Vector2 burstSeconds = new Vector2(0.15f, 0.7f);

    [SerializeField, Range(0f, 1f), Tooltip("지직거릴 때 가장 어두워지는 비율")]
    private float minimumLevel = 0.05f;

    [SerializeField, Range(0f, 0.2f), Tooltip("평소의 아주 작은 떨림")]
    private float hum = 0.03f;

    [SerializeField] private Renderer emissiveRenderer;

    private Light target;
    private float baseIntensity;
    private float nextBurst;
    private float burstEnd;
    private float nextToggle;
    private bool dark;
    private MaterialPropertyBlock block;
    private Color baseEmission;
    private bool hasEmission;
    private AetherAudioLoop buzz;
    private bool buzzTried;
    private float nextSpark;

    private void Awake()
    {
        target = GetComponent<Light>();
        baseIntensity = target.intensity;
        nextBurst = Time.time + Random.Range(calmSeconds.x, calmSeconds.y);

        if (emissiveRenderer != null && emissiveRenderer.sharedMaterial != null &&
            emissiveRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            block = new MaterialPropertyBlock();
            baseEmission = emissiveRenderer.sharedMaterial.GetColor("_EmissionColor");
            hasEmission = true;
        }
    }

    private void Update()
    {
        float now = Time.time;
        float level;

        if (now >= nextBurst && now < burstEnd)
        {
            if (now >= nextToggle)
            {
                dark = !dark;
                nextToggle = now + Random.Range(0.02f, 0.09f);
                // 꺼졌다 들어올 때 안정기에서 '지직'
                if (!dark && now >= nextSpark)
                {
                    nextSpark = now + 0.25f;
                    AetherAudio.PlayAt("spark", transform.position, 0.35f, 1f, 12f, 0.1f, 1f, 200);
                }
            }
            level = dark ? Random.Range(minimumLevel, 0.35f) : Random.Range(0.8f, 1f);
        }
        else
        {
            if (now >= burstEnd && now >= nextBurst)
            {
                // 지직거림이 끝났으니 다음 차례를 잡는다.
                nextBurst = now + Random.Range(calmSeconds.x, calmSeconds.y);
                burstEnd = nextBurst + Random.Range(burstSeconds.x, burstSeconds.y);
                dark = false;
            }
            else if (burstEnd < nextBurst)
            {
                burstEnd = nextBurst + Random.Range(burstSeconds.x, burstSeconds.y);
            }

            level = 1f - hum * Mathf.PerlinNoise(now * 9f, transform.position.x);
        }

        target.intensity = baseIntensity * level;

        // 형광등이 웅 하고 우는 소리. 불이 꺼진 순간엔 같이 끊긴다.
        if (buzz == null && !buzzTried)
        {
            buzzTried = true;
            buzz = AetherAudio.StartLoop("light_buzz", transform, 0.22f, 0.8f, 9f);
        }
        if (buzz != null)
            buzz.Volume = 0.22f * Mathf.Clamp01((level - 0.2f) / 0.6f);

        if (hasEmission)
        {
            block.SetColor("_EmissionColor", baseEmission * level);
            emissiveRenderer.SetPropertyBlock(block);
        }
    }

    public void SetEmissiveRenderer(Renderer renderer)
    {
        emissiveRenderer = renderer;
    }
}
