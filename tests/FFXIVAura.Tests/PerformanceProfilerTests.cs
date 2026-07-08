using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceProfilerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PerformanceProfiler ignores records while disabled", IgnoresRecordsWhileDisabled),
        ("PerformanceProfiler tracks section averages and max", TracksSectionAveragesAndMax),
        ("PerformanceProfiler tracks window averages and max", TracksWindowAveragesAndMax),
        ("PerformanceProfiler prunes stale windows", PrunesStaleWindows),
        ("PerformanceProfiler caps recent samples", CapsRecentSamples),
        ("PerformanceProfiler prunes recent samples without new calls", PrunesRecentSamplesWithoutNewCalls),
        ("PerformanceProfiler resets when toggled", ResetsWhenToggled),
    ];

    private static void IgnoresRecordsWhileDisabled()
    {
        var profiler = new PerformanceProfiler();

        profiler.BeginFrame(enabled: false);
        profiler.Record(PerformanceProfileSection.Cooldown, TimeSpan.FromMilliseconds(1));
        profiler.FinishFrame();

        var snapshot = profiler.GetSnapshot(PerformanceProfileSection.Cooldown);
        Equal(0, snapshot.SampleFrameCount);
        Equal(0L, snapshot.TotalCallCount);
        Near(0, snapshot.LastMilliseconds);
    }

    private static void TracksSectionAveragesAndMax()
    {
        var profiler = new PerformanceProfiler();

        profiler.BeginFrame(enabled: true);
        profiler.Record(PerformanceProfileSection.Cooldown, TimeSpan.FromMilliseconds(1));
        profiler.Record(PerformanceProfileSection.Cooldown, TimeSpan.FromMilliseconds(2));
        profiler.FinishFrame();

        var snapshot = profiler.GetSnapshot(PerformanceProfileSection.Cooldown);
        Equal(1, snapshot.SampleFrameCount);
        Equal(2, snapshot.LastCallCount);
        Equal(2L, snapshot.TotalCallCount);
        Near(3, snapshot.LastMilliseconds);
        Near(3, snapshot.AverageMilliseconds);
        Near(3, snapshot.RecentAverageMilliseconds);
        Near(3, snapshot.MaxMilliseconds);
        True(snapshot.MaxOccurredAtUtc > DateTime.MinValue, "max timestamp should be captured");

        profiler.BeginFrame(enabled: true);
        profiler.Record(PerformanceProfileSection.Cooldown, TimeSpan.FromMilliseconds(1));
        profiler.FinishFrame();

        snapshot = profiler.GetSnapshot(PerformanceProfileSection.Cooldown);
        Equal(2, snapshot.SampleFrameCount);
        Equal(1, snapshot.LastCallCount);
        Equal(3L, snapshot.TotalCallCount);
        Near(1, snapshot.LastMilliseconds);
        Near(2, snapshot.AverageMilliseconds);
        Near(2, snapshot.RecentAverageMilliseconds);
        Near(3, snapshot.MaxMilliseconds);
    }

    private static void TracksWindowAveragesAndMax()
    {
        var profiler = new PerformanceProfiler();

        profiler.BeginFrame(enabled: true);
        profiler.RecordWindow("win1", "Main", TimeSpan.FromMilliseconds(2));
        profiler.FinishFrame();

        profiler.BeginFrame(enabled: true);
        profiler.RecordWindow("win1", "Main", TimeSpan.FromMilliseconds(4));
        profiler.FinishFrame();

        var snapshot = profiler.GetWindowSnapshots().Single();
        Equal("win1", snapshot.Id);
        Equal("Main", snapshot.Label);
        Equal(2, snapshot.SampleFrameCount);
        Equal(2L, snapshot.TotalCallCount);
        Near(4, snapshot.LastMilliseconds);
        Near(3, snapshot.AverageMilliseconds);
        Near(3, snapshot.RecentAverageMilliseconds);
        Near(4, snapshot.MaxMilliseconds);
        True(snapshot.MaxOccurredAtUtc > DateTime.MinValue, "window max timestamp should be captured");
    }

    private static void PrunesStaleWindows()
    {
        var profiler = new PerformanceProfiler();

        profiler.BeginFrame(enabled: true);
        profiler.RecordWindow("win1", "Main", TimeSpan.FromMilliseconds(2));
        profiler.FinishFrame();
        Equal(1, profiler.GetWindowSnapshots().Count());

        profiler.BeginFrame(enabled: true);
        profiler.FinishFrame();
        Equal(0, profiler.GetWindowSnapshots().Count());
    }

    private static void CapsRecentSamples()
    {
        var profiler = new PerformanceProfiler();

        for (var index = 1; index <= 3000; index++)
        {
            profiler.BeginFrame(enabled: true);
            profiler.Record(PerformanceProfileSection.Overlay, TimeSpan.FromMilliseconds(index));
            profiler.FinishFrame();
        }

        var snapshot = profiler.GetSnapshot(PerformanceProfileSection.Overlay);
        Near(1800.5, snapshot.RecentAverageMilliseconds);
    }

    private static void PrunesRecentSamplesWithoutNewCalls()
    {
        var profiler = new PerformanceProfiler();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        profiler.BeginFrame(enabled: true);
        profiler.Record(PerformanceProfileSection.TooltipControl, TimeSpan.FromMilliseconds(5));
        profiler.FinishFrame(now);
        Near(5, profiler.GetSnapshot(PerformanceProfileSection.TooltipControl).RecentAverageMilliseconds);

        profiler.BeginFrame(enabled: true);
        profiler.FinishFrame(now.AddSeconds(6));

        var snapshot = profiler.GetSnapshot(PerformanceProfileSection.TooltipControl);
        Equal(0, snapshot.LastCallCount);
        Near(0, snapshot.LastMilliseconds);
        Near(0, snapshot.RecentAverageMilliseconds);
    }

    private static void ResetsWhenToggled()
    {
        var profiler = new PerformanceProfiler();

        profiler.BeginFrame(enabled: true);
        profiler.Record(PerformanceProfileSection.Overlay, TimeSpan.FromMilliseconds(4));
        profiler.FinishFrame();
        True(profiler.GetSnapshot(PerformanceProfileSection.Overlay).SampleFrameCount > 0, "profile should have samples before toggle");

        profiler.BeginFrame(enabled: false);
        profiler.BeginFrame(enabled: true);

        var snapshot = profiler.GetSnapshot(PerformanceProfileSection.Overlay);
        Equal(0, snapshot.SampleFrameCount);
        Equal(0L, snapshot.TotalCallCount);
        Near(0, snapshot.MaxMilliseconds);
    }
}
