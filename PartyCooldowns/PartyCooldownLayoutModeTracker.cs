namespace FFXIVAura;

internal sealed class PartyCooldownLayoutModeTracker
{
    private readonly TimeSpan retentionDuration;
    private DateTime allianceExpiresAtUtc = DateTime.MinValue;
    private PartyCooldownLayoutEditMode lastPartyMode = PartyCooldownLayoutEditMode.EightPlayer;

    public PartyCooldownLayoutModeTracker(TimeSpan retentionDuration)
    {
        this.retentionDuration = retentionDuration < TimeSpan.Zero ? TimeSpan.Zero : retentionDuration;
    }

    public PartyCooldownLayoutEditMode Resolve(bool allianceObserved, int partyMemberCount, DateTime nowUtc)
    {
        if (allianceObserved)
            this.allianceExpiresAtUtc = nowUtc.Add(this.retentionDuration);

        if (allianceObserved || nowUtc < this.allianceExpiresAtUtc)
            return PartyCooldownLayoutEditMode.Alliance;

        if (partyMemberCount >= 2)
        {
            this.lastPartyMode = partyMemberCount <= 4
                ? PartyCooldownLayoutEditMode.FourPlayer
                : PartyCooldownLayoutEditMode.EightPlayer;
        }

        return this.lastPartyMode;
    }

    public void Reset()
    {
        this.allianceExpiresAtUtc = DateTime.MinValue;
        this.lastPartyMode = PartyCooldownLayoutEditMode.EightPlayer;
    }

    public static PartyCooldownLayoutEditMode ResolvePreview(
        bool configVisible,
        PartyCooldownLayoutEditMode editMode,
        PartyCooldownLayoutEditMode automaticMode)
        => configVisible ? editMode : automaticMode;
}
