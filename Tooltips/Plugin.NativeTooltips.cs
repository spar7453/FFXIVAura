using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void ShowNativeActionTooltip(uint actionId)
    {
        this.tooltipDiagnostics.RecordNativeActionRequest(actionId);
        if (actionId == 0)
        {
            this.tooltipDiagnostics.RecordZeroActionSkip();
            return;
        }

        var agent = AgentActionDetail.Instance();
        if (agent is null)
        {
            this.tooltipDiagnostics.RecordAgentMissingSkip();
            return;
        }

        this.tooltipDiagnostics.RecordNativeAgentIds(agent->ActionId, agent->OriginalId);
        this.overlayTooltipRequestedThisFrame = true;
        this.SetBugDiagnosticEvent($"tooltipActionNative:{actionId}");
        this.nativeActionTooltipController.BeginHover(actionId, DateTime.UtcNow);
        this.ControlNativeActionTooltip(NativeActionTooltipController.AddonName, suppressSound: false);
        try
        {
            agent->HandleActionHover(DetailKind.Action, actionId, flag: 0, isLovmActionDetail: false, a5: 0, a6: 0);
            this.tooltipDiagnostics.RecordNativeAgentIds(agent->ActionId, agent->OriginalId);
        }
        finally
        {
            this.nativeActionTooltipController.EndHover();
        }

        this.ControlNativeActionTooltip(NativeActionTooltipController.AddonName, suppressSound: true);
    }

    private void ShowAuraTooltip(AuraState aura)
    {
        if (!this.config.ShowTooltips)
        {
            this.tooltipDiagnostics.RecordDisabledSkip(TooltipDiagnosticKind.Aura, aura.StatusId.ToString(), 0);
            return;
        }

        this.overlayTooltipRequestedThisFrame = true;
        this.tooltipDiagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Aura, aura.StatusId.ToString(), 0);
        this.SetBugDiagnosticEvent($"tooltipAura:{aura.StatusId}");
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
        this.nativeActionTooltipController.ExpirePositioning();
        if (!this.nativeActionTooltipController.HasVisibleRequest)
        {
            this.nativeActionTooltipController.ClearTooltipRequest();
            this.RestoreNativeTooltipSound();
            return;
        }

        var (actionId, originalId) = this.GetNativeActionTooltipIds();
        if (!this.nativeActionTooltipController.ShouldHideNativeTooltip(actionId, originalId, GetAdjustedActionId))
        {
            this.nativeActionTooltipController.ClearTooltipRequest();
            this.RestoreNativeTooltipSound();
            return;
        }

        try
        {
            var addon = GetNativeTooltipAddon(NativeActionTooltipController.AddonName);
            if (addon is not null)
                this.nativeActionTooltipController.SuppressSound(addon);

            var agent = AgentActionDetail.Instance();
            if (agent is not null)
                agent->Hide();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to hide ActionDetail tooltip.");
        }

        this.nativeActionTooltipController.ClearTooltipRequest();
        this.RestoreNativeTooltipSound();
    }

    private void OnActionDetailTooltipLifecycle(AddonEvent type, AddonArgs args)
    {
        var now = DateTime.UtcNow;
        if (!this.nativeActionTooltipController.CanControlWithoutActionMatch(now))
            return;

        var (actionId, originalId) = this.GetNativeActionTooltipIds();
        if (!this.nativeActionTooltipController.ShouldControl(now, actionId, originalId, GetAdjustedActionId))
            return;

        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            this.ControlNativeActionTooltip(addon, NativeActionTooltipController.ShouldSuppressSound(type, addon is not null && addon->IsVisible));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to control ActionDetail tooltip during {type}.");
        }
    }

    private (uint ActionId, uint OriginalId) GetNativeActionTooltipIds()
    {
        try
        {
            var agent = AgentActionDetail.Instance();
            if (agent is null)
                return (0, 0);

            return (agent->ActionId, agent->OriginalId);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static uint GetAdjustedActionId(uint actionId)
    {
        return ActionManager.Instance()->GetAdjustedActionId(actionId);
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

        var wrapWidth = ImGui.GetFontSize() * 30f;
        var padding = ImGui.GetStyle().WindowPadding * 2f;
        var tooltipSize = ImGui.CalcTextSize(text, false, wrapWidth) + padding;
        ImGui.SetNextWindowPos(NativeActionTooltipController.GetPositionAtMouse(ImGui.GetMousePos(), ImGui.GetIO().DisplaySize, tooltipSize), ImGuiCond.Always);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(wrapWidth);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private void ControlNativeActionTooltip(string addonName, bool suppressSound)
    {
        var addon = GetNativeTooltipAddon(addonName);
        if (addon is not null)
        {
            this.ControlNativeActionTooltip(addon, suppressSound);
            return;
        }

        this.tooltipDiagnostics.RecordAddonMissingSkip();
    }

    private void ControlNativeActionTooltip(AtkUnitBase* addon, bool suppressSound)
    {
        if (addon is null)
        {
            this.tooltipDiagnostics.RecordAddonMissingSkip();
            return;
        }

        this.performanceStats.CountNativeTooltipControl();
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.TooltipControl);
        try
        {
            var control = this.nativeActionTooltipController.Control(addon, suppressSound, ImGui.GetMousePos(), ImGui.GetIO().DisplaySize);
            this.tooltipDiagnostics.RecordNativeControl(control);
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.TooltipControl, profileStart);
        }
    }

    private static AtkUnitBase* GetNativeTooltipAddon(string addonName)
    {
        try
        {
            return (AtkUnitBase*)GameGui.GetAddonByName(addonName).Address;
        }
        catch
        {
            return null;
        }
    }

    private void RestoreNativeTooltipSound()
    {
        if (!this.nativeActionTooltipController.HasCapturedSound)
            return;

        try
        {
            this.nativeActionTooltipController.RestoreSound(GetNativeTooltipAddon(NativeActionTooltipController.AddonName));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to restore ActionDetail tooltip sound.");
        }
    }
}
