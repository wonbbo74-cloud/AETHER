using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartCraftingGridManager : MonoBehaviour
{
    [SerializeField] private Vector2Int gridSize = new Vector2Int(5, 5);
    [SerializeField] private Transform slotRoot;
    [SerializeField] private PartGridSlotUI slotPrefab;
    [SerializeField] private GridLayoutGroup layoutGroup;

    private readonly Dictionary<Vector2Int, PartGridSlotUI> slots = new Dictionary<Vector2Int, PartGridSlotUI>();
    private PartCraftingUI owner;

    public event Action OnGridChanged;

    private void Awake()
    {
        EnsureGrid();
    }

    public void Setup(PartCraftingUI newOwner, Vector2Int newGridSize)
    {
        owner = newOwner;
        gridSize = newGridSize.x > 0 && newGridSize.y > 0 ? newGridSize : new Vector2Int(5, 5);
        EnsureGrid();
    }

    public void PlaceSelectedItem(Vector2Int position)
    {
        if (owner == null)
            return;

        PlaceItem(position, owner.SelectedModule);
    }

    public void PlaceItem(Vector2Int position, ItemData item)
    {
        if (!slots.TryGetValue(position, out PartGridSlotUI slot))
            return;

        if (owner != null && !owner.CanPlaceModuleAt(position, item))
        {
            OnGridChanged?.Invoke();
            return;
        }

        if (item != null)
        {
            foreach (PartGridSlotUI existingSlot in slots.Values)
            {
                if (existingSlot != null && existingSlot != slot && existingSlot.Item == item)
                    existingSlot.SetItem(null);
            }
        }

        slot.SetItem(item);
        OnGridChanged?.Invoke();
    }

    public void ClearGrid()
    {
        foreach (PartGridSlotUI slot in slots.Values)
        {
            if (slot != null)
                slot.SetItem(null);
        }

        OnGridChanged?.Invoke();
    }

    public bool MatchesRecipe(CraftingRecipeData recipe)
    {
        Dictionary<Vector2Int, string> placedItemIds = new Dictionary<Vector2Int, string>();
        foreach (PartGridSlotUI slot in slots.Values)
        {
            if (slot != null && slot.Item != null)
                placedItemIds[slot.Position] = ItemNameRuleUtility.GetBestItemId(slot.Item);
        }

        return MatchesRecipe(recipe, placedItemIds);
    }

    public static bool MatchesRecipe(CraftingRecipeData recipe, Dictionary<Vector2Int, string> placedItemIds)
    {
        if (recipe == null || recipe.expectedPositions == null || recipe.expectedPositions.Length == 0)
            return false;

        if (placedItemIds == null || placedItemIds.Count != recipe.expectedPositions.Length)
            return false;

        for (int i = 0; i < recipe.expectedPositions.Length; i++)
        {
            CraftingRecipeData.ExpectedPosition expected = recipe.expectedPositions[i];
            if (!placedItemIds.TryGetValue(expected.position, out string placedItemId))
                return false;

            if (ItemNameRuleUtility.NormalizeModuleAlias(placedItemId) != ItemNameRuleUtility.NormalizeModuleAlias(expected.itemId))
                return false;
        }

        return true;
    }

    private void EnsureGrid()
    {
        if (slotRoot == null)
            slotRoot = transform;

        if (layoutGroup == null)
            layoutGroup = slotRoot.GetComponent<GridLayoutGroup>();

        if (layoutGroup == null)
            layoutGroup = slotRoot.gameObject.AddComponent<GridLayoutGroup>();

        layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layoutGroup.constraintCount = Mathf.Max(1, gridSize.x);
        layoutGroup.cellSize = new Vector2(72f, 72f);
        layoutGroup.spacing = new Vector2(6f, 6f);

        slots.Clear();
        List<PartGridSlotUI> existing = new List<PartGridSlotUI>(slotRoot.GetComponentsInChildren<PartGridSlotUI>(true));
        int needed = Mathf.Max(1, gridSize.x * gridSize.y);

        for (int i = existing.Count; i < needed; i++)
            existing.Add(CreateSlot(slotRoot));

        for (int i = 0; i < existing.Count; i++)
        {
            bool active = i < needed;
            existing[i].gameObject.SetActive(active);
            if (!active)
                continue;

            Vector2Int position = new Vector2Int(i % gridSize.x, i / gridSize.x);
            existing[i].Setup(this, position);
            slots[position] = existing[i];
        }
    }

    private PartGridSlotUI CreateSlot(Transform parent)
    {
        if (slotPrefab != null)
            return Instantiate(slotPrefab, parent);

        GameObject slotObject = new GameObject("Part Grid Slot", typeof(RectTransform), typeof(Image), typeof(PartGridSlotUI));
        slotObject.transform.SetParent(parent, false);

        Image bg = slotObject.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.18f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(slotObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMPro.TMP_Text label = labelObject.GetComponent<TMPro.TMP_Text>();
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.fontSize = 14f;
        label.color = Color.white;
        label.raycastTarget = false;

        return slotObject.GetComponent<PartGridSlotUI>();
    }
}
