namespace FFXIVAura;

/// <summary>
/// One status-list read result. <see cref="Snapshots"/> may alias a buffer owned by the
/// producing <see cref="IStatusSnapshotRuntime"/>; see that interface for the lifetime contract.
/// </summary>
internal readonly record struct StatusSnapshotBatch(
    bool Succeeded,
    uint OwnerEntityId,
    StatusSnapshotOrigin Origin,
    IReadOnlyList<StatusSnapshot> Snapshots);

internal readonly record struct StatusSnapshotRuntimeDiagnostics(
    int FallbackCount,
    int OwnerCacheCount);

/// <summary>
/// Reads game status lists into per-frame snapshots.
///
/// Lifetime contract: implementations reuse one shared snapshot buffer, so every returned
/// <see cref="StatusSnapshotBatch"/> (including the batches passed to the
/// <see cref="VisitBattleCharacters"/> visitor) is only valid until the next call to any
/// read/visit/<see cref="BeginFrame"/> member on the same runtime. Consume a batch immediately;
/// never store it or its <see cref="StatusSnapshotBatch.Snapshots"/> list across calls —
/// copy the data out instead.
/// </summary>
internal interface IStatusSnapshotRuntime
{
    StatusSnapshotBatch ReadLocalPlayer(string scope);

    StatusSnapshotBatch ReadTarget(string scope);

    StatusSnapshotBatch ReadPartyMember(int index, string scope);

    StatusSnapshotBatch ReadAllianceMember(int index, string scope);

    void VisitBattleCharacters(string scope, Action<StatusSnapshotBatch> visitor);

    uint GetOwnerEntityId(uint entityId);

    StatusSnapshotRuntimeDiagnostics CreateDiagnostics();

    void BeginFrame();

    void ResetRuntimeState();
}

internal sealed class DalamudStatusSnapshotRuntime : IStatusSnapshotRuntime
{
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly IPartyList partyList;
    private readonly Action<string> noteDiagnostic;
    private readonly TimeSpan fallbackRetention;

    // Single reusable buffer aliased by every returned batch; see the IStatusSnapshotRuntime
    // lifetime contract. Also relies on the UI-thread-only invariant (FrameThreadGuard).
    private readonly List<StatusSnapshot> snapshotBuffer = [];
    private readonly StatusSnapshotFallbackCache fallbackCache = new();
    private readonly Dictionary<uint, uint> ownerCache = new();

    public DalamudStatusSnapshotRuntime(
        IObjectTable objectTable,
        ITargetManager targetManager,
        IPartyList partyList,
        Action<string> noteDiagnostic,
        TimeSpan fallbackRetention)
    {
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.partyList = partyList;
        this.noteDiagnostic = noteDiagnostic;
        this.fallbackRetention = fallbackRetention;
    }

    public StatusSnapshotBatch ReadLocalPlayer(string scope)
        => this.ReadBattleCharacter(this.objectTable.LocalPlayer as IBattleChara, scope);

    public StatusSnapshotBatch ReadTarget(string scope)
        => this.ReadBattleCharacter(
            (this.targetManager.SoftTarget ?? this.targetManager.Target) as IBattleChara,
            scope);

    public StatusSnapshotBatch ReadPartyMember(int index, string scope)
    {
        try
        {
            var address = this.partyList.GetPartyMemberAddress(index);
            if (address == IntPtr.Zero)
                return this.EmptyBatch();

            return this.ReadPartyMember(this.partyList.CreatePartyMemberReference(address), scope);
        }
        catch (Exception ex)
        {
            this.noteDiagnostic($"partyMemberReferenceReadFailed:{index}:{ex.GetType().Name}");
            return this.EmptyBatch();
        }
    }

    public StatusSnapshotBatch ReadAllianceMember(int index, string scope)
    {
        try
        {
            var address = this.partyList.GetAllianceMemberAddress(index);
            if (address == IntPtr.Zero)
                return this.EmptyBatch();

            return this.ReadPartyMember(this.partyList.CreateAllianceMemberReference(address), scope);
        }
        catch (Exception ex)
        {
            this.noteDiagnostic($"partyCooldownFlatAllianceMemberReadFailed:{index}:{ex.GetType().Name}");
            return this.EmptyBatch();
        }
    }

