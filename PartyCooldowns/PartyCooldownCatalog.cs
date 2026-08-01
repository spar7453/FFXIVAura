namespace FFXIVAura;

internal readonly record struct PartyCooldownCatalogDiagnostics(
    int DefinitionCount,
    int ActionLookupCount,
    int NameLookupCount,
    int EffectiveScopeCacheCount,
    int EffectiveCategoryCacheCount,
    int StatusIdCacheCount,
    int MaxChargeCacheCount);

internal sealed class PartyCooldownCatalog
{
    private static readonly JsonSerializerOptions DataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string assemblyDirectory;
    private readonly AbilityCatalog abilityCatalog;
    private readonly IGameActionRepository gameActionRepository;
    private readonly IGameActionRuntime gameActionRuntime;
    private readonly IPluginLog log;
    private readonly List<PartyCooldownDefinition> definitions = [];
    private readonly Dictionary<uint, uint[]> statusIdsByActionId = new();
    private readonly Dictionary<uint, PartyCooldownDefinition> definitionsByActionId = new();
    private readonly Dictionary<ulong, uint> maxChargesByActionAndLevel = new();
    private readonly Dictionary<string, List<PartyCooldownDefinition>> definitionsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<PartyCooldownCategory, List<PartyCooldownDefinition>> definitionsByCategory = new();
    private readonly Dictionary<PartyCooldownDefinitionScopeKey, IReadOnlyList<PartyCooldownDefinition>> effectiveDefinitionsByScope = new();
    private readonly Dictionary<PartyCooldownCategory, IReadOnlyList<PartyCooldownDefinition>> presetDefinitionsByCategory = new();
    private readonly Dictionary<PartyCooldownCategoryLevelKey, IReadOnlyList<PartyCooldownDefinition>> effectiveDefinitionsByCategoryAndLevel = new();
    private readonly Dictionary<string, string> canonicalDefinitionIdById = new(StringComparer.OrdinalIgnoreCase);

    public PartyCooldownCatalog(
        string assemblyDirectory,
        AbilityCatalog abilityCatalog,
        IGameActionRepository gameActionRepository,
        IGameActionRuntime gameActionRuntime,
        IPluginLog log)
    {
        this.assemblyDirectory = assemblyDirectory;
        this.abilityCatalog = abilityCatalog;
        this.gameActionRepository = gameActionRepository;
        this.gameActionRuntime = gameActionRuntime;
        this.log = log;
    }

    public IReadOnlyList<PartyCooldownDefinition> Definitions => this.definitions;

    public void Load()
    {
        var loaded = PluginDataFiles.ReadJson<List<PartyCooldownDefinition>>(
            this.assemblyDirectory,
            Path.Combine("Data", "party_cooldowns.json"),
            PluginDataFiles.PartyCooldownsResourceName,
            DataJsonOptions,
            definitions => PartyCooldownDataValidator.IsValid(definitions)
                           && definitions.All(definition =>
                               this.abilityCatalog.FindConfiguredByActionId(definition.ActionId) is not null));
        if (loaded.ExternalFailure is { } externalFailure)
        {
            this.log.Debug(
                externalFailure,
                "Invalid external party cooldown data was replaced with the embedded copy.");
        }

        var normalized = loaded.Value
            .Select(this.NormalizeDefinition)
            .ToList();

        this.definitions.Clear();
        this.definitions.AddRange(normalized);
        this.RebuildLookups();
    }

    public bool ContainsActionId(uint actionId)
        => this.definitionsByActionId.ContainsKey(actionId);

    public bool ContainsActionName(string normalizedActionName)
        => this.definitionsByName.ContainsKey(normalizedActionName);

    public bool TryGetByActionId(uint actionId, out PartyCooldownDefinition definition)
        => this.definitionsByActionId.TryGetValue(actionId, out definition!);

    public bool TryGetByName(
        string normalizedActionName,
        out IReadOnlyList<PartyCooldownDefinition> definitions)
    {
        if (this.definitionsByName.TryGetValue(normalizedActionName, out var matches))
        {
            definitions = matches;
            return true;
        }

        definitions = Array.Empty<PartyCooldownDefinition>();
        return false;
    }

