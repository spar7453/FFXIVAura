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
    string LastKind,
    string LastId,
    uint LastActionId,
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
    private int nativeActionRequests;
    private int disabledSkips;
    private int zeroActionSkips;
    private int agentMissingSkips;
    private int addonMissingSkips;
    private int nativeControls;
    private string lastKind = string.Empty;
    private string lastId = string.Empty;
    private uint lastActionId;
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
        string id,
        uint actionId,
        Vector2 mouse,
        Vector2 rectMin,
        Vector2 rectMax,
        bool containsMouse,
        bool imguiHovered)
    {
        this.hoverHits++;
        this.RememberLast(kind, id, actionId);
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

    public void RecordNativeControl()
    {
        this.nativeControls++;
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
            this.lastKind,
            this.lastId,
            this.lastActionId,
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
        this.nativeActionRequests = 0;
        this.disabledSkips = 0;
        this.zeroActionSkips = 0;
        this.agentMissingSkips = 0;
        this.addonMissingSkips = 0;
        this.nativeControls = 0;
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
