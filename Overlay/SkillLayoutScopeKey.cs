namespace FFXIVAura;

internal readonly struct SkillLayoutScopeKey : IEquatable<SkillLayoutScopeKey>
{
    public SkillLayoutScopeKey(string windowId, string job)
    {
        this.WindowId = windowId ?? string.Empty;
        this.Job = job ?? string.Empty;
    }

    public string WindowId { get; }

    public string Job { get; }

    public bool BelongsToWindow(string windowId)
        => string.Equals(this.WindowId, windowId, StringComparison.OrdinalIgnoreCase);

    public bool Equals(SkillLayoutScopeKey other)
        => string.Equals(this.WindowId, other.WindowId, StringComparison.OrdinalIgnoreCase)
           && string.Equals(this.Job, other.Job, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj)
        => obj is SkillLayoutScopeKey other && this.Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.WindowId ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.Job ?? string.Empty));

    public static bool operator ==(SkillLayoutScopeKey left, SkillLayoutScopeKey right)
        => left.Equals(right);

    public static bool operator !=(SkillLayoutScopeKey left, SkillLayoutScopeKey right)
        => !left.Equals(right);
}
