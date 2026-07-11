namespace FFXIVAura;

internal readonly record struct AuraSearchResultCacheKey(
    string Query,
    IconWindowRole Role,
    bool ActiveOnly,
    bool ShowIndividualIds,
    bool OwnOnly,
    long AuraStateRevision,
    int StatusIdentityGeneration,
    int ActionIndexGeneration);

internal sealed record AuraSearchResultCacheEntry(
    AuraSearchResultCacheKey Key,
    IReadOnlyList<AuraSearchDisplayResult> Results);

internal static class AuraSearchStatusSets
{
    public static bool HaveSameMembers(
        IReadOnlySet<uint> previousStatusIds,
        IReadOnlyCollection<uint> currentStatusIds)
    {
        ArgumentNullException.ThrowIfNull(previousStatusIds);
        ArgumentNullException.ThrowIfNull(currentStatusIds);
        return previousStatusIds.Count == currentStatusIds.Count
               && currentStatusIds.All(previousStatusIds.Contains);
    }
}
