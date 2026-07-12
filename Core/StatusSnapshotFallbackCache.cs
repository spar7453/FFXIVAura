namespace FFXIVAura;

internal sealed class StatusSnapshotFallbackCache
{
    private const int MaxEntries = 128;
    private readonly Dictionary<StatusSnapshotCacheKey, CacheEntry> entries = new();
    private readonly LinkedList<StatusSnapshotCacheKey> recency = new();

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
        if (snapshots.Count == 0)
        {
            this.RemoveEntry(key);
            return;
        }

        if (!this.entries.TryGetValue(key, out var entry))
        {
            this.EvictOldestEntryIfFull();
            entry = new CacheEntry(this.recency.AddLast(key));
            this.entries[key] = entry;
        }
        else
        {
            this.recency.Remove(entry.RecencyNode);
            this.recency.AddLast(entry.RecencyNode);
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
            this.RemoveEntry(new StatusSnapshotCacheKey(scope, ownerEntityId));
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
    {
        this.entries.Clear();
        this.recency.Clear();
    }

    private void EvictOldestEntryIfFull()
    {
        if (this.entries.Count < MaxEntries)
            return;

        if (this.recency.First is { } oldest)
            this.RemoveEntry(oldest.Value);
    }

    private void RemoveEntry(StatusSnapshotCacheKey key)
    {
        if (!this.entries.Remove(key, out var entry))
            return;

        this.recency.Remove(entry.RecencyNode);
    }

    private readonly record struct StatusSnapshotCacheKey(string Scope, uint OwnerEntityId);

    private sealed class CacheEntry(LinkedListNode<StatusSnapshotCacheKey> recencyNode)
    {
        public LinkedListNode<StatusSnapshotCacheKey> RecencyNode { get; } = recencyNode;

        public List<StatusSnapshot> Snapshots { get; } = [];

        public DateTime CapturedAtUtc { get; set; }
    }
}
