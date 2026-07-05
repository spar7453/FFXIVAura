using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void ShowNativeActionTooltip(uint actionId)
    {
        if (actionId == 0)
            return;

        var agent = AgentActionDetail.Instance();
        if (agent is null)
            return;

        this.overlayTooltipRequestedThisFrame = true;
        this.nativeActionTooltipController.BeginHover(actionId, DateTime.UtcNow);
        this.ControlNativeActionTooltip(NativeActionTooltipController.AddonName, suppressSound: false);
        try
        {
            agent->HandleActionHover(DetailKind.Action, actionId, flag: 0, isLovmActionDetail: false, a5: 0, a6: 0);
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

        ImGui.SetNextWindowPos(NativeActionTooltipController.GetPositionAtMouse(ImGui.GetMousePos(), ImGui.GetIO().DisplaySize, Vector2.Zero), ImGuiCond.Always);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private void ControlNativeActionTooltip(string addonName, bool suppressSound)
    {
        var addon = GetNativeTooltipAddon(addonName);
        if (addon is not null)
            this.ControlNativeActionTooltip(addon, suppressSound);
    }

    private void ControlNativeActionTooltip(AtkUnitBase* addon, bool suppressSound)
    {
        this.nativeActionTooltipController.Control(addon, suppressSound, ImGui.GetMousePos(), ImGui.GetIO().DisplaySize);
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
