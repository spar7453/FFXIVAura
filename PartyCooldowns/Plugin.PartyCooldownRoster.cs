using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

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
        bool UsedFlatAllianceFallback,
        int LocalAllianceGroupIndex,
        int CrossRealmGroupCount);

    private readonly record struct CrossRealmAllianceHeader(int LocalGroupIndex, int GroupCount);

    private IReadOnlyList<PartyCooldownMemberSnapshot> GetPartyCooldownMembers()
        => this.GetPartyCooldownRoster().Members;

    private PartyCooldownRosterReadResult GetPartyCooldownRoster()
    {
        var partyListHeader = this.GetPartyListHeader();
        var partyListLength = partyListHeader.Length;
        var hasAllianceSource = partyListHeader.IsAlliance;
        var crossRealmHeader = hasAllianceSource
            ? this.GetCrossRealmAllianceHeader()
            : new CrossRealmAllianceHeader(-1, 0);
        var localAllianceGroupIndex = crossRealmHeader.LocalGroupIndex;
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
            groupedAllianceMemberCount = this.AddPartyCooldownCrossRealmMembers(members, seenKeys, seenEntityIds);
            if (groupedAllianceMemberCount < AllianceGroupCount * AllianceGroupMemberSlotCount)
            {
                partySlotMemberCount = this.AddPartyCooldownPartySlotMembers(
                    members,
                    seenKeys,
                    seenEntityIds,
                    PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localAllianceGroupIndex),
                    partyListHeader.PartySlotCount);
                flatAllianceMemberCount = this.AddPartyCooldownFlatAllianceMembers(
                    members,
                    seenKeys,
                    seenEntityIds,
                    localAllianceGroupIndex);
                usedFlatAllianceFallback = partySlotMemberCount > 0 || flatAllianceMemberCount > 0;
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

        var orderedMembers = hasAllianceSource
            ? PartyCooldownMemberOrdering.PreserveInGameOrder(members)
            : PartyCooldownMemberOrdering.PreserveInGameOrder(members, this.GetHudPartyMemberEntityOrder());
        var allianceMemberCount = CountPartyCooldownAllianceMembers(orderedMembers);
        var hasUsableAllianceSource = hasAllianceSource
                                      && (groupedAllianceMemberCount > 0 || flatAllianceMemberCount > 0 || partySlotMemberCount > 0);
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
                    ? PartyCooldownRosterReadMode.CrossRealmAllianceWithFlatFallback
                    : PartyCooldownRosterReadMode.CrossRealmAlliance;
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
            usedFlatAllianceFallback,
            localAllianceGroupIndex,
            crossRealmHeader.GroupCount);
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

    private CrossRealmAllianceHeader GetCrossRealmAllianceHeader()
    {
        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            if (proxy is null)
                return new CrossRealmAllianceHeader(-1, 0);

            var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
            if (groupCount < 2)
                return new CrossRealmAllianceHeader(-1, groupCount);

            var localGroupIndex = proxy->LocalPlayerGroupIndex < AllianceGroupCount
                ? proxy->LocalPlayerGroupIndex
                : -1;
            return new CrossRealmAllianceHeader(localGroupIndex, groupCount);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownCrossRealmHeaderReadFailed:{ex.GetType().Name}");
            return new CrossRealmAllianceHeader(-1, 0);
        }
    }

    private int AddPartyCooldownCrossRealmMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds)
    {
        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            if (proxy is null)
                return 0;

            var added = 0;
            var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
            if (groupCount < 2)
                return 0;

            Span<int> memberOrder = stackalloc int[AllianceGroupMemberSlotCount];
            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = proxy->CrossRealmGroups[groupIndex];
                var memberCount = Math.Clamp((int)group.GroupMemberCount, 0, AllianceGroupMemberSlotCount);
                var label = PartyCooldownAllianceGroups.GroupLabel(groupIndex);
                for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
                {
                    memberOrder[memberIndex] = memberIndex;
                    var insertIndex = memberIndex;
                    while (insertIndex > 0
                           && group.GroupMembers[memberOrder[insertIndex - 1]].MemberIndex
                           > group.GroupMembers[memberOrder[insertIndex]].MemberIndex)
                    {
                        (memberOrder[insertIndex - 1], memberOrder[insertIndex]) = (memberOrder[insertIndex], memberOrder[insertIndex - 1]);
                        insertIndex--;
                    }
                }

                for (var orderedIndex = 0; orderedIndex < memberCount; orderedIndex++)
                {
                    var member = group.GroupMembers[memberOrder[orderedIndex]];
                    if (this.TryAddPartyCooldownCrossRealmMemberSnapshot(members, seenKeys, seenEntityIds, member, label))
                        added++;
                }
            }

            return added;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownCrossRealmRosterReadFailed:{ex.GetType().Name}");
            return 0;
        }
    }

    private int AddPartyCooldownFlatAllianceMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        int localGroupIndex)
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
                    PartyCooldownAllianceGroups.AllianceSlotLabel(i, localGroupIndex)))
            {
                added++;
            }
        }

        return added;
    }

    private bool TryAddPartyCooldownCrossRealmMemberSnapshot(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        CrossRealmMember member,
        string allianceGroup)
    {
        var entityId = member.EntityId;
        try
        {
            var classJobId = (uint)member.ClassJobId;
            var overrideName = member.NameOverride.HasValue ? member.NameOverride.ToString() : string.Empty;
            var name = string.IsNullOrWhiteSpace(overrideName) ? member.NameString : overrideName;
            if (classJobId == 0 || string.IsNullOrWhiteSpace(name))
                return false;

            var job = JobInfo.Code(classJobId);
            var key = PartyCooldownMemberKey(member.ContentId, entityId, name, job);
            if (entityId != 0 && !seenEntityIds.Add(entityId))
                return false;

            if (!seenKeys.Add(key))
            {
                if (entityId != 0)
                    seenEntityIds.Remove(entityId);
                return false;
            }

            members.Add(new PartyCooldownMemberSnapshot(
                key,
                entityId,
                (ushort)Math.Max(0, (int)member.HomeWorld),
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                allianceGroup));
            return true;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownCrossRealmMemberReadFailed:{entityId}:{ex.GetType().Name}");
            return false;
        }
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
            if (classJobId == 0 || string.IsNullOrWhiteSpace(name))
                return false;

            var contentId = member.ContentId;
            var worldId = (ushort)member.World.RowId;
            var key = PartyCooldownMemberKey(contentId, entityId, name, job);
            if (!seenEntityIds.Add(entityId))
                return false;

            var snapshot = new PartyCooldownMemberSnapshot(
                key,
                entityId,
                worldId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                allianceGroup);
            if (!seenKeys.Add(key))
            {
                var existingIndex = members.FindIndex(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
                if (existingIndex >= 0 && members[existingIndex].EntityId == 0)
                {
                    var existing = members[existingIndex];
                    members[existingIndex] = snapshot with
                    {
                        AllianceGroup = string.IsNullOrWhiteSpace(existing.AllianceGroup)
                            ? snapshot.AllianceGroup
                            : existing.AllianceGroup,
                    };
                    return true;
                }

                seenEntityIds.Remove(entityId);
                return false;
            }

            members.Add(snapshot);
            return true;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownMemberReadFailed:{entityId}:{ex.GetType().Name}");
            return false;
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

    private PartyCooldownRosterDiagnostics CreatePartyCooldownRosterDiagnostics(
        PartyCooldownRosterReadResult roster,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers)
        => PartyCooldownRoster.CreateDiagnostics(
            roster.Source,
            roster.ReadMode,
            roster.PartyListLength,
            roster.Members,
            displayMembers,
            ObjectTable.LocalPlayer?.EntityId ?? 0,
            roster.AlliancePartyCount,
            roster.AllianceMemberCount,
            roster.HasAllianceSource,
            roster.UsedFlatAllianceFallback,
            roster.LocalAllianceGroupIndex,
            roster.CrossRealmGroupCount);

    private IReadOnlyList<uint> GetHudPartyMemberEntityOrder()
    {
        try
        {
            var agent = AgentHUD.Instance();
            if (agent is null)
                return Array.Empty<uint>();

            var count = Math.Clamp(agent->PartyMemberCount, 0, agent->PartyMembers.Length);
            if (count == 0)
                return Array.Empty<uint>();

            Span<(byte DisplayIndex, uint EntityId)> orderedMembers = stackalloc (byte, uint)[10];
            var orderedMemberCount = 0;
            for (var index = 0; index < count; index++)
            {
                var member = agent->PartyMembers[index];
                if (member.EntityId == 0)
                    continue;

                orderedMembers[orderedMemberCount] = (member.Index, member.EntityId);
                var insertIndex = orderedMemberCount;
                while (insertIndex > 0
                       && orderedMembers[insertIndex - 1].DisplayIndex > orderedMembers[insertIndex].DisplayIndex)
                {
                    (orderedMembers[insertIndex - 1], orderedMembers[insertIndex]) = (orderedMembers[insertIndex], orderedMembers[insertIndex - 1]);
                    insertIndex--;
                }

                orderedMemberCount++;
            }

            var result = new uint[orderedMemberCount];
            for (var index = 0; index < orderedMemberCount; index++)
                result[index] = orderedMembers[index].EntityId;

            return result;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownHudPartyOrderReadFailed:{ex.GetType().Name}");
            return Array.Empty<uint>();
        }
    }

    private void AddPartyCooldownStatusSamplesFromPartyList(HashSet<uint> partyEntityIds)
    {
        var partyListHeader = this.GetPartyListHeader();
        if (partyListHeader.IsAlliance)
        {
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
