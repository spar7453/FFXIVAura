namespace FFXIVAura;

internal readonly record struct PartyAuraTimerKey(
    uint OwnerEntityId,
    uint StatusId,
    uint SourceId);

internal sealed class PartyAuraTimerState
{
    public ObservedStatusTimerState Lifetime { get; } = new();
}

internal static class PartyAuraTimerTracker
{
    private static readonly ObservedStatusTimerPolicy Policy = new(
        AcceptObservedIncreaseAsRefresh: true,
        RequireNearFullDurationForObservedRefresh: false);

    public static float Update(
        PartyAuraTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        bool canConfirmRefresh = true)
        => UpdateDetailed(state, observedAtUtc, observedRemaining, canConfirmRefresh).Remaining;

    public static ObservedStatusTimerResult UpdateDetailed(
        PartyAuraTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        bool canConfirmRefresh = true)
        => UpdateDetailed(
            state,
            observedAtUtc,
            observedRemaining,
            observedRemaining > 0f
                ? ObservedStatusObservation.Present
                : ObservedStatusObservation.ConfirmedAbsent,
            canConfirmRefresh);

    public static ObservedStatusTimerResult UpdateDetailed(
        PartyAuraTimerState state,
        DateTime observedAtUtc,
        float observedRemaining,
        ObservedStatusObservation observation,
        bool canConfirmRefresh)
        => ObservedStatusTimer.Update(
            state.Lifetime,
            observedAtUtc,
            observedRemaining,
            activeDuration: 0f,
            explicitRefresh: false,
            canConfirmRefresh,
            observation,
            Policy);
}
