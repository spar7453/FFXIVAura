using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownStatusResolverTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownStatusResolver merges explicit and action statuses", MergesExplicitAndActionStatuses),
        ("PartyCooldownStatusResolver ignores invalid status ids", IgnoresInvalidStatusIds),
    ];

    private static void MergesExplicitAndActionStatuses()
    {
        var statusIds = PartyCooldownStatusResolver.Resolve([30, 10, 30], 20);

        Sequence([10u, 20u, 30u], statusIds);
    }

    private static void IgnoresInvalidStatusIds()
    {
        var statusIds = PartyCooldownStatusResolver.Resolve([0, 50], 0);

        Sequence([50u], statusIds);
    }
}
