using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly Vector2 TooltipMouseOffset = new(18f, 18f);

    private void ShowNativeActionTooltip(uint actionId)
    {
        if (actionId == 0)
            return;

        var agent = AgentActionDetail.Instance();
        if (agent is null)
            return;

        agent->HandleActionHover(DetailKind.Action, actionId, flag: 0, isLovmActionDetail: false, a5: 0, a6: 0);
        this.overlayTooltipRequestedThisFrame = true;
        this.nativeActionTooltipVisible = true;
        MoveNativeTooltipToMouse("ActionDetail");
    }

    private void ShowAuraTooltip(AuraState aura)
    {
        if (!this.config.ShowTooltips)
            return;

        this.overlayTooltipRequestedThisFrame = true;
        this.HideNativeActionTooltip();
        var text = this.GetStatusTooltipText(aura.StatusId);
        ShowTextTooltipAtMouse(text);
    }

    private void FinishOverlayTooltipFrame()
    {
        if (!this.overlayTooltipRequestedThisFrame)
            this.HideNativeActionTooltip();
    }

    private void HideNativeActionTooltip()
    {
        if (!this.nativeActionTooltipVisible)
            return;

        try
        {
            var agent = AgentActionDetail.Instance();
            if (agent is not null)
                agent->Hide();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to hide ActionDetail tooltip.");
        }

        this.nativeActionTooltipVisible = false;
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
                var name = row.Name.ExtractText();
                var description = row.Description.ExtractText();
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

        ImGui.SetNextWindowPos(GetTooltipPositionAtMouse(Vector2.Zero), ImGuiCond.Always);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void MoveNativeTooltipToMouse(string addonName)
    {
        try
        {
            var addon = (AtkUnitBase*)GameGui.GetAddonByName(addonName).Address;
            if (addon is null || !addon->IsVisible)
                return;

            var position = GetTooltipPositionAtMouse(GetAddonSize(addon));
            addon->SetPosition((short)Math.Round(position.X), (short)Math.Round(position.Y));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to position {addonName} tooltip.");
        }
    }

    private static Vector2 GetTooltipPositionAtMouse(Vector2 tooltipSize)
    {
        var position = ImGui.GetMousePos() + TooltipMouseOffset;
        var displaySize = ImGui.GetIO().DisplaySize;
        if (displaySize.X <= 0f || displaySize.Y <= 0f || tooltipSize.X <= 0f || tooltipSize.Y <= 0f)
            return position;

        return new Vector2(
            Math.Clamp(position.X, 0f, Math.Max(0f, displaySize.X - tooltipSize.X)),
            Math.Clamp(position.Y, 0f, Math.Max(0f, displaySize.Y - tooltipSize.Y)));
    }

    private static Vector2 GetAddonSize(AtkUnitBase* addon)
    {
        try
        {
            return new Vector2(
                Math.Max(0f, addon->GetScaledWidth(true)),
                Math.Max(0f, addon->GetScaledHeight(true)));
        }
        catch
        {
            return Vector2.Zero;
        }
    }
}
