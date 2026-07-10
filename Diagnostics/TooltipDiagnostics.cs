namespace FFXIVAura;

internal enum TooltipDiagnosticKind
{
    Ability,
    Aura,
    PartyCooldown,
}

internal readonly record struct TooltipDiagnosticSnapshot(
    int HoverHits,
    int AbilityRequests,
    int AuraRequests,
    int PartyCooldownRequests,
    int NativeActionRequests,
    int DisabledSkips,
    int ZeroActionSkips,
    int AgentMissingSkips,
    int AddonMissingSkips,
    int NativeControls,
    int NativeHoverDispatches,
    int NativeForcedShows,
    string LastKind,
    string LastId,
    string LastWindowId,
    string LastIconId,
    uint LastActionId,
    int LastDrawnIconCount,
    bool LastHitboxExpanded,
    string LastSkipReason,
    DateTime LastEventUtc,
    bool HasLastGeometry,
    Vector2 LastMouse,
    Vector2 LastRectMin,
    Vector2 LastRectMax,
    bool LastContainsMouse,
    bool LastImGuiHovered,
    uint LastAgentActionId,
    uint LastAgentOriginalId,
    bool HasLastNativeAddon,
    bool LastAddonVisible,
    Vector2 LastAddonSize,
    Vector2 LastControlMouse,
    Vector2 LastControlPosition);

internal sealed class TooltipDiagnostics
{
    private int hoverHits;
    private int abilityRequests;
    private int auraRequests;
    private int partyCooldownRequests;
    private int nativeActionRequests;
    private int disabledSkips;
    private int zeroActionSkips;
    private int agentMissingSkips;
    private int addonMissingSkips;
    private int nativeControls;
    private int nativeHoverDispatches;
    private int nativeForcedShows;
    private string lastKind = string.Empty;
    private string lastId = string.Empty;
    private string lastWindowId = string.Empty;
    private string lastIconId = string.Empty;
    private uint lastActionId;
    private int lastDrawnIconCount;
    private bool lastHitboxExpanded;
    private string lastSkipReason = string.Empty;
    private DateTime lastEventUtc = DateTime.MinValue;
    private bool hasLastGeometry;
    private Vector2 lastMouse;
    private Vector2 lastRectMin;
    private Vector2 lastRectMax;
    private bool lastContainsMouse;
    private bool lastImGuiHovered;
    private uint lastAgentActionId;
    private uint lastAgentOriginalId;
    private bool hasLastNativeAddon;
    private bool lastAddonVisible;
    private Vector2 lastAddonSize;
    private Vector2 lastControlMouse;
    private Vector2 lastControlPosition;

    public void RecordHover(
        TooltipDiagnosticKind kind,
        string windowId,
        string id,
        uint actionId,
        int drawnIconCount,
        bool hitboxExpanded,
        Vector2 mouse,
        Vector2 rectMin,
        Vector2 rectMax,
        bool containsMouse,
        bool imguiHovered)
    {
        this.hoverHits++;
        this.RememberLast(kind, id, actionId);
        this.lastWindowId = windowId.Trim();
        this.lastIconId = id.Trim();
        this.lastDrawnIconCount = Math.Max(0, drawnIconCount);
        this.lastHitboxExpanded = hitboxExpanded;
        this.hasLastGeometry = true;
        this.lastMouse = mouse;
        this.lastRectMin = rectMin;
        this.lastRectMax = rectMax;
        this.lastContainsMouse = containsMouse;
        this.lastImGuiHovered = imguiHovered;
    }

    public void RecordTooltipRequest(TooltipDiagnosticKind kind, string id, uint actionId)
    {
        switch (kind)
        {
            case TooltipDiagnosticKind.Ability:
                this.abilityRequests++;
                break;
            case TooltipDiagnosticKind.Aura:
                this.auraRequests++;
                break;
            case TooltipDiagnosticKind.PartyCooldown:
                this.partyCooldownRequests++;
                break;
        }

        this.lastSkipReason = string.Empty;
        this.RememberLast(kind, id, actionId);
    }

