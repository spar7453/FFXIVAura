namespace FFXIVAura;

internal enum ObservedStatusTimerDecision
{
    None,
    Started,
    Refreshed,
    CorrectedEarlier,
    Removed,
    StalePositiveSuppressed,
}

internal enum ObservedStatusObservation
{
    Present,
    ConfirmedAbsent,
    Unavailable,
}

internal sealed class ObservedStatusTimerState
{
    public DateTime EstimatedEndsAtUtc { get; set; } = DateTime.MinValue;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.MinValue;

    public float LastObservedRemaining { get; set; }

    public float LastRefreshEligibleRemaining { get; set; }

    public bool HasRefreshEligibleObservation { get; set; }

    public bool IsStalePositiveSuppressed { get; set; }
}

internal readonly record struct ObservedStatusTimerPolicy(
    bool AcceptObservedIncreaseAsRefresh,
    bool RequireNearFullDurationForObservedRefresh);

internal readonly record struct ObservedStatusTimerResult(
    float Remaining,
    ObservedStatusTimerDecision Decision);

internal static class ObservedStatusTimer
{
    private const float RefreshIncreaseSeconds = 0.5f;
    private const float EarlierCorrectionToleranceSeconds = 0.35f;
    private const float EarlierCorrectionRate = 0.5f;
    private const float MinimumEarlierCorrectionSeconds = 0.05f;
    private const float MaximumEarlierCorrectionSeconds = 0.25f;

    public static ObservedStatusTimerResult Update(
        ObservedStatusTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration,
        bool explicitRefresh,
        bool canConfirmRefresh,
        ObservedStatusTimerPolicy policy)
        => Update(
            state,
            observedAtUtc,
            observedRemaining,
            activeDuration,
            explicitRefresh,
            canConfirmRefresh,
            observedRemaining > 0f
                ? ObservedStatusObservation.Present
                : ObservedStatusObservation.ConfirmedAbsent,
            policy);

    public static ObservedStatusTimerResult Update(
        ObservedStatusTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration,
        bool explicitRefresh,
        bool canConfirmRefresh,
        ObservedStatusObservation observation,
        ObservedStatusTimerPolicy policy)
    {
        if (observation == ObservedStatusObservation.Unavailable || !float.IsFinite(observedRemaining))
            return AdvanceWithoutObservation(state, observedAtUtc);

        if (observation == ObservedStatusObservation.ConfirmedAbsent || observedRemaining <= 0f)
        {
            var removed = state.EstimatedEndsAtUtc != DateTime.MinValue || state.IsStalePositiveSuppressed;
            Reset(state);
            return new ObservedStatusTimerResult(
                0f,
                removed ? ObservedStatusTimerDecision.Removed : ObservedStatusTimerDecision.None);
        }

        var candidateEndsAtUtc = observedAtUtc.AddSeconds(observedRemaining);
        var clockMovedBack = state.UpdatedAtUtc != DateTime.MinValue
                             && observedAtUtc < state.UpdatedAtUtc;
        if (state.EstimatedEndsAtUtc == DateTime.MinValue || clockMovedBack)
        {
            Initialize(state, observedAtUtc, observedRemaining, candidateEndsAtUtc, canConfirmRefresh);
            return new ObservedStatusTimerResult(observedRemaining, ObservedStatusTimerDecision.Started);
        }

        var observedIncrease = false;
        if (canConfirmRefresh)
        {
            if (state.HasRefreshEligibleObservation)
            {
                observedIncrease = observedRemaining > state.LastRefreshEligibleRemaining + RefreshIncreaseSeconds
                                   && candidateEndsAtUtc > state.EstimatedEndsAtUtc.AddSeconds(RefreshIncreaseSeconds);
            }

            state.LastRefreshEligibleRemaining = observedRemaining;
            state.HasRefreshEligibleObservation = true;
        }

        var nearFullDuration = activeDuration <= 0f
                               || observedRemaining >= activeDuration - Math.Max(0.5f, activeDuration * 0.05f);
        var observedRefresh = policy.AcceptObservedIncreaseAsRefresh
                              && observedIncrease
                              && (!policy.RequireNearFullDurationForObservedRefresh || nearFullDuration);
        var decision = ObservedStatusTimerDecision.None;
        if (explicitRefresh || observedRefresh)
        {
            state.EstimatedEndsAtUtc = candidateEndsAtUtc;
            state.IsStalePositiveSuppressed = false;
            decision = ObservedStatusTimerDecision.Refreshed;
        }
        else if (!state.IsStalePositiveSuppressed)
        {
            var correctionSeconds = (candidateEndsAtUtc - state.EstimatedEndsAtUtc).TotalSeconds;
            if (correctionSeconds < -EarlierCorrectionToleranceSeconds)
            {
                var elapsedSinceUpdate = Math.Max(0f, (float)(observedAtUtc - state.UpdatedAtUtc).TotalSeconds);
                var maximumCorrection = Math.Clamp(
                    elapsedSinceUpdate * EarlierCorrectionRate,
                    MinimumEarlierCorrectionSeconds,
                    MaximumEarlierCorrectionSeconds);
                state.EstimatedEndsAtUtc = state.EstimatedEndsAtUtc.AddSeconds(
                    Math.Max(correctionSeconds, -maximumCorrection));
                decision = ObservedStatusTimerDecision.CorrectedEarlier;
            }
        }

        state.LastObservedRemaining = observedRemaining;
        state.UpdatedAtUtc = observedAtUtc;
        var remaining = Math.Max(0f, (float)(state.EstimatedEndsAtUtc - observedAtUtc).TotalSeconds);
        if (remaining > 0f && !state.IsStalePositiveSuppressed)
            return new ObservedStatusTimerResult(remaining, decision);

        var firstSuppression = !state.IsStalePositiveSuppressed;
        state.IsStalePositiveSuppressed = true;
        return new ObservedStatusTimerResult(
            0f,
            firstSuppression ? ObservedStatusTimerDecision.StalePositiveSuppressed : ObservedStatusTimerDecision.None);
    }

