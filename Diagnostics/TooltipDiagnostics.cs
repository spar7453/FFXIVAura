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
    int OverlayActionRenders,
    int DisabledSkips,
    int ZeroActionSkips,
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
    bool LastImGuiHovered);

internal sealed class TooltipDiagnostics
{
    private int hoverHits;
    private int abilityRequests;
    private int auraRequests;
    private int partyCooldownRequests;
    private int overlayActionRenders;
    private int disabledSkips;
    private int zeroActionSkips;
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
        this.RememberLast(kind, id, actionId);
        this.lastSkipReason = "disabled";
    }

    public void RecordOverlayActionRender(uint actionId)
    {
        this.overlayActionRenders++;
        this.lastActionId = actionId;
        this.lastEventUtc = DateTime.UtcNow;
    }

    public void RecordZeroActionSkip()
    {
        this.zeroActionSkips++;
        this.lastSkipReason = "zeroActionId";
        this.lastEventUtc = DateTime.UtcNow;
    }

    public TooltipDiagnosticSnapshot CreateSnapshot()
        => new(
            this.hoverHits,
            this.abilityRequests,
            this.auraRequests,
            this.partyCooldownRequests,
            this.overlayActionRenders,
            this.disabledSkips,
            this.zeroActionSkips,
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
            this.lastImGuiHovered);

    public void ResetIntervalCounters()
    {
        this.hoverHits = 0;
        this.abilityRequests = 0;
        this.auraRequests = 0;
        this.partyCooldownRequests = 0;
        this.overlayActionRenders = 0;
        this.disabledSkips = 0;
        this.zeroActionSkips = 0;
    }

    private void RememberLast(TooltipDiagnosticKind kind, string id, uint actionId)
    {
        this.lastKind = kind.ToString();
        this.lastId = id.Trim();
        this.lastActionId = actionId;
        this.lastEventUtc = DateTime.UtcNow;
    }
}
