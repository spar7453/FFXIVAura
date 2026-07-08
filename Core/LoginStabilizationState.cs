namespace FFXIVAura;

internal sealed class LoginStabilizationState
{
    private readonly TimeSpan skillAutoAlignSuppressionDuration;

    public LoginStabilizationState(TimeSpan skillAutoAlignSuppressionDuration)
    {
        this.skillAutoAlignSuppressionDuration = skillAutoAlignSuppressionDuration;
    }

    public bool WasLoggedInAndLoaded { get; private set; }
    public DateTime SkillAutoAlignSuppressedUntil { get; private set; } = DateTime.MinValue;

    public bool Update(bool loggedInAndLoaded, DateTime now)
    {
        var started = loggedInAndLoaded && !this.WasLoggedInAndLoaded;
        this.WasLoggedInAndLoaded = loggedInAndLoaded;
        if (started)
            this.SkillAutoAlignSuppressedUntil = now.Add(this.skillAutoAlignSuppressionDuration);

        return started;
    }

    public bool ShouldSuppressSkillAutoAlign(DateTime now)
        => now < this.SkillAutoAlignSuppressedUntil;
}