    public void VisitBattleCharacters(string scope, Action<StatusSnapshotBatch> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        try
        {
            foreach (var gameObject in this.objectTable)
            {
                if (gameObject is IBattleChara battleCharacter)
                    visitor(this.ReadBattleCharacter(battleCharacter, scope));
            }
        }
        catch (Exception ex)
        {
            this.noteDiagnostic($"statusObjectTableReadFailed:{ex.GetType().Name}");
        }
    }

    public uint GetOwnerEntityId(uint entityId)
    {
        if (entityId is 0 or 0xE0000000)
            return 0;

        if (this.ownerCache.TryGetValue(entityId, out var cached))
            return cached;

        var ownerId = 0u;
        try
        {
            ownerId = this.objectTable.SearchByEntityId(entityId)?.OwnerId ?? 0;
        }
        catch (Exception ex)
        {
            this.noteDiagnostic($"gameObjectOwnerReadFailed:{entityId}:{ex.GetType().Name}");
        }

        this.ownerCache[entityId] = ownerId;
        return ownerId;
    }

    public StatusSnapshotRuntimeDiagnostics CreateDiagnostics()
        => new(this.fallbackCache.Count, this.ownerCache.Count);

    public void BeginFrame()
    {
        this.snapshotBuffer.Clear();
        this.ownerCache.Clear();
    }

    public void ResetRuntimeState()
    {
        this.BeginFrame();
        this.fallbackCache.Clear();
    }

    private StatusSnapshotBatch ReadBattleCharacter(IBattleChara? character, string scope)
    {
        this.snapshotBuffer.Clear();
        if (character is null)
            return this.EmptyBatch();

        var ownerEntityId = 0u;
        try
        {
            ownerEntityId = character.EntityId;
            return ownerEntityId == 0
                ? this.EmptyBatch()
                : this.ReadStatuses(character.StatusList, scope, ownerEntityId);
        }
        catch (Exception ex)
        {
            return this.UseFallback(scope, ownerEntityId, ex);
        }
    }

    private StatusSnapshotBatch ReadPartyMember(IPartyMember? member, string scope)
    {
        this.snapshotBuffer.Clear();
        if (member is null)
            return this.EmptyBatch();

        var ownerEntityId = 0u;
        try
        {
            ownerEntityId = member.EntityId;
            return ownerEntityId == 0
                ? this.EmptyBatch()
                : this.ReadStatuses(member.Statuses, scope, ownerEntityId);
        }
        catch (Exception ex)
        {
            return this.UseFallback(scope, ownerEntityId, ex);
        }
    }

    private StatusSnapshotBatch ReadStatuses(
        IEnumerable<IStatus>? statuses,
        string scope,
        uint ownerEntityId)
    {
        if (!StatusSnapshotReader.ReadTo(statuses, this.snapshotBuffer, out var error))
            return this.UseFallback(scope, ownerEntityId, error);

        this.fallbackCache.Remember(scope, ownerEntityId, this.snapshotBuffer, DateTime.UtcNow);
        return new StatusSnapshotBatch(true, ownerEntityId, StatusSnapshotOrigin.Live, this.snapshotBuffer);
    }

    private StatusSnapshotBatch UseFallback(string scope, uint ownerEntityId, Exception? error)
    {
        if (this.fallbackCache.TryCopyRecentTo(
                scope,
                ownerEntityId,
                DateTime.UtcNow,
                this.fallbackRetention,
                this.snapshotBuffer))
        {
            this.noteDiagnostic($"statusReadFallback:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
            return new StatusSnapshotBatch(true, ownerEntityId, StatusSnapshotOrigin.Fallback, this.snapshotBuffer);
        }

        this.snapshotBuffer.Clear();
        this.noteDiagnostic($"statusReadFailed:{scope}:{ownerEntityId}:{error?.GetType().Name ?? "unknown"}");
        return this.EmptyBatch();
    }

    private StatusSnapshotBatch EmptyBatch()
    {
        this.snapshotBuffer.Clear();
        return new StatusSnapshotBatch(false, 0, StatusSnapshotOrigin.None, this.snapshotBuffer);
    }
}
