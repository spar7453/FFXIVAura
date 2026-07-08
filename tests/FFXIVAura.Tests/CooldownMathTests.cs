using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class CooldownMathTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("CooldownMath converts aggregate charge cooldown", ConvertsAggregateChargeCooldown),
        ("CooldownMath reports no cooldown at max charges", ReportsNoCooldownAtMaxCharges),
    ];

    private static void ConvertsAggregateChargeCooldown()
    {
        var (total, remaining) = CooldownMath.GetTiming(60, 15, 1, 2, 30);
        Near(30, total);
        Near(15, remaining);
    }

    private static void ReportsNoCooldownAtMaxCharges()
    {
        var (total, remaining) = CooldownMath.GetTiming(60, 15, 2, 2, 30);
        Near(30, total);
        Near(0, remaining);
    }
}
