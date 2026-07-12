using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;

    private readonly record struct PartyCooldownRosterReadResult(
        IReadOnlyList<PartyCooldownMemberSnapshot> Members,
        IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers,
        PartyCooldownRosterSource Source,
        PartyCooldownRosterReadMode ReadMode,
        int PartyListLength,
        int AlliancePartyCount,
        int AllianceMemberCount,
        bool HasAllianceSource,
        bool UsedFlatAllianceFallback,
        int LocalAllianceGroupIndex,
        int RawLocalAllianceGroupIndex,
        int HudLocalAllianceGroupIndex,
        int ObservedHudLocalAllianceGroupIndex,
        bool UsedRetainedHudAllianceGroup,
        int CrossRealmGroupCount,
        int HudAllianceOrderCount);

    private readonly record struct CrossRealmAllianceHeader(
        int LocalGroupIndex,
        int RawLocalGroupIndex,
        int HudLocalGroupIndex,
        int ObservedHudGroupIndex,
        bool UsedRetainedHudGroup,
        int GroupCount);

    private readonly record struct HudRosterEntityOrder(
        IReadOnlyList<uint> EntityIds,
        int LocalPartyCount,
        int AllianceMemberCount);

    private static readonly TimeSpan PartyCooldownHudAllianceOrderRetention = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PartyCooldownHudAllianceGroupRetentionDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PartyCooldownRosterCacheDuration = TimeSpan.FromMilliseconds(100);
    private readonly PartyCooldownAllianceGroupRetention partyCooldownHudAllianceGroupRetention = new();
    private uint[] partyCooldownLastCompleteHudAllianceOrder = [];
    private DateTime partyCooldownHudAllianceOrderExpiresAtUtc = DateTime.MinValue;
    private PartyCooldownRosterReadResult? partyCooldownRosterCache;
    private DateTime partyCooldownRosterCacheBuiltAtUtc = DateTime.MinValue;
    private long partyCooldownRosterCacheHitCount;
    private long partyCooldownRosterCacheMissCount;

    private PartyCooldownRosterReadResult GetPartyCooldownRoster(bool forceRefresh = false)
    {
        var nowUtc = DateTime.UtcNow;
        if (!forceRefresh
            && this.partyCooldownRosterCache is { } cached
            && nowUtc >= this.partyCooldownRosterCacheBuiltAtUtc
            && nowUtc - this.partyCooldownRosterCacheBuiltAtUtc < PartyCooldownRosterCacheDuration)
        {
            this.partyCooldownRosterCacheHitCount++;
            return cached;
        }

        this.partyCooldownRosterCacheMissCount++;
        var partyListHeader = this.GetPartyListHeader();
        var partyListLength = partyListHeader.Length;
        var hasAllianceSource = partyListHeader.IsAlliance;
        if (!hasAllianceSource)
            this.partyCooldownHudAllianceGroupRetention.Reset();

        var crossRealmHeader = hasAllianceSource
            ? this.GetCrossRealmAllianceHeader()
            : new CrossRealmAllianceHeader(-1, -1, -1, -1, false, 0);
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
            groupedAllianceMemberCount = this.AddPartyCooldownCrossRealmMembers(
                members,
                seenKeys,
                seenEntityIds,
                crossRealmHeader);
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
                PartyCooldownMemberKey(PlayerState.ContentId, player.EntityId, name, job),
                player.EntityId,
                PlayerState.ContentId,
                (ushort)PlayerState.HomeWorld.RowId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId),
                string.Empty));
            readMode = PartyCooldownRosterReadMode.SoloFallback;
        }

        var hudRosterOrder = this.GetHudRosterEntityOrder();
        PartyCooldownMemberOrdering.ApplyInGameOrder(members, hudRosterOrder.EntityIds);
        var displayMembers = this.GetPartyCooldownDisplayMembers(members);
        var allianceMemberCount = CountPartyCooldownAllianceMembers(members);
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

        var result = new PartyCooldownRosterReadResult(
            members,
            displayMembers,
            source,
            readMode,
            partyListLength,
            hasAllianceSource ? AllianceGroupCount : 0,
            hasAllianceSource ? allianceMemberCount : 0,
            hasAllianceSource,
            usedFlatAllianceFallback,
            localAllianceGroupIndex,
            crossRealmHeader.RawLocalGroupIndex,
            crossRealmHeader.HudLocalGroupIndex,
            crossRealmHeader.ObservedHudGroupIndex,
            crossRealmHeader.UsedRetainedHudGroup,
            crossRealmHeader.GroupCount,
            hudRosterOrder.AllianceMemberCount);
        this.partyCooldownRosterCache = result;
        this.partyCooldownRosterCacheBuiltAtUtc = nowUtc;
        return result;
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
        var observedHudGroupIndex = this.ReadHudAllianceGroupIndex();
        var hudResolution = this.partyCooldownHudAllianceGroupRetention.Resolve(
            observedHudGroupIndex,
            isAlliance: true,
            DateTime.UtcNow,
            PartyCooldownHudAllianceGroupRetentionDuration);
        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            if (proxy is null)
            {
                return new CrossRealmAllianceHeader(
                    hudResolution.GroupIndex,
                    -1,
                    hudResolution.GroupIndex,
                    hudResolution.ObservedGroupIndex,
                    hudResolution.UsedRetainedValue,
                    0);
            }

            var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
            if (groupCount < 2)
            {
                return new CrossRealmAllianceHeader(
                    hudResolution.GroupIndex,
                    -1,
                    hudResolution.GroupIndex,
                    hudResolution.ObservedGroupIndex,
                    hudResolution.UsedRetainedValue,
                    groupCount);
            }

            var rawLocalGroupIndex = proxy->LocalPlayerGroupIndex < AllianceGroupCount
                ? proxy->LocalPlayerGroupIndex
                : -1;
            var hudLocalGroupIndex = hudResolution.GroupIndex;
            var memberLocalGroupIndex = GetCrossRealmLocalMemberGroupIndex(
                proxy,
                ObjectTable.LocalPlayer?.EntityId ?? 0,
                PlayerState.ContentId);
            var localGroupIndex = PartyCooldownAllianceGroups.ResolveLocalGroupIndex(
                hudLocalGroupIndex,
                memberLocalGroupIndex,
                rawLocalGroupIndex);
            return new CrossRealmAllianceHeader(
                localGroupIndex,
                rawLocalGroupIndex,
                hudLocalGroupIndex,
                hudResolution.ObservedGroupIndex,
                hudResolution.UsedRetainedValue,
                groupCount);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownCrossRealmHeaderReadFailed:{ex.GetType().Name}");
            return new CrossRealmAllianceHeader(
                hudResolution.GroupIndex,
                -1,
                hudResolution.GroupIndex,
                hudResolution.ObservedGroupIndex,
                hudResolution.UsedRetainedValue,
                0);
        }
    }

    private int ReadHudAllianceGroupIndex()
    {
        try
        {
            var addon = GameGui.GetAddonByName<AddonPartyList>("_PartyList");
            if (addon is null || addon->PartyTypeTextNode is null)
                return -1;

            return PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText(
                addon->PartyTypeTextNode->NodeText.ToString());
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownHudAllianceGroupReadFailed:{ex.GetType().Name}");
            return -1;
        }
    }

    private static int GetCrossRealmLocalMemberGroupIndex(
        InfoProxyCrossRealm* proxy,
        uint localEntityId,
        ulong localContentId)
    {
        if (proxy is null || (localEntityId == 0 && localContentId == 0))
            return -1;

        var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            var group = proxy->CrossRealmGroups[groupIndex];
            var memberCount = Math.Clamp((int)group.GroupMemberCount, 0, AllianceGroupMemberSlotCount);
            for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
            {
                var member = group.GroupMembers[memberIndex];
                if (PartyCooldownAllianceGroups.IsLocalMember(
                        member.EntityId,
                        member.ContentId,
                        localEntityId,
                        localContentId)
                    && member.GroupIndex < AllianceGroupCount)
                {
                    return member.GroupIndex;
                }
            }
        }

        return -1;
    }

    private int AddPartyCooldownCrossRealmMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        HashSet<uint> seenEntityIds,
        CrossRealmAllianceHeader header)
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

            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = proxy->CrossRealmGroups[groupIndex];
                var memberCount = Math.Clamp((int)group.GroupMemberCount, 0, AllianceGroupMemberSlotCount);
                var label = PartyCooldownAllianceGroups.ContainerGroupLabel(
                    groupIndex,
                    header.RawLocalGroupIndex,
                    header.LocalGroupIndex);
                for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
                {
                    var member = group.GroupMembers[memberIndex];
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
                member.ContentId,
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
                contentId,
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
            PlayerState.ContentId,
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
            PlayerState.ContentId,
            roster.AlliancePartyCount,
            roster.AllianceMemberCount,
            roster.HasAllianceSource,
            roster.UsedFlatAllianceFallback,
            roster.LocalAllianceGroupIndex,
            roster.RawLocalAllianceGroupIndex,
            roster.HudLocalAllianceGroupIndex,
            roster.ObservedHudLocalAllianceGroupIndex,
            roster.UsedRetainedHudAllianceGroup,
            roster.CrossRealmGroupCount,
            roster.HudAllianceOrderCount);

    private HudRosterEntityOrder GetHudRosterEntityOrder()
    {
        try
        {
            var agent = AgentHUD.Instance();
            if (agent is null)
                return new HudRosterEntityOrder(Array.Empty<uint>(), 0, 0);

            var count = Math.Clamp(agent->PartyMemberCount, 0, agent->PartyMembers.Length);
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

            Span<uint> entityOrder = stackalloc uint[50];
            var entityOrderCount = 0;
            for (var index = 0; index < orderedMemberCount; index++)
                AppendUniqueHudEntityId(entityOrder, ref entityOrderCount, orderedMembers[index].EntityId);

            var localPartyCount = entityOrderCount;
            // RaidMemberIds follows the display slots used by _AllianceList1 and _AllianceList2.
            foreach (var entityId in agent->RaidMemberIds)
                AppendUniqueHudEntityId(entityOrder, ref entityOrderCount, entityId);

            var allianceMemberCount = entityOrderCount - localPartyCount;
            var nowUtc = DateTime.UtcNow;
            if (allianceMemberCount >= AllianceGroupMemberSlotCount * (AllianceGroupCount - 1))
            {
                this.partyCooldownLastCompleteHudAllianceOrder = entityOrder[localPartyCount..entityOrderCount].ToArray();
                this.partyCooldownHudAllianceOrderExpiresAtUtc = nowUtc.Add(PartyCooldownHudAllianceOrderRetention);
            }
            else if (this.GetPartyListHeader().IsAlliance
                     && nowUtc < this.partyCooldownHudAllianceOrderExpiresAtUtc
                     && this.partyCooldownLastCompleteHudAllianceOrder.Length > 0)
            {
                foreach (var entityId in this.partyCooldownLastCompleteHudAllianceOrder)
                    AppendUniqueHudEntityId(entityOrder, ref entityOrderCount, entityId);

                allianceMemberCount = entityOrderCount - localPartyCount;
            }
            else if (!this.GetPartyListHeader().IsAlliance)
            {
                this.partyCooldownLastCompleteHudAllianceOrder = [];
                this.partyCooldownHudAllianceOrderExpiresAtUtc = DateTime.MinValue;
            }

            return new HudRosterEntityOrder(
                entityOrder[..entityOrderCount].ToArray(),
                localPartyCount,
                allianceMemberCount);
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownHudRosterOrderReadFailed:{ex.GetType().Name}");
            return new HudRosterEntityOrder(Array.Empty<uint>(), 0, 0);
        }
    }

    private static void AppendUniqueHudEntityId(Span<uint> destination, ref int count, uint entityId)
    {
        if (entityId is 0 or 0xE0000000 || count >= destination.Length)
            return;

        for (var index = 0; index < count; index++)
        {
            if (destination[index] == entityId)
                return;
        }

        destination[count++] = entityId;
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
        if (!this.TryReadPartyMemberStatusSnapshots(
                member,
                scope,
                out var entityId,
                out var snapshotOrigin))
            return entityId != 0;

        if (snapshotOrigin == StatusSnapshotOrigin.Fallback)
            this.partyCooldownStatusFallbackBatchCount++;
        else if (snapshotOrigin == StatusSnapshotOrigin.Live && entityId != 0)
            this.partyCooldownLiveStatusOwnerIdsBuffer.Add(entityId);

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(
                entityId,
                status.SourceId,
                status.StatusId,
                status.RemainingTime,
                partyEntityIds,
                fromPartyList: true,
                snapshotOrigin);

        return true;
    }

}
