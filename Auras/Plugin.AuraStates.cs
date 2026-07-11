namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AuraState> GetDisplayAuras(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            yield break;

        foreach (var statusId in iconWindow.TrackedStatusIds.Distinct())
        {
            var aura = this.GetAuraState(iconWindow, statusId);
            if (!this.ShouldDisplayAura(aura, iconWindow))
                continue;

            if (!aura.Present && !ShouldShowMissingAura(iconWindow))
                continue;

            yield return aura;
        }
    }

    private static bool ShouldShowMissingAura(IconWindowConfig iconWindow)
        => iconWindow.ShowMissingAuras || iconWindow.DisplayCondition == IconDisplayCondition.ReadyOnly;

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

    private AuraState GetAuraState(IconWindowConfig iconWindow, uint statusId)
    {
        var definition = this.GetStatusDefinition(statusId);
        var active = iconWindow.Role switch
        {
            IconWindowRole.TargetDebuffs => this.FindStatusOnTarget(statusId),
            IconWindowRole.PartyBuffs => this.FindStatusOnParty(statusId, iconWindow.PartyAurasOwnOnly),
            _ => this.FindStatusOnPlayer(statusId),
        };

        if (active is null)
            return new AuraState(statusId, definition.Name, definition.IconId, 0f, 0, 0, 0, false, false);

        return new AuraState(
            statusId,
            definition.Name,
            definition.IconId,
            Math.Max(0f, active.Value.Remaining),
            active.Value.Param,
            active.Value.Count,
            active.Value.OwnCount,
            true,
            active.Value.FromSelf);
    }

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

            if (!this.TryReadBattleCharaStatusSnapshots(chara, "characterAura", out _))
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
        if (ownOnly)
        {
            if (!this.partyAuraFrameOwnCacheValid)
            {
                this.RebuildPartyAuraFrameIndex(this.partyAuraFrameOwnCache, ownOnly: true);
                this.partyAuraFrameOwnCacheValid = true;
            }

            return this.partyAuraFrameOwnCache;
        }

        if (!this.partyAuraFrameAllCacheValid)
        {
            this.RebuildPartyAuraFrameIndex(this.partyAuraFrameAllCache, ownOnly: false);
            this.partyAuraFrameAllCacheValid = true;
        }

        return this.partyAuraFrameAllCache;
    }

    private void RebuildPartyAuraFrameIndex(Dictionary<uint, PartyAuraAggregate> aggregateAuras, bool ownOnly)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraScan);
        try
        {
            aggregateAuras.Clear();

            var memberAuras = new Dictionary<uint, PartyMemberAuraState>();
            var partySlotCount = this.GetPartyListHeader().PartySlotCount;
            for (var i = 0; i < partySlotCount; i++)
            {
                var member = this.TryCreatePartyMemberReference(i);
                if (member is null)
                    continue;

                memberAuras.Clear();
                if (!this.TryReadPartyMemberStatusSnapshots(member, "partyAura", out _))
                    continue;

                foreach (var status in this.statusSnapshotBuffer)
                {
                    var fromSelf = this.IsStatusFromSelf(status.SourceId);
                    var sample = new PartyAuraStatusSample(status.StatusId, status.RemainingTime, status.Param, fromSelf);
                    PartyAuraAggregator.AddMemberStatus(memberAuras, sample, ownOnly);
                }

                PartyAuraAggregator.MergeMemberAuras(memberAuras, aggregateAuras);
            }
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

    private (string Name, uint IconId) GetStatusDefinition(uint statusId)
    {
        if (this.statusDefinitionCache.TryGetValue(statusId, out var cached))
            return cached;

        var definition = ($"Status {statusId}", 0u);
        var shouldCache = false;
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText();
                definition = (string.IsNullOrWhiteSpace(name) ? $"Status {statusId}" : name, row.Icon);
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