    public static void Reset(ObservedStatusTimerState state)
    {
        state.EstimatedEndsAtUtc = DateTime.MinValue;
        state.UpdatedAtUtc = DateTime.MinValue;
        state.LastObservedRemaining = 0f;
        state.LastRefreshEligibleRemaining = 0f;
        state.HasRefreshEligibleObservation = false;
        state.IsStalePositiveSuppressed = false;
    }

    private static ObservedStatusTimerResult AdvanceWithoutObservation(
        ObservedStatusTimerState state,
        DateTime observedAtUtc)
    {
        if (state.EstimatedEndsAtUtc == DateTime.MinValue)
            return new ObservedStatusTimerResult(0f, ObservedStatusTimerDecision.None);

        if (state.UpdatedAtUtc != DateTime.MinValue && observedAtUtc < state.UpdatedAtUtc)
            state.EstimatedEndsAtUtc = observedAtUtc.AddSeconds(Math.Max(0f, state.LastObservedRemaining));

        state.UpdatedAtUtc = observedAtUtc;
        var remaining = Math.Max(0f, (float)(state.EstimatedEndsAtUtc - observedAtUtc).TotalSeconds);
        if (remaining > 0f && !state.IsStalePositiveSuppressed)
            return new ObservedStatusTimerResult(remaining, ObservedStatusTimerDecision.None);

        state.IsStalePositiveSuppressed = true;
        return new ObservedStatusTimerResult(0f, ObservedStatusTimerDecision.None);
    }

    private static void Initialize(
        ObservedStatusTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        DateTime candidateEndsAtUtc,
        bool canConfirmRefresh)
    {
        state.EstimatedEndsAtUtc = candidateEndsAtUtc;
        state.UpdatedAtUtc = observedAtUtc;
        state.LastObservedRemaining = observedRemaining;
        state.LastRefreshEligibleRemaining = canConfirmRefresh ? observedRemaining : 0f;
        state.HasRefreshEligibleObservation = canConfirmRefresh;
        state.IsStalePositiveSuppressed = false;
    }
}
