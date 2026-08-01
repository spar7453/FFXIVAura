using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class GameActionRepositoryTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("GameActionRepository permanently caches confirmed missing rows", CachesConfirmedMissingRows),
        ("GameActionRepository retries transient action failures after backoff", RetriesTransientActionFailures),
        ("GameActionRepository retries transient enumeration failures after backoff", RetriesTransientEnumerationFailures),
    ];

    private static void CachesConfirmedMissingRows()
    {
        var source = new StubGameActionDataSource();
        var repository = new DalamudGameActionRepository(source, static (_, _) => { });

        True(repository.GetAction(999_999) is null, "a missing action should return null");
        True(repository.GetAction(999_999) is null, "a cached missing action should remain null");

        Equal(1, source.ActionReadCount);
        Equal(1, repository.MissingActionCount);
        Equal(0, repository.CachedActionCount);
    }

    private static void RetriesTransientActionFailures()
    {
        var timeProvider = new ManualTimeProvider();
        var source = new StubGameActionDataSource { FailFirstActionRead = true };
        var loggedFailures = 0;
        var repository = new DalamudGameActionRepository(
            source,
            (_, _) => loggedFailures++,
            timeProvider);

        True(repository.GetAction(42) is null, "a transient source failure should return null");
        True(repository.GetAction(42) is null, "the retry backoff should suppress an immediate second read");
        Equal(1, source.ActionReadCount);
        Equal(0, repository.MissingActionCount);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var action = repository.GetAction(42);

        True(action is not null, "the action should load after the retry delay");
        Equal(42u, action!.Value.RowId);
        Equal(2, source.ActionReadCount);
        Equal(1, repository.CachedActionCount);
        Equal(1, loggedFailures);
    }

    private static void RetriesTransientEnumerationFailures()
    {
        var timeProvider = new ManualTimeProvider();
        var source = new StubGameActionDataSource { FailFirstEnumeration = true };
        var repository = new DalamudGameActionRepository(source, static (_, _) => { }, timeProvider);
        IReadOnlySet<uint> classJobs = new HashSet<uint> { 19 };

        True(
            !repository.EnumerateActionsForClassJobs(classJobs).Succeeded,
            "a transient enumeration failure should be reported as incomplete");
        True(
            !repository.EnumerateActionsForClassJobs(classJobs).Succeeded,
            "the retry backoff should suppress an immediate second enumeration");
        Equal(1, source.EnumerationCount);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var retried = repository.EnumerateActionsForClassJobs(classJobs);

        True(retried.Succeeded, "enumeration should recover after the retry delay");
        Equal(1, retried.Actions.Count);
        Equal(2, source.EnumerationCount);
    }

    private sealed class StubGameActionDataSource : IGameActionDataSource
    {
        public bool FailFirstActionRead { get; init; }

        public bool FailFirstEnumeration { get; init; }

        public int ActionReadCount { get; private set; }

        public int EnumerationCount { get; private set; }

        public bool TryGetAction(uint actionId, out GameActionInfo action)
        {
            this.ActionReadCount++;
            if (this.FailFirstActionRead && this.ActionReadCount == 1)
                throw new InvalidOperationException("Action sheet is temporarily unavailable.");

            if (actionId != 42)
            {
                action = default;
                return false;
            }

            action = CreateAction(actionId);
            return true;
        }

        public IReadOnlyList<GameActionInfo> EnumerateActionsForClassJobs(
            IReadOnlySet<uint> classJobIds)
        {
            this.EnumerationCount++;
            if (this.FailFirstEnumeration && this.EnumerationCount == 1)
                throw new InvalidOperationException("Action sheet is temporarily unavailable.");

            return [CreateAction(42)];
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow()
            => this.utcNow;

        public void Advance(TimeSpan duration)
            => this.utcNow += duration;
    }

    private static GameActionInfo CreateAction(uint actionId)
        => new(
            RowId: actionId,
            Name: $"Action {actionId}",
            Icon: 1,
            ActionCategoryId: 4,
            ActionCategoryName: "Ability",
            ClassJobLevel: 1,
            Cast100ms: 0,
            Recast100ms: 25,
            Range: 0,
            EffectRange: 0,
            MaxCharges: 1,
            IsRoleAction: false,
            EquivalenceGroup: 0,
            StatusGainSelfId: 0,
            CanTargetHostile: false,
            CanTargetSelf: true,
            CanTargetParty: false,
            CanTargetAlly: false);
}
