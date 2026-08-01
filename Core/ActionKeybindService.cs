using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameGeneralAction = Lumina.Excel.Sheets.GeneralAction;

namespace FFXIVAura;

internal readonly record struct ActionKeybindDiagnostics(
    int KeybindCount,
    int HotbarVisibilityCount,
    int GeneralActionCount,
    bool IsDirty,
    DateTime RefreshAfterUtc);

internal sealed unsafe class ActionKeybindService
{
    private static readonly string[] CrossHotbarAddonNames =
    [
        "_ActionCross",
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
        "_ActionCrossEditor",
    ];

    private readonly IGameGui gameGui;
    private readonly IDataManager dataManager;
    private readonly IGameActionRuntime gameActionRuntime;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly IPluginLog log;
    private readonly ActionKeybindIndex index = new();
    private readonly Dictionary<uint, bool> hotbarVisibility = new();
    private readonly Dictionary<uint, uint> generalActionIds = new();
    private bool dirty = true;
    private DateTime refreshAfterUtc = DateTime.MinValue;

    public ActionKeybindService(
        IGameGui gameGui,
        IDataManager dataManager,
        IGameActionRuntime gameActionRuntime,
        PerformanceProfiler performanceProfiler,
        IPluginLog log)
    {
        this.gameGui = gameGui;
        this.dataManager = dataManager;
        this.gameActionRuntime = gameActionRuntime;
        this.performanceProfiler = performanceProfiler;
        this.log = log;
    }

    public int Count => this.index.Count;

    public string GetText(uint baseActionId, uint displayActionId)
    {
        try
        {
            this.EnsureIndex();
            return this.index.Find(baseActionId, displayActionId, 0, 0);
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read hotbar keybind for action {baseActionId}/{displayActionId}.");
            return string.Empty;
        }
    }

    public void Invalidate()
    {
        this.dirty = true;
        this.refreshAfterUtc = DateTime.MinValue;
    }

    public ActionKeybindDiagnostics CreateDiagnostics()
        => new(
            this.index.Count,
            this.hotbarVisibility.Count,
            this.generalActionIds.Count,
            this.dirty,
            this.refreshAfterUtc);

    private void EnsureIndex()
    {
        var nowUtc = DateTime.UtcNow;
        if (!this.dirty && nowUtc < this.refreshAfterUtc)
            return;

        this.dirty = false;
        this.refreshAfterUtc = nowUtc.AddSeconds(2);
        this.RebuildIndex();
    }

    private void RebuildIndex()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.KeybindRebuild);
        try
        {
            this.index.Clear();
            this.hotbarVisibility.Clear();

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

        this.index.Register(actionId, this.gameActionRuntime.GetAdjustedActionId(actionId), text);
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
        if (this.hotbarVisibility.TryGetValue(hotbarId, out var visible))
            return visible;

        var normalHotbarAddonName = HotbarKeybindPolicy.GetNormalHotbarAddonName(hotbarId);
        visible = normalHotbarAddonName is not null
            ? this.IsAddonVisible(normalHotbarAddonName)
            : CrossHotbarAddonNames.Any(this.IsAddonVisible);
        this.hotbarVisibility[hotbarId] = visible;
        return visible;
    }

    private bool IsAddonVisible(string addonName)
    {
        try
        {
            var addon = (AtkUnitBase*)this.gameGui.GetAddonByName(addonName).Address;
            return addon is not null && addon->IsVisible && addon->Scale > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsActionHotbarSlot(RaptureHotbarModule.HotbarSlot* slot)
        => HotbarKeybindPolicy.IsSupportedActionType(slot->CommandType)
           || HotbarKeybindPolicy.IsSupportedActionType(slot->ApparentSlotType)
           || HotbarKeybindPolicy.IsSupportedActionType(slot->OriginalApparentSlotType);

    private uint ResolveGeneralActionActionId(uint generalActionId)
    {
        if (generalActionId == 0)
            return 0;

        if (this.generalActionIds.TryGetValue(generalActionId, out var cached))
            return cached;

        var actionId = 0u;
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameGeneralAction>();
            if (sheet is not null)
                actionId = sheet.GetRow(generalActionId).Action.RowId;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to resolve general action {generalActionId}.");
        }

        this.generalActionIds[generalActionId] = actionId;
        return actionId;
    }
}
