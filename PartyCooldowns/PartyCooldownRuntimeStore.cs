namespace FFXIVAura;

internal readonly record struct PartyCooldownRuntimeStoreDiagnostics(
    int RuntimeStateCount,
    int RowBufferCount,
    int RowMemberBufferCount,
    int LiveRuntimeKeyCount);

internal sealed class PartyCooldownRuntimeStore
{
    private readonly Dictionary<PartyCooldownRuntimeKey, PartyCooldownRuntimeState> runtimeStates =
        new(PartyCooldownRuntimeKeyComparer.Instance);
    private readonly List<PartyCooldownRuntimeKey> runtimePruneBuffer = [];
    private readonly Dictionary<string, PartyCooldownWindowRowBuffer> rowBuffersByWindow =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> staleWindowIds = [];
    private readonly HashSet<PartyCooldownRuntimeKey> liveRuntimeKeys =
        new(PartyCooldownRuntimeKeyComparer.Instance);
    private bool liveRuntimeKeysValid;

    public PartyCooldownRuntimeState GetOrCreateState(PartyCooldownRuntimeKey key)
    {
        if (this.runtimeStates.TryGetValue(key, out var state))
            return state;

        state = new PartyCooldownRuntimeState();
        this.runtimeStates[key] = state;
        return state;
    }

    public PartyCooldownWindowRowBuffer GetOrCreateRowBuffer(string windowId)
    {
        if (this.rowBuffersByWindow.TryGetValue(windowId, out var buffer))
            return buffer;

        buffer = new PartyCooldownWindowRowBuffer();
        this.rowBuffersByWindow[windowId] = buffer;
        return buffer;
    }

    public void RemoveWindowBuffer(string windowId)
        => this.rowBuffersByWindow.Remove(windowId);

    public void PruneWindowBuffers(IReadOnlyList<string> liveWindowIds)
    {
        ArgumentNullException.ThrowIfNull(liveWindowIds);
        this.staleWindowIds.Clear();
        foreach (var windowId in this.rowBuffersByWindow.Keys)
        {
            if (!ContainsWindowId(liveWindowIds, windowId))
                this.staleWindowIds.Add(windowId);
        }

        foreach (var windowId in this.staleWindowIds)
            this.rowBuffersByWindow.Remove(windowId);
    }

    public void BeginFrame()
    {
        this.liveRuntimeKeys.Clear();
        this.liveRuntimeKeysValid = false;
    }

    public bool BeginLiveKeyCollection()
    {
        if (this.liveRuntimeKeysValid)
            return false;

        this.liveRuntimeKeys.Clear();
        return true;
    }

    public void MarkLiveKey(PartyCooldownRuntimeKey key)
        => this.liveRuntimeKeys.Add(key);

    public void CompleteLiveKeyCollection()
        => this.liveRuntimeKeysValid = true;

    public void PruneStates()
    {
        if (!this.liveRuntimeKeysValid)
            return;

        this.runtimePruneBuffer.Clear();
        foreach (var key in this.runtimeStates.Keys)
        {
            if (!this.liveRuntimeKeys.Contains(key))
                this.runtimePruneBuffer.Add(key);
        }

        foreach (var key in this.runtimePruneBuffer)
            this.runtimeStates.Remove(key);
    }

    public PartyCooldownRuntimeStoreDiagnostics CreateDiagnostics()
    {
        var rowMemberBufferCount = 0;
        foreach (var buffer in this.rowBuffersByWindow.Values)
            rowMemberBufferCount += buffer.MemberBufferCount;

        return new PartyCooldownRuntimeStoreDiagnostics(
            this.runtimeStates.Count,
            this.rowBuffersByWindow.Count,
            rowMemberBufferCount,
            this.liveRuntimeKeysValid ? this.liveRuntimeKeys.Count : 0);
    }

    public void Reset()
    {
        this.runtimeStates.Clear();
        this.runtimePruneBuffer.Clear();
        this.rowBuffersByWindow.Clear();
        this.staleWindowIds.Clear();
        this.liveRuntimeKeys.Clear();
        this.liveRuntimeKeysValid = false;
    }

    private static bool ContainsWindowId(IReadOnlyList<string> windowIds, string candidate)
    {
        foreach (var windowId in windowIds)
        {
            if (string.Equals(windowId, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
