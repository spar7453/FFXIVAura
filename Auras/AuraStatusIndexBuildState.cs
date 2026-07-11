namespace FFXIVAura;

internal sealed class AuraStatusIndexBuildState
{
    private DateTime retryAfterUtc = DateTime.MinValue;

    public bool IsBuilt { get; private set; }

    public int Generation { get; private set; }

    public bool ShouldAttempt(DateTime nowUtc)
        => !this.IsBuilt && nowUtc >= this.retryAfterUtc;

    public void MarkFailed(DateTime nowUtc, TimeSpan retryDelay)
    {
        this.IsBuilt = false;
        this.retryAfterUtc = nowUtc + retryDelay;
    }

    public void MarkSucceeded()
    {
        this.IsBuilt = true;
        this.retryAfterUtc = DateTime.MinValue;
        this.Generation++;
    }
}
