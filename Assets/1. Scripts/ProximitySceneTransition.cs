using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ProximitySceneTransition : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform player;
    [SerializeField] private float interactDistance = 2.35f;

    [Header("Interaction UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private string title = "";
    [SerializeField] private string actionText = "이동";
    [SerializeField] private float holdTime = 1.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Scene")]
    [SerializeField] private string targetSceneName = "07.\uD30C\uCE20 \uC81C\uC791\uC2E4";
    [SerializeField] private float loadDelay = 0.05f;

    private bool playerInside;
    private bool transitioning;
    private float holdTimer;
    private Collider[] targetColliders;
    private Renderer[] targetRenderers;

    private void Awake()
    {
        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        CacheDistanceSources();
    }

    private void Start()
    {
        HidePrompt();
    }

    private void Update()
    {
        bool inRange = playerInside || IsPlayerInRange();

        if (!inRange || transitioning)
        {
            ResetHold();

            if (!transitioning)
                HidePrompt();

            return;
        }

        ShowPrompt();

        if (GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            SetProgress(holdTimer / Mathf.Max(0.01f, holdTime));

            if (holdTimer >= holdTime)
                StartCoroutine(LoadSceneRoutine());

            return;
        }

        ResetHold();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            playerInside = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag))
            playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = false;
        ResetHold();
        HidePrompt();
    }

    private void CacheDistanceSources()
    {
        targetColliders = GetComponentsInChildren<Collider>(true);
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private bool IsPlayerInRange()
    {
        // 장면이 열린 직후엔 아직 꺼지지 않은 다른 캐릭터(Shiri 를 골라도 Meka)를 잡아 두고 계속 그 위치로 거리를 쟀다
        // (06 엘리베이터가 Shiri 로는 안 열림). 잡아 둔 플레이어가 꺼져 있으면 다시 찾는다.
        if (player == null || !player.gameObject.activeInHierarchy)
            FindPlayer();

        if (player == null)
            return false;

        return GetDistanceToTarget(player.position) <= interactDistance;
    }

    private void FindPlayer()
    {
        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private float GetDistanceToTarget(Vector3 worldPosition)
    {
        Vector3 rootPosition = transform.position;
        rootPosition.y = worldPosition.y;
        float bestDistance = Vector3.Distance(rootPosition, worldPosition);

        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                Vector3 closestPoint = targetCollider.ClosestPointSafe(worldPosition);
                closestPoint.y = worldPosition.y;
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                Vector3 closestPoint = targetRenderer.bounds.ClosestPoint(worldPosition);
                closestPoint.y = worldPosition.y;
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        return bestDistance;
    }

    private IEnumerator LoadSceneRoutine()
    {
        if (transitioning)
            yield break;

        // 문을 열고 들어가는 소리(도착 쪽에서는 화면이 밝아지며 '쉭')
        AetherAudio.Play2D("door_slide_open", 0.7f, 1f, 0.03f);

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("[ProximitySceneTransition] targetSceneName is empty.", this);
            yield break;
        }

        transitioning = true;
        ResetHold();
        HidePrompt();
        ResetPlayerMotionBeforeLoad();

        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);

        // 화면이 툭 끊기던 전환을 검게 어두워졌다 밝아지게
        AetherSceneFade.LoadScene(targetSceneName);
    }

    private void ShowPrompt()
    {
        if (interactionUI != null)
            interactionUI.ShowFor(this, string.IsNullOrWhiteSpace(title) ? DestinationTitle() : title, actionText);
    }

    /// <summary>
    /// 제목이 비어 있으면 "이동"만 떠서 어디로 가는 문인지 알 수 없었다. 목적지 씬 이름으로 만든다.
    /// "07.파츠 제작실" → "파츠 제작실로 이동"
    /// </summary>
    private string DestinationTitle()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
            return "이동";

        string name = targetSceneName.Trim();
        int dot = name.IndexOf('.');
        if (dot >= 0 && dot <= 3)
            name = name.Substring(dot + 1).Trim();

        return name + RoParticle(name) + " 이동";
    }

    /// <summary>받침이 있으면(ㄹ 제외) "으로", 없으면 "로".</summary>
    private static string RoParticle(string word)
    {
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3)
            return "로";

        int jong = (last - 0xAC00) % 28;
        return (jong == 0 || jong == 8) ? "로" : "으로";
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
            interactionUI.HideFor(this);
    }

    private void SetProgress(float progress)
    {
        if (interactionUI != null)
            interactionUI.UpdateProgressFor(this, progress);
    }

    private void ResetHold()
    {
        holdTimer = 0f;
        SetProgress(0f);
    }

    private void ResetPlayerMotionBeforeLoad()
    {
        // 장면이 열린 직후엔 아직 꺼지지 않은 다른 캐릭터(Shiri 를 골라도 Meka)를 잡아 두고 계속 그 위치로 거리를 쟀다
        // (06 엘리베이터가 Shiri 로는 안 열림). 잡아 둔 플레이어가 꺼져 있으면 다시 찾는다.
        if (player == null || !player.gameObject.activeInHierarchy)
            FindPlayer();

        if (player == null)
            return;

        // 한결·윤서 모두 남은 입력·잠금을 풀고 넘어간다.
        AetherPlayerCharacter.ResetControl(player.root.gameObject);

        Rigidbody playerRigidbody = player.GetComponentInParent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private static bool GetKey(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (key)
            {
                case KeyCode.F: return keyboard.fKey.isPressed;
                case KeyCode.E: return keyboard.eKey.isPressed;
                case KeyCode.Space: return keyboard.spaceKey.isPressed;
                case KeyCode.Return: return keyboard.enterKey.isPressed;
            }
        }

        return Input.GetKey(key);
    }
}
