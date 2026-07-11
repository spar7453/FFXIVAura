namespace FFXIVAura;

internal readonly record struct PartyCooldownChargeSnapshot(
    uint CurrentCharges,
    uint MaxCharges,
    float NextChargeRemaining,
    float ChargeCooldownTotal);

internal static class PartyCooldownChargeTracker
{
    public static bool RecordUse(
        PartyCooldownRuntimeState state,
        DateTime usedAtUtc,
        float chargeCooldownSeconds,
        uint maxCharges,
        PartyCooldownUseObservationSource source,
        TimeSpan sameSourceDedupeWindow,
        TimeSpan crossSourceDedupeWindow)
    {
        Prepare(state, usedAtUtc, maxCharges);
        var dedupeWindow = state.LastObservedUseSource is not PartyCooldownUseObservationSource.Unknown
                           && source is not PartyCooldownUseObservationSource.Unknown
                           && state.LastObservedUseSource != source
            ? crossSourceDedupeWindow
            : sameSourceDedupeWindow;
        if (chargeCooldownSeconds <= 0f || IsDuplicateUse(state.LastObservedUseAtUtc, usedAtUtc, dedupeWindow))
            return false;

        state.LastObservedUseAtUtc = usedAtUtc;
        state.LastObservedUseSource = source;
        if (state.ChargeRecoveryEndsAtUtc.Count >= state.MaxCharges)
            return false;

        var recoveryStartsAtUtc = state.ChargeRecoveryEndsAtUtc.Count == 0
            ? usedAtUtc
            : LaterOf(state.LastChargeRecoveryEndsAtUtc, usedAtUtc);
        var recoveryEndsAtUtc = recoveryStartsAtUtc.AddSeconds(chargeCooldownSeconds);
        state.ChargeRecoveryEndsAtUtc.Enqueue(recoveryEndsAtUtc);
        state.LastChargeRecoveryEndsAtUtc = recoveryEndsAtUtc;
        return true;
    }

    public static void ObserveActiveStatus(
        PartyCooldownRuntimeState state,
        DateTime observedAtUtc,
        float activeRemaining,
        float activeDuration,
        float chargeCooldownSeconds,
        uint maxCharges,
        TimeSpan sameSourceDedupeWindow,
        TimeSpan crossSourceDedupeWindow,
        TimeSpan missingStatusGrace)
    {
        Prepare(state, observedAtUtc, maxCharges);
        if (activeRemaining <= 0f)
        {
            var missingWithinGrace = state.ActiveObservedLastFrame
                                     && state.ActiveStatusLastSeenAtUtc != DateTime.MinValue
                                     && observedAtUtc >= state.ActiveStatusLastSeenAtUtc
                                     && observedAtUtc - state.ActiveStatusLastSeenAtUtc <= missingStatusGrace;
            if (missingWithinGrace)
                return;

            state.ActiveObservedLastFrame = false;
            state.ActiveStatusLastSeenAtUtc = DateTime.MinValue;
            return;
        }

        if (!state.ActiveObservedLastFrame)
        {
            var elapsedSinceUse = activeDuration > 0f
                ? Math.Clamp(activeDuration - activeRemaining, 0f, activeDuration)
                : 0f;
            var inferredUseAtUtc = observedAtUtc.AddSeconds(-elapsedSinceUse);
            RecordUse(
                state,
                inferredUseAtUtc,
                chargeCooldownSeconds,
                maxCharges,
                PartyCooldownUseObservationSource.ActiveStatus,
                sameSourceDedupeWindow,
                crossSourceDedupeWindow);
        }

        state.ActiveObservedLastFrame = true;
        state.ActiveStatusLastSeenAtUtc = observedAtUtc;
    }

    public static PartyCooldownChargeSnapshot GetSnapshot(
        PartyCooldownRuntimeState state,
        DateTime nowUtc,
        float chargeCooldownSeconds,
        uint maxCharges)
    {
        Prepare(state, nowUtc, maxCharges);
        var currentCharges = state.MaxCharges - (uint)state.ChargeRecoveryEndsAtUtc.Count;
        var nextChargeRemaining = state.ChargeRecoveryEndsAtUtc.TryPeek(out var recoveryEndsAtUtc)
            ? Math.Max(0f, (float)(recoveryEndsAtUtc - nowUtc).TotalSeconds)
            : 0f;
        return new PartyCooldownChargeSnapshot(
            currentCharges,
            state.MaxCharges,
            nextChargeRemaining,
            Math.Max(0f, chargeCooldownSeconds));
    }

    private static void Prepare(PartyCooldownRuntimeState state, DateTime nowUtc, uint maxCharges)
    {
        var sanitizedMaxCharges = Math.Max(1u, maxCharges);
        if (state.MaxCharges != 0 && state.MaxCharges != sanitizedMaxCharges)
            ResetChargeEstimate(state);

        state.MaxCharges = sanitizedMaxCharges;
        while (state.ChargeRecoveryEndsAtUtc.TryPeek(out var recoveryEndsAtUtc) && recoveryEndsAtUtc <= nowUtc)
            state.ChargeRecoveryEndsAtUtc.Dequeue();

        if (state.ChargeRecoveryEndsAtUtc.Count == 0)
            state.LastChargeRecoveryEndsAtUtc = DateTime.MinValue;
    }

    private static void ResetChargeEstimate(PartyCooldownRuntimeState state)
    {
        state.ChargeRecoveryEndsAtUtc.Clear();
        state.LastChargeRecoveryEndsAtUtc = DateTime.MinValue;
        state.LastObservedUseAtUtc = DateTime.MinValue;
        state.LastObservedUseSource = PartyCooldownUseObservationSource.Unknown;
        state.LastLogTrackedAtUtc = DateTime.MinValue;
        state.ActiveObservedLastFrame = false;
        state.ActiveStatusLastSeenAtUtc = DateTime.MinValue;
    }

    private static bool IsDuplicateUse(DateTime previousUseAtUtc, DateTime usedAtUtc, TimeSpan dedupeWindow)
        => previousUseAtUtc != DateTime.MinValue
           && (usedAtUtc >= previousUseAtUtc
               ? usedAtUtc - previousUseAtUtc
               : previousUseAtUtc - usedAtUtc) < dedupeWindow;

    private static DateTime LaterOf(DateTime first, DateTime second)
        => first >= second ? first : second;
}
