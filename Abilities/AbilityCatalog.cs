namespace FFXIVAura;

internal readonly record struct AbilityCatalogDiagnostics(
    int DefinitionCount,
    int DefinitionIdLookupCount,
    int DefinitionActionLookupCount,
    int JobCandidateCacheCount,
    int GameActionCandidateCacheCount,
    int IdMatcherCacheCount);

internal readonly record struct AbilityExclusionFilter(
    HashSet<string> Ids,
    HashSet<string> EquivalenceKeys);

internal readonly record struct AbilityCandidateSnapshot(
    bool IsComplete,
    IReadOnlyList<AbilityDefinition> Candidates);

internal sealed class AbilityCatalog
{
    private const int CandidateCacheLimit = 512;
    private static readonly JsonSerializerOptions DataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string assemblyDirectory;
    private readonly IGameActionRepository gameActionRepository;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly IPluginLog log;
    private readonly List<AbilityDefinition> definitions = [];
    private readonly Dictionary<string, AbilityDefinition> definitionsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, AbilityDefinition> definitionsByActionId = new();
    private readonly Dictionary<string, List<AbilityDefinition>> gameActionCandidates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<JobLevelKey, IReadOnlyList<AbilityDefinition>> jobCandidates = new();
    private readonly Dictionary<string, Func<string, string, bool>> idMatchersByJob = new(StringComparer.OrdinalIgnoreCase);

    public AbilityCatalog(
        string assemblyDirectory,
        IGameActionRepository gameActionRepository,
        PerformanceProfiler performanceProfiler,
        IPluginLog log)
    {
        this.assemblyDirectory = assemblyDirectory;
        this.gameActionRepository = gameActionRepository;
        this.performanceProfiler = performanceProfiler;
        this.log = log;
    }

    public IReadOnlyList<AbilityDefinition> Definitions => this.definitions;

    public int Count => this.definitions.Count;

    public void Load()
    {
        var loaded = PluginDataFiles.ReadJson<List<AbilityDefinition>>(
            this.assemblyDirectory,
            Path.Combine("Data", "abilities.json"),
            PluginDataFiles.AbilitiesResourceName,
            DataJsonOptions,
            AbilityDataValidator.IsValid);
        if (loaded.ExternalFailure is { } externalFailure)
        {
            this.log.Debug(
                externalFailure,
                "Invalid external ability data was replaced with the embedded copy.");
        }

        var normalized = loaded.Value
            .Select(this.NormalizeDefinition)
            .ToList();

        this.definitions.Clear();
        this.definitions.AddRange(normalized);
        this.RebuildLookups();
    }

    public IReadOnlyList<AbilityDefinition> GetJobCandidates(string job, uint level)
        => this.GetJobCandidateSnapshot(job, level).Candidates;

    public AbilityCandidateSnapshot GetJobCandidateSnapshot(string job, uint level)
    {
        var normalizedJob = job.Trim();
        var key = new JobLevelKey(normalizedJob, level);
        if (this.jobCandidates.TryGetValue(key, out var cached))
            return new AbilityCandidateSnapshot(true, cached);

        var result = this.BuildJobCandidates(normalizedJob, level);
        if (result.IsComplete)
        {
            if (this.jobCandidates.Count >= CandidateCacheLimit)
                this.jobCandidates.Clear();

            this.jobCandidates[key] = result.Candidates;
        }

        return result;
    }

    public AbilityDefinition? FindConfiguredByActionId(uint actionId)
        => this.definitionsByActionId.GetValueOrDefault(actionId);

    public AbilityDefinition? FindTrackedDefinition(string trackedId, string job)
    {
        if (this.definitionsById.TryGetValue(trackedId, out var configured))
            return configured;

        var actionId = ParseActionIdFromGeneratedAbilityId(trackedId);
        if (actionId == 0)
            return null;

        if (this.definitionsByActionId.TryGetValue(actionId, out var configuredByAction))
            return configuredByAction;

        try
        {
            var row = this.gameActionRepository.GetAction(actionId);
            if (row is null)
                return null;

            var action = row.Value;
            var name = action.Name;
            return new AbilityDefinition
            {
                Id = trackedId,
                Name = string.IsNullOrWhiteSpace(name) ? trackedId : name,
                ActionId = action.RowId,
                ActionIds = [action.RowId],
                ActionCategoryId = action.ActionCategoryId,
                Job = action.IsRoleAction ? "ROLE" : job,
                Level = (byte)Math.Min(action.ClassJobLevel, byte.MaxValue),
                Cooldown = action.Recast100ms / 10f,
                Charges = Math.Max(action.MaxCharges, (byte)1),
                IconId = action.Icon,
            };
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to resolve tracked action {trackedId} for {job}.");
            return null;
        }
    }

