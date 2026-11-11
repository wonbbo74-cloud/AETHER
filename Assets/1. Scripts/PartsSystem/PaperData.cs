using UnityEngine;

[CreateAssetMenu(menuName = "AETHER/Parts/Paper Data", fileName = "Paper_")]
public class PaperData : ScriptableObject
{
    public string paperId;
    public string title;

    [TextArea(4, 12)]
    public string bodyText;

    [TextArea(2, 6)]
    public string hintText;

    public CraftingRecipeData relatedRecipe;
    public string[] requiredModuleIds;
    public PartData resultPart;

    public string GetResultPartName()
    {
        PartData part = resultPart != null
            ? resultPart
            : relatedRecipe != null ? relatedRecipe.resultPart : null;

        if (part == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(part.displayName))
            return part.displayName;

        if (!string.IsNullOrWhiteSpace(part.id))
            return part.id;

        return part.name;
    }
}
