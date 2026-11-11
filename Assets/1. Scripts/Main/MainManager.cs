using UnityEngine;
using UnityEngine.SceneManagement;

public class MainManager : MonoBehaviour
{
    [Header("BGM Key")]
    public string mainBgmKey = "bgm_main";

    private void Start()
    {
        GlobalManager.Audio.PlayBGM(mainBgmKey);
    }

    // 버튼에서 이 메서드를 호출
    public void StartGame(string nextSceneName)
    {
        AetherSceneFade.LoadScene(nextSceneName); // fade instead of a hard cut from the title screen
    }
}