    public void RecordDisabledSkip(TooltipDiagnosticKind kind, string id, uint actionId)
    {
        this.disabledSkips++;
        this.RememberSkip(kind, id, actionId, "disabled");
    }

    public void RecordNativeActionRequest(uint actionId)
    {
        this.nativeActionRequests++;
        this.lastActionId = actionId;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public void RecordNativeAgentIds(uint actionId, uint originalId)
    {
        this.lastAgentActionId = actionId;
        this.lastAgentOriginalId = originalId;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public void RecordZeroActionSkip()
    {
        this.zeroActionSkips++;
        this.RememberNativeSkip("zeroActionId");
    }

    public void RecordAgentMissingSkip()
    {
        this.agentMissingSkips++;
        this.RememberNativeSkip("agentMissing");
    }

    public void RecordAddonMissingSkip()
    {
        this.addonMissingSkips++;
        this.RememberNativeSkip("addonMissing");
    }

    public void RecordNativeControl(NativeActionTooltipControlResult control)
    {
        this.nativeControls++;
        this.hasLastNativeAddon = true;
        this.lastAddonVisible = control.AddonVisible;
        this.lastAddonSize = control.AddonSize;
        this.lastControlMouse = control.MousePosition;
        this.lastControlPosition = control.Position;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public void RecordNativeHoverDispatch()
    {
        this.nativeHoverDispatches++;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public void RecordNativeForcedShow()
    {
        this.nativeForcedShows++;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public TooltipDiagnosticSnapshot CreateSnapshot()
        => new(
            this.hoverHits,
            this.abilityRequests,
            this.auraRequests,
            this.partyCooldownRequests,
            this.nativeActionRequests,
            this.disabledSkips,
            this.zeroActionSkips,
            this.agentMissingSkips,
            this.addonMissingSkips,
            this.nativeControls,
            this.nativeHoverDispatches,
            this.nativeForcedShows,
            this.lastKind,
            this.lastId,
            this.lastWindowId,
            this.lastIconId,
            this.lastActionId,
            this.lastDrawnIconCount,
            this.lastHitboxExpanded,
            this.lastSkipReason,
            this.lastEventUtc,
            this.hasLastGeometry,
            this.lastMouse,
            this.lastRectMin,
            this.lastRectMax,
            this.lastContainsMouse,
            this.lastImGuiHovered,
            this.lastAgentActionId,
            this.lastAgentOriginalId,
            this.hasLastNativeAddon,
            this.lastAddonVisible,
            this.lastAddonSize,
            this.lastControlMouse,
            this.lastControlPosition);

    public void ResetIntervalCounters()
    {
        this.hoverHits = 0;
        this.abilityRequests = 0;
        this.auraRequests = 0;
        this.partyCooldownRequests = 0;
        this.nativeActionRequests = 0;
        this.disabledSkips = 0;
        this.zeroActionSkips = 0;
        this.agentMissingSkips = 0;
        this.addonMissingSkips = 0;
        this.nativeControls = 0;
        this.nativeHoverDispatches = 0;
        this.nativeForcedShows = 0;
    }

    private void RememberSkip(TooltipDiagnosticKind kind, string id, uint actionId, string reason)
    {
        this.RememberLast(kind, id, actionId);
        this.lastSkipReason = reason;
    }

    private void RememberNativeSkip(string reason)
    {
        this.lastSkipReason = reason;
        this.lastEventUtc = DateTime.UtcNow;
    }

    private void RememberLast(TooltipDiagnosticKind kind, string id, uint actionId)
    {
        this.lastKind = kind.ToString();
        this.lastId = id.Trim();
        this.lastActionId = actionId;
        this.lastEventUtc = DateTime.UtcNow;
    }
}
