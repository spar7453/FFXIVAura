using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

internal readonly record struct NativeActionTooltipControlResult(
    bool AddonVisible,
    Vector2 AddonSize,
    Vector2 MousePosition,
    Vector2 Position);

internal readonly record struct NativeTooltipAvoidanceRect(Vector2 Min, Vector2 Max);

internal sealed unsafe class NativeActionTooltipController
{
    public const string AddonName = "ActionDetail";

    private static readonly Vector2 TooltipMouseOffset = new(18f, 18f);
    private static readonly TimeSpan PositionDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HiddenRetryInterval = TimeSpan.FromMilliseconds(120);
    private readonly NativeActionTooltipState state;
    private NativeActionTooltipControlResult lastVisibleControl;

    public NativeActionTooltipController()
        : this(new NativeActionTooltipState())
    {
    }

    internal NativeActionTooltipController(NativeActionTooltipState state)
    {
        this.state = state;
    }

    public bool HasVisibleRequest => this.state.Visible;

    public bool HasCapturedSound => this.state.SoundStateCaptured;

    public bool CanControlWithoutActionMatch(DateTime nowUtc)
    {
        return this.state.CanControlWithoutActionMatch(nowUtc);
    }

    public bool BeginHover(uint actionId, DateTime nowUtc, bool addonVisible)
    {
        return this.state.BeginHover(actionId, nowUtc, PositionDuration, HiddenRetryInterval, addonVisible);
    }

    public void EndHover()
    {
        this.state.EndHover();
    }

    public void ExpirePositioning()
    {
        this.state.ExpirePositioning();
    }

    public void ClearTooltipRequest()
    {
        this.state.ClearTooltipRequest();
        this.lastVisibleControl = default;
    }

    public bool ShouldControl(DateTime nowUtc, uint actionId, uint originalId, Func<uint, uint> getAdjustedActionId)
    {
        return this.state.ShouldControl(nowUtc, this.MatchesRequestedAction(actionId, originalId, getAdjustedActionId));
    }

    public bool ShouldHideNativeTooltip(uint actionId, uint originalId, Func<uint, uint> getAdjustedActionId)
    {
        return this.state.ShouldHideNativeTooltip(this.MatchesRequestedAction(actionId, originalId, getAdjustedActionId));
    }

    public bool MatchesRequestedAction(uint actionId, uint originalId, Func<uint, uint> getAdjustedActionId)
    {
        return NativeActionTooltipIdMatcher.MatchesAny(this.state.ActionId, actionId, originalId, getAdjustedActionId);
    }

    public NativeActionTooltipControlResult Control(
        AtkUnitBase* addon,
        bool suppressSound,
        Vector2 mousePos,
        Vector2 displaySize,
        IReadOnlyList<NativeTooltipAvoidanceRect>? avoidanceRects = null)
    {
        if (addon is null)
            return default;

        if (suppressSound)
            this.SuppressSound(addon);

        var addonSize = GetAddonSize(addon);
        var position = GetPositionAtMouse(mousePos, displaySize, addonSize, avoidanceRects);
        addon->SetPosition((short)Math.Round(position.X), (short)Math.Round(position.Y));
        var result = new NativeActionTooltipControlResult(addon->IsVisible, addonSize, mousePos, position);
        if (result.AddonVisible && result.AddonSize.X > 0f && result.AddonSize.Y > 0f)
            this.lastVisibleControl = result;

        return result;
    }

    public bool EnsureVisible(AtkUnitBase* addon)
    {
        if (addon is null || addon->IsVisible || !addon->IsReady)
            return false;

        this.SuppressSound(addon);
        addon->Show(disableShowTransition: true, unsetShowHideFlags: 0);
        return addon->IsVisible;
    }

    public bool ContainsActiveTooltip(Vector2 point)
    {
        if (!this.state.Visible || !this.lastVisibleControl.AddonVisible)
            return false;

        var min = this.lastVisibleControl.Position;
        var max = min + this.lastVisibleControl.AddonSize;
        return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    }

    public void SuppressSound(AtkUnitBase* addon)
    {
        if (addon is null)
            return;

        this.state.CaptureSoundState(addon->ShowSoundEffectId, addon->DisableShowHideSoundEffects);
        addon->ShowSoundEffectId = 0;
        addon->DisableShowHideSoundEffects = true;
    }

    public void RestoreSound(AtkUnitBase* addon)
    {
        if (!this.state.TryGetCapturedSoundState(out var showSoundEffectId, out var disableShowHideSoundEffects))
            return;

        try
        {
            if (addon is not null)
            {
                addon->ShowSoundEffectId = showSoundEffectId;
                addon->DisableShowHideSoundEffects = disableShowHideSoundEffects;
            }
        }
        finally
        {
            this.state.ClearSoundState();
        }
    }

    public static bool ShouldSuppressSound(AddonEvent type, bool addonVisible)
    {
        var isShowEvent = type is AddonEvent.PreShow or AddonEvent.PostShow;
        var isVisibleLifecycleEvent = type is AddonEvent.PreRequestedUpdate
            or AddonEvent.PostRequestedUpdate
            or AddonEvent.PreRefresh
            or AddonEvent.PostRefresh
            or AddonEvent.PreUpdate
            or AddonEvent.PostUpdate
            or AddonEvent.PreDraw
            or AddonEvent.PostDraw;
        return ShouldSuppressSound(isShowEvent, isVisibleLifecycleEvent, addonVisible);
    }

