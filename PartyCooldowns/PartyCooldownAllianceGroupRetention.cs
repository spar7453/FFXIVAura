namespace FFXIVAura;

internal readonly record struct PartyCooldownAllianceGroupResolution(
    int GroupIndex,
    int ObservedGroupIndex,
    bool UsedRetainedValue);

internal sealed class PartyCooldownAllianceGroupRetention
{
    private int retainedGroupIndex = -1;
    private DateTime expiresAtUtc = DateTime.MinValue;

    public PartyCooldownAllianceGroupResolution Resolve(
        int observedGroupIndex,
        bool isAlliance,
        DateTime nowUtc,
        TimeSpan retention)
    {
        if (!isAlliance)
        {
            this.Reset();
            return new PartyCooldownAllianceGroupResolution(-1, observedGroupIndex, false);
        }

        if (IsValidGroupIndex(observedGroupIndex))
        {
            this.retainedGroupIndex = observedGroupIndex;
            this.expiresAtUtc = nowUtc + (retention < TimeSpan.Zero ? TimeSpan.Zero : retention);
            return new PartyCooldownAllianceGroupResolution(observedGroupIndex, observedGroupIndex, false);
        }

        if (IsValidGroupIndex(this.retainedGroupIndex) && nowUtc < this.expiresAtUtc)
            return new PartyCooldownAllianceGroupResolution(this.retainedGroupIndex, observedGroupIndex, true);

        this.Reset();
        return new PartyCooldownAllianceGroupResolution(-1, observedGroupIndex, false);
    }

    public void Reset()
    {
        this.retainedGroupIndex = -1;
        this.expiresAtUtc = DateTime.MinValue;
    }

    private static bool IsValidGroupIndex(int groupIndex)
        => groupIndex is >= 0 and < 3;
}
