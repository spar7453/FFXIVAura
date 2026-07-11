namespace FFXIVAura;

internal sealed class PartyAuraTimerState
{
    public DateTime EstimatedEndsAtUtc { get; set; } = DateTime.MinValue;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.MinValue;

    public float LastObservedRemaining { get; set; }
}

internal static class PartyAuraTimerTracker
{
    private const float ReapplicationIncreaseSeconds = 0.5f;
    private const float EarlierCorrectionToleranceSeconds = 0.35f;
    private const float EarlierCorrectionRate = 0.5f;
    private const float MinimumEarlierCorrectionSeconds = 0.05f;
    private const float MaximumEarlierCorrectionSeconds = 0.25f;

    public static float Update(PartyAuraTimerState state, DateTime observedAtUtc, float observedRemaining)
    {
        if (observedRemaining <= 0f)
            return 0f;

        var clockMovedBack = state.UpdatedAtUtc != DateTime.MinValue
                             && observedAtUtc < state.UpdatedAtUtc;
        var candidateEndsAtUtc = observedAtUtc.AddSeconds(observedRemaining);
        if (state.EstimatedEndsAtUtc == DateTime.MinValue || clockMovedBack)
        {
            state.EstimatedEndsAtUtc = candidateEndsAtUtc;
        }
        else
        {
            var reapplied = observedRemaining > state.LastObservedRemaining + ReapplicationIncreaseSeconds
                            && candidateEndsAtUtc > state.EstimatedEndsAtUtc.AddSeconds(ReapplicationIncreaseSeconds);
            if (reapplied)
            {
                state.EstimatedEndsAtUtc = candidateEndsAtUtc;
            }
            else
            {
                var correctionSeconds = (candidateEndsAtUtc - state.EstimatedEndsAtUtc).TotalSeconds;
                if (correctionSeconds < -EarlierCorrectionToleranceSeconds)
                {
                    var elapsedSinceUpdate = state.UpdatedAtUtc == DateTime.MinValue
                        ? 0f
                        : Math.Max(0f, (float)(observedAtUtc - state.UpdatedAtUtc).TotalSeconds);
                    var maximumCorrection = Math.Clamp(
                        elapsedSinceUpdate * EarlierCorrectionRate,
                        MinimumEarlierCorrectionSeconds,
                        MaximumEarlierCorrectionSeconds);
                    state.EstimatedEndsAtUtc = state.EstimatedEndsAtUtc.AddSeconds(
                        Math.Max(correctionSeconds, -maximumCorrection));
                }
            }
        }

        state.LastObservedRemaining = observedRemaining;
        state.UpdatedAtUtc = observedAtUtc;
        return Math.Max(0f, (float)(state.EstimatedEndsAtUtc - observedAtUtc).TotalSeconds);
    }
}