    public AbilityDefinition? ResolveTrackedForLevel(
        string trackedId,
        string job,
        uint level,
        IReadOnlyList<AbilityDefinition> candidates)
    {
        var exact = candidates.FirstOrDefault(
            ability => string.Equals(ability.Id, trackedId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var trackedAbility = this.FindTrackedDefinition(trackedId, job);
        if (trackedAbility is null)
            return null;

        var equivalenceKey = this.GetEquivalenceKey(trackedAbility);
        if (string.IsNullOrEmpty(equivalenceKey))
            return null;

        return candidates
            .Where(ability => ability.Level <= level)
            .Where(ability => string.Equals(
                this.GetEquivalenceKey(ability),
                equivalenceKey,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ability => ability.Level)
            .ThenByDescending(ability => ability.ActionId)
            .FirstOrDefault();
    }

    public bool IdMatchesAbility(string trackedId, string job, AbilityDefinition ability)
    {
        if (string.Equals(trackedId, ability.Id, StringComparison.OrdinalIgnoreCase))
            return true;

        var trackedAbility = this.FindTrackedDefinition(trackedId, job);
        return trackedAbility is not null && this.ShareEquivalence(trackedAbility, ability);
    }

    public bool IdsMatch(string firstId, string secondId, string job)
    {
        if (string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase))
            return true;

        var secondAbility = this.FindTrackedDefinition(secondId, job);
        return secondAbility is not null && this.IdMatchesAbility(firstId, job, secondAbility);
    }

    public Func<string, string, bool> GetIdMatcher(string job)
    {
        if (this.idMatchersByJob.TryGetValue(job, out var cached))
            return cached;

        bool Matcher(string firstId, string secondId)
            => this.IdsMatch(firstId, secondId, job);

        this.idMatchersByJob[job] = Matcher;
        return Matcher;
    }

    public bool ShareEquivalence(AbilityDefinition first, AbilityDefinition second)
    {
        var firstKey = this.GetEquivalenceKey(first);
        return !string.IsNullOrEmpty(firstKey)
               && string.Equals(firstKey, this.GetEquivalenceKey(second), StringComparison.OrdinalIgnoreCase);
    }

    public AbilityExclusionFilter CreateExclusionFilter(IEnumerable<string>? excludedIds, string job)
    {
        var ids = excludedIds?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                  ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var equivalenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var ability = this.FindTrackedDefinition(id, job);
            var key = ability is null ? string.Empty : this.GetEquivalenceKey(ability);
            if (!string.IsNullOrEmpty(key))
                equivalenceKeys.Add(key);
        }

        return new AbilityExclusionFilter(ids, equivalenceKeys);
    }

    public bool IsExcluded(AbilityExclusionFilter filter, AbilityDefinition ability)
    {
        if (filter.Ids.Contains(ability.Id))
            return true;

        var key = this.GetEquivalenceKey(ability);
        return !string.IsNullOrEmpty(key) && filter.EquivalenceKeys.Contains(key);
    }

    public string GetEquivalenceKey(AbilityDefinition ability)
    {
        var replacementGroup = ability.ReplacementGroup;
        var replacementJob = ability.Job;
        if (string.IsNullOrWhiteSpace(replacementGroup)
            && this.definitionsByActionId.TryGetValue(ability.ActionId, out var configured))
        {
            replacementGroup = configured.ReplacementGroup;
            replacementJob = configured.Job;
        }

        if (!string.IsNullOrWhiteSpace(replacementGroup))
            return $"{replacementJob.Trim()}:{replacementGroup.Trim()}";

        var action = this.gameActionRepository.GetAction(ability.ActionId);
        return action is null || action.Value.EquivalenceGroup == 0 || action.Value.ActionCategoryId == 0
            ? string.Empty
            : $"{ability.Job}:{action.Value.ActionCategoryId}:{action.Value.EquivalenceGroup}";
    }

    public AbilityCatalogDiagnostics CreateDiagnostics()
        => new(
            this.definitions.Count,
            this.definitionsById.Count,
            this.definitionsByActionId.Count,
            this.jobCandidates.Count,
            this.gameActionCandidates.Count,
            this.idMatchersByJob.Count);

    private AbilityDefinition NormalizeDefinition(AbilityDefinition ability)
    {
        ability.Id = ability.Id.Trim();
        ability.Job = ability.Job.Trim().ToUpperInvariant();
        ability.ReplacementGroup = ability.ReplacementGroup?.Trim() ?? string.Empty;
        if (ability.ActionCategoryId == 0)
            ability.ActionCategoryId = this.gameActionRepository.GetAction(ability.ActionId)?.ActionCategoryId ?? 0;

        return ability;
    }

    private void RebuildLookups()
    {
        this.definitionsById.Clear();
        this.definitionsByActionId.Clear();
        this.gameActionCandidates.Clear();
        this.jobCandidates.Clear();
        this.idMatchersByJob.Clear();

        foreach (var ability in this.definitions)
        {
            this.definitionsById.TryAdd(ability.Id, ability);
            this.definitionsByActionId.TryAdd(ability.ActionId, ability);
        }
    }

    private AbilityCandidateSnapshot BuildJobCandidates(string job, uint level)
    {
        var candidates = new List<AbilityDefinition>();
        var seen = new HashSet<uint>();
        foreach (var ability in this.definitions)
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

        var gameActionCandidates = this.GetGameActionCandidates(job);
        foreach (var ability in gameActionCandidates.Candidates)
        {
            if (ability.Level <= level && seen.Add(ability.ActionId))
                candidates.Add(ability);
        }

        return new AbilityCandidateSnapshot(
            gameActionCandidates.IsComplete,
            this.SelectEffectiveCandidates(candidates));
    }

    private IReadOnlyList<AbilityDefinition> SelectEffectiveCandidates(
        IReadOnlyList<AbilityDefinition> candidates)
    {
        var effective = new List<AbilityDefinition>(candidates.Count);
        var indexByEquivalenceKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var equivalenceKey = this.GetEquivalenceKey(candidate);
            if (string.IsNullOrEmpty(equivalenceKey))
            {
                effective.Add(candidate);
                continue;
            }

            if (!indexByEquivalenceKey.TryGetValue(equivalenceKey, out var existingIndex))
            {
                indexByEquivalenceKey[equivalenceKey] = effective.Count;
                effective.Add(candidate);
                continue;
            }

            var existing = effective[existingIndex];
            if (candidate.Level > existing.Level
                || (candidate.Level == existing.Level && candidate.ActionId > existing.ActionId))
            {
                effective[existingIndex] = candidate;
            }
        }

        return effective;
    }

