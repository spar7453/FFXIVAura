using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownChargeTrackerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownChargeTracker keeps a remaining charge ready", KeepsRemainingChargeReady),
        ("PartyCooldownChargeTracker recovers spent charges sequentially", RecoversSpentChargesSequentially),
        ("PartyCooldownChargeTracker infers one use from a new active status", InfersUseFromActiveStatus),
        ("PartyCooldownChargeTracker resets estimates when level changes max charges", ResetsWhenMaxChargesChange),
    ];

    private static void KeepsRemainingChargeReady()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        True(PartyCooldownChargeTracker.RecordUse(state, now, 60, 2, TimeSpan.FromSeconds(1)), "first use should be tracked");
        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(10), 60, 2);

        Equal(1u, snapshot.CurrentCharges);
        Equal(2u, snapshot.MaxCharges);
        Near(50, snapshot.NextChargeRemaining);
    }

    private static void RecoversSpentChargesSequentially()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        PartyCooldownChargeTracker.RecordUse(state, now, 60, 2, TimeSpan.Zero);
        PartyCooldownChargeTracker.RecordUse(state, now.AddSeconds(10), 60, 2, TimeSpan.Zero);

        var empty = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(20), 60, 2);
        Equal(0u, empty.CurrentCharges);
        Near(40, empty.NextChargeRemaining);

        var oneRecovered = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(70), 60, 2);
        Equal(1u, oneRecovered.CurrentCharges);
        Near(50, oneRecovered.NextChargeRemaining);

        var allRecovered = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(121), 60, 2);
        Equal(2u, allRecovered.CurrentCharges);
        Near(0, allRecovered.NextChargeRemaining);
    }

    private static void InfersUseFromActiveStatus()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 10, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        PartyCooldownChargeTracker.ObserveActiveStatus(state, now, 5, 10, 60, 2, TimeSpan.FromSeconds(1));
        PartyCooldownChargeTracker.ObserveActiveStatus(state, now.AddSeconds(1), 4, 10, 60, 2, TimeSpan.FromSeconds(1));
        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now, 60, 2);

        Equal(1u, snapshot.CurrentCharges);
        Near(55, snapshot.NextChargeRemaining);
    }

    private static void ResetsWhenMaxChargesChange()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();
        PartyCooldownChargeTracker.RecordUse(state, now, 60, 2, TimeSpan.Zero);

        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(1), 60, 1);

        Equal(1u, snapshot.CurrentCharges);
        Equal(1u, snapshot.MaxCharges);
        Near(0, snapshot.NextChargeRemaining);
    }
}
