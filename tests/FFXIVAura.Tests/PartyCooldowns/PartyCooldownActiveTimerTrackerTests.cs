using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownActiveTimerTrackerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownActiveTimerTracker interpolates coarse observations", InterpolatesCoarseObservations),
        ("PartyCooldownActiveTimerTracker resets on reapplication", ResetsOnReapplication),
        ("PartyCooldownActiveTimerTracker resets on observed use", ResetsOnObservedUse),
        ("PartyCooldownActiveTimerTracker converges toward earlier corrections", ConvergesTowardEarlierCorrections),
        ("PartyCooldownActiveTimerTracker ignores mid-duration increases", IgnoresMidDurationIncreases),
        ("PartyCooldownActiveTimerTracker suppresses stale positives after expiry", SuppressesStalePositiveAfterExpiry),
        ("PartyCooldownActiveTimerTracker revives an expired estimate after observed use", RevivesExpiredEstimateAfterObservedUse),
        ("PartyCooldownActiveTimerTracker does not refresh from fallback samples", DoesNotRefreshFromFallbackSamples),
        ("PartyCooldownActiveTimerTracker preserves suppression across unavailable samples", PreservesSuppressionAcrossUnavailableSamples),
        ("PartyCooldownActiveTimerTracker resets after status removal", ResetsAfterStatusRemoval),
    ];

    private static void InterpolatesCoarseObservations()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        Near(10, PartyCooldownActiveTimerTracker.Update(state, start, 10, 10));
        Near(9.75, PartyCooldownActiveTimerTracker.Update(state, start.AddMilliseconds(250), 10, 10));
        Near(9.25, PartyCooldownActiveTimerTracker.Update(state, start.AddMilliseconds(750), 10, 10));
        Near(9, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(1), 9, 10));
    }

    private static void ResetsOnReapplication()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 10, 10);
        Near(8, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 8, 10));
        Near(10, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(3), 10, 10));
    }

    private static void ConvergesTowardEarlierCorrections()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 10, 20);
        Near(8.75, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(1), 7, 20));
        Near(7.5, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 6, 20));
    }

    private static void ResetsOnObservedUse()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 10, 20);
        Near(8, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 8, 20));
        state.LastObservedUseAtUtc = start.AddSeconds(2.5);
        Near(8, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(3), 8, 20));
    }

    private static void IgnoresMidDurationIncreases()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 10, 20);
        Near(9, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(1), 11, 20));
    }

    private static void SuppressesStalePositiveAfterExpiry()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 1, 10);
        Near(0, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 1, 10));
    }

    private static void RevivesExpiredEstimateAfterObservedUse()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 1, 10);
        Near(0, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 1, 10));
        state.LastObservedUseAtUtc = start.AddSeconds(2.5);
        Near(8, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(3), 8, 10));
    }

    private static void DoesNotRefreshFromFallbackSamples()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 1, 10);
        Near(
            0,
            PartyCooldownActiveTimerTracker.Update(
                state,
                start.AddSeconds(2),
                10,
                10,
                canConfirmRefresh: false));
    }

    private static void PreservesSuppressionAcrossUnavailableSamples()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 1, 10);
        Near(
            0,
            PartyCooldownActiveTimerTracker.UpdateDetailed(
                state,
                start.AddSeconds(2),
                0,
                10,
                ObservedStatusObservation.Unavailable,
                canConfirmRefresh: false).Remaining);
        Near(0, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2.1), 1, 10));
    }

    private static void ResetsAfterStatusRemoval()
    {
        var state = new PartyCooldownRuntimeState();
        var start = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        PartyCooldownActiveTimerTracker.Update(state, start, 10, 10);
        Near(0, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(1), 0, 10));
        Near(5, PartyCooldownActiveTimerTracker.Update(state, start.AddSeconds(2), 5, 10));
    }
}
