using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownSignalDiagnosticsTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Party cooldown signal diagnostics groups counters", GroupsCounters),
        ("Party cooldown signal diagnostics records timer decisions", RecordsTimerDecisions),
    ];

    private static void GroupsCounters()
    {
        var diagnostics = new PartyCooldownSignalDiagnostics();
        diagnostics.CountCandidateMissing();
        diagnostics.CountCandidateMissingSample();
        diagnostics.CountLocalPlayerLogSkipped();
        diagnostics.CountLocalOwnedObjectLogSkipped();
        diagnostics.CountStatusFallbackBatch();

        var snapshot = diagnostics.CreateSnapshot();
        Equal(1L, snapshot.CandidateMissingTotal);
        Equal(1L, snapshot.CandidateMissingSamples);
        Equal(1L, snapshot.LocalPlayerLogSkippedTotal);
        Equal(1L, snapshot.LocalOwnedObjectLogSkippedTotal);
        Equal(1L, snapshot.StatusFallbackBatchCount);

        diagnostics.ResetCandidateMissing();
        snapshot = diagnostics.CreateSnapshot();
        Equal(0L, snapshot.CandidateMissingTotal);
        Equal(0L, snapshot.CandidateMissingSamples);
        Equal(1L, snapshot.StatusFallbackBatchCount);
    }

    private static void RecordsTimerDecisions()
    {
        var diagnostics = new PartyCooldownSignalDiagnostics();
        var member = new PartyCooldownMemberSnapshot("member", 1, 2, 3, "Name", "N.", "PLD", 90, 4, string.Empty);
        var definition = new PartyCooldownDefinition { ActionId = 17 };

        diagnostics.RecordTimerDecision(
            member,
            definition,
            9.5f,
            true,
            new ObservedStatusTimerResult(8.5f, ObservedStatusTimerDecision.Refreshed));
        diagnostics.RecordTimerDecision(
            member,
            definition,
            1f,
            false,
            new ObservedStatusTimerResult(0f, ObservedStatusTimerDecision.StalePositiveSuppressed));

        var snapshot = diagnostics.CreateSnapshot();
        Equal(1L, snapshot.TimerRefreshAcceptedCount);
        Equal(1L, snapshot.TimerStalePositiveSuppressedCount);
        True(
            snapshot.TimerLastDecision.Contains("action=17", StringComparison.Ordinal),
            "last decision should include the action id");
        True(
            snapshot.TimerLastDecision.Contains("StalePositiveSuppressed", StringComparison.Ordinal),
            "last decision should include the timer decision");
    }
}
