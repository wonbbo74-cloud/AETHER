using UnityEngine;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    [Header("제한 시간")]
    public float timeLimit = 60f;
    private float remainingTime;
    private bool isPlaying = false;

    [Header("이동(회전) 횟수")]
    public int moveCount = 0;

    [Header("참조")]
    public TileSpawner tileSpawner;
    public TileManager tileManager;

    [Header("UI (TMP)")]
    public TMP_Text timeText;
    public TMP_Text moveText;
    public TMP_Text clearText;   // "CLEAR" 표시용

    [Header("퍼즐 클리어 시 열릴 문")]
    public Door Door;

    [Header("퍼즐 열고 닫는 오프너 (화면 닫기용)")]
    public PuzzleOpener puzzleOpener;

    private bool isCleared = false;

    // ====== 스케일 애니메이션 관련 ======
    [Header("연출: 퍼즐 패널 스케일 업")]
    public RectTransform puzzleRoot;              // 퍼즐 전체 패널
    public Vector3 openScale = Vector3.one;       // Screen Space이므로 (1,1,1)
    public float openDuration = 0.25f;
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isAnimating = false;
    // ===================================

    void Awake()
    {
        if (tileSpawner == null)
            tileSpawner = FindObjectOfType<TileSpawner>();
        if (tileManager == null)
            tileManager = FindObjectOfType<TileManager>();
    }

    void Start()
    {
        // 자동 시작 X, 외부에서 StartGame 호출
        remainingTime = timeLimit + AetherLevel.PuzzleBonusSeconds; // 에테르 레벨마다 +5초
        moveCount = 0;
        isCleared = false;
        isPlaying = false;

        if (clearText != null)
            clearText.gameObject.SetActive(false);
        SetClearDesignVisible(false);

        if (puzzleRoot != null)
            puzzleRoot.localScale = Vector3.zero;   // 처음엔 안 보이게
    }

    // 클리어 화면 그림(game-clear-design, "ACCESS GRANTED")이 장면에 켜진 채 저장되어 있어서
    // 퍼즐이 열리자마자 판 위를 덮고 클릭을 전부 가로챘다(타일이 하나도 안 돌아감).
    // 그림은 그대로 두고, 풀었을 때만 보이게 한다.
    [Header("클리어 화면 그림 (비우면 퍼즐 안에서 이름에 clear 가 든 그림을 찾는다)")]
    [SerializeField] private GameObject clearDesign;

    private GameObject FindClearDesign()
    {
        if (clearDesign != null)
            return clearDesign;

        Transform root = puzzleRoot != null ? puzzleRoot : transform;
        foreach (UnityEngine.UI.RawImage raw in root.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
            if (raw.texture != null && raw.texture.name.ToLowerInvariant().Contains("clear"))
                return clearDesign = raw.gameObject;

        foreach (UnityEngine.UI.Image image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (image.sprite != null && image.sprite.name.ToLowerInvariant().Contains("clear"))
                return clearDesign = image.gameObject;

        return null;
    }

    private void SetClearDesignVisible(bool visible)
    {
        GameObject design = FindClearDesign();
        if (design == null)
            return;

        // 보일 때도 클릭은 막지 않는다.
        foreach (UnityEngine.UI.Graphic g in design.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            g.raycastTarget = false;
        design.SetActive(visible);
    }

    void Update()
    {
        if (!isPlaying || isCleared)
            return;

        // TimeScale 0일 수도 있으니 unscaledDeltaTime 사용해도 됨
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isPlaying = false;
            OnTimeOver();
        }

        UpdateUI();
    }

    // 전력박스 상호작용에서 호출
    public void StartGame()
    {
        if (isAnimating)
            return;

        remainingTime = timeLimit + AetherLevel.PuzzleBonusSeconds; // 에테르 레벨마다 +5초
        moveCount = 0;
        isCleared = false;
        isPlaying = false;   // 애니메이션 끝난 뒤 true로
        UpdateUI();

        if (clearText != null)
            clearText.gameObject.SetActive(false);
        SetClearDesignVisible(false);

        if (tileSpawner != null)
            tileSpawner.RegenerateTiles();

        if (puzzleRoot != null)
        {
            StopAllCoroutines();
            StartCoroutine(OpenScaleRoutine());
        }
        else
        {
            isPlaying = true;
        }
    }

    public void OnTileRotated()
    {
        if (!isPlaying || isCleared)
            return;

        moveCount++;
        UpdateUI();
    }

    public void OnPuzzleCleared()
    {
        if (!isPlaying || isCleared)
            return;

        isCleared = true;
        isPlaying = false;
        AetherAudio.Play2D("puzzle_clear", 0.7f, 1f, 0f); // 회로가 이어졌다

        // 에테르: 기본 250 + 남은 시간 1초당 2.
        AetherLevel.ReportPuzzle("04-power-box", AetherLevel.BlockPuzzleExp + Mathf.RoundToInt(Mathf.Max(0f, remainingTime) * 2f), "전력 회로 복구");

        // 클리어 화면 그림이 있으면 그것을 보여 주고, 없을 때만 예전 "CLEAR" 글자를 쓴다.
        if (FindClearDesign() != null)
        {
            SetClearDesignVisible(true);
        }
        else if (clearText != null)
        {
            clearText.text = "CLEAR";
            clearText.gameObject.SetActive(true);
        }

        
        Debug.Log("퍼즐 클리어");
        // 여기에서 PuzzleOpener.ClosePuzzle() 호출하도록 연결해도 됨

        if (puzzleOpener != null)
            puzzleOpener.MarkCleared();

        // 1) 문 열기
        if (Door != null)
            Door.OpenDoors();

        // 목표도 다음 할 일로 바꾼다(예전엔 클리어 뒤 목표 칸이 사라져 어디로 가야 할지 몰랐다).
        AetherModernHUD.SetCurrentObjective("출입문이 열렸다.\n계단을 올라 지상으로 나가라.");
        AetherSave.SetFlag("04-power-box");

        // 2) 3초 후 퍼즐 닫기
        if (puzzleOpener != null)
            StartCoroutine(ClosePuzzleAfterDelay());
    }

    /// <summary>이어하기: 이미 푼 퍼즐이면 퍼즐 화면 없이 문만 열어 둔다.</summary>
    public void RestoreCleared()
    {
        isCleared = true;
        isPlaying = false;
        if (puzzleOpener != null)
            puzzleOpener.MarkCleared();
        if (Door != null)
            Door.OpenImmediately();
        AetherModernHUD.SetCurrentObjective("출입문이 열렸다.\n계단을 올라 지상으로 나가라.");
    }

    private System.Collections.IEnumerator ClosePuzzleAfterDelay()
    {
        // Time.timeScale 1이면 WaitForSeconds, 0으로 멈출 생각이면 WaitForSecondsRealtime 사용
        yield return new WaitForSeconds(3f);

        puzzleOpener.ClosePuzzle();
    }

    private void OnTimeOver()
    {
        AetherAudio.Play2D("ui_error", 0.5f, 0.9f, 0f);
        Debug.Log("시간 종료");

        // 예전엔 시간이 끝나면 아무 일도 없이 멈춰서 '재가동'을 눌러야만 다시 할 수 있었다(눌러야 하는지도 알 수 없었다).
        // 시간 초과를 알리고 잠시 뒤 새 판으로 다시 시작한다.
        if (clearText != null)
        {
            clearText.text = "시간 초과 — 회로 재가동";
            clearText.gameObject.SetActive(true);
        }
        StartCoroutine(RestartAfterTimeOver());
    }

    private System.Collections.IEnumerator RestartAfterTimeOver()
    {
        yield return new WaitForSecondsRealtime(1.6f);
        if (!isCleared && puzzleOpener != null && puzzleOpener.IsPuzzleOpen)
            StartGame();
    }

    private void UpdateUI()
    {
        if (timeText != null)
            timeText.text = Mathf.CeilToInt(remainingTime).ToString();

        if (moveText != null)
            moveText.text = moveCount.ToString();
    }

    public void OnClickRestart()
    {
        StartGame();
    }

    // ====== 스케일 0 → 1 애니메이션 ======
    private System.Collections.IEnumerator OpenScaleRoutine()
    {
        isAnimating = true;

        if (puzzleRoot != null)
            puzzleRoot.localScale = Vector3.zero;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / openDuration;
            float eval = openCurve.Evaluate(Mathf.Clamp01(t));
            if (puzzleRoot != null)
            {
                puzzleRoot.localScale = Vector3.LerpUnclamped(
                    Vector3.zero,
                    openScale,
                    eval
                );
            }
            yield return null;
        }

        if (puzzleRoot != null)
            puzzleRoot.localScale = openScale;

        isAnimating = false;
        isPlaying = true;
    }
}
