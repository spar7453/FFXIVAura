namespace FFXIVAura;

internal sealed class PartyCooldownLayoutModeTracker
{
    private readonly TimeSpan retentionDuration;
    private DateTime allianceExpiresAtUtc = DateTime.MinValue;

    public PartyCooldownLayoutModeTracker(TimeSpan retentionDuration)
    {
        this.retentionDuration = retentionDuration < TimeSpan.Zero ? TimeSpan.Zero : retentionDuration;
    }

    public bool Resolve(bool allianceObserved, DateTime nowUtc)
    {
        if (allianceObserved)
        {
            this.allianceExpiresAtUtc = nowUtc.Add(this.retentionDuration);
            return true;
        }

        return nowUtc < this.allianceExpiresAtUtc;
    }

    public void Reset()
        => this.allianceExpiresAtUtc = DateTime.MinValue;

    public static bool ResolvePreview(bool configVisible, bool? alliancePreview, bool automaticAllianceLayout)
        => configVisible && alliancePreview.HasValue
            ? alliancePreview.Value
            : automaticAllianceLayout;
}
