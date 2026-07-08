namespace FFXIVAura;

internal enum PartyCooldownRosterSource
{
    Unknown,
    PartyList,
    SoloFallback,
    Alliance,
}

internal readonly record struct PartyCooldownRosterDiagnostics(
    PartyCooldownRosterSource Source,
    int PartyListLength,
    int MemberCount,
    int DisplayMemberCount,
    bool ExcludedLocalPlayer,
    int AlliancePartyCount,
    int AllianceMemberCount,
    bool HasAllianceSource);

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
        int partyListLength,
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        uint localEntityId,
        int alliancePartyCount = 0,
        int allianceMemberCount = 0,
        bool hasAllianceSource = false)
    {
        var excludedLocalPlayer = localEntityId != 0
                                  && displayMembers.Count < members.Count
                                  && members.Any(member => member.EntityId == localEntityId)
                                  && displayMembers.All(member => member.EntityId != localEntityId);
        return new PartyCooldownRosterDiagnostics(
            source,
            Math.Max(0, partyListLength),
            members.Count,
            displayMembers.Count,
            excludedLocalPlayer,
            Math.Max(0, alliancePartyCount),
            Math.Max(0, allianceMemberCount),
            hasAllianceSource);
    }
}
