using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownActiveStatusIndexTests
{
    private static readonly DateTime TimestampUtc = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(100);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownActiveStatusIndex reuses a fresh roster cache", ReusesFreshRosterCache),
        ("PartyCooldownActiveStatusIndex refreshes after roster changes", RefreshesAfterRosterChanges),
        ("PartyCooldownActiveStatusIndex prefers live status samples", PrefersLiveStatusSamples),
        ("PartyCooldownActiveStatusIndex confirms absence only after complete live reads", ConfirmsAbsenceOnlyAfterCompleteLiveReads),
        ("PartyCooldownActiveStatusIndex resets all runtime state", ResetsAllRuntimeState),
        ("PartyCooldownActiveStatusIndex reports bounded diagnostics", ReportsBoundedDiagnostics),
    ];

    private static void ReusesFreshRosterCache()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        True(index.BeginRefresh(10, TimestampUtc), "the first snapshot should require a refresh");
        index.MarkLiveOwner(100);
        index.AddSample(
            100,
            200,
            10f,
            fromPartyList: true,
            onSourceMember: true,
            StatusSnapshotOrigin.Live);
        index.CompleteRefresh([100]);

        True(
            !index.BeginRefresh(10, TimestampUtc.AddMilliseconds(99)),
            "an unchanged roster should reuse a snapshot inside the cache window");
        True(
            !index.BeginRefresh(10, TimestampUtc.AddMilliseconds(-1)),
            "a wall-clock rollback should preserve the existing cached snapshot");
        var observation = index.GetObservation(100, [200], TimestampUtc.AddMilliseconds(50));
        Equal(ObservedStatusObservation.Present, observation.Observation);
        Near(9.95f, observation.Remaining);
        True(observation.CanConfirmRefresh, "the cached live sample should retain its provenance");

        True(
            index.BeginRefresh(10, TimestampUtc.AddMilliseconds(100)),
            "the cache should refresh at the expiration boundary");
        Equal(0, index.CreateDiagnostics().StatusCount);
    }

    private static void RefreshesAfterRosterChanges()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        True(index.BeginRefresh(10, TimestampUtc), "the first snapshot should require a refresh");
        index.AddSample(
            100,
            200,
            10f,
            fromPartyList: true,
            onSourceMember: true,
            StatusSnapshotOrigin.Live);

        True(
            index.BeginRefresh(11, TimestampUtc.AddMilliseconds(1)),
            "a roster identity change should bypass the cache window");
        Equal(0, index.CreateDiagnostics().StatusCount);
    }

    private static void PrefersLiveStatusSamples()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        index.BeginRefresh(10, TimestampUtc);
        index.AddSample(
            100,
            200,
            12f,
            fromPartyList: true,
            onSourceMember: true,
            StatusSnapshotOrigin.Fallback);
        index.AddSample(
            100,
            200,
            7f,
            fromPartyList: false,
            onSourceMember: false,
            StatusSnapshotOrigin.Live);
        index.MarkLiveOwner(100);
        index.CompleteRefresh([100]);

        var observation = index.GetObservation(100, [200], TimestampUtc);
        Equal(ObservedStatusObservation.Present, observation.Observation);
        Near(7f, observation.Remaining);
        True(observation.CanConfirmRefresh, "a live object sample should replace a longer fallback sample");
    }

    private static void ConfirmsAbsenceOnlyAfterCompleteLiveReads()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        index.BeginRefresh(10, TimestampUtc);
        index.MarkLiveOwner(100);
        index.CompleteRefresh([100, 101]);

        var incomplete = index.GetObservation(100, [200], TimestampUtc);
        Equal(ObservedStatusObservation.Unavailable, incomplete.Observation);

        index.BeginRefresh(11, TimestampUtc.AddMilliseconds(1));
        index.MarkLiveOwner(100);
        index.MarkLiveOwner(101);
        index.CompleteRefresh([100, 101]);

        var complete = index.GetObservation(100, [200], TimestampUtc.AddMilliseconds(1));
        Equal(ObservedStatusObservation.ConfirmedAbsent, complete.Observation);
    }

    private static void ResetsAllRuntimeState()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        index.BeginRefresh(10, TimestampUtc);
        index.MarkLiveOwner(100);
        index.AddSample(
            100,
            200,
            10f,
            fromPartyList: true,
            onSourceMember: true,
            StatusSnapshotOrigin.Live);
        index.CompleteRefresh([100]);

        index.Reset();

        var diagnostics = index.CreateDiagnostics();
        Equal(0, diagnostics.StatusCount);
        Equal(0, diagnostics.LiveOwnerCount);
        True(!diagnostics.AbsenceConfirmed, "reset should clear confirmed absence state");
        Equal(
            ObservedStatusObservation.Unavailable,
            index.GetObservation(100, [200], TimestampUtc).Observation);
        True(index.BeginRefresh(10, TimestampUtc), "reset should invalidate the previous cache identity");
    }

    private static void ReportsBoundedDiagnostics()
    {
        var index = new PartyCooldownActiveStatusIndex(CacheDuration);
        index.BeginRefresh(10, TimestampUtc);
        index.MarkLiveOwner(100);
        index.MarkLiveOwner(101);
        index.AddSample(100, 200, 10f, fromPartyList: true, onSourceMember: true, StatusSnapshotOrigin.Live);
        index.AddSample(100, 201, 10f, fromPartyList: true, onSourceMember: false, StatusSnapshotOrigin.Fallback);
        index.AddSample(100, 202, 10f, fromPartyList: false, onSourceMember: true, StatusSnapshotOrigin.Live);
        index.AddSample(100, 203, 10f, fromPartyList: false, onSourceMember: false, StatusSnapshotOrigin.Fallback);
        index.CompleteRefresh([100, 101]);

        var diagnostics = index.CreateDiagnostics();
        Equal(4, diagnostics.StatusCount);
        Equal(1, diagnostics.PartyListSourceCount);
        Equal(1, diagnostics.PartyListRecipientCount);
        Equal(1, diagnostics.ObjectSourceCount);
        Equal(1, diagnostics.ObjectRecipientCount);
        Equal(2, diagnostics.LiveStatusCount);
        Equal(2, diagnostics.FallbackStatusCount);
        Equal(2, diagnostics.LiveOwnerCount);
        True(diagnostics.AbsenceConfirmed, "all expected owners were read live");
    }
}
