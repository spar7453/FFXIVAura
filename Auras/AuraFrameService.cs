namespace FFXIVAura;

internal readonly record struct AuraFrameDiagnostics(
    bool PlayerCacheValid,
    int PlayerCacheCount,
    bool TargetCacheValid,
    int TargetCacheCount,
    bool PartyAllCacheValid,
    int PartyAllCacheCount,
    bool PartyOwnCacheValid,
    int PartyOwnCacheCount,
    PartyAuraRuntimeDiagnostics PartyRuntime);

internal interface IAuraStatusFrameSource
{
    IEnumerable<uint> GetPlayerStatusIds();

    IEnumerable<uint> GetTargetStatusIds();

    IEnumerable<uint> GetPartyStatusIds(bool ownOnly);
}

internal sealed class AuraFrameService : IAuraStatusFrameSource
{
    private readonly IStatusSnapshotRuntime statusSnapshotRuntime;
    private readonly IPartyRosterReader partyRosterReader;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly Func<bool> ensureStatusIdentityIndex;
    private readonly Func<uint, AuraStatusDefinition> getStatusDefinition;
    private readonly Dictionary<uint, CharacterAuraAggregate> playerAuraIndex = new();
    private readonly Dictionary<uint, CharacterAuraAggregate> targetAuraIndex = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraAllIndex = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraOwnIndex = new();
    private readonly Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> partyAuraGroupAllIndex = new();
    private readonly Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> partyAuraGroupOwnIndex = new();
    private readonly Dictionary<uint, PartyMemberAuraState> memberAuraBuffer = new();
    private readonly Dictionary<uint, PartyMemberAuraState> memberOwnAuraBuffer = new();
    private readonly Dictionary<AuraStatusGroupKey, bool> memberAuraGroupBuffer = new();
    private readonly Dictionary<AuraStatusGroupKey, bool> memberOwnAuraGroupBuffer = new();
    private readonly PartyAuraRuntimeStore partyRuntimeStore = new();
    private PlayerFrameContext playerContext;
    private bool playerCacheValid;
    private bool targetCacheValid;
    private bool partyCacheValid;

    public AuraFrameService(
        IStatusSnapshotRuntime statusSnapshotRuntime,
        IPartyRosterReader partyRosterReader,
        PerformanceProfiler performanceProfiler,
        Func<bool> ensureStatusIdentityIndex,
        Func<uint, AuraStatusDefinition> getStatusDefinition)
    {
        this.statusSnapshotRuntime = statusSnapshotRuntime;
        this.partyRosterReader = partyRosterReader;
        this.performanceProfiler = performanceProfiler;
        this.ensureStatusIdentityIndex = ensureStatusIdentityIndex;
        this.getStatusDefinition = getStatusDefinition;
    }

    public void BeginFrame(in PlayerFrameContext context)
    {
        this.playerContext = context;
        this.playerCacheValid = false;
        this.targetCacheValid = false;
        this.partyCacheValid = false;
    }

