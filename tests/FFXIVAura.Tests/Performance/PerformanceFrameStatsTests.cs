using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceFrameStatsTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PerformanceFrameStats stays idle while disabled", StaysIdleWhileDisabled),
        ("PerformanceFrameStats counts enabled frame", CountsEnabledFrame),
    ];

    private static void StaysIdleWhileDisabled()
    {
        var stats = new PerformanceFrameStats();

        stats.Begin(false);
        stats.CountOverlayWindow(3, 2);
        stats.CountCooldownCalculation();
        stats.CountTooltipRender();
        stats.CountGrayscaleIcons(1);
        stats.CountPartyStatusScan();
        stats.CountPartyStatusCacheHit();
        stats.Finish(TimeSpan.FromMilliseconds(12.5), 1024, 1);

        True(!stats.Enabled, "stats should remain disabled");
        Equal(0, stats.WindowCount);
        Equal(0, stats.SkillIconCount);
        Equal(0, stats.AuraIconCount);
        Equal(0, stats.CooldownCalculationCount);
        Equal(0, stats.TooltipRenderCount);
        Equal(0, stats.GrayscaleIconProcessCount);
        Equal(0, stats.PartyStatusScanCount);
        Equal(0, stats.PartyStatusCacheHitCount);
        Equal(0L, stats.FrameAllocatedBytes);
        Equal(0, stats.FrameSampleCount);
        Near(0, stats.FrameMilliseconds);
        Near(0, stats.AverageFrameMilliseconds);
        Near(0, stats.MaxFrameMilliseconds);
        Equal(DateTime.MinValue, stats.MaxFrameOccurredAtUtc);
    }

    private static void CountsEnabledFrame()
    {
        var stats = new PerformanceFrameStats();

        stats.Begin(true);
        stats.CountOverlayWindow(3, 2);
        stats.CountOverlayWindow(1, 0);
        stats.CountCooldownCalculation();
        stats.CountCooldownCalculation();
        stats.CountTooltipRender();
        stats.CountGrayscaleIcons(2);
        stats.CountPartyStatusScan();
        stats.CountPartyStatusCacheHit();
        stats.Finish(TimeSpan.FromMilliseconds(8.75), 2048, 1);

        True(stats.Enabled, "stats should be enabled");
        Equal(2, stats.WindowCount);
        Equal(4, stats.SkillIconCount);
        Equal(2, stats.AuraIconCount);
        Equal(2, stats.CooldownCalculationCount);
        Equal(1, stats.TooltipRenderCount);
        Equal(2, stats.GrayscaleIconProcessCount);
        Equal(1, stats.PartyStatusScanCount);
        Equal(1, stats.PartyStatusCacheHitCount);
        Equal(2048L, stats.FrameAllocatedBytes);
        Near(2048, stats.AverageFrameAllocatedBytes);
        Equal(2048L, stats.MaxFrameAllocatedBytes);
        Equal(1, stats.Gen0CollectionCount);
        Equal(1, stats.FrameSampleCount);
        Near(8.75, stats.FrameMilliseconds);
        Near(8.75, stats.AverageFrameMilliseconds);
        Near(8.75, stats.MaxFrameMilliseconds);
        True(stats.MaxFrameOccurredAtUtc > DateTime.MinValue, "max frame timestamp should be captured");

        stats.Begin(true);
        Equal(0, stats.WindowCount);
        Equal(0, stats.SkillIconCount);
        Equal(0, stats.AuraIconCount);
        Equal(0, stats.CooldownCalculationCount);
        Equal(0, stats.TooltipRenderCount);
        Equal(0, stats.GrayscaleIconProcessCount);

        stats.Finish(TimeSpan.FromMilliseconds(11.25), 4096, 0);
        Equal(2, stats.FrameSampleCount);
        Near(10, stats.AverageFrameMilliseconds);
        Near(11.25, stats.MaxFrameMilliseconds);
        Near(3072, stats.AverageFrameAllocatedBytes);
        Equal(4096L, stats.MaxFrameAllocatedBytes);
        True(stats.MaxFrameOccurredAtUtc > DateTime.MinValue, "max frame timestamp should remain captured");

        stats.Begin(false);
        stats.Begin(true);
        stats.Finish(TimeSpan.FromMilliseconds(2), 512, 0);
        Equal(1, stats.FrameSampleCount);
        Near(2, stats.AverageFrameMilliseconds);
        Near(2, stats.MaxFrameMilliseconds);
        Near(512, stats.AverageFrameAllocatedBytes);
        True(stats.MaxFrameOccurredAtUtc > DateTime.MinValue, "max frame timestamp should be reset and captured again");
    }
}
