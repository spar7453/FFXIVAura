using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLogDiagnosticSummaryTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLogDiagnosticSummary counts results and ignored reasons", CountsResultsAndIgnoredReasons),
        ("PartyCooldownLogDiagnosticSummary handles an empty interval", HandlesEmptyInterval),
    ];

    private static void CountsResultsAndIgnoredReasons()
    {
        var observations = new[]
        {
            Observation("추적", PartyCooldownIgnoredLogReason.None),
            Observation("추적", PartyCooldownIgnoredLogReason.None),
            Observation("무시", PartyCooldownIgnoredLogReason.MemberNotFound),
            Observation("무시", PartyCooldownIgnoredLogReason.OwnerNotFound),
            Observation("무시", PartyCooldownIgnoredLogReason.CandidateMissing),
            Observation("무시", PartyCooldownIgnoredLogReason.NotUsableForJob),
            Observation("무시", PartyCooldownIgnoredLogReason.NotTrackedByWindow),
            Observation("무시", PartyCooldownIgnoredLogReason.Ambiguous),
            Observation("무시", PartyCooldownIgnoredLogReason.Other),
            Observation("무시", PartyCooldownIgnoredLogReason.None),
            Observation("무시", PartyCooldownIgnoredLogReason.LocalPlayerExcluded),
            Observation("알 수 없음", PartyCooldownIgnoredLogReason.None),
        };

        var summary = PartyCooldownLogDiagnosticSummary.Create(observations, candidateObservationCount: 3);

        Equal(2, summary.TrackedCount);
        Equal(9, summary.IgnoredCount);
        Equal(1, summary.OtherResultCount);
        Equal(1, summary.MemberNotFoundCount);
        Equal(1, summary.OwnerNotFoundCount);
        Equal(4, summary.CandidateMissingCount);
        Equal(1, summary.NotUsableForJobCount);
        Equal(1, summary.NotTrackedByWindowCount);
        Equal(1, summary.AmbiguousCount);
        Equal(3, summary.OtherIgnoredCount);
    }

    private static void HandlesEmptyInterval()
    {
        var summary = PartyCooldownLogDiagnosticSummary.Create([], candidateObservationCount: 2);

        Equal(0, summary.TrackedCount);
        Equal(0, summary.IgnoredCount);
        Equal(0, summary.OtherResultCount);
        Equal(2, summary.CandidateMissingCount);
        Equal(0, summary.OtherIgnoredCount);
    }

    private static PartyCooldownLogObservation Observation(
        string result,
        PartyCooldownIgnoredLogReason ignoredReason)
        => new(
            DateTime.UnixEpoch,
            1,
            "source",
            0,
            "member",
            1,
            "action",
            "match",
            0,
            result,
            ignoredReason,
            default,
            string.Empty);
}
