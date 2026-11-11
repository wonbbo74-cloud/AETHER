using UnityEngine;

/// <summary>
/// 항상 카메라를 바라보게 한다.
/// Camera.main이 없는 순간(씬 전환 중, 카메라 미배치 씬)에도 터지지 않도록 캐싱하고 검사한다.
/// </summary>
public class Billboard : MonoBehaviour
{
    private Camera cached;

    private void LateUpdate()
    {
        if (cached == null)
            cached = Camera.main;

        if (cached == null)
        {
            cached = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            if (cached == null)
                return;
        }

        transform.forward = cached.transform.forward;
    }
}
