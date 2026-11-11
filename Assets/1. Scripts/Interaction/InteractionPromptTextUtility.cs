public static class InteractionPromptTextUtility
{
    public const string HoldPrefix = "[F키를 길게 누르기]";

    public static string FormatHoldPrompt(string context)
    {
        return FormatHoldPrompt(context, string.Empty);
    }

    public static string FormatHoldPrompt(string title, string actionText)
    {
        string context = ExtractContext(actionText);
        if (string.IsNullOrWhiteSpace(context))
            context = Clean(title);

        if (string.IsNullOrWhiteSpace(context) || LooksCorrupted(context))
            context = "상호작용";

        return HoldPrefix + " " + context;
    }

    public static bool LooksCorrupted(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains("�")
            || value.Contains("���")
            || Count(value, '?') >= 2;
    }

    private static string ExtractContext(string actionText)
    {
        string text = Clean(actionText);
        if (string.IsNullOrWhiteSpace(text) || LooksCorrupted(text))
            return string.Empty;

        if (text.StartsWith(HoldPrefix))
            return Clean(text.Substring(HoldPrefix.Length));

        int closeBracket = text.IndexOf(']');
        if (text.StartsWith("[") && closeBracket >= 0 && closeBracket < text.Length - 1)
            return Clean(text.Substring(closeBracket + 1));

        if (text.StartsWith("F키") || text.StartsWith("F 키") || text.StartsWith("f키") || text.StartsWith("f 키"))
            return string.Empty;

        return text;
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static int Count(string value, char target)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == target)
                count++;
        }

        return count;
    }
}
