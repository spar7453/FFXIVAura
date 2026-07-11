namespace FFXIVAura;

internal sealed class StatusSnapshotFallbackCache
{
    private const int MaxEntries = 128;
    private readonly Dictionary<StatusSnapshotCacheKey, CacheEntry> entries = new();

    public int Count => this.entries.Count;

    public void Remember(
        string scope,
        uint ownerEntityId,
        IReadOnlyList<StatusSnapshot> snapshots,
        DateTime capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(scope) || ownerEntityId == 0)
            return;

        var key = new StatusSnapshotCacheKey(scope, ownerEntityId);
        if (!this.entries.TryGetValue(key, out var entry))
        {
            this.EvictOldestEntryIfFull();
            entry = new CacheEntry();
            this.entries[key] = entry;
        }

        entry.Snapshots.Clear();
        entry.Snapshots.AddRange(snapshots);
        entry.CapturedAtUtc = capturedAtUtc;
    }

    public bool TryCopyRecentTo(
        string scope,
        uint ownerEntityId,
        DateTime nowUtc,
        TimeSpan retention,
        List<StatusSnapshot> output)
    {
        output.Clear();
        if (string.IsNullOrWhiteSpace(scope)
            || ownerEntityId == 0
            || retention < TimeSpan.Zero
            || !this.entries.TryGetValue(new StatusSnapshotCacheKey(scope, ownerEntityId), out var entry))
        {
            return false;
        }

        var age = nowUtc <= entry.CapturedAtUtc ? TimeSpan.Zero : nowUtc - entry.CapturedAtUtc;
        if (age > retention)
        {
            this.entries.Remove(new StatusSnapshotCacheKey(scope, ownerEntityId));
            return false;
        }

        var ageSeconds = (float)age.TotalSeconds;
        foreach (var snapshot in entry.Snapshots)
        {
            if (snapshot.RemainingTime <= 0f)
            {
                output.Add(snapshot);
                continue;
            }

            var remaining = snapshot.RemainingTime - ageSeconds;
            if (remaining > 0f)
                output.Add(snapshot with { RemainingTime = remaining });
        }

        return true;
    }

    public void Clear()
        => this.entries.Clear();

    private void EvictOldestEntryIfFull()
    {
        if (this.entries.Count < MaxEntries)
            return;

        var oldestKey = default(StatusSnapshotCacheKey);
        var oldestAtUtc = DateTime.MaxValue;
        foreach (var (key, entry) in this.entries)
        {
            if (entry.CapturedAtUtc >= oldestAtUtc)
                continue;

            oldestKey = key;
            oldestAtUtc = entry.CapturedAtUtc;
        }

        this.entries.Remove(oldestKey);
    }

    private readonly record struct StatusSnapshotCacheKey(string Scope, uint OwnerEntityId);

    private sealed class CacheEntry
    {
        public List<StatusSnapshot> Snapshots { get; } = [];

        public DateTime CapturedAtUtc { get; set; }
    }
}
