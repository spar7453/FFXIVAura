using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AbilityTrackingServiceTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AbilityTrackingService tracks once and removes saved positions", TracksOnceAndRemovesSavedPositions),
        ("AbilityTrackingService converts overlay removal into an exclusion", ConvertsOverlayRemovalIntoExclusion),
        ("AbilityTrackingService moves and swaps tracked ids", MovesAndSwapsTrackedIds),
        ("AbilityTrackingService resets a job to catalog defaults", ResetsJobToCatalogDefaults),
        ("AbilityTrackingService preserves state when defaults are incomplete", PreservesStateWhenDefaultsAreIncomplete),
    ];

    private static void TracksOnceAndRemovesSavedPositions()
    {
        var (catalog, service) = CreateService();
        var ability = catalog.Definitions[0];
        var window = new IconWindowConfig();

        True(service.Track(window, ability.Job, ability.Id), "the first track should add the ability");
        True(!service.Track(window, ability.Job, ability.Id.ToUpperInvariant()), "equivalent ids should not be added twice");
        window.IconPositionsByJob[ability.Job] = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
        {
            [ability.Id] = new(10, 20),
        };

        service.Untrack(window, ability.Job, ability.Id);

        Equal(0, window.TrackedByJob[ability.Job].Count);
        True(
            AbilityTrackingService.IsManualTracking(window, ability.Job),
            "an empty tracked list should remain in manual mode");
        Equal(0, window.IconPositionsByJob[ability.Job].Count);

        service.Clear(window, ability.Job);
        True(
            !AbilityTrackingService.IsManualTracking(window, ability.Job),
            "clearing tracking should explicitly return the job to automatic mode");
    }

    private static void ConvertsOverlayRemovalIntoExclusion()
    {
        var (catalog, service) = CreateService();
        var ability = catalog.Definitions[0];
        var window = new IconWindowConfig();

        service.UntrackFromOverlay(window, ability.Job, ability.Id);

        Sequence([ability.Id], window.ExcludedByJob[ability.Job]);
        service.Include(window, ability.Job, ability.Id.ToUpperInvariant());
        True(!window.ExcludedByJob.ContainsKey(ability.Job), "including the last excluded ability should remove the empty job entry");
    }

    private static void MovesAndSwapsTrackedIds()
    {
        var (_, service) = CreateService();
        var tracked = new List<string> { "first", "second", "third" };

        True(service.Move(tracked, "DRG", "first", 3), "moving an existing id should succeed");
        Sequence(["second", "third", "first"], tracked);
        True(service.Swap(tracked, "DRG", "first", "second"), "swapping existing ids should succeed");
        Sequence(["first", "third", "second"], tracked);
    }

    private static void ResetsJobToCatalogDefaults()
    {
        var (catalog, service) = CreateService();
        const string job = "DRG";
        var window = new IconWindowConfig();
        window.ExcludedByJob[job] = ["legacy"];
        window.IconPositionsByJob[job] = new Dictionary<string, Vector2> { ["legacy"] = new(1, 2) };

        True(service.ResetToDefault(window, job, 100), "a complete catalog reset should succeed");

        True(window.TrackedByJob[job].Count > 0, "catalog defaults should populate tracked ids");
        True(!window.ExcludedByJob.ContainsKey(job), "reset should clear exclusions for the job");
        True(!window.IconPositionsByJob.ContainsKey(job), "reset should clear saved positions for the job");
    }

    private static void PreservesStateWhenDefaultsAreIncomplete()
    {
        const string job = "DRG";
        var catalog = AbilityCatalogTestFactory.Create(new FailingEnumerationRepository());
        catalog.Load();
        var service = new AbilityTrackingService(catalog);
        var window = new IconWindowConfig();
        window.TrackedByJob[job] = ["keep"];
        window.ExcludedByJob[job] = ["legacy"];
        window.IconPositionsByJob[job] = new Dictionary<string, Vector2>
        {
            ["keep"] = new(1, 2),
        };

        True(
            !service.ResetToDefault(window, job, 100),
            "an incomplete candidate snapshot should defer the reset");

        Sequence(["keep"], window.TrackedByJob[job]);
        Sequence(["legacy"], window.ExcludedByJob[job]);
        Equal(new Vector2(1, 2), window.IconPositionsByJob[job]["keep"]);
        True(
            !AbilityTrackingService.IsManualTracking(window, job),
            "a deferred reset should not change the tracking mode");
    }

    private static (AbilityCatalog Catalog, AbilityTrackingService Service) CreateService()
    {
        var catalog = AbilityCatalogTestFactory.Create();
        catalog.Load();
        return (catalog, new AbilityTrackingService(catalog));
    }

    private sealed class FailingEnumerationRepository : IGameActionRepository
    {
        public int CachedActionCount => 0;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => null;

        public GameActionEnumerationResult EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
            => GameActionEnumerationResult.Failure;
    }
}
