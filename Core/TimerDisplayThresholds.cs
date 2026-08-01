namespace FFXIVAura;

/// <summary>
/// Shared thresholds for deciding when a timed value (cooldown / aura remaining time)
/// should be treated as still "active" for display and state purposes. Consolidates the
/// previously duplicated <c>0.05f</c> epsilon used across cooldown, aura, and party-cooldown code.
/// </summary>
internal static class TimerDisplayThresholds
{
    /// <summary>
    /// Minimum remaining time, in seconds, for a timer to count as active/cooling. Values at or
    /// below this are treated as elapsed to avoid flicker from sub-frame residual times.
    /// </summary>
    public const float MinimumActiveSeconds = 0.05f;
}
