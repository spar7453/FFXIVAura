namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void ShowAuraTooltip(AuraState aura)
    {
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.Aura, aura.StatusId.ToString(), 0);
            return;
        }

        this.MarkOverlayTooltipRequested();
        this.performanceStats.CountTooltipRender();
        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Aura, aura.StatusId.ToString(), 0);
        this.SetBugDiagnosticEvent($"tooltipAura:{aura.StatusId}");
        ShowTextTooltipAtMouse(this.GetStatusTooltipText(aura.StatusId));
    }

    private void MarkOverlayTooltipRequested()
    {
        this.overlayTooltipRequestedThisFrame = true;
    }

    private string GetStatusTooltipText(uint statusId)
    {
        if (this.statusTooltipTextCache.TryGetValue(statusId, out var cached))
            return cached;

        var text = $"Status {statusId}";
        var shouldCache = false;
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText().StripSoftHyphen();
                var description = SeStringEvaluator.Evaluate(row.Description).ExtractText().StripSoftHyphen();
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
            Log.Debug(ex, $"Failed to read status tooltip {statusId}.");
        }

        if (shouldCache)
            this.statusTooltipTextCache[statusId] = text;

        return text;
    }

    private static void ShowTextTooltipAtMouse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var wrapWidth = ImGui.GetFontSize() * 30f;
        var padding = ImGui.GetStyle().WindowPadding * 2f;
        var tooltipSize = ImGui.CalcTextSize(text, false, wrapWidth) + padding;
        ImGui.SetNextWindowPos(
            OverlayTooltipPositioning.GetPositionAtMouse(
                ImGui.GetMousePos(),
                ImGui.GetIO().DisplaySize,
                tooltipSize),
            ImGuiCond.Always);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(wrapWidth);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
