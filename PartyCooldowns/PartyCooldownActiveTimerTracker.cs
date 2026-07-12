namespace FFXIVAura;

internal static class PartyCooldownActiveTimerTracker
{
    private static readonly ObservedStatusTimerPolicy Policy = new(
        AcceptObservedIncreaseAsRefresh: true,
        RequireNearFullDurationForObservedRefresh: true);

    public static float Update(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration,
        bool canConfirmRefresh = true)
        => UpdateDetailed(state, observedAtUtc, observedRemaining, activeDuration, canConfirmRefresh).Remaining;

    public static ObservedStatusTimerResult UpdateDetailed(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration,
        bool canConfirmRefresh = true)
        => UpdateDetailed(
            state,
            observedAtUtc,
            observedRemaining,
            activeDuration,
            observedRemaining > 0f
                ? ObservedStatusObservation.Present
                : ObservedStatusObservation.ConfirmedAbsent,
            canConfirmRefresh);

    public static ObservedStatusTimerResult UpdateDetailed(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float observedRemaining,
        float activeDuration,
        ObservedStatusObservation observation,
        bool canConfirmRefresh)
    {
        var useWasObserved = state.LastObservedUseAtUtc != DateTime.MinValue
                             && state.LastObservedUseAtUtc > state.ActiveTimerLastUseAtUtc;
        var result = ObservedStatusTimer.Update(
            state.ActiveTimer,
            observedAtUtc,
            observedRemaining,
            activeDuration,
            useWasObserved,
            canConfirmRefresh,
            observation,
            Policy);
        state.ActiveTimerLastUseAtUtc = state.LastObservedUseAtUtc;
        return result;
    }

    public static void Reset(PartyCooldownRuntimeState state)
    {
        ObservedStatusTimer.Reset(state.ActiveTimer);
        state.ActiveTimerLastUseAtUtc = state.LastObservedUseAtUtc;
    }
}
