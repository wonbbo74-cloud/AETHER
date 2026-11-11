using UnityEngine;

public class YunseoEquipmentRuntimeBridge : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Move_Yunseo moveYunseo;

    [Header("현재 장착 상태(디버그용)")]
    [SerializeField] private EquipmentItemData currentLegItem;

    private void Awake()
    {
        if (moveYunseo == null)
            moveYunseo = GetComponent<Move_Yunseo>();

        ApplyCurrentLegEffect();
    }

    public void ApplyLegItem(EquipmentItemData itemData)
    {
        currentLegItem = itemData;
        ApplyCurrentLegEffect();
    }

    public void ClearLegItem()
    {
        currentLegItem = null;
        ApplyCurrentLegEffect();
    }

    public EquipmentItemData GetCurrentLegItem()
    {
        return currentLegItem;
    }

    private void ApplyCurrentLegEffect()
    {
        if (moveYunseo == null)
            return;

        if (currentLegItem == null)
        {
            moveYunseo.ClearLegPartEffect();
            return;
        }

        if (currentLegItem.partType != EquipmentPartType.Leg)
        {
            moveYunseo.ClearLegPartEffect();
            return;
        }

        moveYunseo.SetLegPartEffect(currentLegItem);
    }
}