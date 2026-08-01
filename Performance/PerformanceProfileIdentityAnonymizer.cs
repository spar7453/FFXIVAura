namespace FFXIVAura;

internal sealed class PerformanceProfileIdentityAnonymizer
{
    private const int MaxIdentityCount = 512;
    private readonly Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
    private int nextAlias;

    public int Count => this.aliases.Count;

    public string GetAlias(string? actorName, ushort worldId)
    {
        var normalizedName = actorName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0 || normalizedName == "-")
            return "-";

        var key = $"{worldId}:{normalizedName}";
        if (this.aliases.TryGetValue(key, out var alias))
            return alias;

        if (this.aliases.Count >= MaxIdentityCount)
            this.Reset();

        alias = $"actor-{++this.nextAlias:000}";
        this.aliases[key] = alias;
        return alias;
    }

    public static string RedactDetail(string? detail)
        => string.IsNullOrWhiteSpace(detail) ? string.Empty : "[redacted]";

    public void Reset()
    {
        this.aliases.Clear();
        this.nextAlias = 0;
    }
}
