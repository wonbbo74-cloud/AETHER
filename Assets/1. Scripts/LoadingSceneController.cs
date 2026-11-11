using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class LoadingSceneController : MonoBehaviour
{
    [Header("필수 참조")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Text loadingText;

    [Header("시간 설정")]
    [SerializeField] private float minimumLoadingTime = 10f;

    [Header("예외 처리")]
    [SerializeField] private string fallbackSceneName = "MainScene";

    private AsyncOperation asyncLoadOperation;
    private bool isVideoFinished = false;
    private bool isSceneReady = false;
    private float elapsedTime = 0f;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("[LoadingSceneController] VideoPlayer 참조가 없습니다.");
            enabled = false;
            return;
        }

        if (videoRawImage != null && renderTexture != null)
        {
            videoRawImage.texture = renderTexture;
        }

        if (videoPlayer.targetTexture != renderTexture)
        {
            videoPlayer.targetTexture = renderTexture;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
    }

    private IEnumerator Start()
    {
        string targetSceneName = LoadingSceneData.TargetSceneName;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[LoadingSceneController] 전달된 목표 씬 이름이 없어 fallback 씬으로 이동합니다.");
            targetSceneName = fallbackSceneName;
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[LoadingSceneController] 목표 씬 이름도 fallback 씬 이름도 비어 있습니다.");
            yield break;
        }

        if (loadingText != null)
        {
            loadingText.text = "LOADING...";
        }

        asyncLoadOperation = SceneManager.LoadSceneAsync(targetSceneName);
        asyncLoadOperation.allowSceneActivation = false;

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        videoPlayer.Play();

        while (!isVideoFinished || !isSceneReady || elapsedTime < minimumLoadingTime)
        {
            elapsedTime += Time.deltaTime;

            if (asyncLoadOperation != null)
            {
                if (asyncLoadOperation.progress >= 0.9f)
                {
                    isSceneReady = true;
                }

                if (loadingText != null)
                {
                    float displayProgress = Mathf.Clamp01(asyncLoadOperation.progress / 0.9f);
                    int percent = Mathf.RoundToInt(displayProgress * 100f);

                    if (!isSceneReady)
                    {
                        loadingText.text = "LOADING... " + percent + "%";
                    }
                    else
                    {
                        loadingText.text = "LOADING COMPLETE";
                    }
                }
            }

            yield return null;
        }

        // 영상이 끝나는 순간 04 로 화면이 툭 끊겼다. 검게 덮은 뒤 넘어가고 04 에서 밝아지게 한다.
        AetherSceneFade.ActivateWithFade(asyncLoadOperation);
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        isVideoFinished = true;
        Debug.Log("[LoadingSceneController] 영상 재생 완료");
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("[LoadingSceneController] VideoPlayer 에러: " + message);
        isVideoFinished = true;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}