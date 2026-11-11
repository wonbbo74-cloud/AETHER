using UnityEngine;

public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("다음 씬 이름")]
    [SerializeField] private string nextSceneName;

    [Header("플레이어 태그")]
    [SerializeField] private string playerTag = "Player";

    [Header("현재 씬에서 하얗게 덮이는 시간")]
    [SerializeField] private float fadeOutDuration = 2f;

    [Header("완전히 하얘진 뒤 대기 시간")]
    [SerializeField] private float waitAfterFullWhite = 0.3f;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (SceneFadeController.Instance == null)
        {
            Debug.LogError("[SceneTransitionTrigger] SceneFadeController가 없습니다.");
            return;
        }

        triggered = true;
        SceneFadeController.Instance.FadeToScene(nextSceneName, fadeOutDuration, waitAfterFullWhite);
    }
}