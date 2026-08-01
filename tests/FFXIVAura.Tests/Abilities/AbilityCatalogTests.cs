using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AbilityCatalogTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AbilityCatalog loads configured definitions and lookups", LoadsConfiguredDefinitionsAndLookups),
        ("AbilityCatalog caches job candidates and id matchers", CachesJobCandidatesAndIdMatchers),
        ("AbilityCatalog retries failed game action candidate reads", RetriesFailedGameActionCandidateReads),
        ("AbilityCatalog selects the effective replacement for a level", SelectsEffectiveReplacementForLevel),
        ("AbilityCatalog builds case-insensitive exclusion filters", BuildsCaseInsensitiveExclusionFilters),
        ("AbilityCatalog reload clears derived caches", ReloadClearsDerivedCaches),
    ];

    private static void LoadsConfiguredDefinitionsAndLookups()
    {
        var catalog = AbilityCatalogTestFactory.Create();
        catalog.Load();

        True(catalog.Count > 0, "ability definitions should load");
        var definition = catalog.Definitions[0];
        var resolved = catalog.FindConfiguredByActionId(definition.ActionId);
        True(resolved is not null, "an action lookup should resolve a configured definition");
        Equal(definition.Id, resolved!.Id);

        var diagnostics = catalog.CreateDiagnostics();
        Equal(catalog.Count, diagnostics.DefinitionCount);
        Equal(catalog.Count, diagnostics.DefinitionIdLookupCount);
        True(diagnostics.DefinitionActionLookupCount > 0, "action lookup index should be populated");
    }

    private static void CachesJobCandidatesAndIdMatchers()
    {
        var catalog = AbilityCatalogTestFactory.Create();
        catalog.Load();

        var candidates = catalog.GetJobCandidates("VPR", 100);
        True(candidates.Count > 0, "known jobs should have configured candidates");
        True(
            candidates.All(ability => ability.Level <= 100),
            "candidate levels should respect the requested level");

        var matcher = catalog.GetIdMatcher("VPR");
        True(matcher(candidates[0].Id, candidates[0].Id.ToUpperInvariant()), "exact ids should match case-insensitively");

        var diagnostics = catalog.CreateDiagnostics();
        Equal(1, diagnostics.JobCandidateCacheCount);
        Equal(1, diagnostics.GameActionCandidateCacheCount);
        Equal(1, diagnostics.IdMatcherCacheCount);
    }

    private static void ReloadClearsDerivedCaches()
    {
        var catalog = AbilityCatalogTestFactory.Create();
        catalog.Load();
        _ = catalog.GetJobCandidates("DRG", 100);
        _ = catalog.GetIdMatcher("DRG");

        catalog.Load();

        var diagnostics = catalog.CreateDiagnostics();
        Equal(0, diagnostics.JobCandidateCacheCount);
        Equal(0, diagnostics.GameActionCandidateCacheCount);
        Equal(0, diagnostics.IdMatcherCacheCount);
    }

    private static void RetriesFailedGameActionCandidateReads()
    {
        const uint dynamicActionId = 500_000;
        var repository = new FlakyGameActionRepository(CreateAction(dynamicActionId, 1, equivalenceGroup: 0));
        var catalog = AbilityCatalogTestFactory.Create(repository);
        catalog.Load();

        var incompleteSnapshot = catalog.GetJobCandidateSnapshot("PLD", 100);
        True(!incompleteSnapshot.IsComplete, "a failed dynamic query should expose an incomplete snapshot");
        var incomplete = incompleteSnapshot.Candidates;
        True(
            incomplete.All(ability => ability.ActionId != dynamicActionId),
            "a failed dynamic query should return configured fallback candidates");
        Equal(1, repository.EnumerationCount);
        var failedDiagnostics = catalog.CreateDiagnostics();
        Equal(0, failedDiagnostics.JobCandidateCacheCount);
        Equal(0, failedDiagnostics.GameActionCandidateCacheCount);

        var retriedSnapshot = catalog.GetJobCandidateSnapshot("PLD", 100);
        True(retriedSnapshot.IsComplete, "a successful retry should expose a complete snapshot");
        var retried = retriedSnapshot.Candidates;
        True(
            retried.Any(ability => ability.ActionId == dynamicActionId),
            "a later successful query should populate dynamic candidates");
        Equal(2, repository.EnumerationCount);
        var successfulDiagnostics = catalog.CreateDiagnostics();
        Equal(1, successfulDiagnostics.JobCandidateCacheCount);
        Equal(1, successfulDiagnostics.GameActionCandidateCacheCount);
    }

    private static void SelectsEffectiveReplacementForLevel()
    {
        const uint baseActionId = 17;
        const uint replacementActionId = 36920;
        var repository = new ReplacementGameActionRepository(
            CreateAction(baseActionId, 38, equivalenceGroup: 0),
            CreateAction(replacementActionId, 92, equivalenceGroup: 0));
        var catalog = AbilityCatalogTestFactory.Create(repository);
        catalog.Load();

        var beforeReplacement = catalog.GetJobCandidates("PLD", 91);
        True(
            beforeReplacement.Any(ability => ability.ActionId == baseActionId),
            "the base action should remain before the replacement unlocks");
        True(
            beforeReplacement.All(ability => ability.ActionId != replacementActionId),
            "the replacement should not appear before its unlock level");
        var syncedResolution = catalog.ResolveTrackedForLevel(
            "pld-guardian",
            "PLD",
            91,
            beforeReplacement);
        True(syncedResolution is not null, "a tracked replacement should resolve while level-synced");
        Equal(baseActionId, syncedResolution!.ActionId);

        var afterReplacement = catalog.GetJobCandidates("PLD", 100);
        True(
            afterReplacement.All(ability => ability.ActionId != baseActionId),
            "the base action should be removed after its replacement unlocks");
        Equal(
            1,
            afterReplacement.Count(ability => ability.ActionId == replacementActionId));

        var resolved = catalog.ResolveTrackedForLevel(
            "pld-sentinel",
            "PLD",
            100,
            afterReplacement);
        True(resolved is not null, "a tracked base action should resolve to its replacement");
        Equal(replacementActionId, resolved!.ActionId);

        True(
            catalog.IdsMatch("pld-sentinel", "pld-guardian", "PLD"),
            "explicit replacement metadata should match both tracked identities");
        var exclusion = catalog.CreateExclusionFilter(["PLD-SENTINEL"], "PLD");
        True(
            catalog.IsExcluded(exclusion, resolved),
            "excluding the base identity should exclude its replacement");
    }

    private static void BuildsCaseInsensitiveExclusionFilters()
    {
        var catalog = AbilityCatalogTestFactory.Create();
        catalog.Load();
        var ability = catalog.Definitions[0];

        var filter = catalog.CreateExclusionFilter([ability.Id.ToUpperInvariant()], ability.Job);

        True(catalog.IsExcluded(filter, ability), "excluded ids should match case-insensitively");
        True(
            !catalog.IsExcluded(
                filter,
                new AbilityDefinition { Id = "unrelated", ActionId = uint.MaxValue, Job = ability.Job }),
            "unrelated abilities should remain included");
    }

    private static GameActionInfo CreateAction(
        uint actionId,
        byte level,
        byte equivalenceGroup)
        => new(
            RowId: actionId,
            Name: $"Action {actionId}",
            Icon: 1,
            ActionCategoryId: 4,
            ActionCategoryName: "Ability",
            ClassJobLevel: level,
            Cast100ms: 0,
            Recast100ms: 1200,
            Range: 0,
            EffectRange: 0,
            MaxCharges: 1,
            IsRoleAction: false,
            EquivalenceGroup: equivalenceGroup,
            StatusGainSelfId: 0,
            CanTargetHostile: false,
            CanTargetSelf: true,
            CanTargetParty: false,
            CanTargetAlly: false);

    private sealed class ReplacementGameActionRepository(
        params GameActionInfo[] actions) : IGameActionRepository
    {
        private readonly IReadOnlyDictionary<uint, GameActionInfo> actions =
            actions.ToDictionary(action => action.RowId);

        public int CachedActionCount => this.actions.Count;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => this.actions.GetValueOrDefault(actionId);

        public GameActionEnumerationResult EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
            => GameActionEnumerationResult.Success(Array.Empty<GameActionInfo>());
    }

    private sealed class FlakyGameActionRepository(GameActionInfo action) : IGameActionRepository
    {
        public int EnumerationCount { get; private set; }

        public int CachedActionCount => 0;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => actionId == action.RowId ? action : null;

        public GameActionEnumerationResult EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
        {
            this.EnumerationCount++;
            return this.EnumerationCount == 1
                ? GameActionEnumerationResult.Failure
                : GameActionEnumerationResult.Success([action]);
        }
    }
}
