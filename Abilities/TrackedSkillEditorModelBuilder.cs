namespace FFXIVAura;

internal readonly record struct TrackedSkillEditorTab(
    string Id,
    string Label);

internal static class TrackedSkillEditorTabs
{
    public static IReadOnlyList<TrackedSkillEditorTab> Items { get; } =
    [
        new("WeaponSkill", "무기"),
        new("Spell", "마법"),
        new("Ability", "능력"),
        new("Role", "역할"),
    ];
}

internal readonly record struct TrackedSkillEditorModel(
    IReadOnlyList<AbilityDefinition> CurrentCandidates,
    IReadOnlySet<string> Excluded,
    AbilityExclusionFilter ExcludedFilter,
    bool ManualTracking,
    IReadOnlyDictionary<string, int> TabCounts,
    IReadOnlyList<AbilityDefinition> Candidates);

internal sealed class TrackedSkillEditorModelBuilder
{
    private readonly AbilityCatalog abilityCatalog;
    private readonly AbilityTrackingService abilityTrackingService;

    public TrackedSkillEditorModelBuilder(
        AbilityCatalog abilityCatalog,
        AbilityTrackingService abilityTrackingService)
    {
        this.abilityCatalog = abilityCatalog;
        this.abilityTrackingService = abilityTrackingService;
    }

    public TrackedSkillEditorModel Build(
        IconWindowConfig iconWindow,
        string job,
        uint level,
        string selectedTab,
        string? search)
    {
        var allCandidates = this.abilityCatalog.GetJobCandidates(job, uint.MaxValue);
        var currentCandidates = this.abilityCatalog.GetJobCandidates(job, level);
        var excluded = this.abilityTrackingService.GetExcludedIds(iconWindow, job);
        var excludedFilter = this.abilityCatalog.CreateExclusionFilter(
            iconWindow.ExcludedByJob.GetValueOrDefault(job),
            job);
        var manualTracking = AbilityTrackingService.IsManualTracking(iconWindow, job);
        var tabCounts = CountTabs(allCandidates);
        var candidates = this.BuildCandidateList(
            iconWindow,
            job,
            allCandidates,
            excludedFilter,
            manualTracking,
            selectedTab,
            search);

        return new TrackedSkillEditorModel(
            currentCandidates,
            excluded,
            excludedFilter,
            manualTracking,
            tabCounts,
            candidates);
    }

    public IReadOnlyList<AbilityDefinition> ResolveTrackedOrderAbilities(
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
                : this.abilityCatalog.ResolveTrackedForLevel(trackedId, job, uint.MaxValue, candidates);
            if (ability is not null && seen.Add(ability.ActionId))
                visible.Add(ability);
        }

        return visible;
    }

    private static IReadOnlyDictionary<string, int> CountTabs(
        IReadOnlyList<AbilityDefinition> allCandidates)
        => TrackedSkillEditorTabs.Items.ToDictionary(
            tab => tab.Id,
            tab => allCandidates.Count(ability => IsInTab(ability, tab.Id)),
            StringComparer.OrdinalIgnoreCase);

    private List<AbilityDefinition> BuildCandidateList(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> allCandidates,
        AbilityExclusionFilter excludedFilter,
        bool manualTracking,
        string selectedTab,
        string? search)
        => allCandidates
            .Where(ability => IsInTab(ability, selectedTab))
            .Where(ability => MatchesSearch(ability, search))
            .OrderByDescending(ability => manualTracking
                ? this.abilityTrackingService.IsTracked(iconWindow, job, ability.Id)
                : !this.abilityCatalog.IsExcluded(excludedFilter, ability))
            .ThenBy(ability => ability.Job == "ROLE" ? 1 : 0)
            .ThenBy(ability => ability.Level)
            .ThenBy(
                ability => string.IsNullOrWhiteSpace(ability.Name) ? ability.Id : ability.Name,
                StringComparer.CurrentCulture)
            .ToList();

    private static bool MatchesSearch(AbilityDefinition ability, string? search)
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

    private static bool IsInTab(AbilityDefinition ability, string tab)
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

internal sealed class TrackedSkillEditorSession
{
    public string? DraggedAbilityId { get; private set; }

    public bool DefaultResetDeferred { get; private set; }

    public void BeginDrag(string abilityId)
        => this.DraggedAbilityId = abilityId;

    public void EndDrag()
        => this.DraggedAbilityId = null;

    public bool IsDragging(string abilityId)
        => string.Equals(
            this.DraggedAbilityId,
            abilityId,
            StringComparison.OrdinalIgnoreCase);

    public void NoteDefaultResetResult(bool succeeded)
        => this.DefaultResetDeferred = !succeeded;

    public void ClearDefaultResetDeferred()
        => this.DefaultResetDeferred = false;
}