    public IReadOnlyList<PartyCooldownDefinition> GetDefinitionsForCategory(PartyCooldownCategory category)
        => this.definitionsByCategory.TryGetValue(category, out var matches)
            ? matches
            : Array.Empty<PartyCooldownDefinition>();

    public IReadOnlyList<PartyCooldownDefinition> GetPresetDefinitionsForCategory(PartyCooldownCategory category)
    {
        if (this.presetDefinitionsByCategory.TryGetValue(category, out var cached))
            return cached;

        var presetDefinitions = PartyCooldownDefinitionSelector
            .SelectCanonicalReplacements(this.GetDefinitionsForCategory(category), this.GetEquivalenceKey)
            .OrderBy(definition => PartyCooldownDefinitionOrdering.GetJobSortOrder(definition.Job))
            .ThenBy(definition => definition.Level)
            .ThenByDescending(definition => definition.Cooldown)
            .ToList();
        this.presetDefinitionsByCategory[category] = presetDefinitions;
        return presetDefinitions;
    }

    public IReadOnlyList<PartyCooldownDefinition> GetEffectiveDefinitionsForCategory(
        PartyCooldownCategory category,
        uint level)
    {
        var cacheKey = new PartyCooldownCategoryLevelKey(category, level);
        if (this.effectiveDefinitionsByCategoryAndLevel.TryGetValue(cacheKey, out var cached))
            return cached;

        var definitionsByJob = new Dictionary<string, List<PartyCooldownDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in this.GetDefinitionsForCategory(category))
        {
            if (!definitionsByJob.TryGetValue(definition.Job, out var jobDefinitions))
            {
                jobDefinitions = [];
                definitionsByJob[definition.Job] = jobDefinitions;
            }

            jobDefinitions.Add(definition);
        }

        var effectiveDefinitions = new List<PartyCooldownDefinition>();
        foreach (var jobDefinitions in definitionsByJob.Values)
        {
            effectiveDefinitions.AddRange(PartyCooldownDefinitionSelector.SelectEffectiveForLevel(
                jobDefinitions,
                level,
                this.GetEquivalenceKey));
        }