    public (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindPlayer(uint statusId)
    {
        this.EnsurePlayerIndex();
        return SelectCharacterStatus(this.playerAuraIndex, statusId, preferOwnStatus: false);
    }

    public (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindTarget(uint statusId)
    {
        this.EnsureTargetIndex();
        return SelectCharacterStatus(this.targetAuraIndex, statusId, preferOwnStatus: true);
    }

    public (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindParty(uint statusId, bool ownOnly)
    {
        this.EnsurePartyIndexes();
        var index = ownOnly ? this.partyAuraOwnIndex : this.partyAuraAllIndex;
        return index.TryGetValue(statusId, out var aggregate)
            ? (aggregate.Remaining, aggregate.Param, aggregate.Count, aggregate.OwnCount, aggregate.FromSelf)
            : null;
    }

    public IEnumerable<uint> GetPlayerStatusIds()
    {
        this.EnsurePlayerIndex();
        return this.playerAuraIndex.Keys;
    }

    public IEnumerable<uint> GetTargetStatusIds()
    {
        this.EnsureTargetIndex();
        return this.targetAuraIndex.Keys;
    }

    public IEnumerable<uint> GetPartyStatusIds(bool ownOnly)
    {
        this.EnsurePartyIndexes();
        return (ownOnly ? this.partyAuraOwnIndex : this.partyAuraAllIndex).Keys;
    }

    public bool TryGetPartyGroup(
        AuraStatusGroupKey key,
        bool ownOnly,
        out PartyAuraGroupAggregate aggregate)
    {
        this.EnsurePartyIndexes();
        return (ownOnly ? this.partyAuraGroupOwnIndex : this.partyAuraGroupAllIndex)
            .TryGetValue(key, out aggregate);
    }

    public AuraFrameDiagnostics CreateDiagnostics()
        => new(
            this.playerCacheValid,
            this.playerAuraIndex.Count,
            this.targetCacheValid,
            this.targetAuraIndex.Count,
            this.partyCacheValid,
            this.partyAuraAllIndex.Count,
            this.partyCacheValid,
            this.partyAuraOwnIndex.Count,
            this.partyRuntimeStore.CreateDiagnostics());

    public void ResetRuntimeState()
    {
        this.playerAuraIndex.Clear();
        this.targetAuraIndex.Clear();
        this.partyAuraAllIndex.Clear();
        this.partyAuraOwnIndex.Clear();
        this.partyAuraGroupAllIndex.Clear();
        this.partyAuraGroupOwnIndex.Clear();
        this.memberAuraBuffer.Clear();
        this.memberOwnAuraBuffer.Clear();
        this.memberAuraGroupBuffer.Clear();
        this.memberOwnAuraGroupBuffer.Clear();
        this.partyRuntimeStore.ResetRuntimeState();
        this.playerCacheValid = false;
        this.targetCacheValid = false;
        this.partyCacheValid = false;
    }

    private void EnsurePlayerIndex()
    {
        FrameThreadGuard.AssertUiThread();
        if (this.playerCacheValid)
            return;

        this.RebuildCharacterIndex(
            this.playerAuraIndex,
            this.statusSnapshotRuntime.ReadLocalPlayer("characterAura"));
        this.playerCacheValid = true;
    }

    private void EnsureTargetIndex()
    {
        FrameThreadGuard.AssertUiThread();
        if (this.targetCacheValid)
            return;

        this.RebuildCharacterIndex(
            this.targetAuraIndex,
            this.statusSnapshotRuntime.ReadTarget("characterAura"));
        this.targetCacheValid = true;
    }

    private void RebuildCharacterIndex(
        Dictionary<uint, CharacterAuraAggregate> auraIndex,
        StatusSnapshotBatch batch)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraScan);
        try
        {
            auraIndex.Clear();
            if (!batch.Succeeded)
                return;

            foreach (var status in batch.Snapshots)
            {
                AuraStatusFrameIndex.AddStatus(
                    auraIndex,
                    new CharacterAuraStatusSample(
                        status.StatusId,
                        status.RemainingTime,
                        status.Param,
                        this.IsStatusFromPlayer(status.SourceId)));
            }
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraScan, profileStart);
        }
    }

    private void EnsurePartyIndexes()
    {
        FrameThreadGuard.AssertUiThread();
        if (this.partyCacheValid)
            return;

        this.RebuildPartyIndexes();
        this.partyCacheValid = true;
    }