    private AbilityCandidateSnapshot GetGameActionCandidates(string job)
    {
        var key = job.Trim();
        if (!this.gameActionCandidates.TryGetValue(key, out var cached))
        {
            var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AbilityCandidateBuild);
            try
            {
                var result = this.BuildGameActionCandidates(key);
                if (!result.IsComplete)
                    return result;

                cached = result.Candidates.ToList();
            }
            finally
            {
                this.performanceProfiler.EndSection(PerformanceProfileSection.AbilityCandidateBuild, profileStart);
            }

            if (this.gameActionCandidates.Count >= CandidateCacheLimit)
                this.gameActionCandidates.Clear();

            this.gameActionCandidates[key] = cached;
        }

        return new AbilityCandidateSnapshot(true, cached);
    }

    private AbilityCandidateSnapshot BuildGameActionCandidates(string job)
    {
        var classJobIds = JobInfo.ApplicableClassJobIds(job);
        if (classJobIds.Count == 0)
            return new AbilityCandidateSnapshot(true, Array.Empty<AbilityDefinition>());

        var query = this.gameActionRepository.EnumerateActionsForClassJobs(classJobIds);
        if (!query.Succeeded)
            return new AbilityCandidateSnapshot(false, Array.Empty<AbilityDefinition>());

        var candidates = new List<AbilityDefinition>();
        foreach (var row in query.Actions)
        {
            if (row.RowId == 0 || row.ClassJobLevel == 0)
                continue;

            var category = row.ActionCategoryId;
            if (category is not (2 or 3 or 4))
                continue;

            var name = row.Name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            candidates.Add(new AbilityDefinition
            {
                Id = $"{job.ToLowerInvariant()}-{row.RowId}",
                Name = name,
                ActionId = row.RowId,
                ActionIds = [row.RowId],
                ActionCategoryId = row.ActionCategoryId,
                Job = job,
                Level = (byte)Math.Min(row.ClassJobLevel, byte.MaxValue),
                Cooldown = row.Recast100ms / 10f,
                Charges = Math.Max(row.MaxCharges, (byte)1),
                IconId = row.Icon,
            });
        }

        return new AbilityCandidateSnapshot(true, candidates);
    }

    private static uint ParseActionIdFromGeneratedAbilityId(string trackedId)
    {
        var dash = trackedId.LastIndexOf('-');
        if (dash < 0 || dash == trackedId.Length - 1)
            return 0;

        return uint.TryParse(trackedId[(dash + 1)..], out var actionId) ? actionId : 0;
    }
}
