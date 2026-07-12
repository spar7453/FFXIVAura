using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownRosterReadPolicyTests
{
    private static readonly DateTime TimestampUtc = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(100);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownRosterCachePolicy accepts only fresh forward timestamps", AcceptsOnlyFreshForwardTimestamps),
        ("PartyCooldownRosterReadClassifier classifies every read mode", ClassifiesEveryReadMode),
        ("PartyCooldownRosterReadClassifier selects the strongest source", SelectsStrongestSource),
    ];

    private static void AcceptsOnlyFreshForwardTimestamps()
    {
        True(PartyCooldownRosterCachePolicy.CanReuse(false, TimestampUtc, TimestampUtc, CacheDuration), "the build timestamp should be reusable");
        True(PartyCooldownRosterCachePolicy.CanReuse(false, TimestampUtc, TimestampUtc.AddMilliseconds(99), CacheDuration), "a fresh timestamp should be reusable");
        True(!PartyCooldownRosterCachePolicy.CanReuse(false, TimestampUtc, TimestampUtc.AddMilliseconds(100), CacheDuration), "the cache boundary should expire");
        True(!PartyCooldownRosterCachePolicy.CanReuse(false, TimestampUtc, TimestampUtc.AddMilliseconds(-1), CacheDuration), "a backward timestamp should expire");
        True(!PartyCooldownRosterCachePolicy.CanReuse(true, TimestampUtc, TimestampUtc, CacheDuration), "forced refresh should bypass the cache");
    }

    private static void ClassifiesEveryReadMode()
    {
        Equal(PartyCooldownRosterReadMode.SoloFallback, PartyCooldownRosterReadClassifier.ResolveReadMode(default, usedSoloFallback: true));
        Equal(PartyCooldownRosterReadMode.CrossRealmAlliance, PartyCooldownRosterReadClassifier.ResolveReadMode(new PartyCooldownRosterReadCounts(0, 24, 0, false), usedSoloFallback: false));
        Equal(PartyCooldownRosterReadMode.CrossRealmAllianceWithFlatFallback, PartyCooldownRosterReadClassifier.ResolveReadMode(new PartyCooldownRosterReadCounts(8, 16, 8, true), usedSoloFallback: false));
        Equal(PartyCooldownRosterReadMode.FlatAllianceFallback, PartyCooldownRosterReadClassifier.ResolveReadMode(new PartyCooldownRosterReadCounts(8, 0, 16, true), usedSoloFallback: false));
        Equal(PartyCooldownRosterReadMode.PartySlots, PartyCooldownRosterReadClassifier.ResolveReadMode(new PartyCooldownRosterReadCounts(8, 0, 0, false), usedSoloFallback: false));
        Equal(PartyCooldownRosterReadMode.Unknown, PartyCooldownRosterReadClassifier.ResolveReadMode(default, usedSoloFallback: false));
    }

    private static void SelectsStrongestSource()
    {
        Equal(PartyCooldownRosterSource.Alliance, PartyCooldownRosterReadClassifier.ResolveSource(true, 8, new PartyCooldownRosterReadCounts(0, 24, 0, false)));
        Equal(PartyCooldownRosterSource.Alliance, PartyCooldownRosterReadClassifier.ResolveSource(true, 8, new PartyCooldownRosterReadCounts(8, 0, 0, true)));
        Equal(PartyCooldownRosterSource.PartyList, PartyCooldownRosterReadClassifier.ResolveSource(true, 8, default));
        Equal(PartyCooldownRosterSource.PartyList, PartyCooldownRosterReadClassifier.ResolveSource(false, 4, new PartyCooldownRosterReadCounts(4, 0, 0, false)));
        Equal(PartyCooldownRosterSource.SoloFallback, PartyCooldownRosterReadClassifier.ResolveSource(false, 0, default));
    }
}
