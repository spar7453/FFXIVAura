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
        stats.CountNativeTooltipControl();
        stats.CountGrayscaleIcons(1);
        stats.Finish(TimeSpan.FromMilliseconds(12.5));

        True(!stats.Enabled, "stats should remain disabled");
        Equal(0, stats.WindowCount);
        Equal(0, stats.SkillIconCount);
        Equal(0, stats.AuraIconCount);
        Equal(0, stats.CooldownCalculationCount);
        Equal(0, stats.NativeTooltipControlCount);
        Equal(0, stats.GrayscaleIconProcessCount);
        Equal(0, stats.FrameSampleCount);
        Near(0, stats.FrameMilliseconds);
        Near(0, stats.AverageFrameMilliseconds);
        Near(0, stats.MaxFrameMilliseconds);
    }

    private static void CountsEnabledFrame()
    {
        var stats = new PerformanceFrameStats();

        stats.Begin(true);
        stats.CountOverlayWindow(3, 2);
        stats.CountOverlayWindow(1, 0);
        stats.CountCooldownCalculation();
        stats.CountCooldownCalculation();
        stats.CountNativeTooltipControl();
        stats.CountGrayscaleIcons(2);
        stats.Finish(TimeSpan.FromMilliseconds(8.75));

        True(stats.Enabled, "stats should be enabled");
        Equal(2, stats.WindowCount);
        Equal(4, stats.SkillIconCount);
        Equal(2, stats.AuraIconCount);
        Equal(2, stats.CooldownCalculationCount);
        Equal(1, stats.NativeTooltipControlCount);
        Equal(2, stats.GrayscaleIconProcessCount);
        Equal(1, stats.FrameSampleCount);
        Near(8.75, stats.FrameMilliseconds);
        Near(8.75, stats.AverageFrameMilliseconds);
        Near(8.75, stats.MaxFrameMilliseconds);

        stats.Begin(true);
        Equal(0, stats.WindowCount);
        Equal(0, stats.SkillIconCount);
        Equal(0, stats.AuraIconCount);
        Equal(0, stats.CooldownCalculationCount);
        Equal(0, stats.NativeTooltipControlCount);
        Equal(0, stats.GrayscaleIconProcessCount);

        stats.Finish(TimeSpan.FromMilliseconds(11.25));
        Equal(2, stats.FrameSampleCount);
        Near(10, stats.AverageFrameMilliseconds);
        Near(11.25, stats.MaxFrameMilliseconds);

        stats.Begin(false);
        stats.Begin(true);
        stats.Finish(TimeSpan.FromMilliseconds(2));
        Equal(1, stats.FrameSampleCount);
        Near(2, stats.AverageFrameMilliseconds);
        Near(2, stats.MaxFrameMilliseconds);
    }
}
