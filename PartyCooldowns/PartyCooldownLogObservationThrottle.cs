namespace FFXIVAura;

internal sealed class PartyCooldownLogObservationThrottle
{
    private DateTime nextObservationAtUtc = DateTime.MinValue;

    public bool TryAcquire(DateTime nowUtc, TimeSpan interval)
    {
        if (nowUtc < this.nextObservationAtUtc)
            return false;

        this.nextObservationAtUtc = nowUtc.Add(interval);
        return true;
    }

    public void Reset()
        => this.nextObservationAtUtc = DateTime.MinValue;
}
