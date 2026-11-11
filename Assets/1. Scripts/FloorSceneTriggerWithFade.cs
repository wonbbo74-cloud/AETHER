using UnityEngine;

public class FloorSceneTriggerWithFade : MonoBehaviour
{
    [Header("���� �� �̸�")]
    [SerializeField] private string nextSceneName;

    [Header("������ �±�")]
    [SerializeField] private string targetTag = "Player";

    [Header("�� ���� ����")]
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("���̵� �ð�")]
    [SerializeField] private float fadeOutTime = 1.2f;
    [SerializeField] private float fadeInTime = 1.2f;

    private bool hasTriggered = false;

private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        if (!IsTarget(other))
            return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[FloorSceneTriggerWithFade] nextSceneName이 비어 있습니다.");
            return;
        }

        if (SceneFader.Instance == null)
        {
            Debug.LogWarning("[FloorSceneTriggerWithFade] SceneFader가 씬에 없습니다.");
            return;
        }

        hasTriggered = true;
        SceneFader.Instance.FadeOutAndLoadScene(nextSceneName, fadeOutTime, fadeInTime);
    }


private bool IsTarget(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag(targetTag))
            return true;

        Transform current = other.transform.parent;
        while (current != null)
        {
            if (current.CompareTag(targetTag))
                return true;

            current = current.parent;
        }

        return false;
    }
}