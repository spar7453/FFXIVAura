using GameAction = Lumina.Excel.Sheets.Action;

namespace FFXIVAura;

internal readonly record struct GameActionInfo(
    uint RowId,
    string Name,
    uint Icon,
    uint ActionCategoryId,
    string ActionCategoryName,
    byte ClassJobLevel,
    ushort Cast100ms,
    ushort Recast100ms,
    int Range,
    int EffectRange,
    byte MaxCharges,
    bool IsRoleAction,
    byte EquivalenceGroup,
    uint StatusGainSelfId,
    bool CanTargetHostile,
    bool CanTargetSelf,
    bool CanTargetParty,
    bool CanTargetAlly);

internal readonly record struct GameActionEnumerationResult(
    bool Succeeded,
    IReadOnlyList<GameActionInfo> Actions)
{
    public static GameActionEnumerationResult Failure { get; } =
        new(false, Array.Empty<GameActionInfo>());

    public static GameActionEnumerationResult Success(IReadOnlyList<GameActionInfo> actions)
        => new(true, actions ?? throw new ArgumentNullException(nameof(actions)));
}

internal interface IGameActionRepository
{
    int CachedActionCount { get; }

    int MissingActionCount { get; }

    GameActionInfo? GetAction(uint actionId);

    GameActionEnumerationResult EnumerateActionsForClassJobs(IReadOnlySet<uint> classJobIds);
}

internal interface IGameActionDataSource
{
    bool TryGetAction(uint actionId, out GameActionInfo action);

    IReadOnlyList<GameActionInfo> EnumerateActionsForClassJobs(IReadOnlySet<uint> classJobIds);
}

internal sealed class DalamudGameActionDataSource(IDataManager dataManager) : IGameActionDataSource
{
    public IReadOnlyList<GameActionInfo> EnumerateActionsForClassJobs(IReadOnlySet<uint> classJobIds)
    {
        ArgumentNullException.ThrowIfNull(classJobIds);

        var sheet = dataManager.GetExcelSheet<GameAction>()
            ?? throw new InvalidOperationException("The game action sheet is unavailable.");
        var actions = new List<GameActionInfo>();
        foreach (var row in sheet)
        {
            if (classJobIds.Contains(row.ClassJob.RowId))
                actions.Add(CreateActionInfo(row, includeCategoryName: false));
        }

        return actions;
    }

    public bool TryGetAction(uint actionId, out GameActionInfo action)
    {
        var sheet = dataManager.GetExcelSheet<GameAction>()
            ?? throw new InvalidOperationException("The game action sheet is unavailable.");
        if (!sheet.TryGetRow(actionId, out var row))
        {
            action = default;
            return false;
        }

        action = CreateActionInfo(row, includeCategoryName: true);
        return true;
    }

    private static GameActionInfo CreateActionInfo(GameAction action, bool includeCategoryName)
        => new(
            action.RowId,
            action.Name.ExtractText(),
            action.Icon,
            action.ActionCategory.RowId,
            includeCategoryName ? GetActionCategoryName(action) : string.Empty,
            (byte)Math.Min(action.ClassJobLevel, byte.MaxValue),
            action.Cast100ms,
            action.Recast100ms,
            action.Range,
            action.EffectRange,
            action.MaxCharges,
            action.IsRoleAction,
            action.EquivalenceGroup,
            action.StatusGainSelf.RowId,
            action.CanTargetHostile,
            action.CanTargetSelf,
            action.CanTargetParty,
            action.CanTargetAlly);

    private static string GetActionCategoryName(GameAction action)
    {
        try
        {
            return action.ActionCategory.Value.Name.ExtractText();
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal sealed class DalamudGameActionRepository : IGameActionRepository
{
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(1);
    private readonly IGameActionDataSource dataSource;
    private readonly Action<Exception, string> logDebug;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<uint, GameActionInfo> actionRows = new();
    private readonly HashSet<uint> missingActionIds = [];
    private readonly Dictionary<uint, DateTime> actionRetryAfterUtc = new();
    private DateTime enumerationRetryAfterUtc = DateTime.MinValue;

    public DalamudGameActionRepository(
        IDataManager dataManager,
        IPluginLog log,
        TimeProvider? timeProvider = null)
        : this(
            new DalamudGameActionDataSource(dataManager),
            (exception, message) => log.Debug(exception, message),
            timeProvider)
    {
    }

    internal DalamudGameActionRepository(
        IGameActionDataSource dataSource,
        Action<Exception, string> logDebug,
        TimeProvider? timeProvider = null)
    {
        this.dataSource = dataSource;
        this.logDebug = logDebug;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int CachedActionCount => this.actionRows.Count;

    public int MissingActionCount => this.missingActionIds.Count;

    public GameActionEnumerationResult EnumerateActionsForClassJobs(IReadOnlySet<uint> classJobIds)
    {
        ArgumentNullException.ThrowIfNull(classJobIds);
        if (classJobIds.Count == 0)
            return GameActionEnumerationResult.Success(Array.Empty<GameActionInfo>());

        var nowUtc = this.timeProvider.GetUtcNow().UtcDateTime;
        if (nowUtc < this.enumerationRetryAfterUtc)
            return GameActionEnumerationResult.Failure;

        try
        {
            var actions = this.dataSource.EnumerateActionsForClassJobs(classJobIds);
            this.enumerationRetryAfterUtc = DateTime.MinValue;
            return GameActionEnumerationResult.Success(actions);
        }
        catch (Exception ex)
        {
            this.logDebug(ex, "Failed to enumerate action rows.");
            this.enumerationRetryAfterUtc = nowUtc.Add(FailureRetryDelay);
            return GameActionEnumerationResult.Failure;
        }
    }

    public GameActionInfo? GetAction(uint actionId)
    {
        if (actionId == 0)
            return null;

        if (this.actionRows.TryGetValue(actionId, out var cached))
            return cached;

        if (this.missingActionIds.Contains(actionId))
            return null;

        var nowUtc = this.timeProvider.GetUtcNow().UtcDateTime;
        if (this.actionRetryAfterUtc.TryGetValue(actionId, out var retryAfterUtc))
        {
            if (nowUtc < retryAfterUtc)
                return null;

            this.actionRetryAfterUtc.Remove(actionId);
        }

        try
        {
            if (!this.dataSource.TryGetAction(actionId, out var action))
            {
                this.actionRetryAfterUtc.Remove(actionId);
                this.missingActionIds.Add(actionId);
                return null;
            }

            this.actionRetryAfterUtc.Remove(actionId);
            this.actionRows[actionId] = action;
            return action;
        }
        catch (Exception ex)
        {
            this.logDebug(ex, $"Failed to read action row for {actionId}.");
            this.actionRetryAfterUtc[actionId] = nowUtc.Add(FailureRetryDelay);
            return null;
        }
    }
}
