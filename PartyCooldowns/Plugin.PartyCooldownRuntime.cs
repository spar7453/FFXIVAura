namespace FFXIVAura;

public sealed partial class Plugin
{
    private PartyCooldownDisplayItem BuildPartyCooldownDisplayItem(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        uint level,
        DateTime now)
    {
        var runtimeKey = this.partyCooldownCatalog.CreateRuntimeKey(member.Key, definition);
        var runtime = this.partyCooldownRuntimeStore.GetOrCreateState(runtimeKey);

        var maxCharges = this.partyCooldownCatalog.GetMaxCharges(definition, level);
        var observedActiveRemaining = this.GetPartyCooldownActiveRemaining(
            member.EntityId,
            definition,
            now,
            out var activeObservation,
            out var canConfirmRefresh);
        var activeTimerResult = PartyCooldownActiveTimerTracker.UpdateDetailed(
            runtime,
            now,
            observedActiveRemaining,
            definition.Duration,
            activeObservation,
            canConfirmRefresh);
        this.partyCooldownSignalDiagnostics.RecordTimerDecision(
            member,
            definition,
            observedActiveRemaining,
            canConfirmRefresh,
            activeTimerResult);
        var activeRemaining = activeTimerResult.Remaining;
        PartyCooldownChargeTracker.ObserveActiveStatus(
            runtime,
            now,
            activeRemaining,
            definition.Duration,
            definition.Cooldown,
            maxCharges,
            PartyCooldownLogDedupeWindow,
            PartyCooldownCrossSignalDedupeWindow,
            PartyCooldownStatusMissingGrace);
        var chargeSnapshot = PartyCooldownChargeTracker.GetSnapshot(
            runtime,
            now,
            definition.Cooldown,
            maxCharges);
        var state = activeRemaining > 0f
            ? PartyCooldownDisplayState.Active
            : chargeSnapshot.CurrentCharges == 0 && chargeSnapshot.NextChargeRemaining > TimerDisplayThresholds.MinimumActiveSeconds
                ? PartyCooldownDisplayState.Cooldown
                : PartyCooldownDisplayState.Ready;
        return new PartyCooldownDisplayItem(
            definition,
            state,
            activeRemaining,
            chargeSnapshot.NextChargeRemaining,
            chargeSnapshot.ChargeCooldownTotal,
            chargeSnapshot.CurrentCharges,
            chargeSnapshot.MaxCharges);
    }

