using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PartEquipmentUI : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Transform partListParent;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text selectedText;
    [SerializeField] private PartSlotUI[] slotUIs;

    private readonly List<GameObject> entries = new List<GameObject>();
    private PartData selectedPart;
    private bool isOpen;

    private void Awake()
    {
        ResolveReferences();
        if (equipButton != null)
            equipButton.onClick.AddListener(EquipSelected);
        Hide();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe(true);
        if (isOpen)
            Refresh();
    }

    private void OnDisable()
    {
        Subscribe(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool pressed = keyboard != null
            ? keyboard.pKey.wasPressedThisFrame
            : Input.GetKeyDown(toggleKey);

        if (pressed)
            Toggle();
    }

    public void Toggle()
    {
        if (isOpen)
            Hide();
        else
            Show();
    }

    public void Show()
    {
        ResolveReferences();
        isOpen = true;
        if (canvas != null)
            canvas.enabled = true;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
    }

    public void Hide()
    {
        isOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (canvas != null)
            canvas.enabled = false;
    }

    private void Refresh()
    {
        RefreshPartList();
        RefreshSlots();
        if (equipButton != null)
            equipButton.interactable = selectedPart != null;
    }

    private void RefreshPartList()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                Destroy(entries[i]);
        }
        entries.Clear();

        if (inventory == null || partListParent == null)
            return;

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            PartData part = slot != null ? slot.item as PartData : null;
            if (!CanShowPart(part) || slot.count <= 0)
                continue;

            Button button = CreateEntryButton(part);
            entries.Add(button.gameObject);
        }
    }

    private void RefreshSlots()
    {
        if (equipmentManager == null || slotUIs == null)
            return;

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
                slotUIs[i].Refresh(equipmentManager.GetEquipped(slotUIs[i].SlotType));
        }
    }

    private void SelectPart(PartData part)
    {
        selectedPart = part;
        if (selectedText != null)
            selectedText.text = part != null ? $"{part.displayName} -> {part.slotType}" : "No part selected";

        if (equipButton != null)
            equipButton.interactable = selectedPart != null;
    }

    private void EquipSelected()
    {
        if (equipmentManager == null || selectedPart == null)
            return;

        if (!equipmentManager.EquipPart(selectedPart))
            return;

        selectedPart = null;
        Refresh();
    }

    private static bool CanShowPart(PartData part)
    {
        if (part == null || part.partType == PartType.None)
            return false;

        string id = ItemNameRuleUtility.GetBestItemId(part);
        string displayName = part.displayName;
        string bestName = ItemNameRuleUtility.GetBestItemName(null, part);

        if (ItemNameRuleUtility.IsModuleItem(id) || ItemNameRuleUtility.IsModuleItem(displayName) || ItemNameRuleUtility.IsModuleItem(bestName))
            return false;

        if (ItemNameRuleUtility.IsPaperObject(id) || ItemNameRuleUtility.IsPaperObject(displayName) || ItemNameRuleUtility.IsPaperObject(bestName))
            return false;

        return true;
    }

    private Button CreateEntryButton(PartData part)
    {
        GameObject entryObject = new GameObject("Part Entry", typeof(RectTransform), typeof(Image), typeof(Button));
        entryObject.transform.SetParent(partListParent, false);
        entryObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);

        RectTransform rect = entryObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 42f);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(entryObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 30f);

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = part.icon != null ? part.icon : part.itemSprite;
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(entryObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(46f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = $"{part.displayName} ({part.slotType})";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontSize = 18f;
        label.color = Color.white;
        label.raycastTarget = false;

        Button button = entryObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectPart(part));
        return button;
    }

    private void Subscribe(bool subscribe)
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= Refresh;
            if (subscribe)
                inventory.OnInventoryChanged += Refresh;
        }

        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged -= HandleEquipmentChanged;
            if (subscribe)
                equipmentManager.OnEquipmentChanged += HandleEquipmentChanged;
        }
    }

    private void HandleEquipmentChanged(PartSlotType slot, PartData part)
    {
        if (isOpen)
            Refresh();
    }

    private void ResolveReferences()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();
        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>();
    }
}
