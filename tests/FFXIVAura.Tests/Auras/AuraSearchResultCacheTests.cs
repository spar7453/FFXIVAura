using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraSearchResultCacheTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Aura search status sets ignore enumeration order", IgnoresEnumerationOrder),
        ("Aura search status sets detect membership changes", DetectsMembershipChanges),
        ("Aura search cache keys change with aura revisions", CacheKeysChangeWithAuraRevisions),
    ];

    private static void IgnoresEnumerationOrder()
    {
        True(
            AuraSearchStatusSets.HaveSameMembers(
                new HashSet<uint> { 10, 20, 30 },
                new uint[] { 30, 10, 20 }),
            "the same statuses in another order should reuse search results");
    }

    private static void DetectsMembershipChanges()
    {
        True(
            !AuraSearchStatusSets.HaveSameMembers(
                new HashSet<uint> { 10, 20, 30 },
                new uint[] { 10, 20, 40 }),
            "different active status IDs must invalidate search results");
    }

    private static void CacheKeysChangeWithAuraRevisions()
    {
        var first = new AuraSearchResultCacheKey("보호", IconWindowRole.PlayerBuffs, false, false, false, 1, 1, 1);
        var second = first with { AuraStateRevision = 2 };

        True(first != second, "a new aura state revision must invalidate cached search results");
    }
}
