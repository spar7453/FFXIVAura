namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int PartyMemberSlotCount = 8;
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;

    private readonly record struct PartyCooldownRosterReadResult(
        IReadOnlyList<PartyCooldownMemberSnapshot> Members,
        PartyCooldownRosterSource Source,
        int PartyListLength,
        int AlliancePartyCount,
        int AllianceMemberCount,
        bool HasAllianceSource);

    private IReadOnlyList<PartyCooldownMemberSnapshot> GetPartyCooldownMembers()
        => this.GetPartyCooldownRoster().Members;

    private PartyCooldownRosterReadResult GetPartyCooldownRoster()
    {
        var partyListLength = Math.Max(0, PartyList.Length);
        var hasAllianceSource = PartyList.IsAlliance;
        var partyId = (int)PartyList.PartyId;
        var capacity = Math.Max(hasAllianceSource ? AllianceGroupCount * AllianceGroupMemberSlotCount : partyListLength, 1);
        var members = new List<PartyCooldownMemberSnapshot>(capacity);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        var allianceMemberCount = 0;
        if (hasAllianceSource)
        {
            allianceMemberCount = this.AddPartyCooldownAllianceGroupMembers(members, seenKeys);
            if (allianceMemberCount == 0)
            {
                this.AddPartyCooldownPartySlotMembers(
                    members,
                    seenKeys,
                    PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, partyId));
                allianceMemberCount = this.AddPartyCooldownFlatAllianceMembers(members, seenKeys, partyId);
            }
        }
        else
        {
            this.AddPartyCooldownPartySlotMembers(members, seenKeys, string.Empty);
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
        }

        var orderedMembers = PartyCooldownMemberOrdering.PreserveInGameOrder(members);
        var hasUsableAllianceSource = hasAllianceSource && allianceMemberCount > 0;
        var source = hasUsableAllianceSource
            ? PartyCooldownRosterSource.Alliance
            : partyListLength > 0
                ? PartyCooldownRosterSource.PartyList
                : PartyCooldownRosterSource.SoloFallback;

        return new PartyCooldownRosterReadResult(
            orderedMembers,
            source,
            partyListLength,
            hasUsableAllianceSource ? 3 : 0,
            hasUsableAllianceSource ? members.Count : 0,
            hasUsableAllianceSource);
    }

    private int AddPartyCooldownPartySlotMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        string allianceGroup)
    {
        var added = 0;
        var partySlotCount = Math.Clamp(PartyList.Length, 0, PartyMemberSlotCount);
        for (var i = 0; i < partySlotCount; i++)
        {
            var member = this.TryCreatePartyMemberReference(i);
            if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, member, allianceGroup))
                added++;
        }

        return added;
    }

    private int AddPartyCooldownAllianceGroupMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys)
    {
        var added = 0;
        for (var group = 0; group < AllianceGroupCount; group++)
        {
            var label = PartyCooldownAllianceGroups.GroupLabel(group);
            for (var index = 0; index < AllianceGroupMemberSlotCount; index++)
            {
                var member = this.TryCreateAllianceGroupMemberReference(group, index);
                if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, member, label))
                    added++;
            }
        }

        return added;
    }

    private int AddPartyCooldownFlatAllianceMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
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
        IPartyMember member,
        string allianceGroup)
    {
        if (member.EntityId == 0)
            return false;

        try
        {
            var classJobId = member.ClassJob.RowId;
            var job = JobInfo.Code(classJobId);
            var name = member.Name.ToString();
            var key = PartyCooldownMemberKey(member.ContentId, member.EntityId, name, job);
            if (!seenKeys.Add(key))
                return false;

            members.Add(new PartyCooldownMemberSnapshot(
                key,
                member.EntityId,
                (ushort)member.World.RowId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                allianceGroup));
            return true;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownMemberReadFailed:{member.EntityId}:{ex.GetType().Name}");
            return false;
        }
    }

    private IPartyMember? TryCreatePartyMemberReference(int index)
    {
        try
        {
            var address = PartyList.GetPartyMemberAddress(index);
            if (address == IntPtr.Zero)
                return null;

            return PartyList.CreatePartyMemberReference(address);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownPartyMemberReadFailed:{index}:{ex.GetType().Name}");
            return null;
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
        if (PartyList.IsAlliance)
        {
            var added = this.AddPartyCooldownStatusSamplesFromAllianceGroupMembers(partyEntityIds);
            if (added > 0)
                return;

            this.AddPartyCooldownStatusSamplesFromPartySlots(partyEntityIds);
            this.AddPartyCooldownStatusSamplesFromFlatAllianceMembers(partyEntityIds);
            return;
        }

        this.AddPartyCooldownStatusSamplesFromPartySlots(partyEntityIds);
    }

    private int AddPartyCooldownStatusSamplesFromPartySlots(HashSet<uint> partyEntityIds)
    {
        var added = 0;
        var partySlotCount = Math.Clamp(PartyList.Length, 0, PartyMemberSlotCount);
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
        if (member.EntityId == 0)
            return false;

        if (!this.TryReadStatusSnapshots(member.Statuses, scope, member.EntityId))
            return true;

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(member.EntityId, status.SourceId, status.StatusId, status.RemainingTime, partyEntityIds);

        return true;
    }
}
