namespace FFXIVAura;

internal enum PartyCooldownRosterSource
{
    Unknown,
    PartyList,
    SoloFallback,
    Alliance,
}

internal enum PartyCooldownRosterReadMode
{
    Unknown,
    PartySlots,
    SoloFallback,
    GroupedAlliance,
    GroupedAllianceWithFlatFallback,
    FlatAllianceFallback,
}

internal readonly record struct PartyCooldownRosterDiagnostics(
    PartyCooldownRosterSource Source,
    PartyCooldownRosterReadMode ReadMode,
    int PartyListLength,
    int MemberCount,
    int DisplayMemberCount,
    bool ExcludedLocalPlayer,
    int AlliancePartyCount,
    int AllianceMemberCount,
    bool HasAllianceSource,
    int AllianceGroupAMemberCount,
    int AllianceGroupBMemberCount,
    int AllianceGroupCMemberCount,
    int AllianceEmptySlotCount,
    bool UsedFlatAllianceFallback);

internal static class PartyCooldownRoster
{
    public static IReadOnlyList<PartyCooldownMemberSnapshot> CreateDisplayMembers(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        uint localEntityId,
        bool excludeLocalPlayer)
    {
        if (!excludeLocalPlayer || localEntityId == 0)
            return members;

        var filtered = new List<PartyCooldownMemberSnapshot>(members.Count);
        foreach (var member in members)
        {
            if (member.EntityId == localEntityId)
                continue;

            filtered.Add(member);
        }

        return filtered;
    }

    public static PartyCooldownRosterDiagnostics CreateDiagnostics(
        PartyCooldownRosterSource source,
        PartyCooldownRosterReadMode readMode,
        int partyListLength,
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        uint localEntityId,
        int alliancePartyCount = 0,
        int allianceMemberCount = 0,
        bool hasAllianceSource = false,
        bool usedFlatAllianceFallback = false)
    {
        var excludedLocalPlayer = localEntityId != 0
                                  && displayMembers.Count < members.Count
                                  && members.Any(member => member.EntityId == localEntityId)
                                  && displayMembers.All(member => member.EntityId != localEntityId);
        var allianceGroupAMemberCount = CountAllianceGroupMembers(members, PartyCooldownAllianceGroups.GroupLabel(0));
        var allianceGroupBMemberCount = CountAllianceGroupMembers(members, PartyCooldownAllianceGroups.GroupLabel(1));
        var allianceGroupCMemberCount = CountAllianceGroupMembers(members, PartyCooldownAllianceGroups.GroupLabel(2));
        var detectedAllianceMemberCount = allianceGroupAMemberCount + allianceGroupBMemberCount + allianceGroupCMemberCount;
        var sanitizedAllianceMemberCount = Math.Max(0, allianceMemberCount > 0 ? allianceMemberCount : detectedAllianceMemberCount);
        var allianceEmptySlotCount = hasAllianceSource
            ? Math.Max(0, 24 - Math.Min(24, sanitizedAllianceMemberCount))
            : 0;

        return new PartyCooldownRosterDiagnostics(
            source,
            readMode,
            Math.Max(0, partyListLength),
            members.Count,
            displayMembers.Count,
            excludedLocalPlayer,
            Math.Max(0, alliancePartyCount),
            sanitizedAllianceMemberCount,
            hasAllianceSource,
            allianceGroupAMemberCount,
            allianceGroupBMemberCount,
            allianceGroupCMemberCount,
            allianceEmptySlotCount,
            usedFlatAllianceFallback);
    }

    private static int CountAllianceGroupMembers(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        string allianceGroup)
    {
        var count = 0;
        foreach (var member in members)
        {
            if (string.Equals(member.AllianceGroup, allianceGroup, StringComparison.Ordinal))
                count++;
        }

        return count;
    }
}
