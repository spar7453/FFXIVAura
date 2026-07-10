namespace FFXIVAura;

internal sealed class NativeActionTooltipState
{
    public uint ActionId { get; private set; }
    public bool Visible { get; private set; }
    public bool HoverInProgress { get; private set; }
    public bool SoundStateCaptured { get; private set; }

    private DateTime positionUntilUtc = DateTime.MinValue;
    private DateTime nextHiddenRetryUtc = DateTime.MinValue;
    private short originalShowSoundEffectId;
    private bool originalDisableShowHideSoundEffects;

    public bool BeginHover(
        uint actionId,
        DateTime nowUtc,
        TimeSpan positionDuration,
        TimeSpan hiddenRetryInterval,
        bool addonVisible)
    {
        if (actionId == 0)
            return false;

        var actionChanged = !this.Visible || this.ActionId != actionId;
        this.ActionId = actionId;
        this.Visible = true;
        this.HoverInProgress = true;
        this.positionUntilUtc = nowUtc.Add(positionDuration);
        if (actionChanged || (!addonVisible && nowUtc >= this.nextHiddenRetryUtc))
        {
            this.nextHiddenRetryUtc = nowUtc.Add(hiddenRetryInterval);
            return true;
        }

        return false;
    }

    public void EndHover()
    {
        this.HoverInProgress = false;
    }

    public void ExpirePositioning()
    {
        this.positionUntilUtc = DateTime.MinValue;
    }

    public bool ShouldControl(DateTime nowUtc, bool matchesRequestedAction)
    {
        if (!this.CanControlWithoutActionMatch(nowUtc))
            return false;

        return this.HoverInProgress || matchesRequestedAction;
    }

    public bool CanControlWithoutActionMatch(DateTime nowUtc)
    {
        return this.Visible || nowUtc <= this.positionUntilUtc;
    }

    public bool ShouldHideNativeTooltip(bool matchesRequestedAction)
    {
        return this.Visible && (this.HoverInProgress || matchesRequestedAction);
    }

    public void ClearTooltipRequest()
    {
        this.ActionId = 0;
        this.Visible = false;
        this.HoverInProgress = false;
        this.positionUntilUtc = DateTime.MinValue;
        this.nextHiddenRetryUtc = DateTime.MinValue;
    }

    public void CaptureSoundState(short showSoundEffectId, bool disableShowHideSoundEffects)
    {
        if (this.SoundStateCaptured)
            return;

        this.originalShowSoundEffectId = showSoundEffectId;
        this.originalDisableShowHideSoundEffects = disableShowHideSoundEffects;
        this.SoundStateCaptured = true;
    }

    public bool TryGetCapturedSoundState(out short showSoundEffectId, out bool disableShowHideSoundEffects)
    {
        showSoundEffectId = this.originalShowSoundEffectId;
        disableShowHideSoundEffects = this.originalDisableShowHideSoundEffects;
        return this.SoundStateCaptured;
    }

    public void ClearSoundState()
    {
        this.originalShowSoundEffectId = 0;
        this.originalDisableShowHideSoundEffects = false;
        this.SoundStateCaptured = false;
    }
}
