namespace FFXIVAura;

internal readonly record struct PartyCooldownSignalDiagnosticSnapshot(
    long CandidateMissingTotal,
    long CandidateMissingSamples,
    long LocalPlayerLogSkippedTotal,
    long LocalOwnedObjectLogSkippedTotal,
    long StatusFallbackBatchCount,
    long TimerRefreshAcceptedCount,
    long TimerStalePositiveSuppressedCount,
    string TimerLastDecision);

internal sealed class PartyCooldownSignalDiagnostics
{
    private long candidateMissingTotal;
    private long candidateMissingSamples;
    private long localPlayerLogSkippedTotal;
    private long localOwnedObjectLogSkippedTotal;
    private long statusFallbackBatchCount;
    private long timerRefreshAcceptedCount;
    private long timerStalePositiveSuppressedCount;
    private string timerLastDecision = string.Empty;

    public void CountCandidateMissing()
        => this.candidateMissingTotal++;

    public void CountCandidateMissingSample()
        => this.candidateMissingSamples++;

    public void CountLocalPlayerLogSkipped()
        => this.localPlayerLogSkippedTotal++;

    public void CountLocalOwnedObjectLogSkipped()
        => this.localOwnedObjectLogSkippedTotal++;

    public void CountStatusFallbackBatch()
        => this.statusFallbackBatchCount++;

    public void ResetCandidateMissing()
    {
        this.candidateMissingTotal = 0;
        this.candidateMissingSamples = 0;
    }

    public void RecordTimerDecision(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        float rawRemaining,
        bool canConfirmRefresh,
        ObservedStatusTimerResult result)
    {
        switch (result.Decision)
        {
            case ObservedStatusTimerDecision.Refreshed:
                this.timerRefreshAcceptedCount++;
                break;
            case ObservedStatusTimerDecision.StalePositiveSuppressed:
                this.timerStalePositiveSuppressedCount++;
                break;
            default:
                return;
        }

        this.timerLastDecision = FormattableString.Invariant(
            $"{result.Decision}|member={member.Key}|action={definition.ActionId}|raw={rawRemaining:0.###}|remaining={result.Remaining:0.###}|live={canConfirmRefresh}");
    }

    public PartyCooldownSignalDiagnosticSnapshot CreateSnapshot()
        => new(
            this.candidateMissingTotal,
            this.candidateMissingSamples,
            this.localPlayerLogSkippedTotal,
            this.localOwnedObjectLogSkippedTotal,
            this.statusFallbackBatchCount,
            this.timerRefreshAcceptedCount,
            this.timerStalePositiveSuppressedCount,
            this.timerLastDecision);
}
