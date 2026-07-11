using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class StatusSnapshotFallbackCacheTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("StatusSnapshotFallbackCache retains recent status snapshots", RetainsRecentSnapshots),
        ("StatusSnapshotFallbackCache expires stale snapshots", ExpiresStaleSnapshots),
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
}
