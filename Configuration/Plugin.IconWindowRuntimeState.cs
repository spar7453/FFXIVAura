namespace FFXIVAura;

public sealed partial class Plugin
{
    // ImGui window titles are stable per window (and per party layout mode), so cache them
    // instead of interpolating a new string every frame on the locked-overlay hot path.
    private string GetOverlayWindowTitle(IconWindowConfig iconWindow)
    {
        if (!this.overlayWindowTitles.TryGetValue(iconWindow.Id, out var title))
        {
            title = $"FFXIVAuraOverlay-{iconWindow.Id}";
            this.overlayWindowTitles[iconWindow.Id] = title;
        }

        return title;
    }

    private string GetPartyCooldownWindowTitle(
        IconWindowConfig iconWindow,
        PartyCooldownLayoutEditMode layoutMode)
    {
        var key = (iconWindow.Id, layoutMode);
        if (!this.partyCooldownWindowTitles.TryGetValue(key, out var title))
        {
            var layoutModeId = layoutMode switch
            {
                PartyCooldownLayoutEditMode.FourPlayer => "light-party",
                PartyCooldownLayoutEditMode.Alliance => "alliance",
                _ => "party",
            };
            title = $"FFXIVAuraOverlay-{iconWindow.Id}-{layoutModeId}";
            this.partyCooldownWindowTitles[key] = title;
        }

        return title;
    }

    private void RemoveIconWindowRuntimeState(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return;

        RemoveSkillLayoutRuntimeKeys(this.visibleAbilityKeys, windowId);
        RemoveSkillLayoutRuntimeKeys(this.transientSkillPositionsByGroup, windowId);
        this.auraSearchService.RemoveWindow(windowId);
        this.overlayFrameBuffersByWindow.Remove(windowId);
        this.partyCooldownRuntimeStore.RemoveWindowBuffer(windowId);
        this.auraSearchWindowSession.CloseIfTarget(windowId);
        this.overlayWindowTitles.Remove(windowId);
        RemovePartyCooldownWindowTitles(this.partyCooldownWindowTitles, windowId);
    }

    private void PruneIconWindowRuntimeState()
    {
        var windowIds = this.config.IconWindows
            .Select(window => window.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        PruneSkillLayoutRuntimeKeys(this.visibleAbilityKeys, windowIds);
        PruneSkillLayoutRuntimeKeys(this.transientSkillPositionsByGroup, windowIds);
        this.auraSearchService.PruneWindows(windowIds);

        foreach (var key in this.overlayFrameBuffersByWindow.Keys.ToList())
        {
            if (!windowIds.Contains(key, StringComparer.OrdinalIgnoreCase))
                this.overlayFrameBuffersByWindow.Remove(key);
        }

        this.partyCooldownRuntimeStore.PruneWindowBuffers(windowIds);

        this.auraSearchWindowSession.Prune(windowIds);

        foreach (var key in this.overlayWindowTitles.Keys.ToList())
        {
            if (!windowIds.Contains(key, StringComparer.OrdinalIgnoreCase))
                this.overlayWindowTitles.Remove(key);
        }

        foreach (var key in this.partyCooldownWindowTitles.Keys.ToList())
        {
            if (!windowIds.Contains(key.WindowId, StringComparer.OrdinalIgnoreCase))
                this.partyCooldownWindowTitles.Remove(key);
        }
    }

    private static void RemovePartyCooldownWindowTitles(
        Dictionary<(string WindowId, PartyCooldownLayoutEditMode LayoutMode), string> titles,
        string windowId)
    {
        foreach (var key in titles.Keys.ToList())
        {
            if (string.Equals(key.WindowId, windowId, StringComparison.OrdinalIgnoreCase))
                titles.Remove(key);
        }
    }

    private static void RemoveSkillLayoutRuntimeKeys<TValue>(
        Dictionary<SkillLayoutScopeKey, TValue> map,
        string windowId)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (key.BelongsToWindow(windowId))
                map.Remove(key);
        }
    }

    private static void PruneSkillLayoutRuntimeKeys<TValue>(
        Dictionary<SkillLayoutScopeKey, TValue> map,
        IReadOnlyList<string> windowIds)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (windowIds.Any(key.BelongsToWindow))
                continue;

            map.Remove(key);
        }
    }
}
