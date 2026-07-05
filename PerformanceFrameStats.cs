namespace FFXIVAura;

internal sealed class PerformanceFrameStats
{
    public bool Enabled { get; private set; }
    public double FrameMilliseconds { get; private set; }
    public double AverageFrameMilliseconds { get; private set; }
    public double MaxFrameMilliseconds { get; private set; }
    public int WindowCount { get; private set; }
    public int SkillIconCount { get; private set; }
    public int AuraIconCount { get; private set; }
    public int CooldownCalculationCount { get; private set; }
    public int NativeTooltipControlCount { get; private set; }
    public int GrayscaleIconProcessCount { get; private set; }
    public int FrameSampleCount { get; private set; }

    public void Begin(bool enabled)
    {
        var wasEnabled = this.Enabled;
        this.Enabled = enabled;
        if (!enabled)
            return;

        if (!wasEnabled)
        {
            this.AverageFrameMilliseconds = 0;
            this.MaxFrameMilliseconds = 0;
            this.FrameSampleCount = 0;
        }

        this.FrameMilliseconds = 0;
        this.WindowCount = 0;
        this.SkillIconCount = 0;
        this.AuraIconCount = 0;
        this.CooldownCalculationCount = 0;
        this.NativeTooltipControlCount = 0;
        this.GrayscaleIconProcessCount = 0;
    }

    public void Finish(TimeSpan elapsed)
    {
        if (!this.Enabled)
            return;

        this.FrameMilliseconds = Math.Max(0, elapsed.TotalMilliseconds);
        this.FrameSampleCount++;
        this.AverageFrameMilliseconds = this.FrameSampleCount == 1
            ? this.FrameMilliseconds
            : this.AverageFrameMilliseconds + (this.FrameMilliseconds - this.AverageFrameMilliseconds) / this.FrameSampleCount;
        this.MaxFrameMilliseconds = Math.Max(this.MaxFrameMilliseconds, this.FrameMilliseconds);
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

    public void CountNativeTooltipControl()
    {
        if (this.Enabled)
            this.NativeTooltipControlCount++;
    }

    public void CountGrayscaleIcons(int count)
    {
        if (this.Enabled && count > 0)
            this.GrayscaleIconProcessCount += count;
    }
}
