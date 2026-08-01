using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class ConfigSaveTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PluginConfigClone creates an independent snapshot", CreatesIndependentSnapshot),
        ("ConfigSaveWorker drains queued snapshots on dispose", DrainsQueuedSnapshotsOnDispose),
        ("ConfigSaveWorker persists a final snapshot after draining", PersistsFinalSnapshotAfterDraining),
        ("ConfigSaveWorker retries a transient save failure", RetriesTransientSaveFailure),
        ("ConfigSaveWorker reports failed saves", ReportsFailedSaves),
        ("ConfigSaveCoordinator honors the debounce window", CoordinatorHonorsDebounceWindow),
        ("ConfigSaveCoordinator bounds combat deferral", CoordinatorBoundsCombatDeferral),
        ("ConfigSaveCoordinator flushes pending state on completion", CoordinatorFlushesPendingStateOnCompletion),
        ("ConfigSaveCoordinator retries a failed queued snapshot on completion", CoordinatorRetriesFailedQueuedSnapshotOnCompletion),
    ];

    private static void CreatesIndependentSnapshot()
    {
        var source = new PluginConfig
        {
            ActiveWindowId = "win1",
            PartyCooldownLayoutEditMode = PartyCooldownLayoutEditMode.Alliance,
            IconWindows =
            [
                new IconWindowConfig
                {
                    Id = "win1",
                    Name = "main",
                    AuraSearchShowIndividualIds = true,
                    FourPlayerLayout = new IconWindowLayoutConfig
                    {
                        Position = new Vector2(50, 75),
                        IconSize = 40,
                    },
                    AllianceLayout = new IconWindowLayoutConfig
                    {
                        Position = new Vector2(100, 200),
                        IconSize = 36,
                    },
                    TrackedStatusIds = [10],
                    ExactTrackedStatusIds = [10],
                    ManualTrackingJobs = ["WAR", "PLD"],
                    TrackedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WAR"] = ["rampart"],
                        ["PLD"] = [],
                    },
                },
            ],
        };

        var snapshot = PluginConfigClone.CreateSnapshot(source);
        source.IconWindows[0].Name = "changed";
        source.IconWindows[0].TrackedStatusIds.Add(20);
        source.IconWindows[0].ExactTrackedStatusIds.Clear();
        source.IconWindows[0].TrackedByJob["WAR"].Add("reprisal");
        var sourceFourPlayerLayout = source.IconWindows[0].FourPlayerLayout!;
        sourceFourPlayerLayout.Position = new Vector2(500, 600);
        sourceFourPlayerLayout.IconSize = 28;
        var sourceAllianceLayout = source.IconWindows[0].AllianceLayout!;
        sourceAllianceLayout.Position = new Vector2(300, 400);
        sourceAllianceLayout.IconSize = 24;

        Equal("main", snapshot.IconWindows[0].Name);
        Equal(PartyCooldownLayoutEditMode.Alliance, snapshot.PartyCooldownLayoutEditMode);
        True(snapshot.IconWindows[0].AuraSearchShowIndividualIds, "aura ID-view preference should be copied");
        Sequence([10u], snapshot.IconWindows[0].TrackedStatusIds);
        Sequence([10u], snapshot.IconWindows[0].ExactTrackedStatusIds);
        Sequence(["rampart"], snapshot.IconWindows[0].TrackedByJob["WAR"]);
        Sequence(["WAR", "PLD"], snapshot.IconWindows[0].ManualTrackingJobs);
        True(!snapshot.IconWindows[0].TrackedByJob.ContainsKey("PLD"), "empty tracked data should not survive snapshots");
        var snapshotFourPlayerLayout = snapshot.IconWindows[0].FourPlayerLayout!;
        Vector(new Vector2(50, 75), snapshotFourPlayerLayout.Position);
        Near(40, snapshotFourPlayerLayout.IconSize);
        var snapshotAllianceLayout = snapshot.IconWindows[0].AllianceLayout!;
        Vector(new Vector2(100, 200), snapshotAllianceLayout.Position);
        Near(36, snapshotAllianceLayout.IconSize);
    }

    private static void DrainsQueuedSnapshotsOnDispose()
    {
        var savedNames = new List<string>();
        using (var worker = new ConfigSaveWorker(snapshot => savedNames.Add(snapshot.ActiveWindowId)))
        {
            True(worker.TryEnqueue(new PluginConfig { ActiveWindowId = "first" }), "first snapshot should be queued");
            True(worker.TryEnqueue(new PluginConfig { ActiveWindowId = "second" }), "second snapshot should be queued");
        }

        Sequence(["first", "second"], savedNames);
    }

    private static void ReportsFailedSaves()
    {
        using var worker = new ConfigSaveWorker(_ => throw new InvalidOperationException("save failed"));
        True(worker.TryEnqueue(new PluginConfig()), "snapshot should be queued");
        worker.Dispose();

        Equal(0L, worker.CompletedCount);
        Equal(1L, worker.FailedCount);
        True(worker.TakeLastError() is InvalidOperationException, "save exception should be observable");
    }

    private static void PersistsFinalSnapshotAfterDraining()
    {
        var savedNames = new List<string>();
        using var worker = new ConfigSaveWorker(snapshot => savedNames.Add(snapshot.ActiveWindowId));
        True(worker.TryEnqueue(new PluginConfig { ActiveWindowId = "queued" }), "queued snapshot should be accepted");

        True(
            worker.CompleteAndSaveLatest(new PluginConfig { ActiveWindowId = "final" }),
            "final snapshot should be persisted after the queue drains");

        Sequence(["queued", "final"], savedNames);
    }

    private static void RetriesTransientSaveFailure()
    {
        var attempts = 0;
        using var worker = new ConfigSaveWorker(_ =>
        {
            attempts++;
            if (attempts == 1)
                throw new IOException("temporary failure");
        });
        True(worker.TryEnqueue(new PluginConfig()), "snapshot should be queued");
        worker.Dispose();

        Equal(2, attempts);
        Equal(1L, worker.CompletedCount);
        Equal(0L, worker.FailedCount);
        True(worker.TakeLastError() is null, "recovered save should not report an error");
    }

    private static void CoordinatorHonorsDebounceWindow()
    {
        var savedNames = new List<string>();
        using var coordinator = CreateCoordinator(savedNames, debounceDelay: TimeSpan.FromSeconds(1));
        var nowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var config = new PluginConfig { ActiveWindowId = "debounced" };

        coordinator.Queue(nowUtc);
        True(
            !coordinator.Flush(config, default, nowUtc.AddMilliseconds(999), force: false),
            "save should remain pending during debounce");
        True(coordinator.CreateDiagnostics(nowUtc.AddMilliseconds(999)).Pending, "pending state should be observable");
        True(
            coordinator.Flush(config, default, nowUtc.AddSeconds(1), force: false),
            "save should enqueue after debounce");
        True(coordinator.Complete(config, nowUtc.AddSeconds(1)), "queued save should complete");

        Sequence(["debounced"], savedNames);
    }

    private static void CoordinatorBoundsCombatDeferral()
    {
        var savedNames = new List<string>();
        using var coordinator = CreateCoordinator(
            savedNames,
            debounceDelay: TimeSpan.Zero,
            maxCombatDeferDuration: TimeSpan.FromSeconds(2));
        var nowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var config = new PluginConfig { ActiveWindowId = "combat" };
        var combatContext = CreatePlayerContext(isInCombat: true);

        coordinator.Queue(nowUtc);
        True(
            !coordinator.Flush(config, combatContext, nowUtc, force: false),
            "combat should defer the initial save");
        True(coordinator.CreateDiagnostics(nowUtc).DeferredInCombat, "combat deferral should be observable");
        True(
            coordinator.Flush(config, combatContext, nowUtc.AddSeconds(2), force: false),
            "save should proceed when maximum combat deferral expires");
        True(coordinator.Complete(config, nowUtc.AddSeconds(2)), "combat-deferred save should complete");

        Sequence(["combat"], savedNames);
    }

    private static void CoordinatorFlushesPendingStateOnCompletion()
    {
        var savedNames = new List<string>();
        using var coordinator = CreateCoordinator(savedNames, debounceDelay: TimeSpan.FromMinutes(1));
        var nowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var config = new PluginConfig { ActiveWindowId = "final" };

        coordinator.Queue(nowUtc);
        True(coordinator.Complete(config, nowUtc), "completion should force the pending snapshot");

        Sequence(["final"], savedNames);
    }

    private static void CoordinatorRetriesFailedQueuedSnapshotOnCompletion()
    {
        var attempts = 0;
        var savedNames = new List<string>();
        using var coordinator = new ConfigSaveCoordinator(
            snapshot =>
            {
                attempts++;
                if (attempts <= 2)
                    throw new IOException("temporary queued save failure");

                savedNames.Add(snapshot.ActiveWindowId);
            },
            new PerformanceProfiler(),
            (_, _) => { },
            _ => { },
            new ConfigSaveCoordinatorOptions(
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(10),
                TimeSpan.FromSeconds(30)));
        var nowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var config = new PluginConfig { ActiveWindowId = "recovered-final" };

        True(coordinator.SaveNow(config, nowUtc), "snapshot should be accepted by the worker");
        True(
            coordinator.Complete(config, nowUtc),
            "completion should synchronously retry the latest snapshot after the queued save fails");

        Equal(3, attempts);
        Sequence(["recovered-final"], savedNames);
    }

    private static ConfigSaveCoordinator CreateCoordinator(
        List<string> savedNames,
        TimeSpan debounceDelay,
        TimeSpan? maxCombatDeferDuration = null)
        => new(
            snapshot => savedNames.Add(snapshot.ActiveWindowId),
            new PerformanceProfiler(),
            (_, _) => { },
            _ => { },
            new ConfigSaveCoordinatorOptions(
                debounceDelay,
                TimeSpan.FromSeconds(1),
                maxCombatDeferDuration ?? TimeSpan.FromMinutes(10),
                TimeSpan.FromSeconds(30)));

    private static PlayerFrameContext CreatePlayerContext(bool isInCombat)
        => new(
            IsLoggedIn: true,
            IsPlayerLoaded: true,
            Job: "VPR",
            Level: 100,
            EffectiveLevel: 100,
            IsLevelSynced: false,
            IsInCombat: isInCombat,
            BetweenAreas: false,
            BetweenAreas51: false,
            IsMounted: false,
            HasLocalPlayer: true,
            LocalPlayerEntityId: 1,
            HasTarget: false,
            TargetEntityId: 0,
            HasSoftTarget: false,
            SoftTargetEntityId: 0);
}
