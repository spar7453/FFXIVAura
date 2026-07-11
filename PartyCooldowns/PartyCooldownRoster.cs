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
    FlatAllianceFallback,
    CrossRealmAlliance,
    CrossRealmAllianceWithFlatFallback,
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
    bool UsedFlatAllianceFallback,
    int LocalAllianceGroupIndex,
    int RawLocalAllianceGroupIndex,
    int HudLocalAllianceGroupIndex,
    int CrossRealmGroupCount,
    int HudAllianceOrderCount);

internal static class PartyCooldownRoster
{
    public static ulong ComputeMemberIdentityHash(IReadOnlyList<PartyCooldownMemberSnapshot> members)
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        var hash = Offset;
        foreach (var member in members)
        {
            hash = (hash ^ member.EntityId) * Prime;
            foreach (var value in member.Key)
                hash = (hash ^ value) * Prime;
        }

        return (hash ^ (uint)members.Count) * Prime;
    }

    public static IReadOnlyList<PartyCooldownMemberSnapshot> CreateDisplayMembers(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        uint localEntityId,
        ulong localContentId,
        bool excludeLocalPlayer)
    {
        if (!excludeLocalPlayer || (localEntityId == 0 && localContentId == 0))
            return members;

        var filtered = new List<PartyCooldownMemberSnapshot>(members.Count);
        foreach (var member in members)
        {
            if ((localEntityId != 0 && member.EntityId == localEntityId)
                || (localContentId != 0 && member.ContentId == localContentId))
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
        ulong localContentId = 0,
        int alliancePartyCount = 0,
        int allianceMemberCount = 0,
        bool hasAllianceSource = false,
        bool usedFlatAllianceFallback = false,
        int localAllianceGroupIndex = -1,
        int rawLocalAllianceGroupIndex = -1,
        int hudLocalAllianceGroupIndex = -1,
        int crossRealmGroupCount = 0,
        int hudAllianceOrderCount = 0)
    {
        var hasLocalIdentity = localEntityId != 0 || localContentId != 0;
        var excludedLocalPlayer = hasLocalIdentity
                                  && displayMembers.Count < members.Count
                                  && members.Any(member => IsLocalMember(member, localEntityId, localContentId))
                                  && displayMembers.All(member => !IsLocalMember(member, localEntityId, localContentId));
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
            usedFlatAllianceFallback,
            Math.Clamp(localAllianceGroupIndex, -1, 2),
            Math.Clamp(rawLocalAllianceGroupIndex, -1, 2),
            Math.Clamp(hudLocalAllianceGroupIndex, -1, 2),
            Math.Clamp(crossRealmGroupCount, 0, 3),
            Math.Clamp(hudAllianceOrderCount, 0, 40));
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

    private static bool IsLocalMember(PartyCooldownMemberSnapshot member, uint localEntityId, ulong localContentId)
        => (localEntityId != 0 && member.EntityId == localEntityId)
           || (localContentId != 0 && member.ContentId == localContentId);
}