    private PartyCooldownDisplayItem? FilterPartyCooldownDisplayCondition(IconWindowConfig iconWindow, PartyCooldownDisplayItem item)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.playerFrameContext.IsInCombat ? item : null,
            IconDisplayCondition.OutOfCombat => !this.playerFrameContext.IsInCombat ? item : null,
            IconDisplayCondition.CoolingOnly => item.State is PartyCooldownDisplayState.Active or PartyCooldownDisplayState.Cooldown
                                                || item.CooldownRemaining > TimerDisplayThresholds.MinimumActiveSeconds
                ? item
                : null,
            IconDisplayCondition.ReadyOnly => item.State == PartyCooldownDisplayState.Ready ? item : null,
            _ => item,
        };
    }

    private float GetPartyCooldownActiveRemaining(
        uint sourceEntityId,
        PartyCooldownDefinition definition,
        DateTime nowUtc,
        out ObservedStatusObservation observation,
        out bool canConfirmRefresh)
    {
        var result = this.partyCooldownActiveStatusIndex.GetObservation(
            sourceEntityId,
            this.partyCooldownCatalog.ResolveStatusIds(definition),
            nowUtc);
        observation = result.Observation;
        canConfirmRefresh = result.CanConfirmRefresh;
        return result.Remaining;
    }

    private void RebuildPartyCooldownActiveStatusIndex(IReadOnlyList<PartyCooldownMemberSnapshot> members)
    {
        var nowUtc = DateTime.UtcNow;
        var rosterHash = PartyCooldownRoster.ComputeMemberIdentityHash(members);
        if (!this.partyCooldownActiveStatusIndex.BeginRefresh(rosterHash, nowUtc))
        {
            this.performanceStats.CountPartyStatusCacheHit();
            return;
        }

        this.performanceStats.CountPartyStatusScan();
        var memberEntityIds = this.partyCooldownMemberEntityIdsBuffer;
        memberEntityIds.Clear();
        memberEntityIds.EnsureCapacity(members.Count);
        foreach (var member in members)
        {
            if (IsValidPartyCooldownEntityId(member.EntityId))
                memberEntityIds.Add(member.EntityId);
        }

        if (memberEntityIds.Count == 0)
        {
            this.partyCooldownActiveStatusIndex.CompleteRefresh(memberEntityIds);
            return;
        }

        this.AddPartyCooldownStatusSamplesFromPartyList(memberEntityIds);

        var partyListHeader = this.GetPartyListHeader();
        var localPlayerBatch = this.statusSnapshotRuntime.ReadLocalPlayer("partyCooldownCharacter");
        if (localPlayerBatch.OwnerEntityId != 0
            && (partyListHeader.Length == 0 || memberEntityIds.Contains(localPlayerBatch.OwnerEntityId)))
        {
            this.AddPartyCooldownStatusSamplesFromBatch(localPlayerBatch, memberEntityIds);
        }

        this.statusSnapshotRuntime.VisitBattleCharacters(
            "partyCooldownCharacter",
            batch => this.AddPartyCooldownStatusSamplesFromBatch(batch, memberEntityIds));

        this.AddPartyCooldownStatusSamplesFromBatch(
            this.statusSnapshotRuntime.ReadTarget("partyCooldownCharacter"),
            memberEntityIds);

        this.partyCooldownActiveStatusIndex.CompleteRefresh(memberEntityIds);
    }

    private void AddPartyCooldownStatusSamplesFromBatch(
        StatusSnapshotBatch batch,
        HashSet<uint> partyEntityIds)
    {
        if (!batch.Succeeded)
            return;

        var entityId = batch.OwnerEntityId;
        if (batch.Origin == StatusSnapshotOrigin.Fallback)
            this.partyCooldownSignalDiagnostics.CountStatusFallbackBatch();
        else if (batch.Origin == StatusSnapshotOrigin.Live && partyEntityIds.Contains(entityId))
            this.partyCooldownActiveStatusIndex.MarkLiveOwner(entityId);

        foreach (var status in batch.Snapshots)
            this.AddPartyCooldownStatusSample(
                entityId,
                status.SourceId,
                status.StatusId,
                status.RemainingTime,
                partyEntityIds,
                fromPartyList: false,
                batch.Origin);
    }

    private void AddPartyCooldownStatusSample(
        uint ownerEntityId,
        uint sourceEntityId,
        uint statusId,
        float remaining,
        HashSet<uint> partyEntityIds,
        bool fromPartyList,
        StatusSnapshotOrigin snapshotOrigin)
    {
        if (statusId == 0 || remaining <= 0f)
            return;

        var normalizedSourceId = this.ResolvePartyCooldownStatusSourceEntityId(
            ownerEntityId,
            sourceEntityId,
            partyEntityIds);
        if (normalizedSourceId == 0)
            return;

        this.partyCooldownActiveStatusIndex.AddSample(
            normalizedSourceId,
            statusId,
            remaining,
            fromPartyList,
            ownerEntityId == normalizedSourceId,
            snapshotOrigin);
    }

    private uint ResolvePartyCooldownStatusSourceEntityId(
        uint ownerEntityId,
        uint sourceEntityId,
        HashSet<uint> partyEntityIds)
        => PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId,
            sourceEntityId,
            partyEntityIds,
            this.GetPartyOwnedObjectOwnerEntityId);

    private uint GetPartyOwnedObjectOwnerEntityId(uint entityId)
        => PartyCooldownOwnerResolver.IsValidEntityId(entityId)
            ? this.statusSnapshotRuntime.GetOwnerEntityId(entityId)
            : 0;

}
