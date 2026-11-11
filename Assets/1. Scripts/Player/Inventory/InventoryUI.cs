using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("참조")]
    public PlayerInventory inventory;
    public GameObject inventoryPanel;
    public Transform slotsParent;
    public InventoryDetailUI detailUI;
    public PlayerStatus playerStatus;
    public Hankyul_Fire hankyulFire;
    public Yunseo_Move playerMoveScript;
    public Move_Hankyul hankyulMoveScript;

    [Header("버튼들")]
    public Button useButton;
    public Button dropButton;

    [Header("버리기 패널(2단계 구조)")]
    public GameObject dropPanel;
    public TextMeshProUGUI dropTitleText;
    public GameObject dropAmountGroup;
    public TMP_InputField dropAmountInput;

    private readonly List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private bool isOpen = false;
    private int selectedIndex = -1;
    private bool dropAmountMode = false;
    private bool cachedHankyulAllowLook = true;
    private bool hasCachedHankyulAllowLook = false;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();

        if (hankyulFire == null)
            hankyulFire = GetComponentInParent<Hankyul_Fire>();

        if (hankyulMoveScript == null)
            hankyulMoveScript = GetComponentInParent<Move_Hankyul>();

        ModernInventorySkin.Apply(this);

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshAllSlots;
        else
            Debug.LogError("InventoryUI: PlayerInventory를 찾을 수 없습니다.");

        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        if (dropPanel != null)
            dropPanel.SetActive(false);

        CacheSlotsAndInitialize();
        RefreshAllSlots();

        if (detailUI != null)
            detailUI.Clear();

        UpdateButtons();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshAllSlots;
    }

    private void CacheSlotsAndInitialize()
    {
        slotUIs.Clear();

        if (slotsParent == null)
        {
            Debug.LogError("InventoryUI: slotsParent가 설정되지 않았습니다.");
            return;
        }

        var found = slotsParent.GetComponentsInChildren<InventorySlotUI>(true);
        if (found.Length == 0)
        {
            Debug.LogWarning("InventoryUI: InventorySlotUI를 찾지 못했습니다.");
            return;
        }

        if (inventory != null)
            inventory.slotCount = found.Length;

        for (int i = 0; i < found.Length; i++)
        {
            var slotUI = found[i];
            slotUIs.Add(slotUI);
            slotUI.Initialize(inventory, i, this);
        }
    }

    public void RefreshAllSlots()
    {
        foreach (var s in slotUIs)
            s.Refresh();

        if (selectedIndex >= 0)
            OnSlotClicked(selectedIndex, false);
    }

    private void Update()
    {
        bool iPressed = false;
        var kb = Keyboard.current;
        if (kb != null)
            iPressed = kb.iKey.wasPressedThisFrame;
        else
            iPressed = Input.GetKeyDown(KeyCode.I);

        if (iPressed)
            ToggleInventoryPanel();

        if (isOpen)
            ApplyInventoryCursorState(true);
    }

    public void ToggleInventoryPanel()
    {
        isOpen = !isOpen;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(isOpen);

        if (isOpen)
        {
            // 갱신 신호를 한 번이라도 놓쳤을 경우를 대비해 열 때 항상 다시 그린다.
            RefreshAllSlots();

            ApplyInventoryCursorState(true);
            RefreshAllSlots();

            if (playerMoveScript != null)
                playerMoveScript.allowLook = false;

            if (hankyulMoveScript != null)
            {
                cachedHankyulAllowLook = hankyulMoveScript.allowLook;
                hasCachedHankyulAllowLook = true;
                hankyulMoveScript.allowLook = false;
            }

            GlobalManager.Audio.PlaySFX("inventory_Open");
        }
        else
        {
            ApplyInventoryCursorState(false);

            ClearSelection();

            if (detailUI != null)
                detailUI.Clear();

            CloseDropPanel();

            if (playerMoveScript != null)
                playerMoveScript.allowLook = true;

            if (hankyulMoveScript != null)
                hankyulMoveScript.allowLook = hasCachedHankyulAllowLook ? cachedHankyulAllowLook : true;

            hasCachedHankyulAllowLook = false;

            GlobalManager.Audio.PlaySFX("inventory_Close");
        }
    }

    private void ApplyInventoryCursorState(bool open)
    {
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        UIInteractionRuntimeGuard.SetCursorVisibleRequest("InventoryUI", open);

        if (open)
            UIInteractionRuntimeGuard.EnsureInteractive(inventoryPanel != null ? inventoryPanel : gameObject);
    }

    public void OnClickCloseButton()
    {
        if (isOpen)
            ToggleInventoryPanel();
    }

    public void OnSlotClicked(int index)
    {
        GlobalManager.Audio.PlaySFX("select");
        OnSlotClicked(index, true);
    }

