namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private PartyCooldownDisplayItem BuildPartyCooldownDisplayItem(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        uint level,
        DateTime now)
    {
        var runtimeKey = this.CreatePartyCooldownRuntimeKey(member.Key, definition);
        var runtime = this.partyCooldownRuntimeStore.GetOrCreateState(runtimeKey);

        var maxCharges = this.GetPartyCooldownMaxCharges(definition, level);
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
        this.NotePartyCooldownTimerDecision(
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
            : chargeSnapshot.CurrentCharges == 0 && chargeSnapshot.NextChargeRemaining > 0.05f
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
            IconDisplayCondition.InCombat => this.IsInCombat() ? item : null,
            IconDisplayCondition.OutOfCombat => !this.IsInCombat() ? item : null,
            IconDisplayCondition.CoolingOnly => item.State is PartyCooldownDisplayState.Active or PartyCooldownDisplayState.Cooldown
                                                || item.CooldownRemaining > 0.05f
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
            this.ResolvePartyCooldownStatusIds(definition),
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
        if (ObjectTable.LocalPlayer is IBattleChara player && (partyListHeader.Length == 0 || memberEntityIds.Contains(player.EntityId)))
            this.AddPartyCooldownStatusSamplesFromCharacter(player, memberEntityIds);

        try
        {
            foreach (var gameObject in ObjectTable)
            {
                if (gameObject is IBattleChara battleChara)
                    this.AddPartyCooldownStatusSamplesFromCharacter(battleChara, memberEntityIds);
            }
        }
        catch (Exception ex)
        {
            this.SetBugDiagnosticEvent($"partyCooldownObjectTableReadFailed:{ex.GetType().Name}");
        }

        if (TargetManager.Target is IBattleChara target)
            this.AddPartyCooldownStatusSamplesFromCharacter(target, memberEntityIds);

        this.partyCooldownActiveStatusIndex.CompleteRefresh(memberEntityIds);
    }

    private void AddPartyCooldownStatusSamplesFromCharacter(IBattleChara character, HashSet<uint> partyEntityIds)
    {
        if (!this.TryReadBattleCharaStatusSnapshots(
                character,
                "partyCooldownCharacter",
                out var entityId,
                out var snapshotOrigin))
            return;

        if (snapshotOrigin == StatusSnapshotOrigin.Fallback)
            this.partyCooldownStatusFallbackBatchCount++;
        else if (snapshotOrigin == StatusSnapshotOrigin.Live && partyEntityIds.Contains(entityId))
            this.partyCooldownActiveStatusIndex.MarkLiveOwner(entityId);

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(
                entityId,
                status.SourceId,
                status.StatusId,
                status.RemainingTime,
                partyEntityIds,
                fromPartyList: false,
                snapshotOrigin);
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

    private void NotePartyCooldownTimerDecision(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        float rawRemaining,
        bool canConfirmRefresh,
        ObservedStatusTimerResult result)
    {
        switch (result.Decision)
        {
            case ObservedStatusTimerDecision.Refreshed:
                this.partyCooldownTimerRefreshAcceptedCount++;
                break;
            case ObservedStatusTimerDecision.StalePositiveSuppressed:
                this.partyCooldownTimerStalePositiveSuppressedCount++;
                break;
            default:
                return;
        }

        this.partyCooldownTimerLastDecision = FormattableString.Invariant(
            $"{result.Decision}|member={member.Key}|action={definition.ActionId}|raw={rawRemaining:0.###}|remaining={result.Remaining:0.###}|live={canConfirmRefresh}");
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
            ? this.GetGameObjectOwnerEntityId(entityId)
            : 0;

    private IReadOnlyList<uint> ResolvePartyCooldownStatusIds(PartyCooldownDefinition definition)
    {
        if (this.partyCooldownStatusIdsByActionId.TryGetValue(definition.ActionId, out var cached))
            return cached;

        var action = this.GetActionRow(definition.ActionId);
        cached = PartyCooldownStatusResolver.Resolve(definition.StatusIds, action?.StatusGainSelf.RowId ?? 0);
        this.partyCooldownStatusIdsByActionId[definition.ActionId] = cached;
        return cached;
    }

    private uint GetPartyCooldownMaxCharges(PartyCooldownDefinition definition, uint level)
    {
        var effectiveLevel = Math.Max(1u, level);
        var cacheKey = ((ulong)effectiveLevel << 32) | definition.ActionId;
        if (this.partyCooldownMaxChargesByActionAndLevel.TryGetValue(cacheKey, out var cached))
            return cached;

        var resolved = Math.Max(1u, definition.Charges);
        try
        {
            var maxCharges = (uint)ActionManager.GetMaxCharges(definition.ActionId, effectiveLevel);
            if (maxCharges > 0)
                resolved = maxCharges;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read party cooldown charges for {definition.ActionId} at level {level}.");
        }

        this.partyCooldownMaxChargesByActionAndLevel[cacheKey] = resolved;
        return resolved;
    }

}
