using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HackLogRowCellsUI : MonoBehaviour
{
    [Header("Level / Attempt Text (Optional)")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text attemptText;

    [Header("Digit Cell Auto Generation")]
    [Tooltip("숫자칸 프리팹들이 생성될 부모입니다.")]
    [SerializeField] private RectTransform inputCellParent;
    [Tooltip("Image 루트 + TMP_Text 자식 구조의 숫자칸 프리팹입니다.")]
    [SerializeField] private GameObject inputCellPrefab;
    [SerializeField] private bool ensureHorizontalLayoutGroup = true;
    [SerializeField, Min(0f)] private float inputCellSpacing = 6f;
    [SerializeField] private bool matchInputCellHeightToParent = true;
    [SerializeField, Min(0.01f)] private float fallbackInputCellAspectRatio = 1f;
    [SerializeField] private bool clearExistingInputCellChildren = true;

    [Header("Result")]
    [SerializeField] private TMP_Text resultText;

    private readonly List<TMP_Text> inputTexts = new List<TMP_Text>();

    public void BuildCells(int digitCount)
    {
        digitCount = Mathf.Max(1, digitCount);
        inputTexts.Clear();

        if (inputCellParent == null || inputCellPrefab == null)
        {
            Debug.LogWarning($"[{nameof(HackLogRowCellsUI)}] inputCellParent/inputCellPrefab이 연결되지 않아 로그 숫자칸을 생성할 수 없습니다.", this);
            return;
        }

        if (clearExistingInputCellChildren)
            ClearChildren(inputCellParent);

        EnsureLayoutGroup(inputCellParent);

        for (int i = 0; i < digitCount; i++)
        {
            GameObject cellObject = Instantiate(inputCellPrefab, inputCellParent);
            cellObject.name = $"{inputCellPrefab.name}_{i}";
            cellObject.SetActive(true);
            ApplyInputCellSize(cellObject);

            TMP_Text text = cellObject.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                Debug.LogWarning($"[{nameof(HackLogRowCellsUI)}] 생성된 로그 숫자칸 '{cellObject.name}' 내부에서 TMP_Text를 찾지 못했습니다.", cellObject);
                continue;
            }

            inputTexts.Add(text);
        }
    }

    public void Clear()
    {
        Set(0, 0, "", "");
    }

    public void Set(int level, int attemptNumber, string input, string result)
    {
        if (levelText != null)
            levelText.text = level > 0 ? level.ToString() : "";

        if (attemptText != null)
            attemptText.text = attemptNumber > 0 ? attemptNumber.ToString() : "";

        SetInput(input);
        SetResult(result);
    }

    public void Set(CodebreakerLogEntry_Final entry)
    {
        Set(entry.level, entry.attemptNumber, entry.input, entry.result);
    }

    public void SetInput(string input)
    {
        for (int i = 0; i < inputTexts.Count; i++)
        {
            if (inputTexts[i] == null) continue;

            inputTexts[i].text = !string.IsNullOrEmpty(input) && i < input.Length
                ? input[i].ToString()
                : "";
        }
    }

    public void SetResult(string result)
    {
        if (resultText != null)
            resultText.text = result;
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

    private void EnsureLayoutGroup(RectTransform parent)
    {
        if (!ensureHorizontalLayoutGroup || parent == null) return;

        HorizontalLayoutGroup layout = parent.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = inputCellSpacing;
    }

    private void ApplyInputCellSize(GameObject cellObject)
    {
        if (!matchInputCellHeightToParent || inputCellParent == null || cellObject == null) return;

        RectTransform cellRect = cellObject.GetComponent<RectTransform>();
        if (cellRect == null) return;

        float height = inputCellParent.rect.height;
        if (height <= 0f)
            height = cellRect.rect.height;
        if (height <= 0f)
            height = 1f;

        float aspectRatio = GetPrefabAspectRatio();
        float width = height * aspectRatio;

        cellRect.sizeDelta = new Vector2(width, height);

        LayoutElement layoutElement = cellObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = cellObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;
        layoutElement.minWidth = width;
        layoutElement.minHeight = height;
    }

    private float GetPrefabAspectRatio()
    {
        RectTransform prefabRect = inputCellPrefab != null ? inputCellPrefab.GetComponent<RectTransform>() : null;
        if (prefabRect != null && prefabRect.rect.height > 0f && prefabRect.rect.width > 0f)
            return prefabRect.rect.width / prefabRect.rect.height;

        return Mathf.Max(0.01f, fallbackInputCellAspectRatio);
    }
}
