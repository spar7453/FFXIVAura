namespace FFXIVAura;

public sealed partial class Plugin
{
    private IEnumerable<AbilityDefinition> GetVisibleAbilities(string job, uint level, IconWindowConfig? iconWindow = null)
    {
        iconWindow ??= this.GetActiveIconWindow();
        var candidates = this.abilityCatalog.GetJobCandidates(job, level);
        if (AbilityTrackingService.IsManualTracking(iconWindow, job))
        {
            if (!iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
                return Array.Empty<AbilityDefinition>();

            var resolved = new List<AbilityDefinition>();
            var seen = new HashSet<uint>();
            foreach (var trackedId in tracked)
            {
                var ability = this.abilityCatalog.ResolveTrackedForLevel(trackedId, job, level, candidates);
                if (ability is not null && seen.Add(ability.ActionId))
                    resolved.Add(ability);
            }

            return resolved;
        }

        var excluded = this.abilityCatalog.CreateExclusionFilter(
            iconWindow.ExcludedByJob.GetValueOrDefault(job),
            job);
        return candidates
            .Where(ability => !this.abilityCatalog.IsExcluded(excluded, ability))
            .OrderBy(a => a.Job == "ROLE" ? 1 : 0)
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
            IconDisplayCondition.InCombat => this.playerFrameContext.IsInCombat,
            IconDisplayCondition.OutOfCombat => !this.playerFrameContext.IsInCombat,
            IconDisplayCondition.CoolingOnly => this.GetCooldown(ability).ShouldShowInCoolingOnly,
            IconDisplayCondition.ReadyOnly => this.GetCooldown(ability).IsReady,
            _ => true,
        };
    }

    private int GetTrackedOrder(IconWindowConfig iconWindow, string job, string id)
    {
        if (!AbilityTrackingService.IsManualTracking(iconWindow, job)
            || !iconWindow.TrackedByJob.TryGetValue(job, out var tracked))
        {
            return int.MaxValue;
        }

        var index = this.abilityTrackingService.FindIndex(tracked, id, job);
        return index < 0 ? int.MaxValue : index;
    }

}
