using UnityEngine;

public static class ItemNameRuleUtility
{
    public static string NormalizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        return rawName
            .Replace("(Clone)", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    public static bool IsModuleItem(string name)
    {
        string normalized = NormalizeName(name);
        return normalized.Contains("_module") || IsKnownModuleAlias(normalized);
    }

    public static bool IsPaperObject(string name)
    {
        return NormalizeName(name).Contains("_paper");
    }

    public static bool IsModuleObject(GameObject obj, ItemData itemData)
    {
        return IsModuleItem(GetBestItemName(obj, itemData))
            || (obj != null && IsModuleItem(obj.name));
    }

    public static bool IsPaperObject(GameObject obj, ItemData itemData)
    {
        return IsPaperObject(GetBestItemName(obj, itemData))
            || (obj != null && IsPaperObject(obj.name));
    }

    public static string GetBestItemName(GameObject obj, ItemData itemData)
    {
        if (itemData != null)
        {
            if (!string.IsNullOrWhiteSpace(itemData.id))
                return itemData.id;

            if (!string.IsNullOrWhiteSpace(itemData.displayName))
                return itemData.displayName;
        }

        return obj != null ? obj.name : string.Empty;
    }

    public static string GetPickupDisplayName(GameObject obj, ItemData itemData)
    {
        string normalized = NormalizeName(GetBestItemName(obj, itemData));

        // 줍기 안내 이름: 아이템에 한글 이름이 있으면 그것, 없을 때만 기본 이름.
        string known = itemData != null && !string.IsNullOrWhiteSpace(itemData.displayName) ? itemData.displayName : null;

        if (normalized.Contains("cable"))
            return known ?? "케이블";

        if (normalized.Contains("gear"))
            return known ?? "기어";

        if (normalized.Contains("crank"))
            return known ?? "크랭크";

        if (normalized.Contains("battery"))
            return known ?? "배터리";

        // 설계서는 아이템 이름("강화파츠 제작설계서")이 있으면 그것을 보여 준다. 예전엔 늘 영어 "Paper" 였다.
        if (IsPaperObject(normalized))
            return known ?? "설계서";

        if (itemData != null && !string.IsNullOrWhiteSpace(itemData.displayName))
            return itemData.displayName;

        string rawName = obj != null ? obj.name.Replace("(Clone)", string.Empty).Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(rawName))
            return "Item";

        return rawName
            .Replace("_module", string.Empty)
            .Replace("_paper", string.Empty)
            .Replace("_", " ")
            .Trim();
    }

    public static string GetBestItemId(ItemData itemData)
    {
        if (itemData == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(itemData.id))
            return NormalizeModuleAlias(itemData.id);

        return NormalizeModuleAlias(itemData.displayName);
    }

    public static bool ItemMatchesId(ItemData itemData, string itemId)
    {
        return NormalizeModuleAlias(itemId) == GetBestItemId(itemData);
    }

    public static string NormalizeModuleAlias(string rawName)
    {
        string normalized = NormalizeName(rawName);
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;

        normalized = normalized
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("__", "_");

        if (normalized.Contains("battery"))
            return "batteryaa_module";

        if (normalized.Contains("cable"))
            return "cable_black_module";

        if (normalized.Contains("crank"))
            return "crank_module";

        if (normalized.Contains("smallkey") || normalized.Contains("small_key"))
            return "smallkey_module";

        if (normalized.Contains("bookpagewriting4") || normalized.Contains("recipe") || normalized.Contains("paper"))
            return normalized.Contains("shield") || normalized.Contains("breaker") || normalized.Contains("superjump")
                ? normalized
                : "bookpagewriting4_paper";

        if (normalized.Contains("jump_core"))
            return "jump_core_module";

        if (normalized.Contains("shield_core"))
            return "shield_core_module";

        if (normalized.Contains("breaker_core"))
            return "breaker_core_module";

        if (normalized.Contains("power_cell"))
            return "power_cell_module";

        if (normalized.Contains("stabilizer"))
            return "stabilizer_module";

        if (normalized.Contains("frame_core"))
            return "frame_core_module";

        return normalized;
    }

    private static bool IsKnownModuleAlias(string normalizedName)
    {
        string alias = NormalizeModuleAlias(normalizedName);
        return alias.EndsWith("_module") && alias != normalizedName;
    }
}
