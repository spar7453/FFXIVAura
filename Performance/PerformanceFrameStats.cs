namespace FFXIVAura;

internal sealed class PerformanceFrameStats
{
    public bool Enabled { get; private set; }
    public double FrameMilliseconds { get; private set; }
    public double AverageFrameMilliseconds { get; private set; }
    public double MaxFrameMilliseconds { get; private set; }
    public DateTime MaxFrameOccurredAtUtc { get; private set; } = DateTime.MinValue;
    public long FrameAllocatedBytes { get; private set; }
    public double AverageFrameAllocatedBytes { get; private set; }
    public long MaxFrameAllocatedBytes { get; private set; }
    public int Gen0CollectionCount { get; private set; }
    public int WindowCount { get; private set; }
    public int SkillIconCount { get; private set; }
    public int AuraIconCount { get; private set; }
    public int CooldownCalculationCount { get; private set; }
    public int TooltipRenderCount { get; private set; }
    public int GrayscaleIconProcessCount { get; private set; }
    public int PartyStatusScanCount { get; private set; }
    public int PartyStatusCacheHitCount { get; private set; }
    public int FrameSampleCount { get; private set; }

    public void Begin(bool enabled)
    {
        var wasEnabled = this.Enabled;
        this.Enabled = enabled;
        if (!enabled)
            return;

        if (!wasEnabled)
        {
            this.FrameMilliseconds = 0;
            this.AverageFrameMilliseconds = 0;
            this.MaxFrameMilliseconds = 0;
            this.MaxFrameOccurredAtUtc = DateTime.MinValue;
            this.FrameAllocatedBytes = 0;
            this.AverageFrameAllocatedBytes = 0;
            this.MaxFrameAllocatedBytes = 0;
            this.Gen0CollectionCount = 0;
            this.FrameSampleCount = 0;
        }

        this.WindowCount = 0;
        this.SkillIconCount = 0;
        this.AuraIconCount = 0;
        this.CooldownCalculationCount = 0;
        this.TooltipRenderCount = 0;
        this.GrayscaleIconProcessCount = 0;
        this.PartyStatusScanCount = 0;
        this.PartyStatusCacheHitCount = 0;
    }

    public void Finish(TimeSpan elapsed, long allocatedBytes = 0, int gen0Collections = 0)
    {
        if (!this.Enabled)
            return;

        this.FrameMilliseconds = Math.Max(0, elapsed.TotalMilliseconds);
        this.FrameAllocatedBytes = Math.Max(0, allocatedBytes);
        this.Gen0CollectionCount = Math.Max(0, gen0Collections);
        this.FrameSampleCount++;
        this.AverageFrameMilliseconds = this.FrameSampleCount == 1
            ? this.FrameMilliseconds
            : this.AverageFrameMilliseconds + (this.FrameMilliseconds - this.AverageFrameMilliseconds) / this.FrameSampleCount;
        this.AverageFrameAllocatedBytes = this.FrameSampleCount == 1
            ? this.FrameAllocatedBytes
            : this.AverageFrameAllocatedBytes + (this.FrameAllocatedBytes - this.AverageFrameAllocatedBytes) / this.FrameSampleCount;
        this.MaxFrameAllocatedBytes = Math.Max(this.MaxFrameAllocatedBytes, this.FrameAllocatedBytes);
        if (this.FrameMilliseconds >= this.MaxFrameMilliseconds)
        {
            this.MaxFrameMilliseconds = this.FrameMilliseconds;
            this.MaxFrameOccurredAtUtc = DateTime.UtcNow;
        }
    }

    public void IncludePostFrameWork(
        TimeSpan elapsed,
        long allocatedBytes = 0,
        int gen0Collections = 0)
    {
        if (!this.Enabled || this.FrameSampleCount <= 0)
            return;

        var additionalMilliseconds = Math.Max(0, elapsed.TotalMilliseconds);
        var additionalAllocatedBytes = Math.Max(0, allocatedBytes);
        this.FrameMilliseconds += additionalMilliseconds;
        this.FrameAllocatedBytes += additionalAllocatedBytes;
        this.Gen0CollectionCount += Math.Max(0, gen0Collections);
        this.AverageFrameMilliseconds += additionalMilliseconds / this.FrameSampleCount;
        this.AverageFrameAllocatedBytes += (double)additionalAllocatedBytes / this.FrameSampleCount;
        this.MaxFrameAllocatedBytes = Math.Max(this.MaxFrameAllocatedBytes, this.FrameAllocatedBytes);
        if (this.FrameMilliseconds >= this.MaxFrameMilliseconds)
        {
            this.MaxFrameMilliseconds = this.FrameMilliseconds;
            this.MaxFrameOccurredAtUtc = DateTime.UtcNow;
        }
    }

    public void CountOverlayWindow(int skillIconCount, int auraIconCount)
    {
        if (!this.Enabled)
            return;

        this.WindowCount++;
        this.SkillIconCount += Math.Max(0, skillIconCount);
        this.AuraIconCount += Math.Max(0, auraIconCount);
    }

    public void CountCooldownCalculation()
    {
        if (this.Enabled)
            this.CooldownCalculationCount++;
    }

    public void CountTooltipRender()
    {
        if (this.Enabled)
            this.TooltipRenderCount++;
    }

    public void CountGrayscaleIcons(int count)
    {
        if (this.Enabled && count > 0)
            this.GrayscaleIconProcessCount += count;
    }

    public void CountPartyStatusScan()
    {
        if (this.Enabled)
            this.PartyStatusScanCount++;
    }

    public void CountPartyStatusCacheHit()
    {
        if (this.Enabled)
            this.PartyStatusCacheHitCount++;
    }
}
