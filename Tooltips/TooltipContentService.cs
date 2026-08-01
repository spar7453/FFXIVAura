using GameActionTransient = Lumina.Excel.Sheets.ActionTransient;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace FFXIVAura;

internal readonly record struct TooltipContentDiagnostics(
    int StatusTextCount,
    int ActionModelCount,
    int ActionDescriptionCount);

internal sealed class TooltipContentService
{
    private readonly IDataManager dataManager;
    private readonly ISeStringEvaluator seStringEvaluator;
    private readonly IGameActionRepository gameActionRepository;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, string> statusTexts = new();
    private readonly Dictionary<uint, OverlayActionTooltipModel> actionModels = new();
    private readonly Dictionary<uint, string> actionDescriptions = new();

    public TooltipContentService(
        IDataManager dataManager,
        ISeStringEvaluator seStringEvaluator,
        IGameActionRepository gameActionRepository,
        IPluginLog log)
    {
        this.dataManager = dataManager;
        this.seStringEvaluator = seStringEvaluator;
        this.gameActionRepository = gameActionRepository;
        this.log = log;
    }

    public bool ContainsActionModel(uint actionId)
        => this.actionModels.ContainsKey(actionId);

    public string GetStatusText(uint statusId)
    {
        if (this.statusTexts.TryGetValue(statusId, out var cached))
            return cached;

        var text = $"Status {statusId}";
        var shouldCache = false;
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText().StripSoftHyphen();
                var description = this.seStringEvaluator
                    .Evaluate(row.Description)
                    .ExtractText()
                    .StripSoftHyphen();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Status {statusId}";

                text = string.IsNullOrWhiteSpace(description)
                    ? name
                    : $"{name}\n{description}";
                shouldCache = true;
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read status tooltip {statusId}.");
        }

        if (shouldCache)
            this.statusTexts[statusId] = text;

        return text;
    }

    public OverlayActionTooltipModel GetActionModel(
        uint actionId,
        string fallbackName,
        uint fallbackIconId,
        string fallbackCategory)
    {
        if (this.actionModels.TryGetValue(actionId, out var cached))
            return cached;

        var action = this.gameActionRepository.GetAction(actionId);
        if (action is null)
        {
            return new OverlayActionTooltipModel(
                actionId,
                fallbackIconId,
                string.IsNullOrWhiteSpace(fallbackName) ? $"Action {actionId}" : fallbackName,
                fallbackCategory,
                "-",
                "-",
                "-",
                "-",
                string.Empty,
                0,
                0);
        }

        var row = action.Value;
        var name = row.Name;
        var category = row.ActionCategoryName;
        var model = new OverlayActionTooltipModel(
            actionId,
            row.Icon > 0 ? row.Icon : fallbackIconId,
            string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            string.IsNullOrWhiteSpace(category) ? fallbackCategory : category,
            OverlayActionTooltipFormatting.FormatCastTime(row.Cast100ms),
            OverlayActionTooltipFormatting.FormatRecastTime(row.Recast100ms),
            OverlayActionTooltipFormatting.FormatDistance(row.Range),
            OverlayActionTooltipFormatting.FormatDistance(row.EffectRange),
            this.GetActionDescription(actionId),
            row.ClassJobLevel,
            Math.Max(row.MaxCharges, (byte)1));
        this.actionModels[actionId] = model;
        return model;
    }

    public TooltipContentDiagnostics CreateDiagnostics()
        => new(
            this.statusTexts.Count,
            this.actionModels.Count,
            this.actionDescriptions.Count);

    private string GetActionDescription(uint actionId)
    {
        if (this.actionDescriptions.TryGetValue(actionId, out var cached))
            return cached;

        string description;
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameActionTransient>();
            if (sheet is null)
                return string.Empty;

            description = this.seStringEvaluator
                .Evaluate(sheet.GetRow(actionId).Description)
                .ExtractText()
                .StripSoftHyphen();
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read action tooltip description for {actionId}.");
            description = this.GetRawActionDescription(actionId);
        }

        this.actionDescriptions[actionId] = description;
        return description;
    }

    private string GetRawActionDescription(uint actionId)
    {
        try
        {
            return this.dataManager.GetExcelSheet<GameActionTransient>()?
                       .GetRow(actionId)
                       .Description
                       .ExtractText()
                       .StripSoftHyphen()
                   ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
