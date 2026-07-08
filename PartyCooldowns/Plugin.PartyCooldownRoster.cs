namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AllianceMemberSlotCount = 16;

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
        var capacity = Math.Max(partyListLength + (hasAllianceSource ? AllianceMemberSlotCount : 0), 1);
        var members = new List<PartyCooldownMemberSnapshot>(capacity);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        this.AddPartyCooldownPartyListMembers(members, seenKeys);
        var allianceMemberCount = hasAllianceSource
            ? this.AddPartyCooldownAllianceMembers(members, seenKeys)
            : 0;

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
                JobInfo.IconId(classJobId)));
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

    private int AddPartyCooldownPartyListMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys)
    {
        var added = 0;
        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, member))
                added++;
        }

        return added;
    }

    private int AddPartyCooldownAllianceMembers(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys)
    {
        var added = 0;
        for (var i = 0; i < AllianceMemberSlotCount; i++)
        {
            var member = this.TryCreateAllianceMemberReference(i);
            if (member is not null && this.TryAddPartyCooldownMemberSnapshot(members, seenKeys, member))
                added++;
        }

        return added;
    }

    private bool TryAddPartyCooldownMemberSnapshot(
        List<PartyCooldownMemberSnapshot> members,
        HashSet<string> seenKeys,
        IPartyMember member)
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
                JobInfo.IconId(classJobId)));
            return true;
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownMemberReadFailed:{member.EntityId}:{ex.GetType().Name}");
            return false;
        }
    }

    private IPartyMember? TryCreateAllianceMemberReference(int index)
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
            this.SetBugDiagnosticEvent($"partyCooldownAllianceMemberReadFailed:{index}:{ex.GetType().Name}");
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
        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is not null)
                this.AddPartyCooldownStatusSamplesFromPartyMember(member, partyEntityIds, "partyCooldownPartyMember");
        }
    }

    private void AddPartyCooldownStatusSamplesFromPartyMember(
        IPartyMember member,
        HashSet<uint> partyEntityIds,
        string scope)
    {
        if (member.EntityId == 0)
            return;

        if (!this.TryReadStatusSnapshots(member.Statuses, scope, member.EntityId))
            return;

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(member.EntityId, status.SourceId, status.StatusId, status.RemainingTime, partyEntityIds);
    }
}
