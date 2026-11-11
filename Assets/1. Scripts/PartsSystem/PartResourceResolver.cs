using UnityEngine;

public static class PartResourceResolver
{
    private const string ResourceRoot = "AETHERParts";

    public static PaperData FindPaperData(string key)
    {
        string normalizedKey = ItemNameRuleUtility.NormalizeName(key);
        PaperData[] papers = Resources.LoadAll<PaperData>(ResourceRoot);
        for (int i = 0; i < papers.Length; i++)
        {
            PaperData paper = papers[i];
            if (paper == null)
                continue;

            if (ItemNameRuleUtility.NormalizeName(paper.paperId) == normalizedKey)
                return paper;

            if (ItemNameRuleUtility.NormalizeName(paper.name) == normalizedKey)
                return paper;
        }

        return null;
    }

    public static CraftingRecipeData FindRecipe(string key)
    {
        string normalizedKey = ItemNameRuleUtility.NormalizeName(key);
        CraftingRecipeData[] recipes = Resources.LoadAll<CraftingRecipeData>(ResourceRoot);

        if (string.IsNullOrEmpty(normalizedKey))
        {
            for (int i = 0; i < recipes.Length; i++)
            {
                CraftingRecipeData recipe = recipes[i];
                if (recipe == null)
                    continue;

                if (ItemNameRuleUtility.NormalizeName(recipe.recipeId) == "superjump_paper"
                    || ItemNameRuleUtility.NormalizeName(recipe.name) == "recipe_superjump_paper")
                    return recipe;
            }
        }

        for (int i = 0; i < recipes.Length; i++)
        {
            CraftingRecipeData recipe = recipes[i];
            if (recipe == null)
                continue;

            if (ItemNameRuleUtility.NormalizeName(recipe.recipeId) == normalizedKey)
                return recipe;

            if (ItemNameRuleUtility.NormalizeName(recipe.name) == normalizedKey)
                return recipe;
        }

        return recipes.Length > 0 ? recipes[0] : null;
    }

    public static PartData FindPart(PartType type)
    {
        PartData[] parts = Resources.LoadAll<PartData>(ResourceRoot);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null && parts[i].partType == type)
                return parts[i];
        }

        return null;
    }

    public static ItemData FindItemData(string key)
    {
        string normalizedKey = ItemNameRuleUtility.NormalizeName(key);
        ItemData[] items = Resources.LoadAll<ItemData>(ResourceRoot);
        ItemData fallbackPaper = null;

        ItemData exact = FindItemDataByNormalizedKey(items, normalizedKey, ref fallbackPaper);
        if (exact != null)
            return exact;

        string alias = ItemNameRuleUtility.NormalizeModuleAlias(normalizedKey);
        if (!string.IsNullOrEmpty(alias) && alias != normalizedKey)
        {
            ItemData aliasMatch = FindItemDataByNormalizedKey(items, alias, ref fallbackPaper);
            if (aliasMatch != null)
                return aliasMatch;
        }

        if (!string.IsNullOrEmpty(alias) && !alias.EndsWith("_module"))
        {
            ItemData moduleMatch = FindItemDataByNormalizedKey(items, alias + "_module", ref fallbackPaper);
            if (moduleMatch != null)
                return moduleMatch;
        }

        if (ItemNameRuleUtility.IsPaperObject(normalizedKey) && fallbackPaper != null)
            return fallbackPaper;

        return null;
    }

    public static ItemData[] GetAllCraftingItems()
    {
        return Resources.LoadAll<ItemData>(ResourceRoot);
    }

    private static ItemData FindItemDataByNormalizedKey(ItemData[] items, string normalizedKey, ref ItemData fallbackPaper)
    {
        if (items == null)
            return null;

        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;

            if (fallbackPaper == null
                && (ItemNameRuleUtility.IsPaperObject(ItemNameRuleUtility.GetBestItemId(item))
                    || ItemNameRuleUtility.IsPaperObject(item.name)))
                fallbackPaper = item;

            if (ItemNameRuleUtility.GetBestItemId(item) == normalizedKey)
                return item;

            if (ItemNameRuleUtility.NormalizeName(item.name) == normalizedKey)
                return item;
        }

        return null;
    }
}
