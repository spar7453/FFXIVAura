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
        => (int)candidate.Priority > (int)current.Priority
           || (candidate.Priority == current.Priority && candidate.Remaining > current.Remaining);

    public static bool IsPreferredAcrossStatusIds(PartyCooldownActiveStatus candidate, PartyCooldownActiveStatus current)
        => candidate.Remaining > current.Remaining;
}
