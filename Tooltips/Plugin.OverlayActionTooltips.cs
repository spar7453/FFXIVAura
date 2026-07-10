namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void ShowOverlayActionTooltip(
        uint actionId,
        string fallbackName,
        uint fallbackIconId,
        string fallbackCategory)
    {
        if (actionId == 0)
        {
            this.tooltipDiagnostics.RecordZeroActionSkip();
            return;
        }

        this.MarkOverlayTooltipRequested();
        var model = this.GetOverlayActionTooltipModel(actionId, fallbackName, fallbackIconId, fallbackCategory);
        this.performanceStats.CountTooltipRender();
        this.tooltipDiagnostics.RecordOverlayActionRender(actionId);
        this.SetBugDiagnosticEvent($"tooltipActionOverlay:{actionId}");

        var tooltipWidth = Math.Clamp(ImGui.GetFontSize() * 32f, 430f, 560f);
        var contentWidth = tooltipWidth - 28f;
        var descriptionHeight = string.IsNullOrWhiteSpace(model.Description)
            ? 0f
            : ImGui.CalcTextSize(model.Description, false, contentWidth).Y;
        var estimatedSize = new Vector2(tooltipWidth, 176f + descriptionHeight);
        var position = OverlayTooltipPositioning.GetPositionAtMouse(
            ImGui.GetMousePos(),
            ImGui.GetIO().DisplaySize,
            estimatedSize);

        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(tooltipWidth, 0f),
            new Vector2(tooltipWidth, float.MaxValue));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 5f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.055f, 0.055f, 0.06f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.5f, 0.5f, 0.52f, 0.9f));
        ImGui.BeginTooltip();

        this.DrawOverlayActionTooltipHeader(model);
        ImGui.Separator();
        DrawOverlayActionTooltipStats(model);

        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            ImGui.Separator();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + contentWidth);
            ImGui.TextUnformatted(model.Description);
            ImGui.PopTextWrapPos();
        }

        if (model.UnlockLevel > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.58f, 0.9f, 0.3f, 1f), $"습득 레벨 {model.UnlockLevel}");
        }

        ImGui.EndTooltip();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(4);
    }

    private void DrawOverlayActionTooltipHeader(OverlayActionTooltipModel model)
    {
        const float iconSize = 54f;
        if (model.IconId > 0)
        {
            var lookup = new GameIconLookup(model.IconId, false, true, null);
            var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
            ImGui.Image(texture.Handle, new Vector2(iconSize, iconSize));
            ImGui.SameLine();
        }

        ImGui.BeginGroup();
        ImGui.TextColored(new Vector4(0.97f, 0.97f, 0.97f, 1f), model.Name);
        if (!string.IsNullOrWhiteSpace(model.Category))
            ImGui.TextColored(new Vector4(0.68f, 0.68f, 0.7f, 1f), model.Category);
        ImGui.TextDisabled($"ID {model.ActionId}");
        ImGui.EndGroup();
    }

    private static void DrawOverlayActionTooltipStats(OverlayActionTooltipModel model)
    {
        if (!ImGui.BeginTable("##overlay-action-tooltip-stats", 4, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("##label1", ImGuiTableColumnFlags.WidthFixed, 72f);
        ImGui.TableSetupColumn("##value1", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##label2", ImGuiTableColumnFlags.WidthFixed, 72f);
        ImGui.TableSetupColumn("##value2", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        DrawTooltipStatCell("거리", true);
        DrawTooltipStatCell(model.Range, false);
        DrawTooltipStatCell("범위", true);
        DrawTooltipStatCell(model.EffectRange, false);

        ImGui.TableNextRow();
        DrawTooltipStatCell("시전 시간", true);
        DrawTooltipStatCell(model.CastTime, false);
        DrawTooltipStatCell("재사용", true);
        DrawTooltipStatCell(model.RecastTime, false);

        if (OverlayActionTooltipFormatting.ShouldShowMaxCharges(model.MaxCharges, model.Description))
        {
            ImGui.TableNextRow();
            DrawTooltipStatCell("최대 누적 수", true);
            DrawTooltipStatCell(model.MaxCharges.ToString(), false);
            DrawTooltipStatCell(string.Empty, true);
            DrawTooltipStatCell(string.Empty, false);
        }

        ImGui.EndTable();
    }

    private static void DrawTooltipStatCell(string text, bool disabled)
    {
        ImGui.TableNextColumn();
        if (disabled)
            ImGui.TextDisabled(text);
        else
            ImGui.TextUnformatted(text);
    }

    private OverlayActionTooltipModel GetOverlayActionTooltipModel(
        uint actionId,
        string fallbackName,
        uint fallbackIconId,
        string fallbackCategory)
    {
        if (this.actionTooltipModelCache.TryGetValue(actionId, out var cached))
            return cached;

        var action = this.GetActionRow(actionId);
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
        var name = row.Name.ExtractText();
        var category = this.GetActionCategory(actionId).Name;
        var model = new OverlayActionTooltipModel(
            actionId,
            row.Icon > 0 ? row.Icon : fallbackIconId,
            string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            string.IsNullOrWhiteSpace(category) ? fallbackCategory : category,
            OverlayActionTooltipFormatting.FormatCastTime(row.Cast100ms),
            OverlayActionTooltipFormatting.FormatRecastTime(row.Recast100ms),
            OverlayActionTooltipFormatting.FormatDistance(row.Range),
            OverlayActionTooltipFormatting.FormatDistance(row.EffectRange),
            this.GetActionTooltipDescription(actionId),
            row.ClassJobLevel,
            Math.Max(row.MaxCharges, (byte)1));
        this.actionTooltipModelCache[actionId] = model;
        return model;
    }

    private string GetActionTooltipDescription(uint actionId)
    {
        try
        {
            var sheet = DataManager.GetExcelSheet<GameActionTransient>();
            if (sheet is null)
                return string.Empty;

            var description = sheet.GetRow(actionId).Description;
            return SeStringEvaluator.Evaluate(description).ExtractText().StripSoftHyphen();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action tooltip description for {actionId}.");
            try
            {
                return DataManager.GetExcelSheet<GameActionTransient>()?
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
}