        effectiveDefinitions.Sort(PartyCooldownDefinitionOrdering.CompareForUi);
        this.effectiveDefinitionsByCategoryAndLevel[cacheKey] = effectiveDefinitions;
        return effectiveDefinitions;
    }

    public IReadOnlyList<PartyCooldownDefinition> GetEffectiveDefinitionsForMember(
        PartyCooldownCategory category,
        string job,
        uint level)
    {
        var cacheKey = new PartyCooldownDefinitionScopeKey(category, job, level);
        if (this.effectiveDefinitionsByScope.TryGetValue(cacheKey, out var cached))
            return cached;

        var candidates = this.GetDefinitionsForCategory(category)
            .Where(definition => this.IsForJob(definition, job));
        var effectiveDefinitions = PartyCooldownDefinitionSelector.SelectEffectiveForLevel(
            candidates,
            level,
            this.GetEquivalenceKey);
        this.effectiveDefinitionsByScope[cacheKey] = effectiveDefinitions;
        return effectiveDefinitions;
    }

    public IEnumerable<PartyCooldownDefinition> GetDefinitionsForMember(
        PartyCooldownCategory category,
        string job,
        uint level,
        IconWindowConfig iconWindow)
    {
        foreach (var definition in this.GetEffectiveDefinitionsForMember(category, job, level))
        {
            if (!this.IsExcluded(iconWindow, definition))
                yield return definition;
        }
    }

    public bool IsForJob(PartyCooldownDefinition definition, string job)
    {
        if (string.Equals(definition.Job, job, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(definition.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        var roleAbility = this.abilityCatalog.FindConfiguredByActionId(definition.ActionId);
        return roleAbility is not null && JobInfo.CanUseRoleAction(job, roleAbility);
    }

    public bool DefinitionsMatch(PartyCooldownDefinition first, PartyCooldownDefinition second)
    {
        if (first.ActionId == second.ActionId
            || string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstKey = this.GetEquivalenceKey(first);
        return !string.IsNullOrEmpty(firstKey)
               && string.Equals(firstKey, this.GetEquivalenceKey(second), StringComparison.OrdinalIgnoreCase);
    }

    public PartyCooldownDefinition ResolveEffectiveDefinition(
        PartyCooldownDefinition definition,
        string job,
        uint level)
    {
        if (!Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var category))
            return definition;

        return this.GetEffectiveDefinitionsForMember(category, job, level)
                   .FirstOrDefault(candidate => this.DefinitionsMatch(definition, candidate))
               ?? definition;
    }

    public bool IsExcluded(IconWindowConfig iconWindow, PartyCooldownDefinition definition)
        => PartyCooldownDefinitionIdentity.IsExcluded(
            iconWindow.ExcludedPartyCooldownIds,
            this.canonicalDefinitionIdById,
            definition);

    public void SetExcluded(
        IconWindowConfig iconWindow,
        PartyCooldownDefinition definition,
        bool excluded)
    {
        var normalizedId = this.GetCanonicalDefinitionId(definition).Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
            return;

        this.RemoveExclusions(iconWindow, [definition]);
        if (excluded)
            iconWindow.ExcludedPartyCooldownIds.Add(normalizedId);
    }

    public void RemoveExclusions(
        IconWindowConfig iconWindow,
        IEnumerable<PartyCooldownDefinition> definitions)
        => PartyCooldownDefinitionIdentity.RemoveExclusions(
            iconWindow.ExcludedPartyCooldownIds,
            this.canonicalDefinitionIdById,
            definitions);

    public string GetCanonicalDefinitionId(PartyCooldownDefinition definition)
        => PartyCooldownDefinitionIdentity.GetCanonicalDefinitionId(
            this.canonicalDefinitionIdById,
            definition);

    public PartyCooldownRuntimeKey CreateRuntimeKey(
        string memberKey,
        PartyCooldownDefinition definition)
        => new(memberKey, this.GetCanonicalDefinitionId(definition));

    public IReadOnlyList<uint> ResolveStatusIds(PartyCooldownDefinition definition)
    {
        if (this.statusIdsByActionId.TryGetValue(definition.ActionId, out var cached))
            return cached;

        var action = this.gameActionRepository.GetAction(definition.ActionId);
        cached = PartyCooldownStatusResolver.Resolve(definition.StatusIds, action?.StatusGainSelfId ?? 0);
        this.statusIdsByActionId[definition.ActionId] = cached;
        return cached;
    }

    public uint GetMaxCharges(PartyCooldownDefinition definition, uint level)
    {
        var effectiveLevel = Math.Max(1u, level);
        var cacheKey = ((ulong)effectiveLevel << 32) | definition.ActionId;
        if (this.maxChargesByActionAndLevel.TryGetValue(cacheKey, out var cached))
            return cached;

        var resolved = Math.Max(1u, definition.Charges);
        try
        {
            var maxCharges = this.gameActionRuntime.GetMaxCharges(definition.ActionId, effectiveLevel);
            if (maxCharges > 0)
                resolved = maxCharges;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read party cooldown charges for {definition.ActionId} at level {level}.");
        }

        this.maxChargesByActionAndLevel[cacheKey] = resolved;
        return resolved;
    }

    public PartyCooldownCatalogDiagnostics CreateDiagnostics()
        => new(
            this.definitions.Count,
            this.definitionsByActionId.Count,
            this.definitionsByName.Count,
            this.effectiveDefinitionsByScope.Count,
            this.effectiveDefinitionsByCategoryAndLevel.Count,
            this.statusIdsByActionId.Count,
            this.maxChargesByActionAndLevel.Count);

    public static bool IsCategory(PartyCooldownDefinition definition, PartyCooldownCategory category)
        => Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var parsed)
           && parsed == category;

    private PartyCooldownDefinition NormalizeDefinition(PartyCooldownDefinition definition)
    {
        var ability = this.abilityCatalog.FindConfiguredByActionId(definition.ActionId);
        var action = this.gameActionRepository.GetAction(definition.ActionId);

        definition.Id = definition.Id.Trim();
        definition.Category = definition.Category.Trim();
        definition.Job = definition.Job.Trim().ToUpperInvariant();
        definition.Name = definition.Name?.Trim() ?? string.Empty;
        definition.ReplacementGroup = definition.ReplacementGroup?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ability?.ReplacementGroup))
            definition.ReplacementGroup = ability.ReplacementGroup;
        definition.StatusIds = definition.StatusIds
            .Where(statusId => statusId > 0)
            .Distinct()
            .Order()
            .ToArray();

        if (string.IsNullOrWhiteSpace(definition.Id))
            definition.Id = ability?.Id ?? $"action-{definition.ActionId}";

        if (string.IsNullOrWhiteSpace(definition.Job))
            definition.Job = ability?.Job?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(definition.Name))
            definition.Name = action?.Name ?? ability?.Name ?? $"Action {definition.ActionId}";

        if (definition.Level == 0)
            definition.Level = ability?.Level ?? (byte)Math.Min(action?.ClassJobLevel ?? 1, byte.MaxValue);

        if (definition.Cooldown <= 0f)
            definition.Cooldown = ability?.Cooldown ?? ((action?.Recast100ms ?? 0) / 10f);

        definition.Charges = Math.Max(
            definition.Charges,
            Math.Max(ability?.Charges ?? (byte)1, action?.MaxCharges ?? (byte)1));

        if (definition.IconId == 0)
            definition.IconId = ability?.IconId ?? action?.Icon ?? 0;

        return definition;
    }

    private void RebuildLookups()
    {
        this.statusIdsByActionId.Clear();
        this.definitionsByActionId.Clear();
        this.maxChargesByActionAndLevel.Clear();
        this.definitionsByName.Clear();
        this.definitionsByCategory.Clear();
        this.effectiveDefinitionsByScope.Clear();
        this.presetDefinitionsByCategory.Clear();
        this.effectiveDefinitionsByCategoryAndLevel.Clear();
        this.canonicalDefinitionIdById.Clear();

        foreach (var definition in this.definitions
                     .OrderBy(definition => string.Equals(definition.Job, "ROLE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                     .ThenBy(definition => definition.Level)
                     .ThenByDescending(definition => definition.Cooldown))
        {
            if (definition.ActionId > 0)
                this.definitionsByActionId.TryAdd(definition.ActionId, definition);

            if (Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var category))
            {
                if (!this.definitionsByCategory.TryGetValue(category, out var categoryDefinitions))
                {
                    categoryDefinitions = [];
                    this.definitionsByCategory[category] = categoryDefinitions;
                }

                categoryDefinitions.Add(definition);
            }

            var name = PartyCooldownLogMatcher.NormalizeActionName(definition.Name);
            if (name.Length == 0)
                continue;

            if (!this.definitionsByName.TryGetValue(name, out var nameDefinitions))
            {
                nameDefinitions = [];
                this.definitionsByName[name] = nameDefinitions;
            }

            nameDefinitions.Add(definition);
        }

        foreach (var pair in PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(
                     this.definitions,
                     this.GetEquivalenceKey))
        {
            this.canonicalDefinitionIdById[pair.Key] = pair.Value;
        }
    }

    private string GetEquivalenceKey(PartyCooldownDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.ReplacementGroup))
            return $"{definition.Job}:{definition.Category}:{definition.ReplacementGroup}";

        var action = this.gameActionRepository.GetAction(definition.ActionId);
        if (action is null || action.Value.EquivalenceGroup == 0 || action.Value.ActionCategoryId == 0)
            return string.Empty;

        return $"{definition.Job}:{definition.Category}:{action.Value.ActionCategoryId}:{action.Value.EquivalenceGroup}";
    }
}

internal static class PartyCooldownDefinitionOrdering
{
    private static readonly string[] JobOrder =
    [
        "ROLE",
        "PLD", "WAR", "DRK", "GNB",
        "WHM", "SCH", "AST", "SGE",
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR",
        "BRD", "MCH", "DNC",
        "BLM", "SMN", "RDM", "PCT",
    ];

    public static int GetJobSortOrder(string job)
    {
        for (var index = 0; index < JobOrder.Length; index++)
        {
            if (string.Equals(JobOrder[index], job, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return int.MaxValue;
    }

    public static int CompareForUi(PartyCooldownDefinition left, PartyCooldownDefinition right)
    {
        var jobCompare = GetJobSortOrder(left.Job).CompareTo(GetJobSortOrder(right.Job));
        if (jobCompare != 0)
            return jobCompare;

        var levelCompare = left.Level.CompareTo(right.Level);
        if (levelCompare != 0)
            return levelCompare;

        var cooldownCompare = right.Cooldown.CompareTo(left.Cooldown);
        if (cooldownCompare != 0)
            return cooldownCompare;

        return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
    }
}
