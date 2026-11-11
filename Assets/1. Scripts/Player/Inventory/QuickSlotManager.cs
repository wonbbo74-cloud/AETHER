using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class QuickSlotManager : MonoBehaviour
{
    [Header("참조(인스펙터 연결 권장)")]
    public PlayerInventory inventory;
    public PlayerStatus playerStatus;

    [Tooltip("퀵슬롯 개수 (지금은 5칸)")]
    public int slotCount = 5;

    // 각 퀵슬롯 -> 인벤토리 슬롯 인덱스 (-1 비어있음)
    [SerializeField] private int[] quickSlots;

    // UI는 PlayerUIManager가 소유하고 여기엔 주입만 됨
    private QuickSlotUI[] slotUIs;

    private void Awake()
    {
        EnsureQuickSlots();

        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (playerStatus == null)
            playerStatus = FindObjectOfType<PlayerStatus>();

        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;
        else
            Debug.LogWarning("QuickSlotManager: PlayerInventory를 아직 찾지 못했습니다. 런타임 HUD에서 다시 연결합니다.");
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    /// <summary>칸 UI 가 이미 연결돼 있는지.</summary>
    public bool HasBoundUI => slotUIs != null && slotUIs.Length > 0;

    // PlayerUIManager에서 호출 (UI 주입)
    public void BindUI(QuickSlotUI[] uis)
    {
        EnsureQuickSlots();
        slotUIs = uis;
        AutoAssignFromInventory();
        RefreshAllSlots();
    }

    public void SetRuntimeReferences(PlayerInventory newInventory, PlayerStatus newPlayerStatus)
    {
        EnsureQuickSlots();

        if (inventory != newInventory)
        {
            if (inventory != null)
                inventory.OnInventoryChanged -= HandleInventoryChanged;

            inventory = newInventory;

            if (inventory != null)
                inventory.OnInventoryChanged += HandleInventoryChanged;
        }

        playerStatus = newPlayerStatus;
        AutoAssignFromInventory();
        RefreshAllSlots();
    }

    private void HandleInventoryChanged()
    {
        EnsureQuickSlots();
        AutoAssignFromInventory();  // ✅ 자동 배치
        RefreshAllSlots();          // ✅ UI 갱신
    }

    /// <summary>
    /// 인벤토리에 존재하는 아이템을 빈 퀵슬롯에 자동으로 배치한다.
    /// 규칙:
    /// - 이미 어떤 퀵슬롯이 참조 중인 인벤 인덱스는 중복 배치하지 않음
    /// - 빈 퀵슬롯(-1)에만 채움
    /// </summary>
    private void AutoAssignFromInventory()
    {
        EnsureQuickSlots();
        if (inventory == null) return;

        // 퀵슬롯엔 쓸 수 있는 것(물·식량·팩)만 둔다. 예전엔 05 에서 모듈을 주우면 다섯 칸이 모두
        // 배터리·케이블·기어로 차서, 정작 물·식량은 퀵슬롯에서 사라졌다.
        List<int> recentIndices = inventory.GetRecentOccupiedSlotIndices(inventory.SlotCount);
        recentIndices.RemoveAll(i =>
        {
            PlayerInventory.Slot slot = inventory.GetSlot(i);
            return slot == null || !AetherItemUse.IsUsable(slot.item) || !AetherItemUse.IsDroppable(slot.item);
        });
        for (int q = 0; q < quickSlots.Length; q++)
            quickSlots[q] = q < recentIndices.Count ? recentIndices[q] : -1;
    }

    private bool IsInventoryIndexAlreadyAssigned(int invIndex)
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == invIndex) return true;
        return false;
    }

    private int FindEmptyQuickSlot()
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == -1) return i;
        return -1;
    }

    public void AssignSlot(int quickSlotIndex, int inventorySlotIndex)
    {
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (inventory == null) return;
        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventory.SlotCount) return;

        quickSlots[quickSlotIndex] = inventorySlotIndex;
        RefreshSlot(quickSlotIndex);
    }

    public void RefreshAllSlots()
    {
        EnsureQuickSlots();
        for (int i = 0; i < quickSlots.Length; i++)
            RefreshSlot(i);
    }

    public void RefreshSlot(int quickSlotIndex)
    {
        EnsureQuickSlots();
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (inventory == null) return;
        if (slotUIs == null || quickSlotIndex >= slotUIs.Length) return;

        var qsUI = slotUIs[quickSlotIndex];
        if (qsUI == null) return;

        int invIndex = quickSlots[quickSlotIndex];

        if (invIndex < 0 || invIndex >= inventory.SlotCount)
        {
            qsUI.SetEmpty();
            quickSlots[quickSlotIndex] = -1;
            return;
        }

        var slot = inventory.GetSlot(invIndex);
        if (slot == null || slot.item == null || slot.count <= 0)
        {
            qsUI.SetEmpty();
            quickSlots[quickSlotIndex] = -1;
            return;
        }

        qsUI.SetItem(slot.item, slot.count);
    }

    public void UseSlot(int quickSlotIndex)
    {
        EnsureQuickSlots();
        if (!IsValidQuickIndex(quickSlotIndex)) return;
        if (inventory == null) return;

        int invIndex = quickSlots[quickSlotIndex];
        if (invIndex < 0 || invIndex >= inventory.SlotCount) return;

        var slot = inventory.GetSlot(invIndex);
        if (slot == null || slot.item == null || slot.count <= 0) return;

        // 에너지팩은 체력(PlayerStatus)이 아니라 한결의 총(Hankyul_Fire)이 처리한다.
        // 예전에는 여기서 그냥 소모해 버려 효과 없이 사라졌다(윤서는 총이 없어 쓸 수도 없다).
        if (HasEffect(slot.item, EffectType.EnergyPack))
        {
            // HUD 퀵슬롯은 플레이어 밖(HUD)에 붙어 있어 자기 주변에서 찾으면 총이 없다. 연결된 인벤토리 주인에서 찾는다.
            Hankyul_Fire fire = inventory != null ? inventory.transform.root.GetComponentInChildren<Hankyul_Fire>() : null;
            if (fire == null)
                fire = GetComponentInParent<Hankyul_Fire>() ?? transform.root.GetComponentInChildren<Hankyul_Fire>();
            if (fire == null)
                return;

            if (fire.UseEnergyPack(out _))
                inventory.ConsumeAt(invIndex, 1);
            return;
        }

        // 실제로 효과가 났을 때만 하나 줄인다. 예전엔 효과가 없어도(한결의 실드팩, 모듈 등) 사라졌다.
        if (!AetherItemUse.UseAt(inventory, invIndex, out string message) && !string.IsNullOrEmpty(message))
            AetherObjectiveToast.Show(message, 2.5f);
    }

    private static bool HasEffect(ItemData item, EffectType type)
    {
        if (item == null || item.effects == null)
            return false;

        for (int i = 0; i < item.effects.Length; i++)
        {
            if (item.effects[i].type == type)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 숫자 키를 받는 퀵슬롯. HUD 가 윤서용 퀵슬롯을 따로 만들고 윤서 프리팹에도 퀵슬롯이 있어서,
    /// 둘 다 숫자 키를 받아 한 번 누르면 아이템이 두 개씩 쓰였다. HUD 것이 켜져 있으면 그것만 받는다.
    /// </summary>
    public static QuickSlotManager KeyOwner;

    /// <summary>
    /// 숫자 키를 몇 칸 밀지. Meka 는 1번이 총 꺼내기(Hankyul_Battle)라 1번을 누르면 총과 퀵슬롯 1이 함께 반응했다.
    /// Meka 일 때 HUD 가 1 로 두어 퀵슬롯을 2~6번 키로 쓴다(칸에 적힌 번호도 같이 바뀐다).
    /// </summary>
    public static int KeyOffset;

    private float nextUiRefresh;

    private void Update()
    {
        if (KeyOwner != null && KeyOwner != this && KeyOwner.isActiveAndEnabled)
            return;

        // 장면이 바뀐 뒤 다른 옛 퀵슬롯 UI 가 칸을 비워 버려, 아이템이 있는데도 칸이 빈 채로 남았다
        // (05 에 들어서자 식량 칸이 비어 보임). 잠깐마다 칸을 다시 그린다(5칸이라 부담 없다).
        if (Time.unscaledTime >= nextUiRefresh)
        {
            nextUiRefresh = Time.unscaledTime + 0.25f;
            RefreshAllSlots();
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        var keys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key };
        for (int i = 0; i < 5; i++)
        {
            int k = i + Mathf.Clamp(KeyOffset, 0, 2);
            if (keys[k].wasPressedThisFrame) UseSlot(i);
        }
    }

    private void EnsureQuickSlots()
    {
        int safeCount = Mathf.Max(1, slotCount);
        if (quickSlots == null || quickSlots.Length != safeCount)
        {
            int[] previous = quickSlots;
            quickSlots = new int[safeCount];
            for (int i = 0; i < quickSlots.Length; i++)
                quickSlots[i] = previous != null && i < previous.Length ? previous[i] : -1;
        }

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (quickSlots[i] < -1)
                quickSlots[i] = -1;
        }
    }

    private bool IsValidQuickIndex(int i) => quickSlots != null && i >= 0 && i < quickSlots.Length;
}
