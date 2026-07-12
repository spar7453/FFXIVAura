using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly string[] CrossHotbarAddonNames =
    [
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
        "_ActionCrossEditor",
    ];

    private string GetActionKeybindText(uint baseActionId, uint displayActionId)
    {
        try
        {
            this.EnsureActionKeybindIndex();
            return this.actionKeybindIndex.Find(baseActionId, displayActionId, 0, 0);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read hotbar keybind for action {baseActionId}/{displayActionId}.");
            return string.Empty;
        }
    }

    private void EnsureActionKeybindIndex()
    {
        var now = DateTime.UtcNow;
        if (!this.keybindCacheDirty && now < this.keybindCacheRefreshAfter)
            return;

        this.keybindCacheDirty = false;
        this.keybindCacheRefreshAfter = now.AddSeconds(2);
        this.RebuildActionKeybindIndex();
    }

    private void RebuildActionKeybindIndex()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.KeybindRebuild);
        try
        {
            this.actionKeybindIndex.Clear();
            this.hotbarVisibilityCache.Clear();

            var hotbarModule = RaptureHotbarModule.Instance();
            if (hotbarModule is null)
                return;

            for (var visibleOnly = true; ; visibleOnly = false)
            {
                for (uint hotbarId = 0; hotbarId < 18; hotbarId++)
                {
                    var visible = this.IsHotbarVisible(hotbarId);
                    if (visibleOnly != visible)
                        continue;

                    for (uint slotIndex = 0; slotIndex < 16; slotIndex++)
                    {
                        var slot = hotbarModule->GetSlotById(hotbarId, slotIndex);
                        if (slot is null || slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                            continue;

                        if (!IsActionHotbarSlot(slot))
                            continue;

                        var text = GetHotbarSlotKeybindText(slot);
                        if (!string.IsNullOrWhiteSpace(text))
                            this.RegisterHotbarSlotKeybind(slot, text);
                    }
                }

                if (!visibleOnly)
                    break;
            }
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.KeybindRebuild, profileStart);
        }
    }

    private void RegisterHotbarSlotKeybind(RaptureHotbarModule.HotbarSlot* slot, string text)
    {
        this.RegisterHotbarActionKeybind(slot->CommandType, slot->CommandId, text);
        this.RegisterHotbarActionKeybind(slot->ApparentSlotType, slot->ApparentActionId, text);
        this.RegisterHotbarActionKeybind(slot->OriginalApparentSlotType, slot->OriginalApparentActionId, text);
    }

    private void RegisterHotbarActionKeybind(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        string text)
    {
        var actionId = HotbarKeybindPolicy.ResolveActionId(
            type,
            commandId,
            this.ResolveGeneralActionActionId);
        this.RegisterActionKeybind(actionId, text);
    }

    private void RegisterActionKeybind(uint actionId, string text)
    {
        if (actionId == 0)
            return;

        this.actionKeybindIndex.Register(actionId, GetAdjustedActionId(actionId), text);
    }

    private static string GetHotbarSlotKeybindText(RaptureHotbarModule.HotbarSlot* slot)
    {
        var text = KeybindTextFormatter.Format(slot->KeybindHintString, out var hasUnknownGlyph);
        if (!hasUnknownGlyph && !string.IsNullOrWhiteSpace(text))
            return text;

        var popupText = KeybindTextFormatter.Format(slot->PopUpKeybindHintString, out var popupHasUnknownGlyph);
        return !string.IsNullOrWhiteSpace(popupText) && (!popupHasUnknownGlyph || string.IsNullOrWhiteSpace(text))
            ? popupText
            : text;
    }

    private bool IsHotbarVisible(uint hotbarId)
    {
        if (this.hotbarVisibilityCache.TryGetValue(hotbarId, out var visible))
            return visible;

        var normalHotbarAddonName = HotbarKeybindPolicy.GetNormalHotbarAddonName(hotbarId);
        visible = normalHotbarAddonName is not null
            ? IsAddonVisible(normalHotbarAddonName)
            : CrossHotbarAddonNames.Any(IsAddonVisible);
        this.hotbarVisibilityCache[hotbarId] = visible;
        return visible;
    }

    private static bool IsAddonVisible(string addonName)
    {
        try
        {
            var addon = (AtkUnitBase*)GameGui.GetAddonByName(addonName).Address;
            return addon is not null && addon->IsVisible && addon->Scale > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsActionHotbarSlot(RaptureHotbarModule.HotbarSlot* slot)
    {
        return HotbarKeybindPolicy.IsSupportedActionType(slot->CommandType)
               || HotbarKeybindPolicy.IsSupportedActionType(slot->ApparentSlotType)
               || HotbarKeybindPolicy.IsSupportedActionType(slot->OriginalApparentSlotType);
    }

    private uint ResolveGeneralActionActionId(uint generalActionId)
    {
        if (generalActionId == 0)
            return 0;

        if (this.generalActionActionIdCache.TryGetValue(generalActionId, out var cached))
            return cached;

        var actionId = 0u;
        try
        {
            var sheet = DataManager.GetExcelSheet<GameGeneralAction>();
            if (sheet is not null)
                actionId = sheet.GetRow(generalActionId).Action.RowId;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to resolve general action {generalActionId}.");
        }

        this.generalActionActionIdCache[generalActionId] = actionId;
        return actionId;
    }

}
