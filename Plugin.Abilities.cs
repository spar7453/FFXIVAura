namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AbilityDefinition> GetVisibleAbilities(string job, uint level, IconWindowConfig? iconWindow = null)
    {
        iconWindow ??= this.GetActiveIconWindow();
        var candidates = this.GetJobCandidates(job, level).ToList();
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
        var state = this.GetCooldown(ability);
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat(),
            IconDisplayCondition.CoolingOnly => state.ShouldShowInCoolingOnly,
            IconDisplayCondition.ReadyOnly => state.IsReady,
            _ => true,
        };
    }

    private bool IsInCombat()
    {
        return Condition[ConditionFlag.InCombat]
               || (ObjectTable.LocalPlayer is not null && ObjectTable.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat));
    }

    private IEnumerable<AbilityDefinition> GetJobCandidates(string job, uint level)
    {
        var configured = this.abilities
            .Where(a => a.Level <= level)
            .Where(a => string.Equals(a.Job, job, StringComparison.OrdinalIgnoreCase)
                        || JobInfo.CanUseRoleAction(job, a));

        var seen = new HashSet<uint>();
        foreach (var ability in configured)
        {
            seen.Add(ability.ActionId);
            yield return ability;
        }

        foreach (var ability in this.GetGameActionCandidates(job, level))
        {
            if (seen.Add(ability.ActionId))
                yield return ability;
        }
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
        var key = $"{job}:{level}";
        if (!this.gameActionCandidatesCache.TryGetValue(key, out var cached))
        {
            cached = this.BuildGameActionCandidates(job, level).ToList();
            this.gameActionCandidatesCache[key] = cached;
        }

        return cached;
    }

    private IEnumerable<AbilityDefinition> BuildGameActionCandidates(string job, uint level)
    {
        var classJobIds = JobInfo.ApplicableClassJobIds(job);
        if (classJobIds.Count == 0)
            yield break;

        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            yield break;

        foreach (var row in sheet)
        {
            if (row.RowId == 0 || !classJobIds.Contains(row.ClassJob.RowId) || row.ClassJobLevel == 0 || row.ClassJobLevel > level)
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