public bool OnSlotDoubleClicked(int index)
    {
        if (inventory == null)
            return false;

        var slot = inventory.GetSlot(index);
        if (slot == null || slot.item == null || slot.count <= 0)
            return false;

        if (!IsPaperItem(slot.item))
            return false;

        selectedIndex = index;
        OnSlotClicked(index, false);
        PaperInventoryPreviewUI.Toggle(slot.item);
        return true;
    }

    private static bool IsPaperItem(ItemData item)
    {
        if (item == null)
            return false;

        return ItemNameRuleUtility.IsPaperObject(item.id)
            || ItemNameRuleUtility.IsPaperObject(item.displayName)
            || ItemNameRuleUtility.IsPaperObject(item.name);
    }


    private void OnSlotClicked(int index, bool fromClick)
    {
        selectedIndex = index;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            bool selected = (i == selectedIndex);
            slotUIs[i].SetSelected(selected);
        }

        if (inventory == null || detailUI == null)
        {
            UpdateButtons();
            return;
        }

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
        {
            detailUI.Clear();
        }
        else
        {
            detailUI.ShowItem(slot.item);
        }

        UpdateButtons();
    }

    private void ClearSelection()
    {
        selectedIndex = -1;
        foreach (var s in slotUIs)
            s.SetSelected(false);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasItem = false;
        bool usable = false;

        if (inventory != null && selectedIndex >= 0)
        {
            var slot = inventory.GetSlot(selectedIndex);
            hasItem = (slot != null && slot.item != null && slot.count > 0);

            if (hasItem && slot.item.effects != null && slot.item.effects.Length > 0)
                usable = true;
        }

        if (useButton != null) useButton.interactable = hasItem && usable;
        if (dropButton != null) dropButton.interactable = hasItem;
    }

    public void OnClickUseButton()
    {
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;
        if (slot.item.effects == null || slot.item.effects.Length == 0) return;

        string message;
        bool used = TryUseSelectedItem(slot.item, out message);

        if (!used)
        {
            if (!string.IsNullOrEmpty(message))
                Debug.LogWarning(message);
            return;
        }

        inventory.ConsumeAt(selectedIndex, 1);
        RefreshAllSlots();
    }

    private bool TryUseSelectedItem(ItemData itemData, out string message)
    {
        message = "";

        if (itemData == null || itemData.effects == null || itemData.effects.Length == 0)
        {
            message = "사용할 수 없는 아이템입니다.";
            return false;
        }

        bool hasEnergyPack = false;

        for (int i = 0; i < itemData.effects.Length; i++)
        {
            if (itemData.effects[i].type == EffectType.EnergyPack)
            {
                hasEnergyPack = true;
                break;
            }
        }

        if (hasEnergyPack)
        {
            if (hankyulFire == null)
                hankyulFire = GetComponentInParent<Hankyul_Fire>();

            if (hankyulFire == null)
            {
                // 에너지팩은 한결의 총을 채우는 아이템이다. 윤서는 쓰지 않고 남겨 둔다.
                message = AetherCharacterChoice.YunseoDisplayName + "는 사용할 수 없는 아이템입니다. (" + AetherCharacterChoice.HankyulDisplayName + "의 총 에너지)";
                return false;
            }

            return hankyulFire.UseEnergyPack(out message);
        }

        if (playerStatus == null)
            playerStatus = GetComponentInParent<PlayerStatus>();

        if (playerStatus == null)
        {
            message = "상태 정보를 찾을 수 없습니다.";
            return false;
        }

        return playerStatus.ApplyItem(itemData, out message);
    }

    public void OnClickDropButton()
    {
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0 || dropPanel == null) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        dropPanel.SetActive(true);
        dropAmountMode = false;

        if (dropTitleText != null)
            dropTitleText.gameObject.SetActive(true);

        if (dropAmountGroup != null)
            dropAmountGroup.SetActive(false);

        if (dropAmountInput != null)
        {
            dropAmountInput.text = "1";
            dropAmountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }
    }

    public void OnClickDropCancel()
    {
        GlobalManager.Audio.PlaySFX("btn_Click");
        CloseDropPanel();
    }

    private void CloseDropPanel()
    {
        dropAmountMode = false;
        if (dropPanel != null)
            dropPanel.SetActive(false);
    }

    public void OnClickDropConfirm()
    {
        GlobalManager.Audio.PlaySFX("btn_Click");

        if (inventory == null || selectedIndex < 0) return;

        var slot = inventory.GetSlot(selectedIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        if (!dropAmountMode)
        {
            if (slot.count <= 1)
            {
                inventory.ConsumeAt(selectedIndex, 1);
                CloseDropPanel();
                RefreshAllSlots();
            }
            else
            {
                dropAmountMode = true;

                if (dropTitleText != null)
                    dropTitleText.gameObject.SetActive(false);

                if (dropAmountGroup != null)
                    dropAmountGroup.SetActive(true);

                if (dropAmountInput != null)
                    dropAmountInput.text = "1";
            }
        }
        else
        {
            int amount = 1;
            if (dropAmountInput != null && !string.IsNullOrEmpty(dropAmountInput.text))
                int.TryParse(dropAmountInput.text, out amount);

            amount = Mathf.Clamp(amount, 1, slot.count);

            inventory.ConsumeAt(selectedIndex, amount);

            CloseDropPanel();
            RefreshAllSlots();
        }
    }

    public void RequestSwapSlots(int fromIndex, int toIndex)
    {
        if (inventory == null) return;
        inventory.SwapSlots(fromIndex, toIndex);
    }
}
