using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownChargeTrackerTests
{
    private static readonly TimeSpan SameSourceDedupeWindow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CrossSourceDedupeWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MissingStatusGrace = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownChargeTracker keeps a remaining charge ready", KeepsRemainingChargeReady),
        ("PartyCooldownChargeTracker recovers spent charges sequentially", RecoversSpentChargesSequentially),
        ("PartyCooldownChargeTracker infers one use from a new active status", InfersUseFromActiveStatus),
        ("PartyCooldownChargeTracker merges out-of-order log and status signals", MergesOutOfOrderSignals),
        ("PartyCooldownChargeTracker merges status before a delayed log", MergesStatusBeforeDelayedLog),
        ("PartyCooldownChargeTracker tolerates a brief missing status", ToleratesBriefMissingStatus),
        ("PartyCooldownChargeTracker treats a long missing status as a new use", TreatsLongMissingStatusAsNewUse),
        ("PartyCooldownChargeTracker resets estimates when level changes max charges", ResetsWhenMaxChargesChange),
    ];

    private static void KeepsRemainingChargeReady()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        True(RecordLogUse(state, now, 60, 2), "first use should be tracked");
        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(10), 60, 2);

        Equal(1u, snapshot.CurrentCharges);
        Equal(2u, snapshot.MaxCharges);
        Near(50, snapshot.NextChargeRemaining);
    }

    private static void RecoversSpentChargesSequentially()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        RecordLogUse(state, now, 60, 2);
        RecordLogUse(state, now.AddSeconds(10), 60, 2);

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

        ObserveStatus(state, now, 5, 10, 60, 2);
        ObserveStatus(state, now.AddSeconds(1), 4, 10, 60, 2);
        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now, 60, 2);

        Equal(1u, snapshot.CurrentCharges);
        Near(55, snapshot.NextChargeRemaining);
    }

    private static void ResetsWhenMaxChargesChange()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();
        RecordLogUse(state, now, 60, 2);

        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(1), 60, 1);

        Equal(1u, snapshot.CurrentCharges);
        Equal(1u, snapshot.MaxCharges);
        Near(0, snapshot.NextChargeRemaining);
    }

    private static void MergesOutOfOrderSignals()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();
        True(RecordLogUse(state, now, 60, 2), "log use should be tracked");
        ObserveStatus(state, now.AddMilliseconds(900), 10, 10, 60, 2);

        var afterMergedStatus = PartyCooldownChargeTracker.GetSnapshot(state, now.AddMilliseconds(900), 60, 2);
        Equal(1u, afterMergedStatus.CurrentCharges);

        True(
            RecordLogUse(state, now.AddMilliseconds(1100), 60, 2),
            "a real second charge outside the duplicate window should be tracked");
        var afterSecondUse = PartyCooldownChargeTracker.GetSnapshot(state, now.AddMilliseconds(1100), 60, 2);
        Equal(0u, afterSecondUse.CurrentCharges);
    }

    private static void MergesStatusBeforeDelayedLog()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        ObserveStatus(state, now, 10, 10, 60, 2);
        True(!RecordLogUse(state, now.AddMilliseconds(900), 60, 2), "delayed log should merge with status inference");

        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddMilliseconds(900), 60, 2);
        Equal(1u, snapshot.CurrentCharges);
    }

    private static void ToleratesBriefMissingStatus()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        ObserveStatus(state, now, 10, 10, 60, 2);
        ObserveStatus(state, now.AddMilliseconds(700), 0, 10, 60, 2);
        ObserveStatus(state, now.AddSeconds(1), 9, 10, 60, 2);

        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(1), 60, 2);
        Equal(1u, snapshot.CurrentCharges);
    }

    private static void TreatsLongMissingStatusAsNewUse()
    {
        var now = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
        var state = new PartyCooldownRuntimeState();

        ObserveStatus(state, now, 10, 10, 60, 2);
        ObserveStatus(state, now.AddSeconds(2.1), 0, 10, 60, 2);
        ObserveStatus(state, now.AddSeconds(2.2), 10, 10, 60, 2);

        var snapshot = PartyCooldownChargeTracker.GetSnapshot(state, now.AddSeconds(2.2), 60, 2);
        Equal(0u, snapshot.CurrentCharges);
    }

    private static bool RecordLogUse(
        PartyCooldownRuntimeState state,
        DateTime usedAtUtc,
        float cooldown,
        uint maxCharges)
        => PartyCooldownChargeTracker.RecordUse(
            state,
            usedAtUtc,
            cooldown,
            maxCharges,
            PartyCooldownUseObservationSource.CombatLog,
            SameSourceDedupeWindow,
            CrossSourceDedupeWindow);

    private static void ObserveStatus(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float remaining,
        float duration,
        float cooldown,
        uint maxCharges)
        => PartyCooldownChargeTracker.ObserveActiveStatus(
            state,
            observedAtUtc,
            remaining,
            duration,
            cooldown,
            maxCharges,
            SameSourceDedupeWindow,
            CrossSourceDedupeWindow,
            MissingStatusGrace);
}
