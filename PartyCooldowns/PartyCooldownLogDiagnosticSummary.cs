namespace FFXIVAura;

internal readonly record struct PartyCooldownLogDiagnosticSummary(
    int TrackedCount,
    int IgnoredCount,
    int OtherResultCount,
    int MemberNotFoundCount,
    int OwnerNotFoundCount,
    int CandidateMissingCount,
    int NotUsableForJobCount,
    int NotTrackedByWindowCount,
    int AmbiguousCount,
    int OtherIgnoredCount)
{
    private const string TrackedResult = "추적";
    private const string IgnoredResult = "무시";
    private const int IgnoredReasonCount = (int)PartyCooldownIgnoredLogReason.LocalPlayerExcluded + 1;

    public static PartyCooldownLogDiagnosticSummary Create(
        IEnumerable<PartyCooldownLogObservation> observations,
        int candidateObservationCount)
    {
        ArgumentNullException.ThrowIfNull(observations);
        Span<int> ignoredReasonCounts = stackalloc int[IgnoredReasonCount];
        ignoredReasonCounts.Clear();
        var trackedCount = 0;
        var ignoredCount = 0;
        var otherResultCount = 0;

        foreach (var observation in observations)
        {
            if (string.Equals(observation.Result, TrackedResult, StringComparison.Ordinal))
            {
                trackedCount++;
                continue;
            }

            if (!string.Equals(observation.Result, IgnoredResult, StringComparison.Ordinal))
            {
                otherResultCount++;
                continue;
            }

            ignoredCount++;
            var reasonIndex = (int)observation.IgnoredReason;
            if ((uint)reasonIndex < (uint)ignoredReasonCounts.Length)
                ignoredReasonCounts[reasonIndex]++;
        }

        var memberNotFoundCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.MemberNotFound];
        var ownerNotFoundCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.OwnerNotFound];
        var candidateMissingCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.CandidateMissing];
        var notUsableForJobCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.NotUsableForJob];
        var notTrackedByWindowCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.NotTrackedByWindow];
        var ambiguousCount = ignoredReasonCounts[(int)PartyCooldownIgnoredLogReason.Ambiguous];
        var knownIgnoredCount = memberNotFoundCount
                                + ownerNotFoundCount
                                + candidateMissingCount
                                + notUsableForJobCount
                                + notTrackedByWindowCount
                                + ambiguousCount;

        return new PartyCooldownLogDiagnosticSummary(
            trackedCount,
            ignoredCount,
            otherResultCount,
            memberNotFoundCount,
            ownerNotFoundCount,
            candidateObservationCount + candidateMissingCount,
            notUsableForJobCount,
            notTrackedByWindowCount,
            ambiguousCount,
            ignoredCount - knownIgnoredCount);
    }
}
