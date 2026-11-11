using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PartsCraftingInventoryBridge : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Parts Make UI")]
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private InventorySlot[] sourceSlots;
    [SerializeField] private Button createButton;

    [Header("Options")]
    [SerializeField] private bool autoFindSourceSlots = true;

    private void Awake()
    {
        ResolveReferences();
        BindCreateButton();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeInventory(true);
        RefreshFromInventory();
    }

    private void OnDisable()
    {
        SubscribeInventory(false);
    }

    private void OnDestroy()
    {
        SubscribeInventory(false);
    }

    public void RefreshFromInventory()
    {
        ResolveReferences();

        if (sourceSlots == null)
            return;

        foreach (InventorySlot slot in sourceSlots)
        {
            if (slot != null)
                slot.Clear();
        }

        if (inventory == null || craftingController == null)
        {
            RefreshCreateButton();
            return;
        }

        HashSet<string> allowedItemIds = GetAllowedCraftingItemIds();

        int index = 0;
        for (int i = 0; i < inventory.slots.Count && index < sourceSlots.Length; i++)
        {
            PlayerInventory.Slot invSlot = inventory.slots[i];
            if (invSlot == null || invSlot.item == null || invSlot.count <= 0)
                continue;

            string bestName = ItemNameRuleUtility.GetBestItemName(null, invSlot.item);
            string itemId = ItemNameRuleUtility.GetBestItemId(invSlot.item);
            bool allowedByRecipe = allowedItemIds.Count > 0 && allowedItemIds.Contains(itemId);
            bool allowedAsModule = allowedItemIds.Count == 0 && ItemNameRuleUtility.IsModuleItem(bestName);

            if (!allowedByRecipe && !allowedAsModule)
                continue;

            InventorySlot uiSlot = sourceSlots[index++];
            if (uiSlot != null)
                uiSlot.SetItem(craftingController.GetOrCreateRuntimeItem(invSlot.item));
        }

        RefreshCreateButton();
    }

    private HashSet<string> GetAllowedCraftingItemIds()
    {
        HashSet<string> ids = new HashSet<string>();
        if (craftingController == null || craftingController.partRecipes == null)
            return ids;

        foreach (CraftingRecipeData recipe in craftingController.partRecipes)
        {
            if (recipe == null || recipe.requiredModuleIds == null)
                continue;

            foreach (string itemId in recipe.requiredModuleIds)
            {
                string normalized = ItemNameRuleUtility.NormalizeModuleAlias(itemId);
                if (!string.IsNullOrEmpty(normalized))
                    ids.Add(normalized);
            }
        }

        return ids;
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (craftingController == null)
            craftingController = GetComponent<CraftingController>();
        if (craftingController == null)
            craftingController = GetComponentInChildren<CraftingController>(true);
        if (craftingController == null)
            craftingController = FindObjectOfType<CraftingController>(true);

        if (craftingController != null && craftingController.playerInventory == null)
            craftingController.playerInventory = inventory;

        if (autoFindSourceSlots && (sourceSlots == null || sourceSlots.Length == 0))
            sourceSlots = FindSourceSlots();

        if (createButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && button.name.ToLowerInvariant().Contains("create"))
                {
                    createButton = button;
                    break;
                }
            }
        }
    }

    private InventorySlot[] FindSourceSlots()
    {
        List<InventorySlot> slots = new List<InventorySlot>();
        DraggableSlot[] draggables = GetComponentsInChildren<DraggableSlot>(true);

        foreach (DraggableSlot draggable in draggables)
        {
            if (draggable == null || draggable.slot == null)
                continue;

            if (IsMaterialSlot(draggable.slot))
                continue;

            slots.Add(draggable.slot);
        }

        return slots.ToArray();
    }

    private bool IsMaterialSlot(InventorySlot slot)
    {
        if (craftingController == null || craftingController.materialSlots == null)
            return false;

        foreach (InventorySlot materialSlot in craftingController.materialSlots)
        {
            if (materialSlot == slot)
                return true;
        }

        return false;
    }

    private void BindCreateButton()
    {
        ResolveReferences();
        if (createButton == null || craftingController == null)
            return;

        craftingController.createButton = createButton;
        createButton.onClick.RemoveListener(craftingController.CreateParts);
        createButton.onClick.AddListener(craftingController.CreateParts);
    }

    private void RefreshCreateButton()
    {
        if (craftingController != null)
            craftingController.OnMaterialsChanged();
    }

    private void SubscribeInventory(bool subscribe)
    {
        if (inventory == null)
            return;

        inventory.OnInventoryChanged -= RefreshFromInventory;
        if (subscribe)
            inventory.OnInventoryChanged += RefreshFromInventory;
    }
}
