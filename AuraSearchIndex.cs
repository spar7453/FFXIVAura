namespace FFXIVAura;

internal readonly record struct AuraSearchResult(uint StatusId, string Name, uint IconId);

internal readonly record struct AuraSearchIndexEntry(
    uint StatusId,
    string Name,
    uint IconId,
    string PrimarySearchText,
    string SecondarySearchText)
{
    public AuraSearchResult Result => new(this.StatusId, this.Name, this.IconId);
}

internal static class AuraSearchIndex
{
    public static IEnumerable<AuraSearchResult> Search(IEnumerable<AuraSearchIndexEntry> entries, string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        foreach (var entry in entries)
        {
            if (normalizedQuery.Length == 0 || Matches(entry, normalizedQuery))
                yield return entry.Result;
        }
    }

    public static bool Matches(AuraSearchIndexEntry entry, string query)
    {
        return entry.PrimarySearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.SecondarySearchText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSearchableStatusName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return !trimmed.StartsWith("_", StringComparison.Ordinal)
               && !trimmed.StartsWith("rsv_", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("_rsv", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains('\uFF1D')
               && !trimmed.Contains('=')
               && !trimmed.Contains('\u25CB')
               && !trimmed.Contains('\u25CF')
               && !ContainsJapaneseKana(trimmed);
    }

    private static bool ContainsJapaneseKana(string text)
    {
        return text.Any(ch => (ch >= '\u3040' && ch <= '\u30ff') || (ch >= '\u31f0' && ch <= '\u31ff'));
    }
}
