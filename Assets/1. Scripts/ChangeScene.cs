using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    [Header("이 트리거에 들어오면 로드할 씬 이름(Build Settings에 등록된 이름)")]
    [SerializeField] private string targetSceneName;

    [Header("플레이어 태그")]
    [SerializeField] private string playerTag = "Player";

    [Header("중복 호출 방지")]
    [SerializeField] private bool oneShot = true;

    private bool _loaded;

    private void OnTriggerEnter(Collider other)
    {
        if (_loaded && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError($"{name}: targetSceneName이 비어있음");
            return;
        }

        _loaded = true;

        // 04 exit has two overlapping triggers: this one used a hard cut while SceneTransitionTrigger fades to white.
        // Use the same white fade when it exists so the transition looks the same whichever trigger is hit first.
        if (SceneFadeController.Instance != null)
            SceneFadeController.Instance.FadeToScene(targetSceneName, 2f, 0.3f);
        else
            AetherSceneFade.LoadScene(targetSceneName);
    }
}
