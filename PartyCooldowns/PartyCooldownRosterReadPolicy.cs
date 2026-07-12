namespace FFXIVAura;

internal readonly record struct PartyCooldownRosterReadCounts(
    int PartySlotMemberCount,
    int GroupedAllianceMemberCount,
    int FlatAllianceMemberCount,
    bool UsedFlatAllianceFallback)
{
    public bool HasUsableAllianceSource
        => this.PartySlotMemberCount > 0
           || this.GroupedAllianceMemberCount > 0
           || this.FlatAllianceMemberCount > 0;
}

internal static class PartyCooldownRosterReadClassifier
{
    public static PartyCooldownRosterReadMode ResolveReadMode(
        PartyCooldownRosterReadCounts counts,
        bool usedSoloFallback)
        => (usedSoloFallback,
            counts.GroupedAllianceMemberCount,
            counts.FlatAllianceMemberCount,
            counts.PartySlotMemberCount,
            counts.UsedFlatAllianceFallback) switch
        {
            (true, _, _, _, _) => PartyCooldownRosterReadMode.SoloFallback,
            (false, > 0, _, _, true) => PartyCooldownRosterReadMode.CrossRealmAllianceWithFlatFallback,
            (false, > 0, _, _, false) => PartyCooldownRosterReadMode.CrossRealmAlliance,
            (false, 0, > 0, _, _) => PartyCooldownRosterReadMode.FlatAllianceFallback,
            (false, 0, 0, > 0, _) => PartyCooldownRosterReadMode.PartySlots,
            _ => PartyCooldownRosterReadMode.Unknown,
        };

    public static PartyCooldownRosterSource ResolveSource(
        bool hasAllianceSource,
        int partyListLength,
        PartyCooldownRosterReadCounts counts)
    {
        if (hasAllianceSource && counts.HasUsableAllianceSource)
            return PartyCooldownRosterSource.Alliance;

        return partyListLength > 0
            ? PartyCooldownRosterSource.PartyList
            : PartyCooldownRosterSource.SoloFallback;
    }
}

internal static class PartyCooldownRosterCachePolicy
{
    public static bool CanReuse(
        bool forceRefresh,
        DateTime builtAtUtc,
        DateTime nowUtc,
        TimeSpan cacheDuration)
        => !forceRefresh
           && nowUtc >= builtAtUtc
           && nowUtc - builtAtUtc < cacheDuration;
}
