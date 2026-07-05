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

        this.nativeActionTooltipPositionUntil = DateTime.UtcNow.AddMilliseconds(120);
        SuppressNativeTooltipSound("ActionDetail");
        MoveNativeTooltipToMouse("ActionDetail", requireVisible: false);
        agent->HandleActionHover(DetailKind.Action, actionId, flag: 0, isLovmActionDetail: false, a5: 0, a6: 0);
        this.overlayTooltipRequestedThisFrame = true;
        this.nativeActionTooltipVisible = true;
        SuppressNativeTooltipSound("ActionDetail");
        MoveNativeTooltipToMouse("ActionDetail", requireVisible: false);
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
        this.nativeActionTooltipPositionUntil = DateTime.MinValue;
        if (!this.nativeActionTooltipVisible)
        {
            this.RestoreNativeTooltipSound();
            return;
        }

        try
        {
            SuppressNativeTooltipSound("ActionDetail");
            var agent = AgentActionDetail.Instance();
            if (agent is not null)
                agent->Hide();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to hide ActionDetail tooltip.");
        }

        this.nativeActionTooltipVisible = false;
        this.RestoreNativeTooltipSound();
    }

    private void OnActionDetailPreShow(AddonEvent type, AddonArgs args)
    {
        if (!this.ShouldControlNativeActionTooltip())
            return;

        try
        {
            this.SuppressNativeTooltipSound((AtkUnitBase*)args.Addon.Address);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to suppress ActionDetail tooltip sound before show.");
        }
    }

    private void OnActionDetailPreDraw(AddonEvent type, AddonArgs args)
    {
        if (!this.ShouldControlNativeActionTooltip())
            return;

        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            this.SuppressNativeTooltipSound(addon);
            MoveNativeTooltipToMouse(addon, requireVisible: false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to position ActionDetail tooltip before draw.");
        }
    }

    private bool ShouldControlNativeActionTooltip()
    {
        return this.nativeActionTooltipVisible || DateTime.UtcNow <= this.nativeActionTooltipPositionUntil;
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

    private static void MoveNativeTooltipToMouse(string addonName, bool requireVisible)
    {
        try
        {
            var addon = (AtkUnitBase*)GameGui.GetAddonByName(addonName).Address;
            MoveNativeTooltipToMouse(addon, requireVisible);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to position {addonName} tooltip.");
        }
    }

    private static void MoveNativeTooltipToMouse(AtkUnitBase* addon, bool requireVisible)
    {
        if (addon is null || (requireVisible && !addon->IsVisible))
            return;

        var position = GetTooltipPositionAtMouse(GetAddonSize(addon));
        addon->SetPosition((short)Math.Round(position.X), (short)Math.Round(position.Y));
    }

    private void SuppressNativeTooltipSound(string addonName)
    {
        try
        {
            var addon = (AtkUnitBase*)GameGui.GetAddonByName(addonName).Address;
            SuppressNativeTooltipSound(addon);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to suppress {addonName} tooltip sound.");
        }
    }

    private void SuppressNativeTooltipSound(AtkUnitBase* addon)
    {
        if (addon is null)
            return;

        if (!this.nativeActionTooltipSoundStateCaptured)
        {
            this.nativeActionTooltipOriginalShowSoundEffectId = addon->ShowSoundEffectId;
            this.nativeActionTooltipOriginalDisableShowHideSoundEffects = addon->DisableShowHideSoundEffects;
            this.nativeActionTooltipSoundStateCaptured = true;
        }

        addon->ShowSoundEffectId = 0;
        addon->DisableShowHideSoundEffects = true;
    }

    private void RestoreNativeTooltipSound()
    {
        if (!this.nativeActionTooltipSoundStateCaptured)
            return;

        try
        {
            var addon = (AtkUnitBase*)GameGui.GetAddonByName("ActionDetail").Address;
            if (addon is not null)
            {
                addon->ShowSoundEffectId = this.nativeActionTooltipOriginalShowSoundEffectId;
                addon->DisableShowHideSoundEffects = this.nativeActionTooltipOriginalDisableShowHideSoundEffects;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to restore ActionDetail tooltip sound.");
        }

        this.nativeActionTooltipSoundStateCaptured = false;
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
