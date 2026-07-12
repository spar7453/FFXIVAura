namespace FFXIVAura;

internal readonly record struct PartyAuraRuntimeDiagnostics(
    int TimerStateCount,
    long FallbackBatchCount,
    long RefreshAcceptedCount,
    long ExpiredStatusSuppressedCount,
    string LastDecision);

internal readonly record struct PartyAuraRuntimeObservation(
    float Remaining,
    bool Include);

internal sealed class PartyAuraRuntimeStore
{
    private readonly Dictionary<PartyAuraTimerKey, PartyAuraTimerState> timerStates = new();
    private readonly HashSet<PartyAuraTimerKey> liveTimerKeys = [];
    private readonly HashSet<uint> memberOwnerEntityIds = [];
    private readonly HashSet<uint> liveOwnerEntityIds = [];
    private readonly List<PartyAuraTimerKey> timerPruneBuffer = [];
    private long fallbackBatchCount;
    private long refreshAcceptedCount;
    private long expiredStatusSuppressedCount;
    private string lastDecision = string.Empty;

    public void BeginScan()
    {
        this.liveTimerKeys.Clear();
        this.memberOwnerEntityIds.Clear();
        this.liveOwnerEntityIds.Clear();
    }

    public void MarkMemberOwner(uint ownerEntityId)
    {
        if (ownerEntityId != 0)
            this.memberOwnerEntityIds.Add(ownerEntityId);
    }

    public void RecordStatusBatch(uint ownerEntityId, StatusSnapshotOrigin origin)
    {
        if (origin == StatusSnapshotOrigin.Fallback)
        {
            this.fallbackBatchCount++;
        }
        else if (origin == StatusSnapshotOrigin.Live && ownerEntityId != 0)
        {
            this.liveOwnerEntityIds.Add(ownerEntityId);
        }
    }

    public PartyAuraRuntimeObservation ObserveStatus(
        PartyAuraTimerKey key,
        DateTime observedAtUtc,
        float observedRemaining,
        StatusSnapshotOrigin origin)
    {
        this.liveTimerKeys.Add(key);
        bool stateExisted;
        PartyAuraTimerState timerState;
        if (this.timerStates.TryGetValue(key, out var existingTimerState))
        {
            stateExisted = true;
            timerState = existingTimerState;
        }
        else
        {
            stateExisted = false;
            if (!(observedRemaining > 0f))
                return new PartyAuraRuntimeObservation(observedRemaining, Include: true);

            timerState = new PartyAuraTimerState();
            this.timerStates[key] = timerState;
        }

        var result = PartyAuraTimerTracker.UpdateDetailed(
            timerState,
            observedAtUtc,
            observedRemaining,
            ObservedStatusObservation.Present,
            origin == StatusSnapshotOrigin.Live);
        this.NoteTimerDecision(key, observedRemaining, origin, result);
        return new PartyAuraRuntimeObservation(
            result.Remaining,
            Include: !stateExisted || result.Remaining > 0f);
    }

    public void CompleteScan(DateTime observedAtUtc, bool rosterReadComplete)
    {
        this.timerPruneBuffer.Clear();
        foreach (var (timerKey, timerState) in this.timerStates)
        {
            if (this.liveTimerKeys.Contains(timerKey))
                continue;

            if (this.memberOwnerEntityIds.Contains(timerKey.OwnerEntityId))
            {
                if (this.liveOwnerEntityIds.Contains(timerKey.OwnerEntityId))
                {
                    this.timerPruneBuffer.Add(timerKey);
                    continue;
                }

                AdvanceUnavailable(timerState, observedAtUtc);
                continue;
            }

            if (rosterReadComplete)
                this.timerPruneBuffer.Add(timerKey);
            else
                AdvanceUnavailable(timerState, observedAtUtc);
        }

        foreach (var timerKey in this.timerPruneBuffer)
            this.timerStates.Remove(timerKey);
    }

    public PartyAuraRuntimeDiagnostics CreateDiagnostics()
        => new(
            this.timerStates.Count,
            this.fallbackBatchCount,
            this.refreshAcceptedCount,
            this.expiredStatusSuppressedCount,
            this.lastDecision);

    public void ResetRuntimeState()
    {
        this.timerStates.Clear();
        this.liveTimerKeys.Clear();
        this.memberOwnerEntityIds.Clear();
        this.liveOwnerEntityIds.Clear();
        this.timerPruneBuffer.Clear();
    }

    private void NoteTimerDecision(
        PartyAuraTimerKey key,
        float rawRemaining,
        StatusSnapshotOrigin origin,
        ObservedStatusTimerResult result)
    {
        switch (result.Decision)
        {
            case ObservedStatusTimerDecision.Refreshed:
                this.refreshAcceptedCount++;
                break;
            case ObservedStatusTimerDecision.StalePositiveSuppressed:
                this.expiredStatusSuppressedCount++;
                break;
            default:
                return;
        }

        this.lastDecision = FormattableString.Invariant(
            $"{result.Decision}|owner={key.OwnerEntityId}|status={key.StatusId}|source={key.SourceId}|raw={rawRemaining:0.###}|remaining={result.Remaining:0.###}|origin={origin}");
    }

    private static void AdvanceUnavailable(PartyAuraTimerState timerState, DateTime observedAtUtc)
        => _ = PartyAuraTimerTracker.UpdateDetailed(
            timerState,
            observedAtUtc,
            0f,
            ObservedStatusObservation.Unavailable,
            canConfirmRefresh: false);
}
