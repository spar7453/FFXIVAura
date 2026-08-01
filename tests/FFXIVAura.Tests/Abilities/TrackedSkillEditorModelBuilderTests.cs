using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class TrackedSkillEditorModelBuilderTests
{
    private const string Job = "PLD";
    private const string AbilityTab = "Ability";

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("TrackedSkillEditorModelBuilder counts and filters tabs", CountsAndFiltersTabs),
        ("TrackedSkillEditorModelBuilder searches candidate identities", SearchesCandidateIdentities),
        ("TrackedSkillEditorModelBuilder prioritizes tracked candidates", PrioritizesTrackedCandidates),
        ("TrackedSkillEditorModelBuilder preserves empty manual tracking", PreservesEmptyManualTracking),
        ("TrackedSkillEditorModelBuilder resolves tracked display order", ResolvesTrackedDisplayOrder),
        ("TrackedSkillEditorSession owns drag state", OwnsDragState),
    ];

    private static void CountsAndFiltersTabs()
    {
        var (catalog, _, builder) = CreateBuilder();
        var window = new IconWindowConfig();

        var model = builder.Build(window, Job, 100, AbilityTab, string.Empty);
        var expectedCount = catalog.GetJobCandidates(Job, uint.MaxValue)
            .Count(ability => ability.ActionCategoryId == 4 && ability.Job != "ROLE");

        True(expectedCount > 1, "test data should expose multiple job abilities");
        Equal(expectedCount, model.TabCounts[AbilityTab]);
        Equal(expectedCount, model.Candidates.Count);
        True(
            model.Candidates.All(ability =>
                ability.ActionCategoryId == 4
                && !string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase)),
            "the ability tab should exclude role actions and other action categories");
        True(
            model.CurrentCandidates.All(ability => ability.Level <= 100),
            "current candidates should honor the effective level");
    }

    private static void SearchesCandidateIdentities()
    {
        var (_, _, builder) = CreateBuilder();
        var window = new IconWindowConfig();
        var unfiltered = builder.Build(window, Job, 100, AbilityTab, string.Empty);
        var target = unfiltered.Candidates[0];

        var byActionId = builder.Build(
            window,
            Job,
            100,
            AbilityTab,
            target.ActionId.ToString());
        var byAbilityId = builder.Build(
            window,
            Job,
            100,
            AbilityTab,
            target.Id.ToUpperInvariant());

        True(
            byActionId.Candidates.Any(ability => ability.ActionId == target.ActionId),
            "action id search should retain the matching candidate");
        True(
            byAbilityId.Candidates.Any(ability =>
                string.Equals(ability.Id, target.Id, StringComparison.OrdinalIgnoreCase)),
            "ability id search should be case-insensitive");
    }

    private static void PrioritizesTrackedCandidates()
    {
        var (_, tracking, builder) = CreateBuilder();
        var window = new IconWindowConfig();
        var candidates = builder.Build(window, Job, 100, AbilityTab, string.Empty).Candidates;
        var trackedId = candidates[1].Id;
        tracking.Track(window, Job, trackedId);

        var model = builder.Build(window, Job, 100, AbilityTab, string.Empty);

        True(model.ManualTracking, "a non-empty tracked list should enable manual mode");
        Equal(trackedId, model.Candidates[0].Id);
    }

    private static void PreservesEmptyManualTracking()
    {
        var (_, _, builder) = CreateBuilder();
        var window = new IconWindowConfig();
        window.ManualTrackingJobs.Add(Job);

        var model = builder.Build(
            window,
            Job,
            100,
            AbilityTab,
            string.Empty);

        True(model.ManualTracking, "an empty tracked entry should remain in manual mode");
    }

    private static void ResolvesTrackedDisplayOrder()
    {
        var (_, _, builder) = CreateBuilder();
        var window = new IconWindowConfig();
        var candidates = builder.Build(window, Job, 100, AbilityTab, string.Empty).Candidates;
        var first = candidates[0];
        var second = candidates.First(ability => ability.ActionId != first.ActionId);

        var resolved = builder.ResolveTrackedOrderAbilities(
            Job,
            candidates,
            [second.Id, first.Id, second.Id]);

        Equal(2, resolved.Count);
        Equal(second.ActionId, resolved[0].ActionId);
        Equal(first.ActionId, resolved[1].ActionId);
    }

    private static void OwnsDragState()
    {
        var session = new TrackedSkillEditorSession();

        True(session.DraggedAbilityId is null, "a new session should be idle");
        session.BeginDrag("rampart");
        True(session.IsDragging("RAMPART"), "drag identity should be case-insensitive");
        Equal("rampart", session.DraggedAbilityId);
        session.EndDrag();
        True(session.DraggedAbilityId is null, "ending a drag should clear the session");

        session.NoteDefaultResetResult(succeeded: false);
        True(session.DefaultResetDeferred, "a failed default reset should expose retry guidance");
        session.NoteDefaultResetResult(succeeded: true);
        True(!session.DefaultResetDeferred, "a successful reset should clear retry guidance");
        session.NoteDefaultResetResult(succeeded: false);
        session.ClearDefaultResetDeferred();
        True(!session.DefaultResetDeferred, "clearing editor state should dismiss retry guidance");
    }

    private static (
        AbilityCatalog Catalog,
        AbilityTrackingService Tracking,
        TrackedSkillEditorModelBuilder Builder) CreateBuilder()
    {
        var catalog = AbilityCatalogTestFactory.Create(new CategorizedGameActionRepository());
        catalog.Load();
        var tracking = new AbilityTrackingService(catalog);
        return (catalog, tracking, new TrackedSkillEditorModelBuilder(catalog, tracking));
    }

    private sealed class CategorizedGameActionRepository : IGameActionRepository
    {
        public int CachedActionCount => 0;

        public int MissingActionCount => 0;

        public GameActionInfo? GetAction(uint actionId)
            => new(
                actionId,
                $"Action {actionId}",
                1,
                4,
                "Ability",
                1,
                0,
                0,
                0,
                0,
                1,
                false,
                0,
                0,
                false,
                false,
                false,
                false);

        public GameActionEnumerationResult EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
            => GameActionEnumerationResult.Success(Array.Empty<GameActionInfo>());
    }
}
