using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly string[] NormalHotbarAddonNames =
    [
        "_ActionBar09",
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
    ];

    private static readonly string[] CrossHotbarAddonNames =
    [
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
        "_ActionCrossEditor",
    ];

    private string GetActionKeybindText(uint baseActionId, uint displayActionId)
    {
        var now = DateTime.UtcNow;
        if (this.keybindCacheDirty || now >= this.keybindCacheRefreshAfter)
        {
            this.keybindTextCache.Clear();
            this.hotbarVisibilityCache.Clear();
            this.keybindCacheDirty = false;
            this.keybindCacheRefreshAfter = now.AddSeconds(2);
        }

        var key = (baseActionId, displayActionId);
        if (!this.keybindTextCache.TryGetValue(key, out var text))
        {
            text = this.ComputeActionKeybindText(baseActionId, displayActionId);
            this.keybindTextCache[key] = text;
        }

        return text;
    }

    private string ComputeActionKeybindText(uint baseActionId, uint displayActionId)
    {
        try
        {
            var adjustedBaseActionId = ActionManager.Instance()->GetAdjustedActionId(baseActionId);
            var adjustedDisplayActionId = ActionManager.Instance()->GetAdjustedActionId(displayActionId);

            for (var visibleOnly = true; ; visibleOnly = false)
            {
                for (uint hotbarId = 0; hotbarId < 18; hotbarId++)
                {
                    var visible = this.IsHotbarVisible(hotbarId);
                    if (visibleOnly != visible)
                        continue;

                    for (uint slotIndex = 0; slotIndex < 16; slotIndex++)
                    {
                        var slot = RaptureHotbarModule.Instance()->GetSlotById(hotbarId, slotIndex);
                        if (slot is null || slot->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                            continue;

                        if (!IsActionHotbarSlot(slot))
                            continue;

                        if (!MatchesActionSlot(slot, baseActionId, displayActionId, adjustedBaseActionId, adjustedDisplayActionId))
                            continue;

                        var text = KeybindTextFormatter.Format(slot->KeybindHintString, out var hasUnknownGlyph);
                        if (hasUnknownGlyph || string.IsNullOrWhiteSpace(text))
                        {
                            var popupText = KeybindTextFormatter.Format(slot->PopUpKeybindHintString, out var popupHasUnknownGlyph);
                            if (!string.IsNullOrWhiteSpace(popupText) && (!popupHasUnknownGlyph || string.IsNullOrWhiteSpace(text)))
                                text = popupText;
                        }

                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }

                if (!visibleOnly)
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read hotbar keybind for action {baseActionId}/{displayActionId}.");
        }

        return string.Empty;
    }

    private bool IsHotbarVisible(uint hotbarId)
    {
        if (this.hotbarVisibilityCache.TryGetValue(hotbarId, out var visible))
            return visible;

        visible = hotbarId < NormalHotbarAddonNames.Length
            ? IsAddonVisible(NormalHotbarAddonNames[hotbarId])
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
        return slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action
               || slot->CommandType == RaptureHotbarModule.HotbarSlotType.GeneralAction
               || slot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action
               || slot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.GeneralAction
               || slot->OriginalApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action
               || slot->OriginalApparentSlotType == RaptureHotbarModule.HotbarSlotType.GeneralAction;
    }

    private static bool MatchesActionSlot(
        RaptureHotbarModule.HotbarSlot* slot,
        uint baseActionId,
        uint displayActionId,
        uint adjustedBaseActionId,
        uint adjustedDisplayActionId)
    {
        return MatchesActionId(slot->CommandId, baseActionId, displayActionId, adjustedBaseActionId, adjustedDisplayActionId)
               || MatchesActionId(slot->ApparentActionId, baseActionId, displayActionId, adjustedBaseActionId, adjustedDisplayActionId)
               || MatchesActionId(slot->OriginalApparentActionId, baseActionId, displayActionId, adjustedBaseActionId, adjustedDisplayActionId);
    }

    private static bool MatchesActionId(uint hotbarActionId, uint baseActionId, uint displayActionId, uint adjustedBaseActionId, uint adjustedDisplayActionId)
    {
        if (hotbarActionId == 0)
            return false;

        if (hotbarActionId == baseActionId
            || hotbarActionId == displayActionId
            || hotbarActionId == adjustedBaseActionId
            || hotbarActionId == adjustedDisplayActionId)
            return true;

        var adjustedHotbarActionId = ActionManager.Instance()->GetAdjustedActionId(hotbarActionId);
        return adjustedHotbarActionId != 0
               && (adjustedHotbarActionId == baseActionId
                   || adjustedHotbarActionId == displayActionId
                   || adjustedHotbarActionId == adjustedBaseActionId
                   || adjustedHotbarActionId == adjustedDisplayActionId);
    }

}
