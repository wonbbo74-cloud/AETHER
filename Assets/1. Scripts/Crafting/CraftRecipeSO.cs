using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "AETHER/Crafting/Recipe")]
public class CraftRecipeSO : ScriptableObject
{
    public List<CraftItemDataSO> requiredItems; // 정답 조합
    public CraftItemDataSO resultItem;          // 결과 파츠
}
