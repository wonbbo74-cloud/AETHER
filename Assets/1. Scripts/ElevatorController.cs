using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ElevatorController : MonoBehaviour
{
    [Header("문 애니메이션")]
    public Animation doorAnimation;
    public string openAnimation = "OpenDoors";
    public string closeAnimation = "CloseDoors";

    [Header("씬 이동")]
    public string nextSceneName = "Stage02";

    [Header("타이밍")]
    [Tooltip("문 닫힘 후 추가 대기 시간")]
    public float extraCloseDelay = 0.1f;

    [Tooltip("엘리베이터 이동 연출 시간")]
    public float travelTime = 2.5f;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip doorOpenClip;
    public AudioClip doorCloseClip;
    public AudioClip elevatorMoveClip;

    [Header("카메라 흔들림")]
    public Transform playerCamera;
    public float shakeAmount = 0.02f;

    private bool isOpen = false;
    private bool isMoving = false;

    void Awake()
    {
        if (doorAnimation == null)
            doorAnimation = GetComponent<Animation>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Legacy elevator: clips/source are empty in 06. Fill them so opening/closing is never silent if it gets wired again.
        if (doorOpenClip == null) doorOpenClip = AetherAudio.Clip("door_slide_open");
        if (doorCloseClip == null) doorCloseClip = AetherAudio.Clip("door_slide_close");
        if (elevatorMoveClip == null) elevatorMoveClip = AetherAudio.Clip("elevator_move");
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
            audioSource.volume = 0.9f;
            gameObject.AddComponent<AetherSfxScaler>().Bind(audioSource);
        }

        SetDoorClosedInstant();
    }

    IEnumerator Start()
    {
        yield return null;
        SetDoorClosedInstant();
    }

    public void OpenDoor()
    {
        if (isOpen || isMoving) return;
        if (doorAnimation == null) return;

        doorAnimation.Play(openAnimation);
        isOpen = true;

        if (audioSource != null && doorOpenClip != null)
            audioSource.PlayOneShot(doorOpenClip);
    }

    public void CloseDoorAndMove()
    {
        if (isMoving) return;
        StartCoroutine(CloseAndLoadRoutine());
    }

    IEnumerator CloseAndLoadRoutine()
    {
        isMoving = true;
        isOpen = false;

        // 1. 문 닫기
        if (doorAnimation != null)
            doorAnimation.Play(closeAnimation);

        if (audioSource != null && doorCloseClip != null)
            audioSource.PlayOneShot(doorCloseClip);

        // 2. 문 닫힘 애니메이션 끝날 때까지 대기
        float closeAnimLength = GetAnimationLength(closeAnimation);
        yield return new WaitForSeconds(closeAnimLength + extraCloseDelay);

        // 3. 이동 사운드 재생
        if (audioSource != null && elevatorMoveClip != null)
        {
            audioSource.clip = elevatorMoveClip;
            audioSource.loop = false;
            audioSource.Play();
        }

        // 4. 씬 전환 직전까지 계속 흔들기
        if (playerCamera != null)
            yield return StartCoroutine(ShakeCameraForDuration(travelTime));
        else
            yield return new WaitForSeconds(travelTime);

        // 5. 다음 씬 이동
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogError("[ElevatorController] nextSceneName이 비어 있습니다.");
    }

    private float GetAnimationLength(string clipName)
    {
        if (doorAnimation == null) return 0f;
        if (doorAnimation.GetClip(clipName) == null) return 0f;

        return doorAnimation[clipName].length;
    }

    private IEnumerator ShakeCameraForDuration(float duration)
    {
        Vector3 originalPos = playerCamera.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-shakeAmount, shakeAmount);
            float y = Random.Range(-shakeAmount, shakeAmount);

            playerCamera.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.localPosition = originalPos;
    }

    private void SetDoorClosedInstant()
    {
        if (doorAnimation == null) return;
        if (doorAnimation.GetClip(closeAnimation) == null) return;

        doorAnimation.Play(closeAnimation);
        doorAnimation[closeAnimation].time = doorAnimation[closeAnimation].length;
        doorAnimation.Sample();
        doorAnimation.Stop();

        isOpen = false;
    }
}