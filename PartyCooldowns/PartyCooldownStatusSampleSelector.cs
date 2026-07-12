namespace FFXIVAura;

internal enum PartyCooldownStatusSamplePriority
{
    ObjectRecipient = 1,
    ObjectSource = 2,
    PartyListRecipient = 3,
    PartyListSource = 4,
}

internal static class PartyCooldownStatusSampleSelector
{
    public static PartyCooldownStatusSamplePriority GetPriority(bool fromPartyList, bool onSourceMember)
        => (fromPartyList, onSourceMember) switch
        {
            (true, true) => PartyCooldownStatusSamplePriority.PartyListSource,
            (true, false) => PartyCooldownStatusSamplePriority.PartyListRecipient,
            (false, true) => PartyCooldownStatusSamplePriority.ObjectSource,
            _ => PartyCooldownStatusSamplePriority.ObjectRecipient,
        };

    public static bool IsPreferredDuplicateSample(PartyCooldownActiveStatus candidate, PartyCooldownActiveStatus current)
    {
        var candidateOriginPriority = GetOriginPriority(candidate.Origin);
        var currentOriginPriority = GetOriginPriority(current.Origin);
        if (candidateOriginPriority != currentOriginPriority)
            return candidateOriginPriority > currentOriginPriority;

        return (int)candidate.Priority > (int)current.Priority
               || candidate.Priority == current.Priority && candidate.Remaining > current.Remaining;
    }

    public static bool IsPreferredAcrossStatusIds(PartyCooldownActiveStatus candidate, PartyCooldownActiveStatus current)
    {
        var candidateOriginPriority = GetOriginPriority(candidate.Origin);
        var currentOriginPriority = GetOriginPriority(current.Origin);
        return candidateOriginPriority != currentOriginPriority
            ? candidateOriginPriority > currentOriginPriority
            : candidate.Remaining > current.Remaining;
    }

    private static int GetOriginPriority(StatusSnapshotOrigin origin)
        => origin switch
        {
            StatusSnapshotOrigin.Live => 2,
            StatusSnapshotOrigin.Fallback => 1,
            _ => 0,
        };
}
