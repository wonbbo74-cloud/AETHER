using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button startGameButton;

    [SerializeField] private TMP_InputField joinCodeInputField;

    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text statusText;

    [Header("다음 씬")]
    [Tooltip("방이 만들어진 뒤 시작 버튼을 누르면 갈 씬. 비우면 플레이어 선택으로 갑니다.")]
    [SerializeField] private string nextSceneName = "02.플레이어선택";

    private ISession currentSession;
    private bool initialized;

    private async void Start()
    {
        SetButtonsInteractable(false);

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(false);
        }

        await InitializeUnityServicesAsync();
    }

    private async Task InitializeUnityServicesAsync()
    {
        try
        {
            statusText.text = "온라인 연결 중... (혼자 하기는 바로 시작할 수 있습니다)";

            // 인터넷이 끊겨 있으면 응답이 한참 오지 않을 수 있다. 10초가 넘으면 안내를 바꿔 둔다(연결은 계속 시도).
            Task connect = ConnectServicesAsync();
            if (await Task.WhenAny(connect, Task.Delay(10000)) != connect && statusText != null)
                statusText.text = "온라인 연결이 늦어지고 있습니다.\n위의 [혼자 하기]로 바로 시작할 수 있습니다.";
            await connect;

            initialized = true;

            statusText.text =
                "온라인 준비 완료\n친구와 하려면 방을 만들거나 코드로 참가하세요.";

            SetButtonsInteractable(true);
        }
        catch (Exception exception)
        {
            initialized = false;

            // 인터넷이 없거나 Unity 서버가 안 될 때. 오류 내용 대신 할 수 있는 일을 알려 준다.
            statusText.text =
                "온라인에 연결할 수 없습니다.\n위의 [혼자 하기]로 바로 시작할 수 있습니다.";

            Debug.LogWarning($"[로비] Unity 서비스 연결 실패(오프라인으로 진행 가능): {exception.Message}");
        }
    }

    private static async Task ConnectServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async void CreateRoom()
    {
        if (!initialized)
        {
            statusText.text = "Unity Services are not ready.";
            return;
        }

        try
        {
            SetButtonsInteractable(false);

            statusText.text = "Creating Room...";

            SessionOptions options = new SessionOptions
            {
                // Maximum number of players including Host
                MaxPlayers = 2,

                // Private room (Join Code only)
                IsPrivate = true
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            // 방을 만드는 중에 [혼자 하기]를 눌렀으면, 늦게 만들어진 방은 바로 닫는다.
            if (CloseIfSoloStarted())
                return;

            string joinCode = currentSession.Code;

            joinCodeText.text = $"Join Code: {joinCode}";

            statusText.text =
                "Room Created\nShare the Join Code with your friend.";

            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
            joinCodeInputField.interactable = false;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = true;
            }

            Debug.Log($"Room Created: {currentSession.Id}");
            Debug.Log($"Join Code: {joinCode}");
        }
        catch (Exception exception)
        {
            statusText.text =
                $"Failed to Create Room\n{exception.Message}";

            Debug.LogException(exception);

            SetButtonsInteractable(true);
        }
    }

    public async void JoinRoom()
    {
        if (!initialized)
        {
            statusText.text = "Unity Services are not ready.";
            return;
        }

        string joinCode =
            joinCodeInputField.text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            statusText.text = "Please enter a Join Code.";
            return;
        }

        try
        {
            SetButtonsInteractable(false);

            statusText.text = "Joining Room...";

            currentSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

            if (CloseIfSoloStarted())
                return;

            statusText.text =
                "Successfully Joined\nWaiting for the Host to start the game...";

            joinCodeText.text = $"Joined Code: {joinCode}";

            createRoomButton.interactable = false;
            joinRoomButton.interactable = false;
            joinCodeInputField.interactable = false;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
            }

            Debug.Log($"Joined Room: {currentSession.Id}");
        }
        catch (Exception exception)
        {
            statusText.text =
                $"Failed to Join Room\n{exception.Message}";

            Debug.LogException(exception);

            SetButtonsInteractable(true);
        }
    }

    public void StartGame()
    {
        if (NetworkManager.Singleton == null)
        {
            statusText.text = "Network Manager not found.";
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Only the Host can start the game.";
            return;
        }

        statusText.text = "Loading Game...";

        // 예전에는 테스트용 "GameScene"으로 바로 갔다. 지금은 방이 잡힌 뒤
        // 본 게임 흐름(플레이어 선택 → 로딩 → 튜토리얼)으로 들어간다.
        string target = string.IsNullOrWhiteSpace(nextSceneName) ? "02.플레이어선택" : nextSceneName;
        target = AetherCinematics.Redirect(target);   // 2인도 벙커 대화 시네마틱을 두 사람이 함께 보고 02 로 간다

        NetworkManager.Singleton.SceneManager.LoadScene(
            target,
            LoadSceneMode.Single
        );
    }

    /// <summary>
    /// 혼자 하기(오프라인). 방을 만들거나 Unity 서비스에 접속하지 않고 바로 캐릭터 선택으로 간다.
    /// 예전엔 혼자 하려 해도 방을 만들어야 했고, 인터넷·Unity 서버가 안 되면 게임을 시작할 수 없었다.
    /// 게임 진행(04~07)은 네트워크 없이 그대로 돌아간다.
    /// </summary>
    public void StartSolo() => StartSoloInternal(false);

    /// <summary>혼자 하기로 저장된 곳에서 이어 한다.</summary>
    public void ContinueSolo() => StartSoloInternal(true);

    private async void StartSoloInternal(bool continueSave)
    {
        if (soloStarting)
            return;
        soloStarting = true;
        SetButtonsInteractable(false);

        if (statusText != null)
            statusText.text = "혼자 하기로 시작합니다...";

        // 방을 만들어 둔 상태였으면 정리하고 간다(친구가 들어오지 못하게).
        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[혼자 하기] 네트워크 종료 중 문제: {exception.Message}");
        }

        if (currentSession != null)
        {
            // 오프라인이면 기다려도 답이 없으니 오래 붙잡지 않는다.
            Task leave = LeaveSessionAsync();
            await Task.WhenAny(leave, Task.Delay(1500));
        }

        if (continueSave && AetherSave.Continue())
            return;

        string target = string.IsNullOrWhiteSpace(nextSceneName) ? "02.플레이어선택" : nextSceneName;
        target = AetherCinematics.Redirect(target);   // 새 게임: 벙커 대화 시네마틱을 먼저
        SceneManager.LoadScene(target, LoadSceneMode.Single);
    }

    private bool soloStarting;

    /// <summary>혼자 하기를 이미 시작했으면(이 오브젝트가 장면과 함께 사라졌어도) 방을 닫는다.</summary>
    private bool CloseIfSoloStarted()
    {
        if (this != null && !soloStarting)
            return false;

        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[혼자 하기] 늦게 열린 방을 닫는 중 문제: {exception.Message}");
        }

        _ = LeaveSessionAsync();
        return true;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = interactable;
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = interactable;
        }

        if (joinCodeInputField != null)
        {
            joinCodeInputField.interactable = interactable;
        }
    }

    private async void OnApplicationQuit()
    {
        await LeaveSessionAsync();
    }

    private async Task LeaveSessionAsync()
    {
        if (currentSession == null)
        {
            return;
        }

        try
        {
            await currentSession.LeaveAsync();
            currentSession = null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to Leave Session: {exception.Message}");
        }
    }
}