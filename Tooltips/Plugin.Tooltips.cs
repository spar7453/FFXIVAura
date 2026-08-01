namespace FFXIVAura;

public sealed partial class Plugin
{
    private void ShowAuraTooltip(AuraState aura)
    {
        var statusId = aura.TooltipStatusId;
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.Aura, statusId.ToString(), 0);
            return;
        }

        this.MarkOverlayTooltipRequested();
        this.performanceStats.CountTooltipRender();
        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Aura, statusId.ToString(), 0);
        this.SetBugDiagnosticEvent($"tooltipAura:{aura.StatusId}:{statusId}");
        ShowTextTooltipAtMouse(this.tooltipContentService.GetStatusText(statusId));
    }

    private void MarkOverlayTooltipRequested()
    {
        this.overlayTooltipRequestedThisFrame = true;
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
        using var tooltip = ImRaii.Tooltip();
        using var wrapPos = ImRaii.TextWrapPos(wrapWidth);
        ImGui.TextUnformatted(text);
    }
}
