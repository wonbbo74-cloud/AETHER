using UnityEngine;

/// <summary>
/// 제작설계서(종이) ↔ 제작법 연결.
///
/// 설계서가 재료로 들어가는 제작법(지금은 슈퍼 점프 파츠)은 "강화파츠"다. 이 제작법은 제작 화면이 정답을
/// 알려 주지 않고, 설계서에 그려진 배치대로 놓아야만 만들어진다. 설계서 그림도 같은 제작법 데이터로 그려서
/// 그림과 실제 판정이 어긋날 수 없게 한다.
/// </summary>
public static class AetherBlueprint
{
    /// <summary>제작 도면 슬롯 이름. 제작 화면 배경 그림에 적힌 것과 같다.</summary>
    public static readonly string[] SlotTitles = { "1. 동력 모듈", "2. 제어 모듈", "3. 보조 모듈", "4. 보조 슬롯", "5. 보조 슬롯" };

    /// <summary>설계서가 재료로 들어가는(=설계서대로 놓아야 하는) 제작법인지.</summary>
    public static bool RequiresBlueprint(CraftingRecipeData recipe)
    {
        return BlueprintId(recipe) != null;
    }

    /// <summary>제작법에 들어가는 설계서의 아이디. 없으면 null.</summary>
    public static string BlueprintId(CraftingRecipeData recipe)
    {
        if (recipe == null || recipe.expectedPositions == null)
            return null;

        foreach (CraftingRecipeData.ExpectedPosition position in recipe.expectedPositions)
            if (!string.IsNullOrEmpty(position.itemId) && ItemNameRuleUtility.IsPaperObject(position.itemId))
                return position.itemId;

        return null;
    }

    /// <summary>이 설계서 아이템이 그려 주는 제작법. 없으면 null.</summary>
    public static CraftingRecipeData FindRecipeFor(ItemData paper)
    {
        if (paper == null)
            return null;

        string paperId = ItemNameRuleUtility.GetBestItemId(paper);
        foreach (CraftingRecipeData recipe in Resources.LoadAll<CraftingRecipeData>("AETHERParts"))
        {
            string id = BlueprintId(recipe);
            if (id != null && ItemNameRuleUtility.NormalizeModuleAlias(id) == paperId)
                return recipe;
        }

        return null;
    }

    /// <summary>인벤토리에 이 제작법의 설계서가 있는지.</summary>
    public static bool Owns(PlayerInventory inventory, CraftingRecipeData recipe)
    {
        string id = BlueprintId(recipe);
        if (inventory == null || id == null)
            return false;

        string wanted = ItemNameRuleUtility.NormalizeModuleAlias(id);
        foreach (PlayerInventory.Slot slot in inventory.slots)
            if (slot != null && slot.item != null && slot.count > 0 && ItemNameRuleUtility.GetBestItemId(slot.item) == wanted)
                return true;

        return false;
    }
}
