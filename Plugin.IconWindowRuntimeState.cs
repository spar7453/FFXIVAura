namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void RemoveIconWindowRuntimeState(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return;

        RemoveScopedRuntimeKeys(this.visibleAbilityKeys, windowId);
        RemoveScopedRuntimeKeys(this.visibleAurasByScope, windowId);
        RemoveScopedRuntimeKeys(this.auraFirstSeenByScope, windowId);
        if (string.Equals(this.auraSearchWindowId, windowId, StringComparison.OrdinalIgnoreCase))
            this.CloseAuraSearchWindow();
    }

    private void PruneIconWindowRuntimeState()
    {
        var windowIds = this.config.IconWindows
            .Select(window => window.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        PruneScopedRuntimeKeys(this.visibleAbilityKeys, windowIds);
        PruneScopedRuntimeKeys(this.visibleAurasByScope, windowIds);
        PruneScopedRuntimeKeys(this.auraFirstSeenByScope, windowIds);
        if (!string.IsNullOrWhiteSpace(this.auraSearchWindowId)
            && !windowIds.Contains(this.auraSearchWindowId, StringComparer.OrdinalIgnoreCase))
        {
            this.CloseAuraSearchWindow();
        }
    }

    private static void RemoveScopedRuntimeKeys<TValue>(Dictionary<string, TValue> map, string windowId)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (RuntimeScopeKeys.BelongsToWindow(key, windowId))
                map.Remove(key);
        }
    }

    private static void PruneScopedRuntimeKeys<TValue>(Dictionary<string, TValue> map, IReadOnlyList<string> windowIds)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (windowIds.Any(windowId => RuntimeScopeKeys.BelongsToWindow(key, windowId)))
                continue;

            map.Remove(key);
        }
    }
}
