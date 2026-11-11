using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CodebreakerLogManager : MonoBehaviour
{
    [Serializable]
    private class RecentLogSlot
    {
        [Tooltip("Inspector에서 구분하기 위한 이름입니다. 표시 로직에는 영향을 주지 않습니다.")]
        public string label = "Latest";
        [Tooltip("0 = 가장 최근 로그, 1 = 최근 로그 -1, 2 = 최근 로그 -2")]
        [Min(0)] public int latestOffset = 0;
    }

    [Header("Recent Log Auto Generation")]
    [Tooltip("최근 로그 row들이 생성될 부모입니다.")]
    [SerializeField] private RectTransform recentLogParent;
    [Tooltip("HackLogRowCellsUI가 붙은 최근 로그 row 프리팹입니다.")]
    [SerializeField] private HackLogRowCellsUI recentLogRowPrefab;
    [Tooltip("생성할 최근 로그 줄과 각 줄이 보여줄 로그 순서입니다.")]
    [SerializeField] private RecentLogSlot[] recentLogSlots =
    {
        new RecentLogSlot { label = "Latest", latestOffset = 0 },
        new RecentLogSlot { label = "Latest -1", latestOffset = 1 },
        new RecentLogSlot { label = "Latest -2", latestOffset = 2 }
    };
    [SerializeField] private bool ensureRecentRowsVerticalLayoutGroup = true;
    [SerializeField, Min(0f)] private float recentRowSpacing = 8f;

    [Header("Full Log Panel")]
    [SerializeField] private GameObject fullLogPanel;
    [SerializeField] private Transform fullLogContent;
    [SerializeField] private HackLogRowCellsUI fullLogRowPrefab;
    [SerializeField] private ScrollRect fullLogScrollRect;
    [SerializeField] private bool ensureFullLogVerticalLayoutGroup = true;
    [SerializeField, Min(0f)] private float fullLogRowSpacing = 8f;
    [SerializeField] private bool ensureFullLogContentSizeFitter = true;
    [SerializeField] private bool ensureFullLogViewportRectMask = true;

    private readonly List<CodebreakerLogEntry_Final> sessionLogs = new List<CodebreakerLogEntry_Final>();
    private readonly List<CodebreakerLogEntry_Final> recentLogs = new List<CodebreakerLogEntry_Final>();
    private readonly List<HackLogRowCellsUI> recentRows = new List<HackLogRowCellsUI>();
    private readonly List<HackLogRowCellsUI> fullLogRows = new List<HackLogRowCellsUI>();
    private int digitCount = 4;
    private int recentScrollOffset;

    private void Awake()
    {
        Configure(digitCount);
        CloseFullLogPanel();
    }

    // Unity Button On Click 연결:
    // Full Log Button On Click -> CodebreakerLogManager.OnClickOpenFullLog()
    public void OnClickOpenFullLog()
    {
        OpenFullLogPanel();
    }

    // Unity Button On Click 연결:
    // Close Log Button On Click -> CodebreakerLogManager.OnClickCloseFullLog()
    public void OnClickCloseFullLog()
    {
        CloseFullLogPanel();
    }

    public void Configure(int newDigitCount)
    {
        digitCount = Mathf.Max(1, newDigitCount);
        EnsureDefaultRecentSlots();
        BuildRecentRows();
        RebuildExistingFullLogRows();
        RefreshRecentLogs();

        if (fullLogPanel != null && fullLogPanel.activeSelf)
            RefreshFullLogPanel();
    }

    public void AddLog(int level, int attemptNumber, string input, string result)
    {
        CodebreakerLogEntry_Final entry = new CodebreakerLogEntry_Final(level, attemptNumber, input, result);
        sessionLogs.Add(entry);
        recentLogs.Add(entry);

        recentScrollOffset = 0;
        RefreshRecentLogs();

        if (fullLogPanel != null && fullLogPanel.activeSelf)
            RefreshFullLogPanel();
    }

    public void ClearRecentLogs()
    {
        recentLogs.Clear();
        recentScrollOffset = 0;
        RefreshRecentLogs();
    }

    public void ClearAllLogs()
    {
        sessionLogs.Clear();
        ClearRecentLogs();
        RefreshFullLogPanel();
    }

    public void RefreshRecentLogs()
    {
        if (recentRows.Count == 0 || recentLogSlots == null) return;

        for (int rowIndex = 0; rowIndex < recentRows.Count; rowIndex++)
        {
            HackLogRowCellsUI row = recentRows[rowIndex];
            if (row == null) continue;

            int latestOffset = GetSlotOffset(rowIndex) + recentScrollOffset;
            int logIndex = recentLogs.Count - 1 - latestOffset;

            if (logIndex >= 0 && logIndex < recentLogs.Count)
            {
                CodebreakerLogEntry_Final entry = recentLogs[logIndex];
                row.Set(entry);
            }
            else
            {
                row.Clear();
            }
        }
    }

    public void OpenFullLogPanel()
    {
        if (fullLogPanel == null)
        {
            Debug.LogWarning($"[{nameof(CodebreakerLogManager)}] fullLogPanel이 연결되지 않았습니다.", this);
            return;
        }

        fullLogPanel.SetActive(true);
        EnsureFullLogClipping();
        RefreshFullLogPanel();
        Canvas.ForceUpdateCanvases();

        if (fullLogScrollRect != null)
            fullLogScrollRect.verticalNormalizedPosition = 1f;
    }

    public void CloseFullLogPanel()
    {
        if (fullLogPanel != null)
            fullLogPanel.SetActive(false);
    }

    public void ScrollUp()
    {
        int maxOffset = Mathf.Max(0, recentLogs.Count - 1 - GetMinimumSlotOffset());
        if (recentScrollOffset >= maxOffset) return;

        recentScrollOffset++;
        RefreshRecentLogs();
    }

    public void ScrollDown()
    {
        if (recentScrollOffset <= 0) return;

        recentScrollOffset--;
        RefreshRecentLogs();
    }

    private void BuildRecentRows()
    {
        ClearRows(recentRows);

        if (recentLogParent == null || recentLogRowPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CodebreakerLogManager)}] recentLogParent/recentLogRowPrefab이 연결되지 않아 최근 로그 row를 생성할 수 없습니다.", this);
            return;
        }

        ClearChildren(recentLogParent);
        EnsureRecentRowsLayoutGroup();

        int count = Mathf.Max(1, recentLogSlots.Length);
        for (int i = 0; i < count; i++)
        {
            HackLogRowCellsUI row = Instantiate(recentLogRowPrefab, recentLogParent);
            row.name = $"{recentLogRowPrefab.name}_Recent_{i}_{GetSlotOffset(i)}";
            row.gameObject.SetActive(true);
            row.BuildCells(digitCount);
            recentRows.Add(row);
        }
    }

    private void RefreshFullLogPanel()
    {
        if (fullLogContent == null || fullLogRowPrefab == null)
        {
            if (sessionLogs.Count > 0)
                Debug.LogWarning($"[{nameof(CodebreakerLogManager)}] fullLogContent/fullLogRowPrefab이 연결되지 않아 전체 로그 row를 생성할 수 없습니다.", this);
            return;
        }

        EnsureFullLogLayoutGroup();
        EnsureFullLogClipping();
        EnsureFullLogRowCount(sessionLogs.Count);

        for (int i = 0; i < fullLogRows.Count; i++)
        {
            HackLogRowCellsUI row = fullLogRows[i];
            if (row == null) continue;

            bool hasLog = i < sessionLogs.Count;
            row.gameObject.SetActive(hasLog);

            if (hasLog)
            {
                CodebreakerLogEntry_Final entry = sessionLogs[i];
                row.Set(entry);
            }
        }
    }

    private void EnsureFullLogRowCount(int count)
    {
        while (fullLogRows.Count < count)
        {
            HackLogRowCellsUI row = Instantiate(fullLogRowPrefab, fullLogContent);
            row.name = $"{fullLogRowPrefab.name}_Full_{fullLogRows.Count}";
            row.gameObject.SetActive(true);
            row.BuildCells(digitCount);
            fullLogRows.Add(row);
        }
    }

    private void RebuildExistingFullLogRows()
    {
        for (int i = 0; i < fullLogRows.Count; i++)
        {
            if (fullLogRows[i] != null)
                fullLogRows[i].BuildCells(digitCount);
        }
    }

    private void EnsureDefaultRecentSlots()
    {
        if (recentLogSlots != null && recentLogSlots.Length > 0) return;

        recentLogSlots = new[]
        {
            new RecentLogSlot { label = "Latest", latestOffset = 0 },
            new RecentLogSlot { label = "Latest -1", latestOffset = 1 },
            new RecentLogSlot { label = "Latest -2", latestOffset = 2 }
        };
    }

    private int GetSlotOffset(int index)
    {
        if (recentLogSlots == null || index < 0 || index >= recentLogSlots.Length || recentLogSlots[index] == null)
            return index;

        return Mathf.Max(0, recentLogSlots[index].latestOffset);
    }

    private int GetMinimumSlotOffset()
    {
        int min = int.MaxValue;
        if (recentLogSlots != null)
        {
            for (int i = 0; i < recentLogSlots.Length; i++)
                min = Mathf.Min(min, GetSlotOffset(i));
        }

        return min == int.MaxValue ? 0 : min;
    }

    private void ClearRows(List<HackLogRowCellsUI> rows)
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i] == null) continue;

            if (Application.isPlaying)
                Destroy(rows[i].gameObject);
            else
                DestroyImmediate(rows[i].gameObject);
        }

        rows.Clear();
    }

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void EnsureRecentRowsLayoutGroup()
    {
        if (!ensureRecentRowsVerticalLayoutGroup || recentLogParent == null) return;

        VerticalLayoutGroup layout = recentLogParent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = recentLogParent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = recentRowSpacing;
    }

    private void EnsureFullLogLayoutGroup()
    {
        if (!ensureFullLogVerticalLayoutGroup || fullLogContent == null) return;

        RectTransform contentRect = fullLogContent as RectTransform;
        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(contentRect.anchorMin.x, 1f);
            contentRect.anchorMax = new Vector2(contentRect.anchorMax.x, 1f);
            contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);
        }

        VerticalLayoutGroup layout = fullLogContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = fullLogContent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = fullLogRowSpacing;

        if (!ensureFullLogContentSizeFitter) return;

        ContentSizeFitter fitter = fullLogContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = fullLogContent.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureFullLogClipping()
    {
        if (!ensureFullLogViewportRectMask || fullLogScrollRect == null || fullLogScrollRect.viewport == null)
            return;

        RectTransform viewport = fullLogScrollRect.viewport;
        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
            viewport.gameObject.AddComponent<RectMask2D>();
    }
}
