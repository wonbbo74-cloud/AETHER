using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Data", fileName = "NewItemData")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string id;
    public string displayName;
    public Sprite itemSprite;

    [TextArea(2, 4)]
    public string description;

    [Header("아이템 효과 (0개 이상)")]
    public ItemEffect[] effects;

    public bool IsUsable
    {
        get
        {
            return effects != null && effects.Length > 0;
        }
    }
}