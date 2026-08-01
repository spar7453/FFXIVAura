namespace FFXIVAura;

internal readonly record struct PlayerFrameContext(
    bool IsLoggedIn,
    bool IsPlayerLoaded,
    string Job,
    uint Level,
    uint EffectiveLevel,
    bool IsLevelSynced,
    bool IsInCombat,
    bool BetweenAreas,
    bool BetweenAreas51,
    bool IsMounted,
    bool HasLocalPlayer,
    uint LocalPlayerEntityId,
    bool HasTarget,
    uint TargetEntityId,
    bool HasSoftTarget,
    uint SoftTargetEntityId)
{
    public bool IsReady => this.IsLoggedIn && this.IsPlayerLoaded;

    public bool IsBetweenAreas => this.BetweenAreas || this.BetweenAreas51;

    public uint EffectiveTargetEntityId
        => this.HasSoftTarget && this.SoftTargetEntityId != 0
            ? this.SoftTargetEntityId
            : this.HasTarget
                ? this.TargetEntityId
                : 0;

    public bool HasEffectiveTarget => this.EffectiveTargetEntityId != 0;
}

internal interface IPlayerRuntimeContext
{
    PlayerFrameContext Capture();

    bool TryCaptureLocalPlayerIdentity(out LocalPlayerIdentity identity);
}

internal readonly record struct LocalPlayerIdentity(string Name, ushort HomeWorldId);

internal sealed class DalamudPlayerRuntimeContext : IPlayerRuntimeContext
{
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly ICondition condition;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;

    public DalamudPlayerRuntimeContext(
        IClientState clientState,
        IPlayerState playerState,
        ICondition condition,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        this.clientState = clientState;
        this.playerState = playerState;
        this.condition = condition;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
    }

    public PlayerFrameContext Capture()
    {
        var playerLoaded = this.playerState.IsLoaded;
        var localPlayer = this.objectTable.LocalPlayer;
        var target = this.targetManager.Target;
        var softTarget = this.targetManager.SoftTarget;
        var level = playerLoaded ? (uint)this.playerState.Level : 0;
        var effectiveLevel = playerLoaded && this.playerState.EffectiveLevel > 0
            ? (uint)this.playerState.EffectiveLevel
            : level;

        return new PlayerFrameContext(
            IsLoggedIn: this.clientState.IsLoggedIn,
            IsPlayerLoaded: playerLoaded,
            Job: playerLoaded ? JobInfo.Code(this.playerState.ClassJob.RowId) : string.Empty,
            Level: level,
            EffectiveLevel: effectiveLevel,
            IsLevelSynced: playerLoaded && this.playerState.IsLevelSynced,
            IsInCombat: this.condition[ConditionFlag.InCombat]
                        || (localPlayer is not null && (localPlayer.StatusFlags & StatusFlags.InCombat) != 0),
            BetweenAreas: this.condition[ConditionFlag.BetweenAreas],
            BetweenAreas51: this.condition[ConditionFlag.BetweenAreas51],
            IsMounted: this.condition[ConditionFlag.Mounted],
            HasLocalPlayer: localPlayer is not null,
            LocalPlayerEntityId: localPlayer?.EntityId ?? 0,
            HasTarget: target is not null,
            TargetEntityId: target?.EntityId ?? 0,
            HasSoftTarget: softTarget is not null,
            SoftTargetEntityId: softTarget?.EntityId ?? 0);
    }

    public bool TryCaptureLocalPlayerIdentity(out LocalPlayerIdentity identity)
    {
        if (!this.playerState.IsLoaded || this.objectTable.LocalPlayer is not { } localPlayer)
        {
            identity = default;
            return false;
        }

        identity = new LocalPlayerIdentity(
            localPlayer.Name.ToString(),
            (ushort)this.playerState.HomeWorld.RowId);
        return true;
    }
}
