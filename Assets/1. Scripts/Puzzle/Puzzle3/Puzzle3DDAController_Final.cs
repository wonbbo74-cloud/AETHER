using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum Puzzle3DDAStage_Final
{
    None = 0,
    Stage1 = 1,
    Stage2 = 2,
    Stage3 = 3
}

public class Puzzle3DDAController_Final : MonoBehaviour
{
    class AttemptLog
    {
        public int[] inputDigits;
        public int strikeCount;
        public int ballCount;
    }

    [Header("Hint Texts")]
    [SerializeField] private TMP_Text stage1HintText;
    [SerializeField] private TMP_Text stage2HintText;
    [SerializeField] private TMP_Text stage3HintText;

    [Header("DDA Settings")]
    [SerializeField] private DDAInterventionTiming_Final interventionTiming = DDAInterventionTiming_Final.Normal;
    [SerializeField, Min(1)] private int noStrikeProgressSubmitThreshold = 3;
    [SerializeField, Min(0)] private int lowPossibilityDigitCount = 3;

    readonly List<AttemptLog> attemptLogs = new List<AttemptLog>();
    readonly List<int> lowPossibilityDigits = new List<int>();

    Puzzle3DDAStage_Final activeDDAStage;
    int noStrikeProgressSubmitCount;
    int bestStrikeCount;
    int[] answerDigits;
    int[] lastInputDigits;
    int[] wrongCountByPosition;
    bool[] revealedAnswerPositions;
    int referenceLevel;
    bool stage1Applied;
    bool stage2Applied;
    bool stage3Applied;

    void Awake()
    {
        ApplyInterventionTimingSettings();
    }

    void OnValidate()
    {
        ApplyInterventionTimingSettings();
    }

    public void ResetDDAForNewGame()
    {
        ApplyInterventionTimingSettings();
        referenceLevel = 0;
        answerDigits = null;
        ResetLevelDDAState();
        HideDDAHints();
    }

    public void OnPuzzleStarted(int level, int[] answer)
    {
        ApplyInterventionTimingSettings();
        referenceLevel = level;
        answerDigits = CloneDigits(answer);
        ResetLevelDDAState();
        HideDDAHints();
    }

    public void OnAttemptSubmitted(int[] inputDigits, int strikeCount, int ballCount)
    {
        if (answerDigits == null || answerDigits.Length == 0)
            return;

        lastInputDigits = CloneDigits(inputDigits);
        attemptLogs.Add(new AttemptLog
        {
            inputDigits = CloneDigits(inputDigits),
            strikeCount = strikeCount,
            ballCount = ballCount
        });

        EnsurePositionArrays();
        int length = Mathf.Min(lastInputDigits.Length, answerDigits.Length);
        for (int i = 0; i < length; i++)
        {
            if (lastInputDigits[i] != answerDigits[i])
                wrongCountByPosition[i]++;
        }

        UpdateStrikeProgressAndTryUpgradeDDAStage(strikeCount);
    }

    public void OnPuzzleCleared()
    {
        ResetLevelDDAState();
        HideDDAHints();
    }

    void ApplyInterventionTimingSettings()
    {
        switch (interventionTiming)
        {
            case DDAInterventionTiming_Final.Early:
                noStrikeProgressSubmitThreshold = 2;
                break;
            case DDAInterventionTiming_Final.Late:
                noStrikeProgressSubmitThreshold = 5;
                break;
            default:
                noStrikeProgressSubmitThreshold = 3;
                break;
        }
    }

    void ResetLevelDDAState()
    {
        attemptLogs.Clear();
        lowPossibilityDigits.Clear();
        lastInputDigits = null;
        stage1Applied = false;
        stage2Applied = false;
        stage3Applied = false;
        activeDDAStage = Puzzle3DDAStage_Final.None;
        noStrikeProgressSubmitCount = 0;
        bestStrikeCount = 0;

        int length = answerDigits != null ? answerDigits.Length : 0;
        wrongCountByPosition = new int[length];
        revealedAnswerPositions = new bool[length];
    }

    void UpdateStrikeProgressAndTryUpgradeDDAStage(int strikeCount)
    {
        if (strikeCount > bestStrikeCount)
        {
            bestStrikeCount = strikeCount;
            noStrikeProgressSubmitCount = 0;
            return;
        }

        noStrikeProgressSubmitCount++;
        if (noStrikeProgressSubmitCount < noStrikeProgressSubmitThreshold)
            return;

        if (activeDDAStage == Puzzle3DDAStage_Final.None && ApplyDDAStage(Puzzle3DDAStage_Final.Stage1))
            activeDDAStage = Puzzle3DDAStage_Final.Stage1;
        else if (activeDDAStage == Puzzle3DDAStage_Final.Stage1 && ApplyDDAStage(Puzzle3DDAStage_Final.Stage2))
            activeDDAStage = Puzzle3DDAStage_Final.Stage2;
        else if (activeDDAStage == Puzzle3DDAStage_Final.Stage2 && ApplyDDAStage(Puzzle3DDAStage_Final.Stage3))
            activeDDAStage = Puzzle3DDAStage_Final.Stage3;
        else
            return;

        noStrikeProgressSubmitCount = 0;
    }

