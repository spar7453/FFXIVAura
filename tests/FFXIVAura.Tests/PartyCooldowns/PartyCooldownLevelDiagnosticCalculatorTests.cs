using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLevelDiagnosticCalculatorTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Party cooldown level diagnostics expose reported and fallback levels", ExposesReportedAndFallbackLevels),
        ("Party cooldown level diagnostics handle an empty roster", HandlesEmptyRoster),
    ];

    private static void ExposesReportedAndFallbackLevels()
    {
        var members = new[]
        {
            CreateMember("one", 90),
            CreateMember("two", 0),
            CreateMember("three", 15),
        };

        var diagnostics = PartyCooldownLevelDiagnosticCalculator.Create(members, 18);

        Equal(15u, diagnostics.ReportedMinimum);
        Equal(90u, diagnostics.ReportedMaximum);
        Equal(1, diagnostics.MissingCount);
        Equal(15u, diagnostics.ResolvedMinimum);
        Equal(90u, diagnostics.ResolvedMaximum);
        Equal(1, diagnostics.ReportedAboveFallbackCount);
    }

    private static void HandlesEmptyRoster()
    {
        var diagnostics = PartyCooldownLevelDiagnosticCalculator.Create(
            Array.Empty<PartyCooldownMemberSnapshot>(),
            18);

        Equal(0u, diagnostics.ReportedMinimum);
        Equal(0u, diagnostics.ReportedMaximum);
        Equal(0, diagnostics.MissingCount);
        Equal(0u, diagnostics.ResolvedMinimum);
        Equal(0u, diagnostics.ResolvedMaximum);
        Equal(0, diagnostics.ReportedAboveFallbackCount);
    }

    private static PartyCooldownMemberSnapshot CreateMember(string key, uint level)
        => new(
            key,
            0,
            0,
            0,
            key,
            key,
            "PLD",
            level,
            0,
            string.Empty);
}
