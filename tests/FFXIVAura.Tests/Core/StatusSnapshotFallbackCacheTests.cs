using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class StatusSnapshotFallbackCacheTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("StatusSnapshotFallbackCache retains recent status snapshots", RetainsRecentSnapshots),
        ("StatusSnapshotFallbackCache expires stale snapshots", ExpiresStaleSnapshots),
        ("StatusSnapshotFallbackCache clears stale data after an empty live read", ClearsStaleDataAfterEmptyRead),
        ("StatusSnapshotFallbackCache enforces its entry limit", EnforcesEntryLimit),
    ];

    private static void RetainsRecentSnapshots()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var cache = new StatusSnapshotFallbackCache();
        var output = new List<StatusSnapshot>();
        cache.Remember("party", 10, [new StatusSnapshot(100, 20, 1, 10f)], now);

        True(
            cache.TryCopyRecentTo("party", 10, now.AddMilliseconds(250), TimeSpan.FromMilliseconds(500), output),
            "recent cached snapshots should be available");
        Equal(1, output.Count);
        Near(9.75, output[0].RemainingTime);
    }

    private static void ExpiresStaleSnapshots()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var cache = new StatusSnapshotFallbackCache();
        var output = new List<StatusSnapshot>();
        cache.Remember("party", 10, [new StatusSnapshot(100, 20, 1, 10f)], now);

        True(
            !cache.TryCopyRecentTo("party", 10, now.AddSeconds(1), TimeSpan.FromMilliseconds(500), output),
            "expired cached snapshots should not be returned");
        Equal(0, output.Count);
    }

    private static void ClearsStaleDataAfterEmptyRead()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var cache = new StatusSnapshotFallbackCache();
        var output = new List<StatusSnapshot>();
        cache.Remember("party", 10, [new StatusSnapshot(100, 20, 1, 10f)], now);
        cache.Remember("party", 10, [], now.AddMilliseconds(100));

        True(
            !cache.TryCopyRecentTo("party", 10, now.AddMilliseconds(200), TimeSpan.FromMilliseconds(500), output),
            "an empty successful read should invalidate older cached statuses");
        Equal(0, cache.Count);
    }

    private static void EnforcesEntryLimit()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var cache = new StatusSnapshotFallbackCache();
        for (uint ownerEntityId = 1; ownerEntityId <= 129; ownerEntityId++)
        {
            cache.Remember(
                "party",
                ownerEntityId,
                [new StatusSnapshot(100, ownerEntityId, 1, 10f)],
                now.AddMilliseconds(ownerEntityId));
        }

        Equal(128, cache.Count);
        var output = new List<StatusSnapshot>();
        True(
            !cache.TryCopyRecentTo("party", 1, now.AddMilliseconds(200), TimeSpan.FromSeconds(1), output),
            "the oldest cache entry should be evicted first");
    }
}
