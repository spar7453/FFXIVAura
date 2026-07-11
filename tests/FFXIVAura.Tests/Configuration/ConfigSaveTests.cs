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
    ];

    private static void CreatesIndependentSnapshot()
    {
        var source = new PluginConfig
        {
            ActiveWindowId = "win1",
            IconWindows =
            [
                new IconWindowConfig
                {
                    Id = "win1",
                    Name = "main",
                    AuraSearchShowIndividualIds = true,
                    AllianceLayout = new IconWindowLayoutConfig
                    {
                        Position = new Vector2(100, 200),
                        IconSize = 36,
                    },
                    TrackedStatusIds = [10],
                    ExactTrackedStatusIds = [10],
                    TrackedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WAR"] = ["rampart"],
                    },
                },
            ],
        };

        var snapshot = PluginConfigClone.CreateSnapshot(source);
        source.IconWindows[0].Name = "changed";
        source.IconWindows[0].TrackedStatusIds.Add(20);
        source.IconWindows[0].ExactTrackedStatusIds.Clear();
        source.IconWindows[0].TrackedByJob["WAR"].Add("reprisal");
        var sourceAllianceLayout = source.IconWindows[0].AllianceLayout!;
        sourceAllianceLayout.Position = new Vector2(300, 400);
        sourceAllianceLayout.IconSize = 24;

        Equal("main", snapshot.IconWindows[0].Name);
        True(snapshot.IconWindows[0].AuraSearchShowIndividualIds, "aura ID-view preference should be copied");
        Sequence([10u], snapshot.IconWindows[0].TrackedStatusIds);
        Sequence([10u], snapshot.IconWindows[0].ExactTrackedStatusIds);
        Sequence(["rampart"], snapshot.IconWindows[0].TrackedByJob["WAR"]);
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
}
