namespace FFXIVAura;

internal sealed class AbilityTrackingService
{
    private readonly AbilityCatalog abilityCatalog;

    public AbilityTrackingService(AbilityCatalog abilityCatalog)
    {
        this.abilityCatalog = abilityCatalog;
    }

    public IReadOnlyList<string> GetTracked(IconWindowConfig iconWindow, string job)
        => iconWindow.TrackedByJob.TryGetValue(job, out var tracked)
            ? tracked
            : Array.Empty<string>();

    public static bool IsManualTracking(IconWindowConfig iconWindow, string job)
        => iconWindow.ManualTrackingJobs.Contains(job, StringComparer.OrdinalIgnoreCase);

    public HashSet<string> GetExcludedIds(IconWindowConfig iconWindow, string job)
        => iconWindow.ExcludedByJob.TryGetValue(job, out var excluded)
            ? excluded.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool IsTracked(IconWindowConfig iconWindow, string job, string abilityId)
        => iconWindow.TrackedByJob.TryGetValue(job, out var tracked)
           && this.FindIndex(tracked, abilityId, job) >= 0;

    public bool ResetToDefault(IconWindowConfig iconWindow, string job, uint level)
    {
        var snapshot = this.abilityCatalog.GetJobCandidateSnapshot(job, level);
        if (!snapshot.IsComplete)
            return false;

        var tracked = snapshot.Candidates
            .OrderBy(ability => ability.Job == "ROLE" ? 1 : 0)
            .ThenByDescending(ability => ability.Cooldown)
            .ThenBy(ability => ability.Level)
            .Select(ability => ability.Id)
            .ToList();

        MarkManualTracking(iconWindow, job);
        iconWindow.TrackedByJob[job] = tracked;
        iconWindow.ExcludedByJob.Remove(job);
        iconWindow.IconPositionsByJob.Remove(job);
        return true;
    }

    public void Clear(IconWindowConfig iconWindow, string job)
    {
        iconWindow.ManualTrackingJobs.RemoveAll(
            candidate => string.Equals(candidate, job, StringComparison.OrdinalIgnoreCase));
        iconWindow.TrackedByJob.Remove(job);
        iconWindow.ExcludedByJob.Remove(job);
        iconWindow.IconPositionsByJob.Remove(job);
    }

    public bool Track(IconWindowConfig iconWindow, string job, string abilityId)
    {
        var tracked = GetOrCreateTracked(iconWindow, job);
        if (this.FindIndex(tracked, abilityId, job) >= 0)
            return false;

        tracked.Add(abilityId);
        return true;
    }

    public void Untrack(IconWindowConfig iconWindow, string job, string abilityId)
    {
        MarkManualTracking(iconWindow, job);
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            tracked.RemoveAll(id => this.abilityCatalog.IdsMatch(id, abilityId, job));

        this.RemovePositions(iconWindow, job, abilityId);
    }

    public void UntrackFromOverlay(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!IsManualTracking(iconWindow, job))
        {
            this.Exclude(iconWindow, job, abilityId);
            return;
        }

        this.Untrack(iconWindow, job, abilityId);
    }

    public void Exclude(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.ExcludedByJob.TryGetValue(job, out var excluded))
        {
            excluded = [];
            iconWindow.ExcludedByJob[job] = excluded;
        }

        if (this.FindIndex(excluded, abilityId, job) < 0)
            excluded.Add(abilityId);

        this.RemovePositions(iconWindow, job, abilityId);
    }

    public void Include(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.ExcludedByJob.TryGetValue(job, out var excluded))
            return;

        excluded.RemoveAll(id => this.abilityCatalog.IdsMatch(id, abilityId, job));
        if (excluded.Count == 0)
            iconWindow.ExcludedByJob.Remove(job);
    }

    public int FindIndex(IReadOnlyList<string> tracked, string abilityId, string job)
    {
        for (var index = 0; index < tracked.Count; index++)
        {
            if (string.Equals(tracked[index], abilityId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        for (var index = 0; index < tracked.Count; index++)
        {
            if (this.abilityCatalog.IdsMatch(tracked[index], abilityId, job))
                return index;
        }

        return -1;
    }

    public bool Move(List<string> tracked, string job, string abilityId, int targetIndex)
    {
        var currentIndex = this.FindIndex(tracked, abilityId, job);
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

    public bool Swap(List<string> tracked, string job, string firstId, string secondId)
    {
        var firstIndex = this.FindIndex(tracked, firstId, job);
        var secondIndex = this.FindIndex(tracked, secondId, job);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            return false;

        (tracked[firstIndex], tracked[secondIndex]) = (tracked[secondIndex], tracked[firstIndex]);
        return true;
    }

    private static List<string> GetOrCreateTracked(IconWindowConfig iconWindow, string job)
    {
        MarkManualTracking(iconWindow, job);
        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
        {
            tracked = [];
            iconWindow.TrackedByJob[job] = tracked;
        }

        return tracked;
    }

    private static void MarkManualTracking(IconWindowConfig iconWindow, string job)
    {
        var normalizedJob = job.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedJob)
            && !iconWindow.ManualTrackingJobs.Contains(normalizedJob, StringComparer.OrdinalIgnoreCase))
        {
            iconWindow.ManualTrackingJobs.Add(normalizedJob);
        }
    }

    private void RemovePositions(IconWindowConfig iconWindow, string job, string abilityId)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
            return;

        foreach (var key in positions.Keys.ToList())
        {
            if (this.abilityCatalog.IdsMatch(key, abilityId, job))
                positions.Remove(key);
        }
    }
}