    public static bool ShouldSuppressSound(bool isShowEvent, bool isVisibleLifecycleEvent, bool addonVisible)
    {
        if (isShowEvent)
            return true;

        return addonVisible && isVisibleLifecycleEvent;
    }

    public static Vector2 GetPositionAtMouse(
        Vector2 mousePos,
        Vector2 displaySize,
        Vector2 tooltipSize,
        IReadOnlyList<NativeTooltipAvoidanceRect>? avoidanceRects = null)
    {
        var position = mousePos + TooltipMouseOffset;
        if (displaySize.X <= 0f || displaySize.Y <= 0f || tooltipSize.X <= 0f || tooltipSize.Y <= 0f)
            return position;

        Span<Vector2> candidates = stackalloc Vector2[8];
        var candidateCount = 0;
        if (TryFindContainingRect(mousePos, avoidanceRects, out var containingRect))
        {
            candidates[candidateCount++] = new Vector2(mousePos.X + TooltipMouseOffset.X, containingRect.Max.Y + TooltipMouseOffset.Y);
            candidates[candidateCount++] = new Vector2(mousePos.X + TooltipMouseOffset.X, containingRect.Min.Y - tooltipSize.Y - TooltipMouseOffset.Y);
            candidates[candidateCount++] = new Vector2(containingRect.Max.X + TooltipMouseOffset.X, mousePos.Y + TooltipMouseOffset.Y);
            candidates[candidateCount++] = new Vector2(containingRect.Min.X - tooltipSize.X - TooltipMouseOffset.X, mousePos.Y + TooltipMouseOffset.Y);
        }

        candidates[candidateCount++] = mousePos + TooltipMouseOffset;
        candidates[candidateCount++] = new Vector2(mousePos.X - tooltipSize.X - TooltipMouseOffset.X, mousePos.Y + TooltipMouseOffset.Y);
        candidates[candidateCount++] = new Vector2(mousePos.X + TooltipMouseOffset.X, mousePos.Y - tooltipSize.Y - TooltipMouseOffset.Y);
        candidates[candidateCount++] = mousePos - tooltipSize - TooltipMouseOffset;

        for (var index = 0; index < candidateCount; index++)
        {
            var candidate = candidates[index];
            if (!FitsDisplay(candidate, displaySize, tooltipSize))
                continue;

            if (GetOverlapArea(candidate, tooltipSize, avoidanceRects) <= 0f)
                return candidate;
        }

        var bestPosition = ClampPosition(candidates[0], displaySize, tooltipSize);
        var bestOverlap = GetOverlapArea(bestPosition, tooltipSize, avoidanceRects);
        for (var index = 0; index < candidateCount; index++)
        {
            var candidate = ClampPosition(candidates[index], displaySize, tooltipSize);
            var overlap = GetOverlapArea(candidate, tooltipSize, avoidanceRects);
            if (overlap >= bestOverlap)
                continue;

            bestPosition = candidate;
            bestOverlap = overlap;
        }

        return bestPosition;
    }

    private static bool FitsDisplay(Vector2 position, Vector2 displaySize, Vector2 tooltipSize)
        => position.X >= 0f
           && position.Y >= 0f
           && position.X + tooltipSize.X <= displaySize.X
           && position.Y + tooltipSize.Y <= displaySize.Y;

    private static Vector2 ClampPosition(Vector2 position, Vector2 displaySize, Vector2 tooltipSize)
        => new(
            Math.Clamp(position.X, 0f, Math.Max(0f, displaySize.X - tooltipSize.X)),
            Math.Clamp(position.Y, 0f, Math.Max(0f, displaySize.Y - tooltipSize.Y)));

    private static bool TryFindContainingRect(
        Vector2 point,
        IReadOnlyList<NativeTooltipAvoidanceRect>? avoidanceRects,
        out NativeTooltipAvoidanceRect containingRect)
    {
        if (avoidanceRects is not null)
        {
            foreach (var rect in avoidanceRects)
            {
                if (point.X < rect.Min.X || point.X > rect.Max.X || point.Y < rect.Min.Y || point.Y > rect.Max.Y)
                    continue;

                containingRect = rect;
                return true;
            }
        }

        containingRect = default;
        return false;
    }

    private static float GetOverlapArea(
        Vector2 position,
        Vector2 tooltipSize,
        IReadOnlyList<NativeTooltipAvoidanceRect>? avoidanceRects)
    {
        if (avoidanceRects is null || avoidanceRects.Count == 0)
            return 0f;

        var max = position + tooltipSize;
        var overlapArea = 0f;
        foreach (var rect in avoidanceRects)
        {
            var overlapWidth = Math.Max(0f, Math.Min(max.X, rect.Max.X) - Math.Max(position.X, rect.Min.X));
            var overlapHeight = Math.Max(0f, Math.Min(max.Y, rect.Max.Y) - Math.Max(position.Y, rect.Min.Y));
            overlapArea += overlapWidth * overlapHeight;
        }

        return overlapArea;
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
