using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class PartsMaterialPickup : MonoBehaviour, IInteractable, IPickupInteractable
{
    public enum MaterialKind
    {
        NanoFiber,
        HydraulicActuator,
        MicroEnergyCore
    }

    [Header("Material")]
    [SerializeField] private MaterialKind materialKind;
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    private void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    private void Awake()
    {
        ResolveItemData();
    }

    public string GetDisplayName()
    {
        ResolveItemData();
        if (itemData != null && !string.IsNullOrEmpty(itemData.displayName))
            return itemData.displayName;

        return gameObject.name;
    }

    public Sprite GetIcon()
    {
        ResolveItemData();
        return itemData != null ? itemData.itemSprite : null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        ResolveItemData();

        if (interactor == null || interactor.Inventory == null || itemData == null)
            return;

        if (!interactor.Inventory.AddItem(itemData, Mathf.Max(1, amount)))
            return;

        AetherLevel.ReportPickup(this, itemData);
        Debug.Log($"{GetDisplayName()} {Mathf.Max(1, amount)}개를 획득했습니다.");
        Destroy(gameObject);
    }

    private void ResolveItemData()
    {
        if (itemData != null)
            return;

        itemData = PartResourceResolver.FindItemData(materialKind.ToString());
        if (itemData != null)
            return;

#if UNITY_EDITOR
        itemData = AssetDatabase.LoadAssetAtPath<ItemData>(GetDefaultAssetPath(materialKind));
#endif
    }

    private static string GetDefaultAssetPath(MaterialKind kind)
    {
        switch (kind)
        {
            case MaterialKind.HydraulicActuator:
                return "Assets/Resources/AETHERParts/HydraulicActuator.asset";
            case MaterialKind.MicroEnergyCore:
                return "Assets/Resources/AETHERParts/MicroEnergyCore.asset";
            default:
                return "Assets/Resources/AETHERParts/NanoFiber.asset";
        }
    }
}
