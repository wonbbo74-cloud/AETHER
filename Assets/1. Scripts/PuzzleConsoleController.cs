using System.Collections.Generic;
using UnityEngine;

public class PuzzleConsoleController : MonoBehaviour
{
    [Header("퍼즐 UI 루트")]
    [SerializeField] private GameObject puzzleRoot;

    [Header("퍼즐 중 비활성화할 플레이어 스크립트들")]
    [SerializeField] private List<MonoBehaviour> playerControlScriptsToDisable = new List<MonoBehaviour>();

    [Header("보상 총 스포너")]
    [SerializeField] private RewardGunSpawner rewardGunSpawner;

    [Header("반복 실행 여부")]
    [SerializeField] private bool canOpenOnlyOnce = true;

    [Header("퍼즐 성공 후 자동 닫기")]
    [SerializeField] private bool closePuzzleOnSuccess = true;

    private bool isPuzzleOpen = false;
    private bool isSolved = false;

    private void Start()
    {
        if (puzzleRoot != null)
            puzzleRoot.SetActive(false);
    }

    public bool CanOpenPuzzle()
    {
        if (canOpenOnlyOnce && isSolved)
            return false;

        return !isPuzzleOpen;
    }

    public void OpenPuzzle()
    {
        if (!CanOpenPuzzle())
            return;

        isPuzzleOpen = true;

        if (puzzleRoot != null)
            puzzleRoot.SetActive(true);

        SetPlayerControlEnabled(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ClosePuzzle()
    {
        isPuzzleOpen = false;

        if (puzzleRoot != null)
            puzzleRoot.SetActive(false);

        SetPlayerControlEnabled(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void SolvePuzzle()
    {
        if (isSolved)
            return;

        isSolved = true;

        // ✅ 보상 생성
        if (rewardGunSpawner != null)
            rewardGunSpawner.SpawnReward();

        if (closePuzzleOnSuccess)
            ClosePuzzle();
    }

    public void FailPuzzle()
    {
        // 실패 시 처리 필요하면 여기 추가 (현재는 비워둠)
        Debug.Log("퍼즐 실패");
    }

    private void SetPlayerControlEnabled(bool enabledValue)
    {
        for (int i = 0; i < playerControlScriptsToDisable.Count; i++)
        {
            if (playerControlScriptsToDisable[i] != null)
                playerControlScriptsToDisable[i].enabled = enabledValue;
        }
    }

    public bool IsSolved()
    {
        return isSolved;
    }

    public bool IsPuzzleOpen()
    {
        return isPuzzleOpen;
    }
}