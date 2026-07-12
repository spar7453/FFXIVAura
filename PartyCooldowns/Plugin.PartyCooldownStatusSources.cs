namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;

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
            this.partyCooldownActiveStatusIndex.MarkLiveOwner(entityId);

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
