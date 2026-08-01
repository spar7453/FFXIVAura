namespace FFXIVAura;

internal readonly struct JobLevelKey : IEquatable<JobLevelKey>
{
    public JobLevelKey(string job, uint level)
    {
        this.Job = job?.Trim() ?? string.Empty;
        this.Level = level;
    }

    public string Job { get; }

    public uint Level { get; }

    public bool Equals(JobLevelKey other)
        => this.Level == other.Level
           && string.Equals(this.Job, other.Job, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj)
        => obj is JobLevelKey other && this.Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.Job ?? string.Empty),
            this.Level);
}
