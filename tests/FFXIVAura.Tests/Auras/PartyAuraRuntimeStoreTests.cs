using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyAuraRuntimeStoreTests
{
    private static readonly DateTime TimestampUtc = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly PartyAuraTimerKey TimerKey = new(100, 200, 300);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyAuraRuntimeStore interpolates observed status timers", InterpolatesObservedStatusTimers),
        ("PartyAuraRuntimeStore includes an untracked zero-duration status", IncludesUntrackedZeroDurationStatus),
        ("PartyAuraRuntimeStore removes missing statuses after a complete live read", RemovesMissingStatusesAfterCompleteLiveRead),
        ("PartyAuraRuntimeStore preserves timers across unavailable roster reads", PreservesTimersAcrossUnavailableRosterReads),
        ("PartyAuraRuntimeStore records refresh and suppression diagnostics", RecordsRefreshAndSuppressionDiagnostics),
        ("PartyAuraRuntimeStore resets runtime state without losing cumulative diagnostics", ResetsRuntimeWithoutLosingDiagnostics),
    ];

    private static void InterpolatesObservedStatusTimers()
    {
        var store = new PartyAuraRuntimeStore();
        BeginLiveScan(store, TimerKey.OwnerEntityId);
        var first = store.ObserveStatus(TimerKey, TimestampUtc, 10f, StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        BeginLiveScan(store, TimerKey.OwnerEntityId);
        var second = store.ObserveStatus(
            TimerKey,
            TimestampUtc.AddSeconds(0.25),
            10f,
            StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc.AddSeconds(0.25), rosterReadComplete: true);

        True(first.Include, "a newly observed timer should be included");
        Near(10f, first.Remaining);
        True(second.Include, "a repeated timer should remain included");
        Near(9.75f, second.Remaining);
        Equal(1, store.CreateDiagnostics().TimerStateCount);
    }

    private static void IncludesUntrackedZeroDurationStatus()
    {
        var store = new PartyAuraRuntimeStore();
        BeginLiveScan(store, TimerKey.OwnerEntityId);
        var observation = store.ObserveStatus(TimerKey, TimestampUtc, 0f, StatusSnapshotOrigin.Live);
        var nonFinite = store.ObserveStatus(
            TimerKey with { StatusId = 201 },
            TimestampUtc,
            float.PositiveInfinity,
            StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        True(observation.Include, "a first zero-duration status should preserve the original aggregation behavior");
        Near(0f, observation.Remaining);
        True(nonFinite.Include, "a first non-finite positive sample should preserve the original aggregation behavior");
        Near(0f, nonFinite.Remaining);
        Equal(1, store.CreateDiagnostics().TimerStateCount);
    }

    private static void RemovesMissingStatusesAfterCompleteLiveRead()
    {
        var store = new PartyAuraRuntimeStore();
        BeginLiveScan(store, TimerKey.OwnerEntityId);
        store.ObserveStatus(TimerKey, TimestampUtc, 10f, StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        BeginLiveScan(store, TimerKey.OwnerEntityId);
        store.CompleteScan(TimestampUtc.AddSeconds(1), rosterReadComplete: true);

        Equal(0, store.CreateDiagnostics().TimerStateCount);
    }

    private static void PreservesTimersAcrossUnavailableRosterReads()
    {
        var store = new PartyAuraRuntimeStore();
        BeginLiveScan(store, TimerKey.OwnerEntityId);
        store.ObserveStatus(TimerKey, TimestampUtc, 10f, StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        store.BeginScan();
        store.MarkMemberOwner(TimerKey.OwnerEntityId);
        store.CompleteScan(TimestampUtc.AddSeconds(1), rosterReadComplete: true);
        Equal(1, store.CreateDiagnostics().TimerStateCount);

        store.BeginScan();
        store.CompleteScan(TimestampUtc.AddSeconds(2), rosterReadComplete: false);
        Equal(1, store.CreateDiagnostics().TimerStateCount);

        store.BeginScan();
        store.CompleteScan(TimestampUtc.AddSeconds(3), rosterReadComplete: true);
        Equal(0, store.CreateDiagnostics().TimerStateCount);
    }

    private static void RecordsRefreshAndSuppressionDiagnostics()
    {
        var store = new PartyAuraRuntimeStore();
        var expiringKey = TimerKey;
        var refreshedKey = new PartyAuraTimerKey(100, 201, 300);

        BeginLiveScan(store, 100);
        store.ObserveStatus(expiringKey, TimestampUtc, 2f, StatusSnapshotOrigin.Live);
        store.ObserveStatus(refreshedKey, TimestampUtc, 5f, StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        BeginLiveScan(store, 100);
        store.ObserveStatus(expiringKey, TimestampUtc.AddSeconds(1), 1f, StatusSnapshotOrigin.Live);
        store.ObserveStatus(refreshedKey, TimestampUtc.AddSeconds(1), 10f, StatusSnapshotOrigin.Live);
        store.CompleteScan(TimestampUtc.AddSeconds(1), rosterReadComplete: true);

        store.BeginScan();
        store.MarkMemberOwner(100);
        store.RecordStatusBatch(100, StatusSnapshotOrigin.Fallback);
        var suppressed = store.ObserveStatus(
            expiringKey,
            TimestampUtc.AddSeconds(2.1),
            10f,
            StatusSnapshotOrigin.Fallback);
        store.CompleteScan(TimestampUtc.AddSeconds(2.1), rosterReadComplete: true);

        var diagnostics = store.CreateDiagnostics();
        True(!suppressed.Include, "an expired timer should suppress a stale fallback sample");
        Equal(1L, diagnostics.FallbackBatchCount);
        Equal(1L, diagnostics.RefreshAcceptedCount);
        Equal(1L, diagnostics.ExpiredStatusSuppressedCount);
        True(
            diagnostics.LastDecision.Contains("StalePositiveSuppressed", StringComparison.Ordinal),
            "the last decision should describe the suppressed stale sample");
    }

    private static void ResetsRuntimeWithoutLosingDiagnostics()
    {
        var store = new PartyAuraRuntimeStore();
        store.BeginScan();
        store.MarkMemberOwner(100);
        store.RecordStatusBatch(100, StatusSnapshotOrigin.Fallback);
        store.ObserveStatus(TimerKey, TimestampUtc, 10f, StatusSnapshotOrigin.Fallback);
        store.CompleteScan(TimestampUtc, rosterReadComplete: true);

        store.ResetRuntimeState();

        var diagnostics = store.CreateDiagnostics();
        Equal(0, diagnostics.TimerStateCount);
        Equal(1L, diagnostics.FallbackBatchCount);
    }

    private static void BeginLiveScan(PartyAuraRuntimeStore store, uint ownerEntityId)
    {
        store.BeginScan();
        store.MarkMemberOwner(ownerEntityId);
        store.RecordStatusBatch(ownerEntityId, StatusSnapshotOrigin.Live);
    }
}
