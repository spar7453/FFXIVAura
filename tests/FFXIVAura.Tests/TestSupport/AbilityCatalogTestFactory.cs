using FFXIVAura;

namespace FFXIVAura.Tests;

internal static class AbilityCatalogTestFactory
{
    public static AbilityCatalog Create(IGameActionRepository? gameActionRepository = null)
    {
        var abilityDataPath = TestFiles.FindRepoFile("Data", "abilities.json");
        var dataDirectory = Directory.GetParent(abilityDataPath)!;
        var repositoryRoot = dataDirectory.Parent!.FullName;
        return new AbilityCatalog(
            repositoryRoot,
            gameActionRepository ?? new StubGameActionRepository(),
            new PerformanceProfiler(),
            null!);
    }

    private sealed class StubGameActionRepository : IGameActionRepository
    {
        public int CachedActionCount => 0;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => null;

        public GameActionEnumerationResult EnumerateActionsForClassJobs(IReadOnlySet<uint> classJobIds)
            => GameActionEnumerationResult.Success(Array.Empty<GameActionInfo>());
    }
}
