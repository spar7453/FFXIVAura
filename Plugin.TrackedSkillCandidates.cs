namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private readonly record struct TrackedSkillEditorData(
        List<AbilityDefinition> CurrentCandidates,
        HashSet<string> Excluded,
        ExcludedAbilityFilter ExcludedFilter,
        bool ManualTracking,
        Dictionary<string, int> TabCounts,
        List<AbilityDefinition> Candidates);

    private TrackedSkillEditorData BuildTrackedSkillEditorData(
        IconWindowConfig iconWindow,
        string job,
        uint level,
        IReadOnlyList<string> tracked)
    {
        var allCandidates = this.GetJobCandidates(job, uint.MaxValue).ToList();
        var currentCandidates = this.GetJobCandidates(job, level).ToList();
        var excluded = this.GetExcludedAbilityIdsForJob(iconWindow, job);
        var excludedFilter = this.BuildExcludedAbilityFilter(iconWindow, job);
        var manualTracking = tracked.Count > 0;
        var tabCounts = CountTrackedEditorTabs(allCandidates);
        var candidates = this.BuildTrackedSkillCandidateList(
            iconWindow,
            job,
            allCandidates,
            excludedFilter,
            manualTracking);

        return new TrackedSkillEditorData(
            currentCandidates,
            excluded,
            excludedFilter,
            manualTracking,
            tabCounts,
            candidates);
    }

    private static Dictionary<string, int> CountTrackedEditorTabs(IReadOnlyList<AbilityDefinition> allCandidates)
    {
        return TrackedEditorTabs.ToDictionary(
            tab => tab.Id,
            tab => allCandidates.Count(ability => IsInTrackedEditorTab(ability, tab.Id)),
            StringComparer.OrdinalIgnoreCase);
    }

    private List<AbilityDefinition> BuildTrackedSkillCandidateList(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> allCandidates,
        ExcludedAbilityFilter excludedFilter,
        bool manualTracking)
    {
        return allCandidates
            .Where(ability => IsInTrackedEditorTab(ability, this.config.TrackedEditorTab))
            .Where(ability => MatchesTrackedSkillSearch(ability, this.config.TrackedSkillSearch))
            .OrderByDescending(ability => manualTracking
                ? this.IsAbilityTracked(iconWindow, job, ability.Id)
                : !this.IsAbilityExcluded(excludedFilter, ability))
            .ThenBy(ability => ability.Job == "ROLE" ? 1 : 0)
            .ThenBy(ability => ability.Level)
            .ThenBy(ability => string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private List<AbilityDefinition> ResolveTrackedOrderAbilities(
        string job,
        IReadOnlyList<AbilityDefinition> candidates,
        IReadOnlyList<string> tracked)
    {
        var byId = candidates
            .GroupBy(ability => ability.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var visible = new List<AbilityDefinition>();
        var seen = new HashSet<uint>();

        foreach (var trackedId in tracked)
        {
            var ability = byId.TryGetValue(trackedId, out var candidate)
                ? candidate
                : this.ResolveTrackedAbilityForLevel(trackedId, job, uint.MaxValue, candidates);
            if (ability is not null && seen.Add(ability.ActionId))
                visible.Add(ability);
        }

        return visible;
    }

    private static bool MatchesTrackedSkillSearch(AbilityDefinition ability, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var query = search.Trim();
        var displayName = string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name;
        return displayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || ability.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
               || ability.ActionId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
               || ability.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInTrackedEditorTab(AbilityDefinition ability, string tab)
    {
        if (string.Equals(tab, "Role", StringComparison.OrdinalIgnoreCase))
            return string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        return tab switch
        {
            "WeaponSkill" => ability.ActionCategoryId == 3,
            "Spell" => ability.ActionCategoryId == 2,
            "Ability" => ability.ActionCategoryId == 4,
            _ => true,
        };
    }
}
