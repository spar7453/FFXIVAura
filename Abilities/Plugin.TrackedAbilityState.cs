namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private List<string> GetOrCreateTrackedAbilityList(IconWindowConfig iconWindow, string job)
    {
        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
        {
            tracked = [];
            iconWindow.TrackedByJob[job] = tracked;
        }

        return tracked;
    }

    private IReadOnlyList<string> GetTrackedAbilityList(IconWindowConfig iconWindow, string job)
    {
        return iconWindow.TrackedByJob.TryGetValue(job, out var tracked)
            ? tracked
            : Array.Empty<string>();
    }

    private bool IsAbilityTracked(IconWindowConfig iconWindow, string job, string abilityId)
    {
        return iconWindow.TrackedByJob.TryGetValue(job, out var tracked)
               && this.FindTrackedAbilityIndex(tracked, abilityId, job) >= 0;
    }

    private void ResetTrackedAbilitiesToDefault(IconWindowConfig iconWindow, string job, uint level)
    {
        var tracked = this.GetOrCreateTrackedAbilityList(iconWindow, job);
        tracked.Clear();
        tracked.AddRange(this.GetJobCandidates(job, level)
            .OrderBy(a => a.Job == "ROLE" ? 1 : 0)
            .ThenByDescending(a => a.Cooldown)
            .ThenBy(a => a.Level)
            .Select(a => a.Id));

        iconWindow.ExcludedByJob.Remove(job);
        iconWindow.IconPositionsByJob.Remove(job);
        this.AlignOverlayIcons(iconWindow, job, level, preferTrackedOrder: true);
    }

    private void ClearTrackedAbilities(IconWindowConfig iconWindow, string job)
    {
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            tracked.Clear();

        iconWindow.ExcludedByJob.Remove(job);
        iconWindow.IconPositionsByJob.Remove(job);
    }

    private void TrackAbility(IconWindowConfig iconWindow, string job, uint level, string abilityId)
    {
        var tracked = this.GetOrCreateTrackedAbilityList(iconWindow, job);
        if (this.FindTrackedAbilityIndex(tracked, abilityId, job) >= 0)
            return;

        tracked.Add(abilityId);
        var visible = this.GetOverlayAbilities(iconWindow, job, level, OverlayItemVisibility.Layout).ToList();
        this.AddMissingOverlayIconPositions(iconWindow, job, visible, new Vector2(iconWindow.Width, iconWindow.Height));
    }

    private void UntrackAbility(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            tracked.RemoveAll(id => this.TrackedAbilityIdsMatch(id, abilityId, job));

        this.RemoveOverlayPositionsForAbility(iconWindow, job, abilityId);
    }

    private void UntrackAbilityFromOverlay(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked) || tracked.Count == 0)
        {
            this.ExcludeAbility(iconWindow, job, abilityId);
            return;
        }

        tracked.RemoveAll(id => this.TrackedAbilityIdsMatch(id, abilityId, job));
        this.RemoveOverlayPositionsForAbility(iconWindow, job, abilityId);
    }

    private HashSet<string> GetExcludedAbilityIdsForJob(IconWindowConfig iconWindow, string job)
    {
        return iconWindow.ExcludedByJob.TryGetValue(job, out var excluded)
            ? excluded.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void ExcludeAbility(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.ExcludedByJob.TryGetValue(job, out var excluded))
        {
            excluded = [];
            iconWindow.ExcludedByJob[job] = excluded;
        }

        if (this.FindTrackedAbilityIndex(excluded, abilityId, job) < 0)
            excluded.Add(abilityId);

        this.RemoveOverlayPositionsForAbility(iconWindow, job, abilityId);
    }

    private void IncludeAbility(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.ExcludedByJob.TryGetValue(job, out var excluded))
            return;

        excluded.RemoveAll(id => this.TrackedAbilityIdsMatch(id, abilityId, job));
        if (excluded.Count == 0)
            iconWindow.ExcludedByJob.Remove(job);
    }

    private void RemoveOverlayPositionsForAbility(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
            return;

        foreach (var key in positions.Keys.ToList())
        {
            if (this.TrackedAbilityIdsMatch(key, abilityId, job))
                positions.Remove(key);
        }
    }

    private int FindTrackedAbilityIndex(IReadOnlyList<string> tracked, string abilityId, string job)
    {
        for (var index = 0; index < tracked.Count; index++)
        {
            if (string.Equals(tracked[index], abilityId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        for (var index = 0; index < tracked.Count; index++)
        {
            if (this.TrackedAbilityIdsMatch(tracked[index], abilityId, job))
                return index;
        }

        return -1;
    }

    private void SaveAndRealignTrackedOrder(IconWindowConfig iconWindow, string job)
    {
        var level = (uint)(PlayerState.EffectiveLevel > 0 ? PlayerState.EffectiveLevel : PlayerState.Level);
        this.AlignOverlayIcons(iconWindow, job, level, preferTrackedOrder: true);
        this.QueueConfigSave();
    }

    private bool MoveTrackedSkill(List<string> tracked, string job, string id, int targetIndex)
    {
        var currentIndex = this.FindTrackedAbilityIndex(tracked, id, job);
        if (currentIndex < 0 || currentIndex == targetIndex)
            return false;

        var item = tracked[currentIndex];
        tracked.RemoveAt(currentIndex);
        if (currentIndex < targetIndex)
            targetIndex--;

        targetIndex = Math.Clamp(targetIndex, 0, tracked.Count);
        tracked.Insert(targetIndex, item);
        return true;
    }

    private bool SwapTrackedSkills(List<string> tracked, string job, string firstId, string secondId)
    {
        var firstIndex = this.FindTrackedAbilityIndex(tracked, firstId, job);
        var secondIndex = this.FindTrackedAbilityIndex(tracked, secondId, job);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            return false;

        (tracked[firstIndex], tracked[secondIndex]) = (tracked[secondIndex], tracked[firstIndex]);
        return true;
    }
}
