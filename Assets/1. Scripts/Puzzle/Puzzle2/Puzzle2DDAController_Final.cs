using System.Collections.Generic;
using UnityEngine;

namespace CircuitPuzzle
{
    public enum Puzzle2DDAStage_Final
    {
        None = 0,
        Stage1 = 1,
        Stage2 = 2,
        Stage3 = 3
    }

    public class Puzzle2DDAController_Final : MonoBehaviour
    {
        [SerializeField] private DDAInterventionTiming_Final interventionTiming = DDAInterventionTiming_Final.Normal;
        [SerializeField, Min(1)] private int wrongGateCountToUpgradeDDA = 3;

        IReadOnlyList<CP_Gate_Final> allGates;
        IReadOnlyList<List<CP_Gate_Final>> gatesByColumn;
        Puzzle2DDAStage_Final activeDDAStage = Puzzle2DDAStage_Final.None;
        int wrongGateCountInCurrentDDAStage;
        bool stage3Applied;
        int referenceLevel;

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
            activeDDAStage = Puzzle2DDAStage_Final.None;
            wrongGateCountInCurrentDDAStage = 0;
            stage3Applied = false;
            RestoreDDADisabledGates();
        }

        public void OnPuzzleStarted(int level, IReadOnlyList<CP_Gate_Final> levelGates, IReadOnlyList<List<CP_Gate_Final>> levelGatesByColumn)
        {
            ApplyInterventionTimingSettings();
            referenceLevel = level;
            allGates = levelGates;
            gatesByColumn = levelGatesByColumn;
            activeDDAStage = Puzzle2DDAStage_Final.None;
            wrongGateCountInCurrentDDAStage = 0;
            stage3Applied = false;
            RestoreDDADisabledGates();
        }

        public void OnPuzzleCleared()
        {
            RestoreDDADisabledGates();
            activeDDAStage = Puzzle2DDAStage_Final.None;
        }

        public string OnWrongGateSelected(CP_Gate_Final selectedGate, string baseFeedback)
        {
            string finalFeedback = GetWrongGateDistanceFeedback(selectedGate, baseFeedback);

            wrongGateCountInCurrentDDAStage++;
            TryUpgradeDDAStage();

            if (activeDDAStage >= Puzzle2DDAStage_Final.Stage2)
            {
                string hint = GetCorrectGateDirectionHint(selectedGate);
                if (!string.IsNullOrEmpty(hint))
                    finalFeedback += "\n" + hint;
            }

            if (activeDDAStage >= Puzzle2DDAStage_Final.Stage1)
                DisableWrongGate(selectedGate);

            return finalFeedback;
        }

        string GetWrongGateDistanceFeedback(CP_Gate_Final selectedGate, string fallback)
        {
            CP_Gate_Final correctGate = FindCorrectGateInColumn(selectedGate != null ? selectedGate.columnIndex : -1);
            if (selectedGate == null || correctGate == null)
                return fallback;

            int distance = Mathf.Abs(correctGate.gateIndexInColumn - selectedGate.gateIndexInColumn);
            return distance <= 1
                ? "신호가 근접했습니다. 정답 게이트가 가까운 위치에 있습니다."
                : "신호가 크게 벗어났습니다. 정답 게이트와 거리가 있습니다.";
        }

        void ApplyInterventionTimingSettings()
        {
            switch (interventionTiming)
            {
                case DDAInterventionTiming_Final.Early:
                    wrongGateCountToUpgradeDDA = 2;
                    break;
                case DDAInterventionTiming_Final.Late:
                    wrongGateCountToUpgradeDDA = 4;
                    break;
                default:
                    wrongGateCountToUpgradeDDA = 3;
                    break;
            }
        }

