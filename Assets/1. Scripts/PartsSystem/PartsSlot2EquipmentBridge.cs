using UnityEngine;

public class PartsSlot2EquipmentBridge : MonoBehaviour
{
    [SerializeField] private EquipmentUIController controller;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private YunseoEquipmentRuntimeBridge yunseoRuntimeBridge;

    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void ResolveReferences()
    {
        if (controller == null)
            controller = GetComponent<EquipmentUIController>();

        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);

        if (yunseoRuntimeBridge == null)
            yunseoRuntimeBridge = GetComponentInParent<YunseoEquipmentRuntimeBridge>();

        if (yunseoRuntimeBridge == null)
            yunseoRuntimeBridge = FindObjectOfType<YunseoEquipmentRuntimeBridge>(true);
    }

    private void Subscribe()
    {
        if (subscribed || controller == null)
            return;

        controller.OnEquipmentChanged += HandleEquipmentChanged;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && controller != null)
            controller.OnEquipmentChanged -= HandleEquipmentChanged;

        subscribed = false;
    }

    private void HandleEquipmentChanged(EquipmentUIController.EquipSlot slot, EquipmentItemData item)
    {
        PartSlotType slotType = ConvertSlot(slot);
        PartType partType = item != null ? item.abilityType : PartType.None;

        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>();

        if (equipmentManager != null)
            equipmentManager.SetExternalEquipped(slotType, partType);

        if (slot == EquipmentUIController.EquipSlot.LeftLeg || slot == EquipmentUIController.EquipSlot.RightLeg)
        {
            if (yunseoRuntimeBridge == null)
                yunseoRuntimeBridge = FindObjectOfType<YunseoEquipmentRuntimeBridge>();

            if (yunseoRuntimeBridge != null)
            {
                if (item != null && item.partType == EquipmentPartType.Leg)
                    yunseoRuntimeBridge.ApplyLegItem(item);
                else
                    yunseoRuntimeBridge.ClearLegItem();
            }
        }
    }

    private static PartSlotType ConvertSlot(EquipmentUIController.EquipSlot slot)
    {
        switch (slot)
        {
            case EquipmentUIController.EquipSlot.LeftLeg:
            case EquipmentUIController.EquipSlot.RightLeg:
                return PartSlotType.Leg;

            case EquipmentUIController.EquipSlot.LeftArm:
            case EquipmentUIController.EquipSlot.RightArm:
                return PartSlotType.Arm;

            default:
                return PartSlotType.Utility;
        }
    }
}
