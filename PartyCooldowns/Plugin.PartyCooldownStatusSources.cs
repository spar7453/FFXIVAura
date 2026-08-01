namespace FFXIVAura;

public sealed partial class Plugin
{
    private const int AllianceGroupCount = 3;
    private const int AllianceGroupMemberSlotCount = 8;
    private const int FlatAllianceMemberSlotCount = 20;

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
            var batch = this.statusSnapshotRuntime.ReadPartyMember(i, "partyCooldownPartyMember");
            if (this.AddPartyCooldownStatusSamplesFromPartyMember(batch, partyEntityIds))
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
            var batch = this.statusSnapshotRuntime.ReadAllianceMember(i, "partyCooldownFlatAllianceMember");
            if (this.AddPartyCooldownStatusSamplesFromPartyMember(batch, partyEntityIds))
                added++;
        }

        return added;
    }

    private bool AddPartyCooldownStatusSamplesFromPartyMember(
        StatusSnapshotBatch batch,
        HashSet<uint> partyEntityIds)
    {
        var entityId = batch.OwnerEntityId;
        if (!batch.Succeeded)
            return entityId != 0;

        if (batch.Origin == StatusSnapshotOrigin.Fallback)
            this.partyCooldownSignalDiagnostics.CountStatusFallbackBatch();
        else if (batch.Origin == StatusSnapshotOrigin.Live && entityId != 0)
            this.partyCooldownActiveStatusIndex.MarkLiveOwner(entityId);

        foreach (var status in batch.Snapshots)
            this.AddPartyCooldownStatusSample(
                entityId,
                status.SourceId,
                status.StatusId,
                status.RemainingTime,
                partyEntityIds,
                fromPartyList: true,
                batch.Origin);

        return true;
    }

}
