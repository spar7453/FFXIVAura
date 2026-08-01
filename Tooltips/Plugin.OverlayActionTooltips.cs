namespace FFXIVAura;

public sealed partial class Plugin
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
        var model = this.tooltipContentService.GetActionModel(
            actionId,
            fallbackName,
            fallbackIconId,
            fallbackCategory);
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
        using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f));
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 3f);
        using var windowBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 5f));
        using var popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, new Vector4(0.055f, 0.055f, 0.06f, 0.98f));
        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, new Vector4(0.5f, 0.5f, 0.52f, 0.9f));
        using var tooltip = ImRaii.Tooltip();

        this.DrawOverlayActionTooltipHeader(model);
        ImGui.Separator();
        DrawOverlayActionTooltipStats(model);

        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            ImGui.Separator();
            using var wrapPos = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + contentWidth);
            ImGui.TextUnformatted(model.Description);
        }

        if (model.UnlockLevel > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.58f, 0.9f, 0.3f, 1f), $"습득 레벨 {model.UnlockLevel}");
        }
    }

    private void DrawOverlayActionTooltipHeader(OverlayActionTooltipModel model)
    {
        const float iconSize = 54f;
        if (model.IconId > 0)
        {
            var texture = this.iconTextureService.GetIcon(model.IconId);
            ImGui.Image(texture.Handle, new Vector2(iconSize, iconSize));
            ImGui.SameLine();
        }

        using var headerGroup = ImRaii.Group();
        ImGui.TextColored(new Vector4(0.97f, 0.97f, 0.97f, 1f), model.Name);
        if (!string.IsNullOrWhiteSpace(model.Category))
            ImGui.TextColored(new Vector4(0.68f, 0.68f, 0.7f, 1f), model.Category);
        ImGui.TextDisabled($"ID {model.ActionId}");
    }

    private static void DrawOverlayActionTooltipStats(OverlayActionTooltipModel model)
    {
        using var statsTable = ImRaii.Table("##overlay-action-tooltip-stats", 4, ImGuiTableFlags.SizingStretchProp);
        if (!statsTable)
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
    }

    private static void DrawTooltipStatCell(string text, bool disabled)
    {
        ImGui.TableNextColumn();
        if (disabled)
            ImGui.TextDisabled(text);
        else
            ImGui.TextUnformatted(text);
    }

}