    bool ApplyDDAStage(Puzzle3DDAStage_Final stage)
    {
        switch (stage)
        {
            case Puzzle3DDAStage_Final.Stage1:
                if (stage1Applied || !ApplyLowPossibilityHint(Puzzle3DDAStage_Final.Stage1))
                    return false;
                stage1Applied = true;
                break;
            case Puzzle3DDAStage_Final.Stage2:
                if (stage2Applied || !ApplyCompareHint(Puzzle3DDAStage_Final.Stage2))
                    return false;
                stage2Applied = true;
                break;
            case Puzzle3DDAStage_Final.Stage3:
                if (stage3Applied || ApplyStage3Hint() <= 0)
                    return false;
                stage3Applied = true;
                break;
            default:
                return false;
        }

        Debug.Log($"[Puzzle3 DDA Final] ReferenceLevel={referenceLevel}, Stage={stage}");
        return true;
    }

    bool ApplyCompareHint(Puzzle3DDAStage_Final displayStage)
    {
        if (answerDigits == null || lastInputDigits == null)
            return false;

        EnsurePositionArrays();
        int selectedIndex = -1;
        int selectedWrongCount = int.MinValue;
        int length = Mathf.Min(answerDigits.Length, lastInputDigits.Length);
        for (int i = 0; i < length; i++)
        {
            if (lastInputDigits[i] == answerDigits[i])
                continue;
            if (revealedAnswerPositions != null && i < revealedAnswerPositions.Length && revealedAnswerPositions[i])
                continue;

            int wrongCount = wrongCountByPosition != null && i < wrongCountByPosition.Length ? wrongCountByPosition[i] : 0;
            if (wrongCount > selectedWrongCount)
            {
                selectedWrongCount = wrongCount;
                selectedIndex = i;
            }
        }

        if (selectedIndex < 0)
        {
            ShowDDAHint(displayStage, "힌트: 비교할 수 있는 자리가 없습니다.");
            return false;
        }

        string direction = answerDigits[selectedIndex] > lastInputDigits[selectedIndex] ? "큽니다" : "작습니다";
        ShowDDAHint(displayStage, $"힌트: {selectedIndex + 1}번째 자리는 입력한 숫자보다 {direction}.");
        return true;
    }

    bool ApplyLowPossibilityHint(Puzzle3DDAStage_Final displayStage)
    {
        if (answerDigits == null || answerDigits.Length == 0)
            return false;

        if (lowPossibilityDigits.Count == 0)
        {
            HashSet<int> answerSet = new HashSet<int>(answerDigits);
            List<int> candidates = new List<int>();
            for (int digit = 0; digit <= 9; digit++)
            {
                if (!answerSet.Contains(digit))
                    candidates.Add(digit);
            }

            Shuffle(candidates);
            int count = Mathf.Min(Mathf.Max(0, lowPossibilityDigitCount), candidates.Count);
            for (int i = 0; i < count; i++)
                lowPossibilityDigits.Add(candidates[i]);
        }

        if (lowPossibilityDigits.Count == 0)
            return false;

        ShowDDAHint(displayStage, "힌트: 낮은 가능성 숫자 : " + string.Join(", ", lowPossibilityDigits));
        return true;
    }

    int ApplyStage3Hint()
    {
        if (answerDigits == null || answerDigits.Length == 0)
            return 0;

        EnsurePositionArrays();
        List<int> candidates = new List<int>();
        for (int i = 0; i < answerDigits.Length; i++)
        {
            if (revealedAnswerPositions[i])
                continue;
            if (lastInputDigits != null && i < lastInputDigits.Length && lastInputDigits[i] == answerDigits[i])
                continue;
            candidates.Add(i);
        }

        candidates.Sort((a, b) => wrongCountByPosition[b].CompareTo(wrongCountByPosition[a]));

        int targetRevealCount = referenceLevel <= 4 ? 1 : 2;
        int revealCount = Mathf.Min(targetRevealCount, candidates.Count);
        List<string> hintLines = new List<string>();
        for (int i = 0; i < revealCount; i++)
        {
            int index = candidates[i];
            revealedAnswerPositions[index] = true;
            hintLines.Add($"힌트: {index + 1}번째 자리의 정답은 {answerDigits[index]}입니다.");
        }

        if (hintLines.Count == 0)
        {
            ShowDDAHint(Puzzle3DDAStage_Final.Stage3, "힌트: 공개할 수 있는 정답 자리가 없습니다.");
            return 0;
        }

        ShowDDAHint(Puzzle3DDAStage_Final.Stage3, string.Join("\n", hintLines));
        return hintLines.Count;
    }

    void ShowDDAHint(Puzzle3DDAStage_Final stage, string message)
    {
        TMP_Text target = GetHintText(stage);
        if (target != null)
        {
            target.gameObject.SetActive(true);
            target.text = message;
        }
    }

    TMP_Text GetHintText(Puzzle3DDAStage_Final stage)
    {
        switch (stage)
        {
            case Puzzle3DDAStage_Final.Stage1: return stage1HintText;
            case Puzzle3DDAStage_Final.Stage2: return stage2HintText;
            case Puzzle3DDAStage_Final.Stage3: return stage3HintText;
            default: return null;
        }
    }

    void HideDDAHints()
    {
        HideHintText(stage1HintText);
        HideHintText(stage2HintText);
        HideHintText(stage3HintText);
    }

    void HideHintText(TMP_Text text)
    {
        if (text == null) return;
        text.text = "";
        text.gameObject.SetActive(false);
    }

    void EnsurePositionArrays()
    {
        int length = answerDigits != null ? answerDigits.Length : 0;
        if (wrongCountByPosition == null || wrongCountByPosition.Length != length)
            wrongCountByPosition = new int[length];
        if (revealedAnswerPositions == null || revealedAnswerPositions.Length != length)
            revealedAnswerPositions = new bool[length];
    }

    int[] CloneDigits(int[] source)
    {
        if (source == null) return null;
        int[] clone = new int[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }
}
