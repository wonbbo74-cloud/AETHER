using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "AETHER/Parts/Crafting Recipe", fileName = "Recipe_")]
public class CraftingRecipeData : ScriptableObject
{
    [Serializable]
    public struct ExpectedPosition
    {
        public string itemId;
        public Vector2Int position;
    }

    public string recipeId;
    [FormerlySerializedAs("targetPart")]
    public PartData resultPart;
    public string[] requiredModuleIds;
    public Vector2Int gridSize = new Vector2Int(5, 5);
    public ExpectedPosition[] expectedPositions;

    [TextArea(2, 6)]
    public string recipeHintText;

    public PartData targetPart
    {
        get { return resultPart; }
        set { resultPart = value; }
    }
}
