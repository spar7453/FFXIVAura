using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class StatusSourceOwnershipTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("StatusSourceOwnership accepts player and owned summon sources", AcceptsPlayerAndOwnedSummonSources),
        ("StatusSourceOwnership rejects unrelated sources", RejectsUnrelatedSources),
    ];

    private static void AcceptsPlayerAndOwnedSummonSources()
    {
        True(StatusSourceOwnership.IsFromPlayer(10, 10, _ => 0), "direct player source should match");
        True(StatusSourceOwnership.IsFromPlayer(20, 10, source => source == 20 ? 10u : 0u), "owned summon source should match");
    }

    private static void RejectsUnrelatedSources()
    {
        True(!StatusSourceOwnership.IsFromPlayer(20, 10, _ => 30), "another player's source should not match");
        True(!StatusSourceOwnership.IsFromPlayer(0, 10, _ => 10), "invalid source should not match");
    }
}
