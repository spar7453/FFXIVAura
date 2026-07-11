using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownAllianceGroupRetentionTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Alliance group retention keeps the last HUD group briefly", KeepsLastHudGroupBriefly),
        ("Alliance group retention prefers a new HUD group", PrefersNewHudGroup),
        ("Alliance group retention clears outside alliance content", ClearsOutsideAlliance),
        ("Alliance group retention expires stale values", ExpiresStaleValues),
    ];

    private static void KeepsLastHudGroupBriefly()
    {
        var state = new PartyCooldownAllianceGroupRetention();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var observed = state.Resolve(1, true, now, TimeSpan.FromSeconds(3));
        var retained = state.Resolve(-1, true, now.AddSeconds(1), TimeSpan.FromSeconds(3));

        Equal(1, observed.GroupIndex);
        True(!observed.UsedRetainedValue, "a live HUD value should not be marked retained");
        Equal(1, retained.GroupIndex);
        Equal(-1, retained.ObservedGroupIndex);
        True(retained.UsedRetainedValue, "a missing HUD value should use the recent group");
    }

    private static void PrefersNewHudGroup()
    {
        var state = new PartyCooldownAllianceGroupRetention();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = state.Resolve(1, true, now, TimeSpan.FromSeconds(3));

        var resolution = state.Resolve(2, true, now.AddSeconds(1), TimeSpan.FromSeconds(3));

        Equal(2, resolution.GroupIndex);
        True(!resolution.UsedRetainedValue, "a new live HUD value should replace retention");
    }

    private static void ClearsOutsideAlliance()
    {
        var state = new PartyCooldownAllianceGroupRetention();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = state.Resolve(1, true, now, TimeSpan.FromSeconds(3));
        _ = state.Resolve(-1, false, now.AddSeconds(1), TimeSpan.FromSeconds(3));

        var resolution = state.Resolve(-1, true, now.AddSeconds(2), TimeSpan.FromSeconds(3));

        Equal(-1, resolution.GroupIndex);
        True(!resolution.UsedRetainedValue, "a previous duty group must not leak into a new alliance");
    }

    private static void ExpiresStaleValues()
    {
        var state = new PartyCooldownAllianceGroupRetention();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = state.Resolve(1, true, now, TimeSpan.FromSeconds(3));

        var resolution = state.Resolve(-1, true, now.AddSeconds(3), TimeSpan.FromSeconds(3));

        Equal(-1, resolution.GroupIndex);
        True(!resolution.UsedRetainedValue, "the retained HUD group should expire at the deadline");
    }
}
