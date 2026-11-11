
using UnityEngine;

public enum CraftItemType
{
    Material,
    Blueprint,
    Module
}

[CreateAssetMenu(menuName = "AETHER/Crafting/Item Data", fileName = "ItemData_")]
public class CraftItemDataSO : ScriptableObject
{
    [Header("�⺻ ����")]
    public string itemId;
    public string itemName;
    [TextArea] public string description;

    [Header("�з�")]
    public CraftItemType itemType;

    [Header("UI - ������ (RawImage ����)")]
    public Texture2D iconTexture;

    [Header("UI - ������� (������ iconTexture ���)")]
    public Texture2D previewTexture;

    [Header("���� (����� �� ���)")]
    public int attack;
    public int defense;
    public int speed;

    [Header("AETHER Inventory Link")]
    public ItemData linkedInventoryItem;
    public PartData linkedPartData;

    /// <summary>
    /// �����信 ����� �ؽ�ó ��ȯ
    /// previewTexture�� ������ iconTexture ���
    /// </summary>
public Texture2D GetPreviewTexture()
    {
        if (previewTexture != null)
            return previewTexture;

        if (iconTexture != null)
            return iconTexture;

        if (linkedInventoryItem != null && linkedInventoryItem.itemSprite != null)
            return linkedInventoryItem.itemSprite.texture;

        if (linkedPartData != null && linkedPartData.itemSprite != null)
            return linkedPartData.itemSprite.texture;

        Debug.LogWarning($"[CraftItemDataSO] {name} : preview texture is missing.");
        return null;
    }

public string GetInventoryId()
    {
        if (linkedPartData != null)
            return ItemNameRuleUtility.GetBestItemId(linkedPartData);

        if (linkedInventoryItem != null)
            return ItemNameRuleUtility.GetBestItemId(linkedInventoryItem);

        if (!string.IsNullOrWhiteSpace(itemId))
            return ItemNameRuleUtility.NormalizeModuleAlias(itemId);

        return ItemNameRuleUtility.NormalizeModuleAlias(itemName);
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        // ���̵� �ڵ� ���� (����)
        if (string.IsNullOrEmpty(itemId))
            itemId = name;

        // ������ ������ ������ ��� ����
        if (previewTexture == null && iconTexture == null)
        {
            Debug.LogWarning(
                $"[CraftItemDataSO] {name} : iconTexture �Ǵ� previewTexture �� �ϳ��� �ʿ��մϴ�."
            );
        }
    }
#endif
}
