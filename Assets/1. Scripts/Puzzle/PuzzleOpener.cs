using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleOpener : MonoBehaviour, IInteractable
{
    [Header("UI에 표시될 이름/아이콘")]
    [SerializeField] private string displayName = "전력 박스를 조작한다";
    [SerializeField] private Sprite icon;

    [Header("퍼즐 관련 참조")]
    [SerializeField] private GameObject puzzleCanvas;
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("플레이어 이동/시야 스크립트")]
    public Yunseo_Move playerMoveScript;

    private bool isPuzzleOpen = false;
    private bool isCleared = false;
    private bool holdsScreen = false;
    private int openedFrame = -10;

    public bool IsPuzzleOpen => isPuzzleOpen;
    public bool IsCleared => isCleared;

    public string GetDisplayName() => displayName;
    public Sprite GetIcon() => icon;

    /// <summary>
    /// 같은 자리에 '동그라미(F 길게)' 안내가 켜져 있으면 그것으로만 연다.
    /// 예전엔 일반 상호작용(F 한 번)도 같이 받아서 길게 누르기 전에 퍼즐이 바로 열렸다.
    /// </summary>
    public bool UsesHoldPrompt
    {
        get
        {
            ProximityPuzzleOpener hold = GetComponent<ProximityPuzzleOpener>();
            return hold != null && hold.isActiveAndEnabled;
        }
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (isPuzzleOpen || isCleared || UsesHoldPrompt) return;

        if (puzzleCanvas == null || puzzleManager == null)
        {
            Debug.LogError("[PuzzleOpener] 퍼즐 레퍼런스가 비어있습니다.", this);
            return;
        }

        if (playerMoveScript == null && interactor != null)
            playerMoveScript = interactor.GetComponent<Yunseo_Move>();

        OpenPuzzle();
    }

    private void OpenPuzzle()
    {
        isPuzzleOpen = true;
        openedFrame = Time.frameCount;
        AetherAudio.Play2D("ui_open", 0.55f, 1f, 0f);

        // 04 의 퍼즐 화면(PuzzleUI)은 Meka(Hankyul_찐) 아래에 들어 있어, Shiri 를 고르면 Meka 가 꺼진 채라
        // 화면이 켜지지 않았다(Shiri 로는 문을 열 수 없어 진행이 막힘). 화면 전체를 덮는 캔버스라
        // 부모가 상관없으므로, 부모가 꺼져 있으면 장면 맨 위로 꺼내서 켠다.
        if (puzzleCanvas.transform.parent != null && !puzzleCanvas.transform.parent.gameObject.activeInHierarchy)
            puzzleCanvas.transform.SetParent(null, false);

        // Screen Space Canvas 켜기
        puzzleCanvas.SetActive(true);
        HookCloseButton();

        // 스케일 0→1 애니메이션 포함
        puzzleManager.StartGame();

        // 예전엔 옛 이동 스크립트(Yunseo_Move)만 멈췄다. 지금 쓰는 Move_Hankyul/Move_Yunseo 는 그대로 돌면서
        // 매 프레임 커서를 다시 잠가 퍼즐 칸을 클릭할 수 없었다. 인벤토리와 같은 화면 잠금을 쓰되,
        // 퍼즐 제한 시간이 흘러야 하므로 시간은 멈추지 않는다.
        if (!holdsScreen)
        {
            AetherUIState.BeginScreen(freezeTime: false);
            holdsScreen = true;
        }

        if (playerMoveScript != null)
        {
            playerMoveScript.allowMove = false;
            playerMoveScript.allowLook = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenFromProximity(Move_Hankyul playerMove)
    {
        if (isPuzzleOpen || isCleared) return;

        if (puzzleCanvas == null || puzzleManager == null)
        {
            Debug.LogError("[PuzzleOpener] 퍼즐 레퍼런스가 비어있습니다.", this);
            return;
        }

        OpenPuzzle();
    }

    private bool closeHooked;

    /// <summary>퍼즐 창의 X 버튼(ButtonX)에 아무 기능도 연결돼 있지 않아 눌러도 닫히지 않았다.</summary>
    private void HookCloseButton()
    {
        if (closeHooked || puzzleCanvas == null)
            return;

        foreach (UnityEngine.UI.Button button in puzzleCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (button != null && button.name == "ButtonX")
            {
                button.onClick.AddListener(ClosePuzzle);
                closeHooked = true;
            }
        }
    }

    /// <summary>퍼즐을 풀었다. 문이 열린 뒤에는 다시 열리지 않게 한다.</summary>
    public void MarkCleared()
    {
        isCleared = true;
    }

    private void Update()
    {
        // ESC 로 퍼즐을 닫는다(푼 것으로 치지 않음). 연 바로 그 프레임의 입력은 무시한다.
        if (!isPuzzleOpen || Time.frameCount - openedFrame <= 1)
            return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            ClosePuzzle();
    }

    private void OnDisable()
    {
        if (isPuzzleOpen)
            ClosePuzzle();
    }

    public void ClosePuzzle()
    {
        if (!isPuzzleOpen && !holdsScreen)
            return;

        AetherAudio.Play2D("ui_close", 0.5f, 1f, 0f);
        if (puzzleCanvas != null)
            puzzleCanvas.SetActive(false);

        if (playerMoveScript != null)
        {
            playerMoveScript.allowMove = true;
            playerMoveScript.allowLook = true;
        }

        isPuzzleOpen = false;

        if (holdsScreen)
        {
            holdsScreen = false;
            AetherUIState.EndScreen(); // 조작·커서 잠금을 되돌린다
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
