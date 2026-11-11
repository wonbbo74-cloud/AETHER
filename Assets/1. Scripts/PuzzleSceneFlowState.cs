using System.Collections.Generic;
using UnityEngine;

public static class PuzzleSceneFlowState
{
    private static readonly HashSet<string> ClearedPuzzleIds = new HashSet<string>();
    private static readonly HashSet<string> PendingRewardPuzzleIds = new HashSet<string>();

    public static string ActivePuzzleId { get; private set; }
    public static string ReturnSceneName { get; private set; }
    public static string ReturnPoseSceneName { get; private set; }
    public static Vector3 ReturnPlayerPosition { get; private set; }
    public static Quaternion ReturnPlayerRotation { get; private set; }
    public static bool HasStoredReturnPose { get; private set; }

    public static bool HasPendingReturn =>
        !string.IsNullOrWhiteSpace(ActivePuzzleId) &&
        !string.IsNullOrWhiteSpace(ReturnSceneName);

    public static bool HasAnyPendingReward => PendingRewardPuzzleIds.Count > 0;

    public static void BeginPuzzle(string puzzleId, string returnSceneName, Vector3 playerPosition, Quaternion playerRotation)
    {
        ActivePuzzleId = puzzleId;
        ReturnSceneName = returnSceneName;
        ReturnPoseSceneName = returnSceneName;
        ReturnPlayerPosition = playerPosition;
        ReturnPlayerRotation = playerRotation;
        HasStoredReturnPose = true;
    }

    public static void MarkPuzzleCleared(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId))
            return;

        ClearedPuzzleIds.Add(puzzleId);
        PendingRewardPuzzleIds.Add(puzzleId);
    }

    public static List<string> ExportCleared() => new List<string>(ClearedPuzzleIds);

    public static void ImportCleared(List<string> ids)
    {
        ClearedPuzzleIds.Clear();
        PendingRewardPuzzleIds.Clear();
        if (ids != null)
            foreach (string id in ids) ClearedPuzzleIds.Add(id);
    }

    public static bool IsPuzzleCleared(string puzzleId)
    {
        return !string.IsNullOrWhiteSpace(puzzleId) && ClearedPuzzleIds.Contains(puzzleId);
    }

    public static bool ConsumePendingReward(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId))
            return false;

        if (!PendingRewardPuzzleIds.Contains(puzzleId))
            return false;

        PendingRewardPuzzleIds.Remove(puzzleId);
        return true;
    }

    public static string ConsumeReturnScene()
    {
        string sceneName = ReturnSceneName;
        ActivePuzzleId = null;
        ReturnSceneName = null;
        return sceneName;
    }

    public static bool TryConsumeReturnPose(string sceneName, out Vector3 position, out Quaternion rotation)
    {
        if (HasStoredReturnPose && sceneName == ReturnPoseSceneName)
        {
            position = ReturnPlayerPosition;
            rotation = ReturnPlayerRotation;

            HasStoredReturnPose = false;
            ReturnPoseSceneName = null;
            ReturnPlayerPosition = default;
            ReturnPlayerRotation = Quaternion.identity;
            return true;
        }

        position = default;
        rotation = Quaternion.identity;
        return false;
    }
}