    private void RebuildPartyIndexes()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraScan);
        try
        {
            this.partyAuraAllIndex.Clear();
            this.partyAuraOwnIndex.Clear();
            this.partyAuraGroupAllIndex.Clear();
            this.partyAuraGroupOwnIndex.Clear();
            _ = this.ensureStatusIdentityIndex();

            var observedAtUtc = DateTime.UtcNow;
            this.ClearMemberBuffers();
            this.partyRuntimeStore.BeginScan();
            var rosterReadComplete = true;
            var partySlotCount = this.partyRosterReader.ReadPartyListHeader().PartySlotCount;
            for (var index = 0; index < partySlotCount; index++)
            {
                this.ClearMemberBuffers();
                var batch = this.statusSnapshotRuntime.ReadPartyMember(index, "partyAura");
                var memberEntityId = batch.OwnerEntityId;
                if (memberEntityId == 0)
                {
                    rosterReadComplete = false;
                }
                else
                {
                    this.partyRuntimeStore.MarkMemberOwner(memberEntityId);
                }

                if (!batch.Succeeded)
                    continue;

                this.partyRuntimeStore.RecordStatusBatch(memberEntityId, batch.Origin);
                this.AddPartyMemberStatuses(batch, memberEntityId, observedAtUtc);
                this.AddPartyMemberGroups(this.memberAuraBuffer, this.memberAuraGroupBuffer);
                this.AddPartyMemberGroups(this.memberOwnAuraBuffer, this.memberOwnAuraGroupBuffer);
                PartyAuraAggregator.MergeMemberAuras(this.memberAuraBuffer, this.partyAuraAllIndex);
                PartyAuraAggregator.MergeMemberAuras(this.memberOwnAuraBuffer, this.partyAuraOwnIndex);
                PartyAuraAggregator.MergeMemberAuraGroups(this.memberAuraGroupBuffer, this.partyAuraGroupAllIndex);
                PartyAuraAggregator.MergeMemberAuraGroups(this.memberOwnAuraGroupBuffer, this.partyAuraGroupOwnIndex);
            }

            this.ClearMemberBuffers();
            this.partyRuntimeStore.CompleteScan(observedAtUtc, rosterReadComplete);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraScan, profileStart);
        }
    }

    private void AddPartyMemberStatuses(
        StatusSnapshotBatch batch,
        uint memberEntityId,
        DateTime observedAtUtc)
    {
        foreach (var status in batch.Snapshots)
        {
            var timerKey = new PartyAuraTimerKey(memberEntityId, status.StatusId, status.SourceId);
            var timerObservation = this.partyRuntimeStore.ObserveStatus(
                timerKey,
                observedAtUtc,
                status.RemainingTime,
                batch.Origin);
            if (!timerObservation.Include)
                continue;

            var sample = new PartyAuraStatusSample(
                status.StatusId,
                timerObservation.Remaining,
                status.Param,
                this.IsStatusFromPlayer(status.SourceId));
            PartyAuraAggregator.AddMemberStatus(this.memberAuraBuffer, sample, ownOnly: false);
            PartyAuraAggregator.AddMemberStatus(this.memberOwnAuraBuffer, sample, ownOnly: true);
        }
    }

    private void AddPartyMemberGroups(
        Dictionary<uint, PartyMemberAuraState> memberAuras,
        Dictionary<AuraStatusGroupKey, bool> memberGroups)
    {
        foreach (var (statusId, memberAura) in memberAuras)
        {
            PartyAuraAggregator.AddMemberAuraGroup(
                memberGroups,
                this.getStatusDefinition(statusId).GroupKey,
                memberAura.FromSelf);
        }
    }

    private void ClearMemberBuffers()
    {
        this.memberAuraBuffer.Clear();
        this.memberOwnAuraBuffer.Clear();
        this.memberAuraGroupBuffer.Clear();
        this.memberOwnAuraGroupBuffer.Clear();
    }

    private bool IsStatusFromPlayer(uint sourceId)
        => StatusSourceOwnership.IsFromPlayer(
            sourceId,
            this.playerContext.LocalPlayerEntityId,
            this.statusSnapshotRuntime.GetOwnerEntityId);

    private static (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? SelectCharacterStatus(
        Dictionary<uint, CharacterAuraAggregate> auraIndex,
        uint statusId,
        bool preferOwnStatus)
        => auraIndex.TryGetValue(statusId, out var aggregate)
            ? aggregate.Select(preferOwnStatus)
            : null;
}
