namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AuraState> GetDisplayAuras(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            yield break;

        foreach (var group in this.GetTrackedAuraGroups(iconWindow))
        {
            var aura = this.GetAuraState(iconWindow, group);
            if (!this.ShouldDisplayAura(aura, iconWindow))
                continue;

            if (!aura.Present && !ShouldShowMissingAura(iconWindow))
                continue;

            yield return aura;
        }
    }

    private static bool ShouldShowMissingAura(IconWindowConfig iconWindow)
        => iconWindow.ShowMissingAuras || iconWindow.DisplayCondition == IconDisplayCondition.ReadyOnly;

    private IReadOnlyList<AuraStatusGroup> GetTrackedAuraGroups(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            return Array.Empty<AuraStatusGroup>();

        var statusIndexBuilt = this.EnsureStatusIdentityIndex();
        var statusIndexGeneration = this.statusIdentityIndexState.Generation;
        if (statusIndexBuilt
            && this.trackedAuraGroupCache.TryGetValue(iconWindow.Id, out var cached)
            && cached.Matches(
                iconWindow.TrackedStatusIds,
                iconWindow.ExactTrackedStatusIds,
                statusIndexGeneration))
        {
            return cached.Groups;
        }

        var groups = AuraStatusGroups.Build(
            iconWindow.TrackedStatusIds,
            iconWindow.ExactTrackedStatusIds,
            this.GetStatusDefinition,
            key => statusIndexBuilt
                ? this.GetStatusIdsByGroup(key)
                : iconWindow.TrackedStatusIds
                    .Where(statusId => !iconWindow.ExactTrackedStatusIds.Contains(statusId)
                                       && this.GetStatusDefinition(statusId).GroupKey == key)
                    .ToArray());
        if (statusIndexBuilt)
        {
            this.trackedAuraGroupCache[iconWindow.Id] = new AuraStatusGroupCacheEntry(
                iconWindow.TrackedStatusIds.ToArray(),
                iconWindow.ExactTrackedStatusIds.ToArray(),
                statusIndexGeneration,
                groups);
        }
        else
        {
            this.trackedAuraGroupCache.Remove(iconWindow.Id);
        }

        return groups;
    }

    private void InvalidateTrackedAuraGroups(IconWindowConfig iconWindow)
        => this.trackedAuraGroupCache.Remove(iconWindow.Id);

    private bool ShouldDisplayAura(AuraState aura, IconWindowConfig iconWindow)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat(),
            IconDisplayCondition.CoolingOnly => aura.Present,
            IconDisplayCondition.ReadyOnly => !aura.Present,
            _ => true,
        };
    }

    private AuraState GetAuraState(IconWindowConfig iconWindow, AuraStatusGroup group)
    {
        var builder = new AuraStatusGroupStateBuilder(group);
        foreach (var statusId in group.MemberStatusIds)
        {
            var active = this.FindAuraStatus(iconWindow, statusId);
            if (active is null)
                continue;

            var definition = this.GetStatusDefinition(statusId);
            builder.Add(new AuraStatusGroupActiveState(
                statusId,
                definition.IconId,
                active.Value.Remaining,
                active.Value.Param,
                active.Value.Count,
                active.Value.OwnCount,
                active.Value.FromSelf));
        }

        var state = builder.Build();
        if (iconWindow.Role != IconWindowRole.PartyBuffs
            || group.IsExact
            || !group.Key.IsValid
            || !this.GetPartyAuraGroupFrameIndex(iconWindow.PartyAurasOwnOnly).TryGetValue(group.Key, out var groupCount))
        {
            return state;
        }

        return state with
        {
            Count = groupCount.Count,
            OwnCount = groupCount.OwnCount,
        };
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindAuraStatus(
        IconWindowConfig iconWindow,
        uint statusId)
        => iconWindow.Role switch
        {
            IconWindowRole.TargetDebuffs => this.FindStatusOnTarget(statusId),
            IconWindowRole.PartyBuffs => this.FindStatusOnParty(statusId, iconWindow.PartyAurasOwnOnly),
            _ => this.FindStatusOnPlayer(statusId),
        };

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnPlayer(uint statusId)
    {
        return SelectCharacterAuraStatus(this.GetPlayerAuraFrameIndex(), statusId, preferOwnStatus: false);
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnTarget(uint statusId)
    {
        return SelectCharacterAuraStatus(this.GetTargetAuraFrameIndex(), statusId, preferOwnStatus: true);
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnParty(uint statusId, bool ownOnly)
    {
        var index = this.GetPartyAuraFrameIndex(ownOnly);
        return index.TryGetValue(statusId, out var aggregate)
            ? (aggregate.Remaining, aggregate.Param, aggregate.Count, aggregate.OwnCount, aggregate.FromSelf)
            : null;
    }

    private Dictionary<uint, CharacterAuraAggregate> GetPlayerAuraFrameIndex()
    {
        if (!this.playerAuraFrameCacheValid)
        {
            this.RebuildCharacterAuraFrameIndex(this.playerAuraFrameCache, ObjectTable.LocalPlayer as IBattleChara);
            this.playerAuraFrameCacheValid = true;
        }

        return this.playerAuraFrameCache;
    }

    private Dictionary<uint, CharacterAuraAggregate> GetTargetAuraFrameIndex()
    {
        if (!this.targetAuraFrameCacheValid)
        {
            this.RebuildCharacterAuraFrameIndex(this.targetAuraFrameCache, TargetManager.Target as IBattleChara);
            this.targetAuraFrameCacheValid = true;
        }

        return this.targetAuraFrameCache;
    }

    private void RebuildCharacterAuraFrameIndex(Dictionary<uint, CharacterAuraAggregate> auraIndex, IBattleChara? chara)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraScan);
        try
        {
            auraIndex.Clear();
            if (chara is null)
                return;

            if (!this.TryReadBattleCharaStatusSnapshots(chara, "characterAura", out _, out _))
                return;

            foreach (var status in this.statusSnapshotBuffer)
            {
                AuraStatusFrameIndex.AddStatus(
                    auraIndex,
                    new CharacterAuraStatusSample(
                        status.StatusId,
                        status.RemainingTime,
                        status.Param,
                        this.IsStatusFromSelf(status.SourceId)));
            }
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraScan, profileStart);
        }
    }

    private static (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? SelectCharacterAuraStatus(
        Dictionary<uint, CharacterAuraAggregate> auraIndex,
        uint statusId,
        bool preferOwnStatus)
    {
        return auraIndex.TryGetValue(statusId, out var aggregate)
            ? aggregate.Select(preferOwnStatus)
            : null;
    }

    private Dictionary<uint, PartyAuraAggregate> GetPartyAuraFrameIndex(bool ownOnly)
    {
        if (!this.partyAuraFrameAllCacheValid || !this.partyAuraFrameOwnCacheValid)
        {
            this.RebuildPartyAuraFrameIndexes();
            this.partyAuraFrameAllCacheValid = true;
            this.partyAuraFrameOwnCacheValid = true;
        }

        return ownOnly ? this.partyAuraFrameOwnCache : this.partyAuraFrameAllCache;
    }

    private Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> GetPartyAuraGroupFrameIndex(bool ownOnly)
    {
        _ = this.GetPartyAuraFrameIndex(ownOnly);
        return ownOnly
            ? this.partyAuraGroupFrameOwnCache
            : this.partyAuraGroupFrameAllCache;
    }

    private void RebuildPartyAuraFrameIndexes()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraScan);
        try
        {
            this.partyAuraFrameAllCache.Clear();
            this.partyAuraFrameOwnCache.Clear();
            this.partyAuraGroupFrameAllCache.Clear();
            this.partyAuraGroupFrameOwnCache.Clear();
            _ = this.EnsureStatusIdentityIndex();

            var memberAuras = this.partyMemberAuraFrameBuffer;
            var memberOwnAuras = this.partyMemberAuraOwnFrameBuffer;
            var memberGroups = this.partyMemberAuraGroupFrameBuffer;
            var memberOwnGroups = this.partyMemberAuraGroupOwnFrameBuffer;
            var observedAtUtc = DateTime.UtcNow;
            memberAuras.Clear();
            memberOwnAuras.Clear();
            memberGroups.Clear();
            memberOwnGroups.Clear();
            this.partyAuraRuntimeStore.BeginScan();
            var rosterReadComplete = true;
            var partySlotCount = this.GetPartyListHeader().PartySlotCount;
            for (var i = 0; i < partySlotCount; i++)
            {
                var member = this.TryCreatePartyMemberReference(i);
                if (member is null)
                {
                    rosterReadComplete = false;
                    continue;
                }

                memberAuras.Clear();
                memberOwnAuras.Clear();
                memberGroups.Clear();
                memberOwnGroups.Clear();
                var readSucceeded = this.TryReadPartyMemberStatusSnapshots(
                        member,
                        "partyAura",
                        out var memberEntityId,
                        out var snapshotOrigin);
                if (memberEntityId == 0)
                {
                    rosterReadComplete = false;
                }
                else
                {
                    this.partyAuraRuntimeStore.MarkMemberOwner(memberEntityId);
                }

                if (!readSucceeded)
                    continue;

                this.partyAuraRuntimeStore.RecordStatusBatch(memberEntityId, snapshotOrigin);

                foreach (var status in this.statusSnapshotBuffer)
                {
                    var timerKey = new PartyAuraTimerKey(memberEntityId, status.StatusId, status.SourceId);
                    var timerObservation = this.partyAuraRuntimeStore.ObserveStatus(
                        timerKey,
                        observedAtUtc,
                        status.RemainingTime,
                        snapshotOrigin);
                    if (!timerObservation.Include)
                        continue;

                    var fromSelf = this.IsStatusFromSelf(status.SourceId);
                    var sample = new PartyAuraStatusSample(
                        status.StatusId,
                        timerObservation.Remaining,
                        status.Param,
                        fromSelf);
                    PartyAuraAggregator.AddMemberStatus(memberAuras, sample, ownOnly: false);
                    PartyAuraAggregator.AddMemberStatus(memberOwnAuras, sample, ownOnly: true);
                }

                foreach (var (statusId, memberAura) in memberAuras)
                {
                    PartyAuraAggregator.AddMemberAuraGroup(
                        memberGroups,
                        this.GetStatusDefinition(statusId).GroupKey,
                        memberAura.FromSelf);
                }

                foreach (var (statusId, memberAura) in memberOwnAuras)
                {
                    PartyAuraAggregator.AddMemberAuraGroup(
                        memberOwnGroups,
                        this.GetStatusDefinition(statusId).GroupKey,
                        memberAura.FromSelf);
                }

                PartyAuraAggregator.MergeMemberAuras(memberAuras, this.partyAuraFrameAllCache);
                PartyAuraAggregator.MergeMemberAuras(memberOwnAuras, this.partyAuraFrameOwnCache);
                PartyAuraAggregator.MergeMemberAuraGroups(memberGroups, this.partyAuraGroupFrameAllCache);
                PartyAuraAggregator.MergeMemberAuraGroups(memberOwnGroups, this.partyAuraGroupFrameOwnCache);
            }

            memberAuras.Clear();
            memberOwnAuras.Clear();
            memberGroups.Clear();
            memberOwnGroups.Clear();
            this.partyAuraRuntimeStore.CompleteScan(observedAtUtc, rosterReadComplete);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraScan, profileStart);
        }
    }

    private bool IsStatusFromSelf(uint sourceId)
    {
        return StatusSourceOwnership.IsFromPlayer(
            sourceId,
            ObjectTable.LocalPlayer?.EntityId ?? 0,
            this.GetGameObjectOwnerEntityId);
    }

    private AuraStatusDefinition GetStatusDefinition(uint statusId)
    {
        if (this.statusDefinitionCache.TryGetValue(statusId, out var cached))
            return cached;

        var definition = new AuraStatusDefinition($"Status {statusId}", 0u, 0);
        var shouldCache = false;
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText();
                definition = new AuraStatusDefinition(
                    string.IsNullOrWhiteSpace(name) ? $"Status {statusId}" : name,
                    row.Icon,
                    row.StatusCategory);
                shouldCache = true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read status {statusId}.");
        }

        if (shouldCache)
            this.statusDefinitionCache[statusId] = definition;

        return definition;
    }
}
