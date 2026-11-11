using UnityEngine;
using UnityEngine.UI;

public class ButtonEffect : MonoBehaviour
{
    public Button startButton;          // 버튼 연결
    public GameObject startEffectPrefab; // 이펙트 Prefab 연결
    public Transform effectSpawnPoint;  // 이펙트 생성 위치 (예: 버튼 위)

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(PlayEffect);
        }
    }

    void PlayEffect()
    {
        if (startEffectPrefab != null)
        {
            // 이펙트 생성
            GameObject effect = Instantiate(startEffectPrefab, effectSpawnPoint.position, Quaternion.identity);

            // 자동 삭제 (ParticleSystem 길이에 맞춰)
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(effect, ps.main.duration);
            else
                Destroy(effect, 2f); // 기본 2초 후 삭제
        }

        // 여기에 게임 시작 씬 전환 코드도 추가 가능
        // 예: UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }
}
