namespace FFXIVAura;

internal readonly record struct AuraSearchDisplayResult(
    uint StatusId,
    string Name,
    uint IconId,
    bool IsCurrent,
    bool WasRecentlySeen,
    bool FromAction,
    bool FromStatusSheet,
    DateTime SeenAtUtc,
    string SourceActionNames = "",
    int SameNameCount = 1,
    byte StatusCategory = 0)
{
    public AuraStatusGroupKey GroupKey => AuraStatusGroupKey.Create(this.Name, this.IconId, this.StatusCategory);

    public AuraSearchDisplayResult Merge(AuraSearchDisplayResult other)
    {
        var name = string.IsNullOrWhiteSpace(this.Name) ? other.Name : this.Name;
        var iconId = this.IconId == 0 ? other.IconId : this.IconId;
        return this with
        {
            Name = name,
            IconId = iconId,
            StatusCategory = this.StatusCategory == 0 ? other.StatusCategory : this.StatusCategory,
            IsCurrent = this.IsCurrent || other.IsCurrent,
            WasRecentlySeen = this.WasRecentlySeen || other.WasRecentlySeen,
            FromAction = this.FromAction || other.FromAction,
            FromStatusSheet = this.FromStatusSheet || other.FromStatusSheet,
            SeenAtUtc = this.SeenAtUtc >= other.SeenAtUtc ? this.SeenAtUtc : other.SeenAtUtc,
            SourceActionNames = MergeActionNames(this.SourceActionNames, other.SourceActionNames),
            SameNameCount = Math.Max(this.SameNameCount, other.SameNameCount),
        };
    }

    public AuraSearchDisplayResult WithSameNameCount(int count)
        => this with { SameNameCount = Math.Max(1, count) };

    private static string MergeActionNames(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second.Trim();

        if (string.IsNullOrWhiteSpace(second))
            return first.Trim();

        var names = first
            .Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        foreach (var name in second.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!names.Contains(name, StringComparer.CurrentCultureIgnoreCase))
                names.Add(name);
        }

        return string.Join(" / ", names);
    }
}

internal static class AuraSearchDisplayResults
{
    public static List<AuraSearchDisplayResult> MergeAndSort(
        IEnumerable<AuraSearchDisplayResult> candidates,
        string query,
        bool separateSameNameIds = false)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        var hasIdQuery = uint.TryParse(trimmedQuery, out var idQuery);
        var byId = new Dictionary<uint, AuraSearchDisplayResult>();
        foreach (var candidate in candidates)
        {
            if (candidate.StatusId == 0)
                continue;

            byId[candidate.StatusId] = byId.TryGetValue(candidate.StatusId, out var existing)
                ? existing.Merge(candidate)
                : candidate;
        }

        var sameNameCounts = new Dictionary<AuraStatusGroupKey, int>();
        foreach (var result in byId.Values)
        {
            if (!result.GroupKey.IsValid)
                continue;

            sameNameCounts.TryGetValue(result.GroupKey, out var count);
            sameNameCounts[result.GroupKey] = count + 1;
        }

        var results = new List<AuraSearchDisplayResult>(byId.Count);
        foreach (var result in byId.Values)
        {
            var sameNameCount = result.GroupKey.IsValid
                                && sameNameCounts.TryGetValue(result.GroupKey, out var count)
                ? count
                : 1;
            results.Add(result.WithSameNameCount(sameNameCount));
        }

        results.Sort((left, right) => Compare(left, right, trimmedQuery, hasIdQuery, idQuery));
        if (separateSameNameIds || hasIdQuery)
            return results;

        var groupedResults = new List<AuraSearchDisplayResult>(results.Count);
        var resultIndexByGroup = new Dictionary<AuraStatusGroupKey, int>();
        foreach (var result in results)
        {
            var key = result.GroupKey;
            if (!key.IsValid || !resultIndexByGroup.TryGetValue(key, out var existingIndex))
            {
                if (key.IsValid)
                    resultIndexByGroup[key] = groupedResults.Count;

                groupedResults.Add(result);
                continue;
            }

            groupedResults[existingIndex] = groupedResults[existingIndex]
                .Merge(result)
                .WithSameNameCount(result.SameNameCount);
        }

        groupedResults.Sort((left, right) => Compare(left, right, trimmedQuery, hasIdQuery, idQuery));
        return groupedResults;
    }

    private static int Compare(AuraSearchDisplayResult left, AuraSearchDisplayResult right, string query, bool hasIdQuery, uint idQuery)
    {
        return CompareTrueFirst(hasIdQuery && left.StatusId == idQuery, hasIdQuery && right.StatusId == idQuery)
               ?? CompareTrueFirst(query.Length > 0 && left.Name.Equals(query, StringComparison.CurrentCultureIgnoreCase), query.Length > 0 && right.Name.Equals(query, StringComparison.CurrentCultureIgnoreCase))
               ?? CompareTrueFirst(query.Length > 0 && left.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase), query.Length > 0 && right.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
               ?? CompareTrueFirst(left.IsCurrent, right.IsCurrent)
               ?? CompareTrueFirst(left.WasRecentlySeen, right.WasRecentlySeen)
               ?? CompareDateDescending(left.SeenAtUtc, right.SeenAtUtc)
               ?? CompareTrueFirst(left.FromAction, right.FromAction)
               ?? left.StatusId.CompareTo(right.StatusId);
    }

    private static int? CompareTrueFirst(bool left, bool right)
    {
        if (left == right)
            return null;

        return left ? -1 : 1;
    }

    private static int? CompareDateDescending(DateTime left, DateTime right)
    {
        var compare = right.CompareTo(left);
        return compare == 0 ? null : compare;
    }
}

internal static class AuraSearchDisplayResultFormatter
{
    public static string GetTagText(AuraSearchDisplayResult result)
    {
        var text = string.Empty;
        AppendTag(ref text, result.IsCurrent, "활성");
        AppendTag(ref text, result.WasRecentlySeen && !result.IsCurrent, "최근");
        AppendTag(ref text, result.FromAction, string.IsNullOrWhiteSpace(result.SourceActionNames) ? "스킬 부여" : $"스킬: {result.SourceActionNames}");
        AppendTag(ref text, result.FromStatusSheet && !result.FromAction, "상태 목록");
        AppendTag(ref text, result.SameNameCount > 1, $"동명 {result.SameNameCount}개");
        return text;
    }

    private static void AppendTag(ref string text, bool condition, string tag)
    {
        if (!condition)
            return;

        text = string.IsNullOrEmpty(text) ? tag : $"{text} · {tag}";
    }
}
