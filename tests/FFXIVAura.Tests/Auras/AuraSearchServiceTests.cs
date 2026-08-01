using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraSearchServiceTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraSearchService reuses unchanged window results", ReusesUnchangedWindowResults),
        ("AuraSearchService retains recently observed statuses", RetainsRecentlyObservedStatuses),
        ("AuraSearchService invalidates cached results on world reset", InvalidatesCachedResultsOnWorldReset),
    ];

    private static void ReusesUnchangedWindowResults()
    {
        var frameSource = new StubAuraStatusFrameSource { PlayerStatusIds = [42] };
        var service = new AuraSearchService(new StubAuraCatalog(), frameSource, new PerformanceProfiler());
        var window = CreateWindow(activeOnly: true);

        var first = service.Search(string.Empty, window);
        var second = service.Search(string.Empty, window);
        var diagnostics = service.CreateDiagnostics();

        Equal(1, first.Count);
        Equal(42u, first[0].StatusId);
        True(first[0].IsCurrent, "active status should be marked current");
        True(ReferenceEquals(first, second), "unchanged search should reuse the cached result list");
        Equal(1L, diagnostics.ResultCacheHitCount);
        Equal(1L, diagnostics.ResultCacheMissCount);
    }

    private static void RetainsRecentlyObservedStatuses()
    {
        var frameSource = new StubAuraStatusFrameSource { PlayerStatusIds = [42] };
        var service = new AuraSearchService(new StubAuraCatalog(), frameSource, new PerformanceProfiler());
        var window = CreateWindow(activeOnly: false);

        _ = service.Search(string.Empty, window);
        frameSource.PlayerStatusIds = [];
        var recent = service.Search(string.Empty, window);

        Equal(1, recent.Count);
        Equal(42u, recent[0].StatusId);
        True(!recent[0].IsCurrent, "removed status should no longer be current");
        True(recent[0].WasRecentlySeen, "removed status should remain in recent history");
    }

    private static void InvalidatesCachedResultsOnWorldReset()
    {
        var frameSource = new StubAuraStatusFrameSource { PlayerStatusIds = [42] };
        var service = new AuraSearchService(new StubAuraCatalog(), frameSource, new PerformanceProfiler());
        var window = CreateWindow(activeOnly: true);

        var beforeReset = service.Search(string.Empty, window);
        frameSource.PlayerStatusIds = [];
        service.ResetWorldRuntimeState();
        var afterReset = service.Search(string.Empty, window);

        Equal(1, beforeReset.Count);
        Equal(0, afterReset.Count);
        True(!ReferenceEquals(beforeReset, afterReset), "world reset should discard cached search results");
    }

    private static IconWindowConfig CreateWindow(bool activeOnly)
        => new()
        {
            Id = "aura-window",
            Role = IconWindowRole.PlayerBuffs,
            AuraSearchActiveOnly = activeOnly,
        };

    private sealed class StubAuraCatalog : IAuraCatalog
    {
        public int StatusIdentityGeneration => 1;

        public int ActionGrantedGeneration => 1;

        public bool CanCacheSearch(bool activeOnly, string query)
            => true;

        public AuraStatusDefinition GetDefinition(uint statusId)
            => statusId == 42
                ? new AuraStatusDefinition("Test Aura", 100, 1)
                : new AuraStatusDefinition($"Status {statusId}", 0, 0);

        public bool EnsureStatusIdentityIndex()
            => true;

        public IReadOnlyList<uint> GetStatusIdsByGroup(AuraStatusGroupKey key)
            => key == AuraStatusGroupKey.Create("Test Aura", 100, 1)
                ? [42u]
                : Array.Empty<uint>();

        public IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedMatches(string query)
            => Array.Empty<AuraSearchIndexEntry>();

        public IReadOnlyList<AuraSearchResult> GetAllStatusMatches(string query)
            => Array.Empty<AuraSearchResult>();

        public string GetActionGrantedSourceNames(uint statusId, string query)
            => string.Empty;

        public AuraCatalogDiagnostics CreateDiagnostics()
            => default;
    }

    private sealed class StubAuraStatusFrameSource : IAuraStatusFrameSource
    {
        public uint[] PlayerStatusIds { get; set; } = [];

        public IEnumerable<uint> GetPlayerStatusIds()
            => this.PlayerStatusIds;

        public IEnumerable<uint> GetTargetStatusIds()
            => Array.Empty<uint>();

        public IEnumerable<uint> GetPartyStatusIds(bool ownOnly)
            => Array.Empty<uint>();
    }
}
