namespace FFXIVAura;

internal static class KeybindTextFormatter
{
    public static string Format(string? text, out bool hasUnknownGlyph)
    {
        hasUnknownGlyph = false;
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var formatted = text.Trim()
            .Trim('[', ']')
            .Replace("\u00a7", "s", StringComparison.Ordinal)
            .Replace("\u00a2", "c", StringComparison.Ordinal)
            .Replace("\u00aa", "a", StringComparison.Ordinal)
            .Replace("\u00ba", "n", StringComparison.Ordinal)
            .Replace("Shift+", "s", StringComparison.OrdinalIgnoreCase)
            .Replace("Shift-", "s", StringComparison.OrdinalIgnoreCase)
            .Replace("Ctrl+", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Ctrl-", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Control+", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Control-", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt+", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt-", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("Num", "n", StringComparison.OrdinalIgnoreCase)
            .Replace("+", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        hasUnknownGlyph = HasUnknownGlyph(formatted);
        return RemoveUnsupportedGlyphs(formatted);
    }

    private static bool HasUnknownGlyph(string text)
    {
        return text.Any(ch => ch == '?' || ch == '\ufffd' || !IsSupportedChar(ch));
    }

    private static string RemoveUnsupportedGlyphs(string text)
    {
        return new string(text.Where(IsSupportedChar).ToArray());
    }

    private static bool IsSupportedChar(char ch)
    {
        return ch is >= 'a' and <= 'z' or >= '0' and <= '9';
    }
}
