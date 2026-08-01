using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace FFXIVAura;

internal sealed unsafe class DalamudPartyRosterReader : IPartyRosterReader
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;
    private static readonly TimeSpan HudAllianceOrderRetention = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HudAllianceGroupRetention = TimeSpan.FromSeconds(3);

    private readonly IPartyList partyList;
    private readonly IObjectTable objectTable;
    private readonly IPlayerState playerState;
    private readonly IGameGui gameGui;
    private readonly Action<string> reportDiagnostic;
    private readonly PartyCooldownAllianceGroupRetention hudAllianceGroupRetention = new();
    private readonly PartyCooldownHudRosterOrderTracker hudRosterOrderTracker = new();

    public DalamudPartyRosterReader(
        IPartyList partyList,
        IObjectTable objectTable,
        IPlayerState playerState,
        IGameGui gameGui,
        Action<string> reportDiagnostic)
    {
        this.partyList = partyList;
        this.objectTable = objectTable;
        this.playerState = playerState;
        this.gameGui = gameGui;
        this.reportDiagnostic = reportDiagnostic;
    }

    public PartyListHeader ReadPartyListHeader()
    {
        try
        {
            return new PartyListHeader(
                Math.Max(0, this.partyList.Length),
                this.partyList.IsAlliance,
                (int)this.partyList.PartyId);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyListHeaderReadFailed:{ex.GetType().Name}");
            return default;
        }
    }

    public PartyCooldownRosterSourceRead ReadSource()
    {
        var partyListHeader = this.ReadPartyListHeader();
        var localIdentity = this.ReadLocalIdentity();
        if (!partyListHeader.IsAlliance)
        {
            this.hudAllianceGroupRetention.Reset();
            return new PartyCooldownRosterSourceRead(
                partyListHeader,
                PartyCooldownCrossRealmHeader.Empty,
                Array.Empty<PartyCooldownCrossRealmMemberRead>(),
                localIdentity);
        }

        var hudResolution = this.ResolveHudAllianceGroup();
        return new PartyCooldownRosterSourceRead(
            partyListHeader,
            this.ReadCrossRealmHeader(hudResolution, localIdentity),
            this.ReadCrossRealmMembers(),
            localIdentity);
    }

    public IReadOnlyList<PartyCooldownMemberSnapshot> ReadPartySlotMembers(int partySlotCount)
    {
        var count = Math.Clamp(partySlotCount, 0, 8);
        var members = new List<PartyCooldownMemberSnapshot>(count);
        for (var index = 0; index < count; index++)
        {
            var member = this.TryCreatePartyMemberReference(index, "partyMemberReferenceReadFailed");
            if (member is not null && this.TryReadPartyMember(member) is { } snapshot)
                members.Add(snapshot);
        }

        return members;
    }

    public IReadOnlyList<PartyCooldownFlatAllianceMemberRead> ReadFlatAllianceMembers()
    {
        var members = new List<PartyCooldownFlatAllianceMemberRead>(FlatAllianceMemberSlotCount);
        for (var index = 0; index < FlatAllianceMemberSlotCount; index++)
        {
            var member = this.TryCreateAllianceMemberReference(index);
            if (member is not null && this.TryReadPartyMember(member) is { } snapshot)
                members.Add(new PartyCooldownFlatAllianceMemberRead(snapshot, index));
        }

        return members;
    }

    public PartyCooldownMemberSnapshot? ReadLocalPlayer()
    {
        try
        {
            if (this.objectTable.LocalPlayer is not IBattleChara player)
                return null;

            return PartyCooldownMemberSnapshotFactory.Create(
                player.EntityId,
                this.playerState.ContentId,
                (ushort)this.playerState.HomeWorld.RowId,
                player.Name.ToString(),
                this.playerState.ClassJob.RowId,
                this.playerState.EffectiveLevel > 0
                    ? (uint)this.playerState.EffectiveLevel
                    : (uint)this.playerState.Level);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownLocalPlayerReadFailed:{ex.GetType().Name}");
            return null;
        }
    }

    public PartyCooldownHudRosterOrder ReadHudRosterOrder(bool isAlliance)
    {
        try
        {
            var agent = AgentHUD.Instance();
            if (agent is null)
                return PartyCooldownHudRosterOrder.Empty;

            Span<PartyCooldownHudPartyMember> partyMembers = stackalloc PartyCooldownHudPartyMember[PartyCooldownHudRosterOrderTracker.MaxLocalPartyMemberCount];
            var partyMemberCount = ReadHudPartyMembers(agent, partyMembers);
            Span<uint> raidMemberIds = stackalloc uint[PartyCooldownHudRosterOrderTracker.MaxEntityOrderCount];
            var raidMemberCount = ReadHudRaidMemberIds(agent, raidMemberIds);
            return this.hudRosterOrderTracker.Resolve(
                partyMembers[..partyMemberCount],
                raidMemberIds[..raidMemberCount],
                isAlliance,
                DateTime.UtcNow,
                HudAllianceOrderRetention,
                AllianceGroupMemberSlotCount * (AllianceGroupCount - 1));
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownHudRosterOrderReadFailed:{ex.GetType().Name}");
            return PartyCooldownHudRosterOrder.Empty;
        }
    }

    public void Reset()
    {
        this.hudAllianceGroupRetention.Reset();
        this.hudRosterOrderTracker.Reset();
    }

    private PartyRosterLocalIdentity ReadLocalIdentity()
    {
        try
        {
            return new PartyRosterLocalIdentity(
                this.objectTable.LocalPlayer?.EntityId ?? 0,
                this.playerState.ContentId);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownLocalIdentityReadFailed:{ex.GetType().Name}");
            return default;
        }
    }

    private PartyCooldownAllianceGroupResolution ResolveHudAllianceGroup()
        => this.hudAllianceGroupRetention.Resolve(
            this.ReadHudAllianceGroupIndex(),
            isAlliance: true,
            DateTime.UtcNow,
            HudAllianceGroupRetention);

    private PartyCooldownCrossRealmHeader ReadCrossRealmHeader(
        PartyCooldownAllianceGroupResolution hudResolution,
        PartyRosterLocalIdentity localIdentity)
    {
        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            if (proxy is null)
                return PartyCooldownCrossRealmHeaderResolver.Resolve(hudResolution, snapshot: null);

            var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
            var rawLocalGroupIndex = groupCount >= 2 && proxy->LocalPlayerGroupIndex < AllianceGroupCount
                ? proxy->LocalPlayerGroupIndex
                : -1;
            var memberLocalGroupIndex = groupCount >= 2
                ? GetCrossRealmLocalMemberGroupIndex(
                    proxy,
                    localIdentity.EntityId,
                    localIdentity.ContentId)
                : -1;
            return PartyCooldownCrossRealmHeaderResolver.Resolve(
                hudResolution,
                new PartyCooldownCrossRealmSnapshot(
                    groupCount,
                    rawLocalGroupIndex,
                    memberLocalGroupIndex));
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownCrossRealmHeaderReadFailed:{ex.GetType().Name}");
            return PartyCooldownCrossRealmHeaderResolver.Resolve(hudResolution, snapshot: null);
        }
    }

    private IReadOnlyList<PartyCooldownCrossRealmMemberRead> ReadCrossRealmMembers()
    {
        var members = new List<PartyCooldownCrossRealmMemberRead>(
            AllianceGroupCount * AllianceGroupMemberSlotCount);
        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            if (proxy is null)
                return members;

            var groupCount = Math.Clamp((int)proxy->GroupCount, 0, AllianceGroupCount);
            if (groupCount < 2)
                return members;

            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = proxy->CrossRealmGroups[groupIndex];
                var memberCount = Math.Clamp((int)group.GroupMemberCount, 0, AllianceGroupMemberSlotCount);
                for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
                {
                    var member = group.GroupMembers[memberIndex];
                    if (this.TryReadCrossRealmMember(member) is { } snapshot)
                        members.Add(new PartyCooldownCrossRealmMemberRead(snapshot, groupIndex));
                }
            }
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownCrossRealmRosterReadFailed:{ex.GetType().Name}");
        }

        return members;
    }

    private PartyCooldownMemberSnapshot? TryReadCrossRealmMember(CrossRealmMember member)
    {
        var entityId = member.EntityId;
        try
        {
            var classJobId = (uint)member.ClassJobId;
            var overrideName = member.NameOverride.HasValue ? member.NameOverride.ToString() : string.Empty;
            var name = string.IsNullOrWhiteSpace(overrideName) ? member.NameString : overrideName;
            if (classJobId == 0 || string.IsNullOrWhiteSpace(name))
                return null;

            return PartyCooldownMemberSnapshotFactory.Create(
                entityId,
                member.ContentId,
                (ushort)Math.Max(0, (int)member.HomeWorld),
                name,
                classJobId,
                member.Level);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownCrossRealmMemberReadFailed:{entityId}:{ex.GetType().Name}");
            return null;
        }
    }

    private IPartyMember? TryCreatePartyMemberReference(int index, string diagnosticPrefix)
    {
        try
        {
            var address = this.partyList.GetPartyMemberAddress(index);
            return address == IntPtr.Zero
                ? null
                : this.partyList.CreatePartyMemberReference(address);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"{diagnosticPrefix}:{index}:{ex.GetType().Name}");
            return null;
        }
    }

    private IPartyMember? TryCreateAllianceMemberReference(int index)
    {
        try
        {
            var address = this.partyList.GetAllianceMemberAddress(index);
            return address == IntPtr.Zero
                ? null
                : this.partyList.CreateAllianceMemberReference(address);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownFlatAllianceMemberReadFailed:{index}:{ex.GetType().Name}");
            return null;
        }
    }

    private PartyCooldownMemberSnapshot? TryReadPartyMember(IPartyMember member)
    {
        var entityId = 0u;
        try
        {
            entityId = member.EntityId;
            var classJobId = member.ClassJob.RowId;
            var name = member.Name.ToString();
            if (entityId == 0 || classJobId == 0 || string.IsNullOrWhiteSpace(name))
                return null;

            return PartyCooldownMemberSnapshotFactory.Create(
                entityId,
                member.ContentId,
                (ushort)member.World.RowId,
                name,
                classJobId,
                member.Level);
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownMemberReadFailed:{entityId}:{ex.GetType().Name}");
            return null;
        }
    }

    private int ReadHudAllianceGroupIndex()
    {
        try
        {
            var addon = this.gameGui.GetAddonByName<AddonPartyList>("_PartyList");
            if (addon is null || addon->PartyTypeTextNode is null)
                return -1;

            return PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText(
                addon->PartyTypeTextNode->NodeText.ToString());
        }
        catch (Exception ex)
        {
            this.reportDiagnostic($"partyCooldownHudAllianceGroupReadFailed:{ex.GetType().Name}");
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

    private static int ReadHudPartyMembers(
        AgentHUD* agent,
        Span<PartyCooldownHudPartyMember> destination)
    {
        var count = Math.Clamp(
            agent->PartyMemberCount,
            0,
            Math.Min(agent->PartyMembers.Length, destination.Length));
        for (var index = 0; index < count; index++)
        {
            var member = agent->PartyMembers[index];
            destination[index] = new PartyCooldownHudPartyMember(member.Index, member.EntityId);
        }

        return count;
    }

    private static int ReadHudRaidMemberIds(AgentHUD* agent, Span<uint> destination)
    {
        var count = 0;
        foreach (var entityId in agent->RaidMemberIds)
        {
            if (count >= destination.Length)
                break;

            destination[count++] = entityId;
        }

        return count;
    }
}
