namespace FFXIVAura;

internal static class PartyCooldownActiveTimerTracker
{
    private const float ReapplicationIncreaseSeconds = 0.5f;
    private const float MinimumObservedRemaining = 0.05f;
    private const float EarlierCorrectionToleranceSeconds = 0.35f;
    private const float EarlierCorrectionRate = 0.5f;
    private const float MinimumEarlierCorrectionSeconds = 0.05f;
    private const float MaximumEarlierCorrectionSeconds = 0.25f;

    public static float Update(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration)
    {
        if (observedRemaining <= 0f)
        {
            Reset(state);
            return 0f;
        }

        var clockMovedBack = state.ActiveTimerUpdatedAtUtc != DateTime.MinValue
                             && observedAtUtc < state.ActiveTimerUpdatedAtUtc;
        if (state.ActiveTimerEstimatedEndsAtUtc == DateTime.MinValue || clockMovedBack)
        {
            state.ActiveTimerEstimatedEndsAtUtc = observedAtUtc.AddSeconds(observedRemaining);
        }
        else
        {
            var candidateEndsAtUtc = observedAtUtc.AddSeconds(observedRemaining);
            var useWasObserved = state.LastObservedUseAtUtc != DateTime.MinValue
                                 && state.LastObservedUseAtUtc > state.ActiveTimerLastUseAtUtc;
            var nearFullDuration = activeDuration > 0f
                                   && observedRemaining >= activeDuration - Math.Max(0.5f, activeDuration * 0.05f);
            var statusWasReapplied = nearFullDuration
                                     && observedRemaining > state.ActiveTimerLastObservedRemaining + ReapplicationIncreaseSeconds;
            if (useWasObserved || statusWasReapplied)
            {
                state.ActiveTimerEstimatedEndsAtUtc = candidateEndsAtUtc;
            }
            else
            {
                var correctionSeconds = (candidateEndsAtUtc - state.ActiveTimerEstimatedEndsAtUtc).TotalSeconds;
                if (correctionSeconds < -EarlierCorrectionToleranceSeconds)
                {
                    var elapsedSinceUpdate = state.ActiveTimerUpdatedAtUtc == DateTime.MinValue
                        ? 0f
                        : Math.Max(0f, (float)(observedAtUtc - state.ActiveTimerUpdatedAtUtc).TotalSeconds);
                    var maximumCorrection = Math.Clamp(
                        elapsedSinceUpdate * EarlierCorrectionRate,
                        MinimumEarlierCorrectionSeconds,
                        MaximumEarlierCorrectionSeconds);
                    state.ActiveTimerEstimatedEndsAtUtc = state.ActiveTimerEstimatedEndsAtUtc.AddSeconds(
                        Math.Max(correctionSeconds, -maximumCorrection));
                }
                else if (state.ActiveTimerEstimatedEndsAtUtc <= observedAtUtc && candidateEndsAtUtc > observedAtUtc)
                {
                    state.ActiveTimerEstimatedEndsAtUtc = candidateEndsAtUtc;
                }
            }
        }

        state.ActiveTimerLastObservedRemaining = observedRemaining;
        state.ActiveTimerLastUseAtUtc = state.LastObservedUseAtUtc;
        state.ActiveTimerUpdatedAtUtc = observedAtUtc;
        var remaining = Math.Max(0f, (float)(state.ActiveTimerEstimatedEndsAtUtc - observedAtUtc).TotalSeconds);
        return remaining > 0f ? remaining : Math.Min(observedRemaining, MinimumObservedRemaining);
    }

    public static void Reset(PartyCooldownRuntimeState state)
    {
        state.ActiveTimerEstimatedEndsAtUtc = DateTime.MinValue;
        state.ActiveTimerUpdatedAtUtc = DateTime.MinValue;
        state.ActiveTimerLastUseAtUtc = DateTime.MinValue;
        state.ActiveTimerLastObservedRemaining = 0f;
    }
}
