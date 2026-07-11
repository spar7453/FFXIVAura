namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AbilityDefinition> GetVisibleAbilities(string job, uint level, IconWindowConfig? iconWindow = null)
    {
        iconWindow ??= this.GetActiveIconWindow();
        var candidates = this.GetJobCandidates(job, level);
        if (iconWindow.TrackedByJob.TryGetValue(job, out var tracked) && tracked.Count > 0)
        {
            var resolved = new List<AbilityDefinition>();
            var seen = new HashSet<uint>();
            foreach (var trackedId in tracked)
            {
                var ability = this.ResolveTrackedAbilityForLevel(trackedId, job, level, candidates);
                if (ability is not null && seen.Add(ability.ActionId))
                    resolved.Add(ability);
            }

            return resolved;
        }

        var excluded = this.BuildExcludedAbilityFilter(iconWindow, job);
        return candidates
            .Where(ability => !this.IsAbilityExcluded(excluded, ability))
            .OrderBy(a => a.Job == "ROLE" ? 1 : 0)
            .ThenBy(a => this.GetTrackedOrder(iconWindow, job, a.Id))
            .ThenByDescending(a => a.Cooldown)
            .ThenBy(a => a.Level);
    }

    private IEnumerable<AbilityDefinition> GetDisplayAbilities(string job, uint level, IconWindowConfig iconWindow)
    {
        return this.GetVisibleAbilities(job, level, iconWindow)
            .Where(ability => this.ShouldDisplayAbility(ability, iconWindow));
    }

    private bool ShouldDisplayAbility(AbilityDefinition ability, IconWindowConfig iconWindow)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat(),
            IconDisplayCondition.CoolingOnly => this.GetCooldown(ability).ShouldShowInCoolingOnly,
            IconDisplayCondition.ReadyOnly => this.GetCooldown(ability).IsReady,
            _ => true,
        };
    }

    private bool IsInCombat()
    {
        return Condition[ConditionFlag.InCombat]
               || (ObjectTable.LocalPlayer is not null && ObjectTable.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat));
    }

    private IReadOnlyList<AbilityDefinition> GetJobCandidates(string job, uint level)
    {
        var normalizedJob = job.Trim().ToUpperInvariant();
        var key = $"{normalizedJob}:{level}";
        if (this.jobCandidatesCache.TryGetValue(key, out var cached))
            return cached;

        var candidates = this.BuildJobCandidates(normalizedJob, level);
        if (this.jobCandidatesCache.Count >= AbilityCandidateCacheLimit)
            this.jobCandidatesCache.Clear();

        this.jobCandidatesCache[key] = candidates;
        return candidates;
    }

    private IReadOnlyList<AbilityDefinition> BuildJobCandidates(string job, uint level)
    {
        var candidates = new List<AbilityDefinition>();
        var seen = new HashSet<uint>();
        foreach (var ability in this.abilities)
        {
            if (ability.Level > level
                || (!string.Equals(ability.Job, job, StringComparison.OrdinalIgnoreCase)
                    && !JobInfo.CanUseRoleAction(job, ability)))
            {
                continue;
            }

            seen.Add(ability.ActionId);
            candidates.Add(ability);
        }

        foreach (var ability in this.GetGameActionCandidates(job, level))
        {
            if (seen.Add(ability.ActionId))
                candidates.Add(ability);
        }

        return candidates;
    }

    private int GetTrackedOrder(IconWindowConfig iconWindow, string job, string id)
    {
        if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
            return int.MaxValue;

        var index = this.FindTrackedAbilityIndex(tracked, id, job);
        return index < 0 ? int.MaxValue : index;
    }

    private IEnumerable<AbilityDefinition> GetGameActionCandidates(string job, uint level)
    {
        var key = job.Trim().ToUpperInvariant();
        if (!this.gameActionCandidatesCache.TryGetValue(key, out var cached))
        {
            var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AbilityCandidateBuild);
            try
            {
                cached = this.BuildGameActionCandidates(key).ToList();
            }
            finally
            {
                this.performanceProfiler.EndSection(PerformanceProfileSection.AbilityCandidateBuild, profileStart);
            }

            if (this.gameActionCandidatesCache.Count >= AbilityCandidateCacheLimit)
                this.gameActionCandidatesCache.Clear();

            this.gameActionCandidatesCache[key] = cached;
        }

        return level == uint.MaxValue
            ? cached
            : cached.Where(ability => ability.Level <= level);
    }

    private IEnumerable<AbilityDefinition> BuildGameActionCandidates(string job)
    {
        var classJobIds = JobInfo.ApplicableClassJobIds(job);
        if (classJobIds.Count == 0)
            yield break;

        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            yield break;

        foreach (var row in sheet)
        {
            if (row.RowId == 0 || !classJobIds.Contains(row.ClassJob.RowId) || row.ClassJobLevel == 0)
                continue;

            var category = row.ActionCategory.RowId;
            if (category is not (2 or 3 or 4))
                continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            yield return new AbilityDefinition
            {
                Id = $"{job.ToLowerInvariant()}-{row.RowId}",
                Name = name,
                ActionId = row.RowId,
                ActionIds = [row.RowId],
                ActionCategoryId = row.ActionCategory.RowId,
                Job = job,
                Level = (byte)Math.Min(row.ClassJobLevel, byte.MaxValue),
                Cooldown = row.Recast100ms / 10f,
                Charges = Math.Max(row.MaxCharges, (byte)1),
                IconId = row.Icon,
            };
        }
    }
}
