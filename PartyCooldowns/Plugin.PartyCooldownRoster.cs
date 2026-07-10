namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;

    private readonly record struct PartyCooldownRosterReadResult(
        IReadOnlyList<PartyCooldownMemberSnapshot> Members,
        PartyCooldownRosterSource Source,
        PartyCooldownRosterReadMode ReadMode,
        int PartyListLength,
        int AlliancePartyCount,
        int AllianceMemberCount,
        bool HasAllianceSource,
        bool UsedFlatAllianceFallback);

    private IReadOnlyList<PartyCooldownMemberSnapshot> GetPartyCooldownMembers()
        => this.GetPartyCooldownRoster().Members;

    private PartyCooldownRosterReadResult GetPartyCooldownRoster()
    {
        var partyListHeader = this.GetPartyListHeader();
        var partyListLength = partyListHeader.Length;
        var hasAllianceSource = partyListHeader.IsAlliance;
        var partyId = partyListHeader.PartyId;
        var capacity = Math.Max(hasAllianceSource ? AllianceGroupCount * AllianceGroupMemberSlotCount : partyListLength, 1);
        var members = new List<PartyCooldownMemberSnapshot>(capacity);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenEntityIds = new HashSet<uint>();

        var partySlotMemberCount = 0;
        var groupedAllianceMemberCount = 0;
        var flatAllianceMemberCount = 0;
        var usedFlatAllianceFallback = false;
        var readMode = PartyCooldownRosterReadMode.Unknown;
        if (hasAllianceSource)
        {
            groupedAllianceMemberCount = this.AddPartyCooldownAllianceGroupMembers(members, seenKeys, seenEntityIds);
            if (groupedAllianceMemberCount == 0)
            {
                partySlotMemberCount = this.AddPartyCooldownPartySlotMembers(
                    members,
                    seenKeys,
                    seenEntityIds,
                    PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, partyId),
                    partyListHeader.PartySlotCount);
                usedFlatAllianceFallback = true;
                flatAllianceMemberCount = this.AddPartyCooldownFlatAllianceMembers(members, seenKeys, seenEntityIds, partyId);
            }
            else if (groupedAllianceMemberCount < AllianceGroupCount * AllianceGroupMemberSlotCount)
            {
                usedFlatAllianceFallback = true;
                flatAllianceMemberCount = this.AddPartyCooldownFlatAllianceMembers(members, seenKeys, seenEntityIds, partyId);
            }
        }
        else
        {
            partySlotMemberCount = this.AddPartyCooldownPartySlotMembers(
                members,
                seenKeys,
                seenEntityIds,
                string.Empty,
                partyListHeader.PartySlotCount);
        }

        if (members.Count == 0 && ObjectTable.LocalPlayer is IBattleChara player)
        {
            var classJobId = PlayerState.ClassJob.RowId;
            var job = JobInfo.Code(classJobId);
            var name = player.Name.ToString();
            members.Add(new PartyCooldownMemberSnapshot(
                PartyCooldownMemberKey(0, player.EntityId, name, job),
                player.EntityId,
                (ushort)PlayerState.HomeWorld.RowId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                string.Empty));
            readMode = PartyCooldownRosterReadMode.SoloFallback;
        }

        var orderedMembers = PartyCooldownMemberOrdering.PreserveInGameOrder(members);
        var allianceMemberCount = CountPartyCooldownAllianceMembers(orderedMembers);
        var hasUsableAllianceSource = hasAllianceSource && (groupedAllianceMemberCount > 0 || flatAllianceMemberCount > 0);
        var source = hasUsableAllianceSource
            ? PartyCooldownRosterSource.Alliance
            : partyListLength > 0
                ? PartyCooldownRosterSource.PartyList
                : PartyCooldownRosterSource.SoloFallback;

        if (readMode == PartyCooldownRosterReadMode.Unknown)
        {
            if (groupedAllianceMemberCount > 0)
            {
                readMode = usedFlatAllianceFallback
                    ? PartyCooldownRosterReadMode.GroupedAllianceWithFlatFallback
                    : PartyCooldownRosterReadMode.GroupedAlliance;
            }
            else if (flatAllianceMemberCount > 0)
            {
                readMode = PartyCooldownRosterReadMode.FlatAllianceFallback;
            }
            else if (partySlotMemberCount > 0)
            {
                readMode = PartyCooldownRosterReadMode.PartySlots;
            }
            else
            {
                readMode = PartyCooldownRosterReadMode.Unknown;
            }
        }

        return new PartyCooldownRosterReadResult(
            orderedMembers,
            source,
            readMode,
            partyListLength,
            hasAllianceSource ? AllianceGroupCount : 0,
            hasAllianceSource ? allianceMemberCount : 0,
            hasAllianceSource,
            usedFlatAllianceFallback);
    }

    private static int CountPartyCooldownAllianceMembers(IReadOnlyList<PartyCooldownMemberSnapshot> members)
    {
        var count = 0;
        foreach (var member in members)
        {
            for (var group = 0; group < AllianceGroupCount; group++)
            {
                if (!string.Equals(member.AllianceGroup, PartyCooldownAllianceGroups.GroupLabel(group), StringComparison.Ordinal))
                    continue;

                count++;
                break;
            }
        }

        return count;
    }

    private int AddPartyCooldownPartySlotMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        string allianceGroup,
        int partySlotCount)
    {
        var added = 0;
        for (var i = 0; i < partySlotCount; i++)
        {
            var member = this.TryCreatePartyMemberReference(i);
            if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, seenEntityIds, member, allianceGroup))
                added++;
        }

        return added;
    }

    private int AddPartyCooldownAllianceGroupMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds)
    {
        var added = 0;
        for (var group = 0; group < AllianceGroupCount; group++)
        {
            var label = PartyCooldownAllianceGroups.GroupLabel(group);
            for (var index = 0; index < AllianceGroupMemberSlotCount; index++)
            {
                var member = this.TryCreateAllianceGroupMemberReference(group, index);
                if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, seenEntityIds, member, label))
                    added++;
            }
        }

        return added;
    }

    private int AddPartyCooldownFlatAllianceMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        int localPartyId)
    {
        var added = 0;
        for (var i = 0; i < FlatAllianceMemberSlotCount; i++)
        {
            var member = this.TryCreateFlatAllianceMemberReference(i);
            if (member is not null
                && this.TryAddPartyCooldownMemberSnapshot(
                    members,
                    seenKeys,
                    seenEntityIds,
                    member,
                    PartyCooldownAllianceGroups.AllianceSlotLabel(i, localPartyId)))
            {
                added++;
            }
        }

        return added;
    }

    private bool TryAddPartyCooldownMemberSnapshot(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        IPartyMember member,
        string allianceGroup)
    {
        var entityId = 0u;
        try
        {
            entityId = member.EntityId;
            if (entityId == 0)
                return false;

            var classJobId = member.ClassJob.RowId;
            var job = JobInfo.Code(classJobId);
            var name = member.Name.ToString();
            var contentId = member.ContentId;
            var worldId = (ushort)member.World.RowId;
            var key = PartyCooldownMemberKey(contentId, entityId, name, job);
            if (!seenEntityIds.Add(entityId))
                return false;

            if (!seenKeys.Add(key))
            {
                seenEntityIds.Remove(entityId);
                return false;
            }

            members.Add(new PartyCooldownMemberSnapshot(
                key,
                entityId,
                worldId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                allianceGroup));
            return true;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownMemberReadFailed:{entityId}:{ex.GetType().Name}");
            return false;
        }
    }

    private IPartyMember? TryCreateAllianceGroupMemberReference(int group, int index)
    {
        try
        {
            var groupManager = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance();
            if (groupManager is null)
                return null;

            var member = groupManager->MainGroup.GetAllianceMemberByGroupAndIndex(group, index);
            if (member is null)
                return null;

            return PartyList.CreateAllianceMemberReference((IntPtr)member);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownAllianceGroupMemberReadFailed:{group}:{index}:{ex.GetType().Name}");
            return null;
        }
    }

    private IPartyMember? TryCreateFlatAllianceMemberReference(int index)
    {
        try
        {
            var address = PartyList.GetAllianceMemberAddress(index);
            if (address == IntPtr.Zero)
                return null;

            return PartyList.CreateAllianceMemberReference(address);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownFlatAllianceMemberReadFailed:{index}:{ex.GetType().Name}");
            return null;
        }
    }

    private IReadOnlyList<PartyCooldownMemberSnapshot> GetPartyCooldownDisplayMembers(
        IReadOnlyList<PartyCooldownMemberSnapshot> members)
        => PartyCooldownRoster.CreateDisplayMembers(
            members,
            ObjectTable.LocalPlayer?.EntityId ?? 0,
            excludeLocalPlayer: true);

    private void AddPartyCooldownStatusSamplesFromPartyList(HashSet<uint> partyEntityIds)
    {
        var partyListHeader = this.GetPartyListHeader();
        if (partyListHeader.IsAlliance)
        {
            var added = this.AddPartyCooldownStatusSamplesFromAllianceGroupMembers(partyEntityIds);
            if (added >= AllianceGroupCount * AllianceGroupMemberSlotCount)
                return;

            if (added == 0)
                this.AddPartyCooldownStatusSamplesFromPartySlots(partyEntityIds, partyListHeader.PartySlotCount);

            this.AddPartyCooldownStatusSamplesFromFlatAllianceMembers(partyEntityIds);
            return;
        }

        this.AddPartyCooldownStatusSamplesFromPartySlots(partyEntityIds, partyListHeader.PartySlotCount);
    }

    private int AddPartyCooldownStatusSamplesFromPartySlots(HashSet<uint> partyEntityIds, int partySlotCount)
    {
        var added = 0;
        for (var i = 0; i < partySlotCount; i++)
        {
            var member = this.TryCreatePartyMemberReference(i);
            if (member is not null
                && this.AddPartyCooldownStatusSamplesFromPartyMember(member, partyEntityIds, "partyCooldownPartyMember"))
            {
                added++;
            }
        }

        return added;
    }

    private int AddPartyCooldownStatusSamplesFromAllianceGroupMembers(HashSet<uint> partyEntityIds)
    {
        var added = 0;
        for (var group = 0; group < AllianceGroupCount; group++)
        {
            for (var index = 0; index < AllianceGroupMemberSlotCount; index++)
            {
                var member = this.TryCreateAllianceGroupMemberReference(group, index);
                if (member is null)
                    continue;

                if (this.AddPartyCooldownStatusSamplesFromPartyMember(member, partyEntityIds, "partyCooldownAllianceGroupMember"))
                    added++;
            }
        }

        return added;
    }

    private int AddPartyCooldownStatusSamplesFromFlatAllianceMembers(HashSet<uint> partyEntityIds)
    {
        var added = 0;
        for (var i = 0; i < FlatAllianceMemberSlotCount; i++)
        {
            var member = this.TryCreateFlatAllianceMemberReference(i);
            if (member is null)
                continue;

            if (this.AddPartyCooldownStatusSamplesFromPartyMember(member, partyEntityIds, "partyCooldownFlatAllianceMember"))
                added++;
        }

        return added;
    }

    private bool AddPartyCooldownStatusSamplesFromPartyMember(
        IPartyMember member,
        HashSet<uint> partyEntityIds,
        string scope)
    {
        if (!this.TryReadPartyMemberStatusSnapshots(member, scope, out var entityId))
            return entityId != 0;

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(entityId, status.SourceId, status.StatusId, status.RemainingTime, partyEntityIds);

        return true;
    }

}
