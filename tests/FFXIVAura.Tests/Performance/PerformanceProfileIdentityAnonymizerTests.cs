using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceProfileIdentityAnonymizerTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Performance profile anonymizer creates stable actor aliases", CreatesStableActorAliases),
        ("Performance profile anonymizer redacts diagnostic details", RedactsDiagnosticDetails),
    ];

    private static void CreatesStableActorAliases()
    {
        var anonymizer = new PerformanceProfileIdentityAnonymizer();

        var first = anonymizer.GetAlias("Alice Example", 10);
        var same = anonymizer.GetAlias(" alice example ", 10);
        var otherWorld = anonymizer.GetAlias("Alice Example", 20);
        var otherActor = anonymizer.GetAlias("Bob Example", 10);

        Equal("actor-001", first);
        Equal(first, same);
        True(first != otherWorld, "the same name on another world should receive another alias");
        True(first != otherActor, "another actor should receive another alias");
        Equal("-", anonymizer.GetAlias("-", 0));
        Equal(3, anonymizer.Count);

        anonymizer.Reset();
        Equal(0, anonymizer.Count);
        Equal("actor-001", anonymizer.GetAlias("Alice Example", 10));
    }

    private static void RedactsDiagnosticDetails()
    {
        Equal(string.Empty, PerformanceProfileIdentityAnonymizer.RedactDetail(string.Empty));
        Equal("[redacted]", PerformanceProfileIdentityAnonymizer.RedactDetail("Alice Example used an action"));
    }
}