        void TryUpgradeDDAStage()
        {
            if (wrongGateCountInCurrentDDAStage < wrongGateCountToUpgradeDDA)
                return;

            switch (activeDDAStage)
            {
                case Puzzle2DDAStage_Final.None:
                    ApplyDDAStage(Puzzle2DDAStage_Final.Stage1);
                    break;
                case Puzzle2DDAStage_Final.Stage1:
                    ApplyDDAStage(Puzzle2DDAStage_Final.Stage2);
                    break;
                case Puzzle2DDAStage_Final.Stage2:
                    ApplyDDAStage(Puzzle2DDAStage_Final.Stage3);
                    break;
            }

            wrongGateCountInCurrentDDAStage = 0;
        }

        void ApplyDDAStage(Puzzle2DDAStage_Final stage)
        {
            if (stage == Puzzle2DDAStage_Final.None || stage <= activeDDAStage)
                return;

            activeDDAStage = stage;
            if (stage == Puzzle2DDAStage_Final.Stage3)
                ApplyStage3();

            Debug.Log($"[Puzzle2 DDA Final] ReferenceLevel={referenceLevel}, Stage={stage}");
        }

        void ApplyStage3()
        {
            if (stage3Applied)
                return;

            DisableWrongGatesByColumnRatio(0.3f);
            stage3Applied = true;
        }

        void RestoreDDADisabledGates()
        {
            if (allGates == null)
                return;

            for (int i = 0; i < allGates.Count; i++)
            {
                if (allGates[i] != null)
                    allGates[i].SetDDADisabled(false);
            }
        }

        bool DisableWrongGate(CP_Gate_Final gate)
        {
            if (!CanDDADisableGate(gate))
                return false;

            gate.SetDDADisabled(true);
            return true;
        }

        bool CanDDADisableGate(CP_Gate_Final gate)
        {
            return gate != null &&
                   gate.gateType == "normal" &&
                   !gate.isCorrectGate &&
                   !gate.isActivatedByCorrectColor &&
                   !gate.IsDDADisabled;
        }

        int DisableWrongGatesByColumnRatio(float ratio)
        {
            if (gatesByColumn == null)
                return 0;

            int disabledCount = 0;
            for (int c = 0; c < gatesByColumn.Count; c++)
            {
                List<CP_Gate_Final> candidates = new List<CP_Gate_Final>();
                List<CP_Gate_Final> column = gatesByColumn[c];
                if (column == null)
                    continue;

                for (int i = 0; i < column.Count; i++)
                {
                    if (CanDDADisableGate(column[i]))
                        candidates.Add(column[i]);
                }

                int disableCount = Mathf.CeilToInt(candidates.Count * ratio);
                Shuffle(candidates);
                for (int i = 0; i < disableCount && i < candidates.Count; i++)
                {
                    if (DisableWrongGate(candidates[i]))
                        disabledCount++;
                }
            }

            return disabledCount;
        }

        string GetCorrectGateDirectionHint(CP_Gate_Final selectedGate)
        {
            if (selectedGate == null || selectedGate.gateType != "normal")
                return "";

            CP_Gate_Final correctGate = FindCorrectGateInColumn(selectedGate.columnIndex);
            if (correctGate == null)
                return "";

            if (correctGate.gateIndexInColumn < selectedGate.gateIndexInColumn)
                return "정답 게이트는 선택한 게이트보다 위에 있습니다.";

            if (correctGate.gateIndexInColumn > selectedGate.gateIndexInColumn)
                return "정답 게이트는 선택한 게이트보다 아래에 있습니다.";

            return "";
        }

        CP_Gate_Final FindCorrectGateInColumn(int columnIndex)
        {
            if (gatesByColumn == null || columnIndex < 0 || columnIndex >= gatesByColumn.Count)
                return null;

            List<CP_Gate_Final> column = gatesByColumn[columnIndex];
            if (column == null)
                return null;

            for (int i = 0; i < column.Count; i++)
            {
                if (column[i] != null && column[i].isCorrectGate)
                    return column[i];
            }

            return null;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }
    }
}
