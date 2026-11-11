using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 defaultLocalPos;
    private Coroutine shakeCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        defaultLocalPos = transform.localPosition;
    }

    private void OnDisable()
    {
        // �� ��ȯ/��Ȱ��ȭ �� ��ġ ����
        transform.localPosition = defaultLocalPos;
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = null;
    }

    /// <param name="duration">��鸲 ���ӽð�(��)</param>
    /// <param name="amplitude">��鸲 ����(�Ÿ�)</param>
    /// <param name="frequency">�ʴ� ��鸲 ��ȭ �ӵ�</param>
    public void Shake(float duration = 0.12f, float amplitude = 0.18f, float frequency = 25f)
    {
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine(duration, amplitude, frequency));
    }

    private IEnumerator ShakeRoutine(float duration, float amplitude, float frequency)
    {
        float t = 0f;

        // duration ���� ���� ������, ������ ������ ����
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // TimeScale=0���� ��鸲 ����
            float normalized = Mathf.Clamp01(t / duration);
            float damper = 1f - normalized; // 1 -> 0

            // Perlin �Ǵ� Random. ���⼱ Random�� �����ϰ� ����.
            float x = (Random.value * 2f - 1f) * amplitude * damper;
            float y = (Random.value * 2f - 1f) * amplitude * damper;

            transform.localPosition = defaultLocalPos + new Vector3(x, y, 0f);

            // frequency �ݿ�: �ʹ� ������ ���������� �ణ ��
            float wait = Mathf.Max(0.001f, 1f / frequency);
            yield return new WaitForSecondsRealtime(wait);
        }

        transform.localPosition = defaultLocalPos;
        shakeCo = null;
    }
}