using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraTrackingServiceTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraTrackingService prevents duplicate identity groups", PreventsDuplicateIdentityGroups),
        ("AuraTrackingService preserves positions across mode changes", PreservesPositionsAcrossModeChanges),
        ("AuraTrackingService preserves exact aliases when removing a group", PreservesExactAliasesWhenRemovingGroup),
    ];

    private static void PreventsDuplicateIdentityGroups()
    {
        var (service, _) = CreateService();
        var window = CreateWindow();

        True(service.Track(window, 10, exact: false), "the first identity should be tracked");
        True(!service.Track(window, 11, exact: false), "an alias in the same identity group should not be duplicated");
        Equal(AuraTrackingCoverage.Group, service.GetCoverage(window, 11, exact: false));
    }

    private static void PreservesPositionsAcrossModeChanges()
    {
        var (service, groups) = CreateService();
        var window = CreateWindow();
        service.Track(window, 10, exact: false);
        var scope = OverlayPositionKeys.AuraGroup(window);
        window.AuraPositionsByRole[scope] = new Dictionary<string, Vector2>
        {
            [OverlayPositionKeys.Aura(10)] = new(12, 34),
        };

        True(service.SetMode(window, 11, exact: true), "switching a group to an exact alias should change tracking mode");

        Sequence([11u], window.TrackedStatusIds);
        Sequence([11u], window.ExactTrackedStatusIds);
        True(!window.AuraPositionsByRole[scope].ContainsKey(OverlayPositionKeys.Aura(10)), "the replaced position key should be removed");
        Vector(new Vector2(12, 34), window.AuraPositionsByRole[scope][OverlayPositionKeys.Aura(11)]);
        True(groups.InvalidationCount >= 2, "tracking mutations should invalidate cached groups");
    }

    private static void PreservesExactAliasesWhenRemovingGroup()
    {
        var (service, groups) = CreateService();
        var window = CreateWindow();
        window.TrackedStatusIds.AddRange([10u, 11u]);
        window.ExactTrackedStatusIds.Add(11);
        var scope = OverlayPositionKeys.AuraGroup(window);
        window.AuraPositionsByRole[scope] = new Dictionary<string, Vector2>
        {
            [OverlayPositionKeys.Aura(10)] = new(10, 10),
            [OverlayPositionKeys.Aura(11)] = new(20, 20),
        };
        var group = groups.GetTrackedGroups(window).Single(item => !item.IsExact);

        service.Untrack(window, group);

        Sequence([11u], window.TrackedStatusIds);
        Sequence([11u], window.ExactTrackedStatusIds);
        True(!window.AuraPositionsByRole[scope].ContainsKey(OverlayPositionKeys.Aura(10)), "the removed group position should be deleted");
        True(window.AuraPositionsByRole[scope].ContainsKey(OverlayPositionKeys.Aura(11)), "the exact alias position should be preserved");
    }

    private static (AuraTrackingService Service, FakeTrackedGroupProvider Groups) CreateService()
    {
        var catalog = new FakeAuraCatalog();
        var groups = new FakeTrackedGroupProvider(catalog);
        return (new AuraTrackingService(catalog, groups), groups);
    }

    private static IconWindowConfig CreateWindow()
        => new()
        {
            Id = "win1",
            Role = IconWindowRole.PlayerBuffs,
        };

    private sealed class FakeTrackedGroupProvider : IAuraTrackedGroupProvider
    {
        private readonly IAuraCatalog catalog;

        public FakeTrackedGroupProvider(IAuraCatalog catalog)
        {
            this.catalog = catalog;
        }

        public int InvalidationCount { get; private set; }

        public IReadOnlyList<AuraStatusGroup> GetTrackedGroups(IconWindowConfig iconWindow)
            => AuraStatusGroups.Build(
                iconWindow.TrackedStatusIds,
                iconWindow.ExactTrackedStatusIds,
                this.catalog.GetDefinition,
                this.catalog.GetStatusIdsByGroup);

        public void InvalidateTrackedGroups(IconWindowConfig iconWindow)
            => this.InvalidationCount++;
    }

    private sealed class FakeAuraCatalog : IAuraCatalog
    {
        public int StatusIdentityGeneration => 1;

        public int ActionGrantedGeneration => 0;

        public bool CanCacheSearch(bool activeOnly, string query)
            => true;

        public AuraStatusDefinition GetDefinition(uint statusId)
            => statusId switch
            {
                10 or 11 => new AuraStatusDefinition("Shared", 100, 1),
                _ => new AuraStatusDefinition($"Status {statusId}", statusId, 1),
            };

        public bool EnsureStatusIdentityIndex()
            => true;

        public IReadOnlyList<uint> GetStatusIdsByGroup(AuraStatusGroupKey key)
            => key == this.GetDefinition(10).GroupKey ? [10u, 11u] : Array.Empty<uint>();

        public IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedMatches(string query)
            => Array.Empty<AuraSearchIndexEntry>();

        public IReadOnlyList<AuraSearchResult> GetAllStatusMatches(string query)
            => Array.Empty<AuraSearchResult>();

        public string GetActionGrantedSourceNames(uint statusId, string query)
            => string.Empty;

        public AuraCatalogDiagnostics CreateDiagnostics()
            => default;
    }
}
