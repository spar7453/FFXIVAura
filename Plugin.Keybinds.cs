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
        if (this.keybindCacheDirty)
        {
            this.keybindTextCache.Clear();
            this.keybindCacheDirty = false;
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

            for (uint hotbarId = 0; hotbarId < 18; hotbarId++)
            {
                if (!IsHotbarVisible(hotbarId))
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

                    var text = FormatKeybindText(slot->KeybindHintString);
                    if (HasUnknownKeybindGlyph(text))
                        text = FormatKeybindText(slot->PopUpKeybindHintString);

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read hotbar keybind for action {baseActionId}/{displayActionId}.");
        }

        return string.Empty;
    }

    private static bool IsHotbarVisible(uint hotbarId)
    {
        if (hotbarId < NormalHotbarAddonNames.Length)
            return IsAddonVisible(NormalHotbarAddonNames[hotbarId]);

        return CrossHotbarAddonNames.Any(IsAddonVisible);
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

    private static string FormatKeybindText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var formatted = text.Trim()
            .Trim('[', ']')
            .Replace("\u00a7", "s", StringComparison.Ordinal)
            .Replace("\u00a2", "c", StringComparison.Ordinal)
            .Replace("\u00aa", "a", StringComparison.Ordinal)
            .Replace("\u00ba", "n", StringComparison.Ordinal)
            .Replace("?", "a", StringComparison.Ordinal)
            .Replace("Shift+", "s", StringComparison.OrdinalIgnoreCase)
            .Replace("Shift-", "s", StringComparison.OrdinalIgnoreCase)
            .Replace("Ctrl+", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Ctrl-", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Control+", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Control-", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt+", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("Alt-", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("Num", "n", StringComparison.OrdinalIgnoreCase)
            .Replace("+", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return RemoveUnsupportedKeybindGlyphs(formatted);
    }

    private static bool HasUnknownKeybindGlyph(string text)
    {
        return text.Any(ch => ch == '?' || ch == '\ufffd' || !IsSupportedKeybindChar(ch));
    }

    private static string RemoveUnsupportedKeybindGlyphs(string text)
    {
        return new string(text.Where(IsSupportedKeybindChar).ToArray());
    }

    private static bool IsSupportedKeybindChar(char ch)
    {
        return ch is >= 'a' and <= 'z' or >= '0' and <= '9';
    }
}
