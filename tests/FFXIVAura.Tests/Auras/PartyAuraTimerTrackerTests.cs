using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyAuraTimerTrackerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyAuraTimerTracker interpolates repeated coarse samples", InterpolatesRepeatedCoarseSamples),
        ("PartyAuraTimerTracker expires despite stale positive samples", ExpiresDespiteStalePositiveSamples),
        ("PartyAuraTimerTracker resets on a clear reapplication", ResetsOnClearReapplication),
        ("PartyAuraTimerTracker smooths earlier corrections", SmoothsEarlierCorrections),
    ];

    private static void InterpolatesRepeatedCoarseSamples()
    {
        var state = new PartyAuraTimerState();
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        Near(10f, PartyAuraTimerTracker.Update(state, now, 10f));
        Near(9.75f, PartyAuraTimerTracker.Update(state, now.AddSeconds(0.25), 10f));
    }

    private static void ExpiresDespiteStalePositiveSamples()
    {
        var state = new PartyAuraTimerState();
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        PartyAuraTimerTracker.Update(state, now, 10f);
        Near(0f, PartyAuraTimerTracker.Update(state, now.AddSeconds(10.1), 1f));
    }

    private static void ResetsOnClearReapplication()
    {
        var state = new PartyAuraTimerState();
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        PartyAuraTimerTracker.Update(state, now, 10f);
        PartyAuraTimerTracker.Update(state, now.AddSeconds(10.1), 1f);
        Near(20f, PartyAuraTimerTracker.Update(state, now.AddSeconds(10.2), 20f));
    }

    private static void SmoothsEarlierCorrections()
    {
        var state = new PartyAuraTimerState();
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        PartyAuraTimerTracker.Update(state, now, 10f);
        Near(8.75f, PartyAuraTimerTracker.Update(state, now.AddSeconds(1), 8f));
    }
}
