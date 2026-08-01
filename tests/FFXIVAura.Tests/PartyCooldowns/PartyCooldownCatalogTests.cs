using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownCatalogTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownCatalog uses shared ability replacement metadata", UsesSharedAbilityReplacementMetadata),
    ];

    private static void UsesSharedAbilityReplacementMetadata()
    {
        var repository = new StubGameActionRepository();
        var abilityCatalog = AbilityCatalogTestFactory.Create(repository);
        abilityCatalog.Load();
        var abilityDataPath = TestFiles.FindRepoFile("Data", "abilities.json");
        var repositoryRoot = Directory.GetParent(abilityDataPath)!.Parent!.FullName;
        var catalog = new PartyCooldownCatalog(
            repositoryRoot,
            abilityCatalog,
            repository,
            new StubGameActionRuntime(),
            null!);
        catalog.Load();

        var beforeUnlock = catalog
            .GetEffectiveDefinitionsForMember(PartyCooldownCategory.Defensive, "PLD", 91)
            .Select(definition => definition.ActionId)
            .ToArray();
        var atUnlock = catalog
            .GetEffectiveDefinitionsForMember(PartyCooldownCategory.Defensive, "PLD", 92)
            .Select(definition => definition.ActionId)
            .ToArray();

        True(beforeUnlock.Contains(17u), "the base action should be selected before the trait unlock");
        True(!beforeUnlock.Contains(36920u), "the replacement should remain unavailable before the trait unlock");
        True(!atUnlock.Contains(17u), "the base action should be replaced at the trait unlock");
        True(atUnlock.Contains(36920u), "the replacement should be selected at the trait unlock");
        Equal(
            "pld-sentinel",
            catalog.Definitions.First(definition => definition.ActionId == 17).ReplacementGroup);
        Equal(
            "pld-sentinel",
            catalog.Definitions.First(definition => definition.ActionId == 36920).ReplacementGroup);
    }

    private sealed class StubGameActionRepository : IGameActionRepository
    {
        public int CachedActionCount => 0;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => null;

        public GameActionEnumerationResult EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
            => GameActionEnumerationResult.Success(Array.Empty<GameActionInfo>());
    }

    private sealed class StubGameActionRuntime : IGameActionRuntime
    {
        public CooldownState ReadCooldown(
            AbilityDefinition ability,
            in PlayerFrameContext playerContext)
            => default;

        public uint GetAdjustedActionId(uint actionId)
            => actionId;

        public uint GetMaxCharges(uint actionId, uint effectiveLevel)
            => 1;
    }
}
