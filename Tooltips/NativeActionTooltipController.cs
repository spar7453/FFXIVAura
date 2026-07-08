using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FFXIVAura;

internal readonly record struct NativeActionTooltipControlResult(
    bool AddonVisible,
    Vector2 AddonSize,
    Vector2 MousePosition,
    Vector2 Position);

internal sealed unsafe class NativeActionTooltipController
{
    public const string AddonName = "ActionDetail";

    private static readonly Vector2 TooltipMouseOffset = new(18f, 18f);
    private static readonly TimeSpan PositionDuration = TimeSpan.FromMilliseconds(500);
    private readonly NativeActionTooltipState state;

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

    public void BeginHover(uint actionId, DateTime nowUtc)
    {
        this.state.BeginHover(actionId, nowUtc, PositionDuration);
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

    public NativeActionTooltipControlResult Control(AtkUnitBase* addon, bool suppressSound, Vector2 mousePos, Vector2 displaySize)
    {
        if (addon is null)
            return default;

        if (suppressSound)
            this.SuppressSound(addon);

        var addonSize = GetAddonSize(addon);
        var position = GetPositionAtMouse(mousePos, displaySize, addonSize);
        addon->SetPosition((short)Math.Round(position.X), (short)Math.Round(position.Y));
        return new NativeActionTooltipControlResult(addon->IsVisible, addonSize, mousePos, position);
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

    public static Vector2 GetPositionAtMouse(Vector2 mousePos, Vector2 displaySize, Vector2 tooltipSize)
    {
        var position = mousePos + TooltipMouseOffset;
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
