using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownMemberOrderingTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownMemberOrdering preserves in-game party order", PreservesInGamePartyOrder),
        ("PartyCooldownMemberOrdering removes only local player during display filtering", RemovesOnlyLocalPlayerDuringDisplayFiltering),
    ];

    private static void PreservesInGamePartyOrder()
    {
        var sorted = PartyCooldownMemberOrdering.PreserveInGameOrder(
            [
                Member(30, "SAM"),
                Member(10, "DNC"),
                Member(40, "WAR"),
                Member(20, "WHM"),
                Member(50, "JOB"),
                Member(60, "PLD"),
            ],
            [40, 20, 10, 30]);

        Sequence([40u, 20u, 10u, 30u, 50u, 60u], sorted.Select(member => member.EntityId).ToList());
    }

    private static void RemovesOnlyLocalPlayerDuringDisplayFiltering()
    {
        var sorted = PartyCooldownMemberOrdering.PreserveInGameOrder(
            [
                Member(10, "GNB"),
                Member(20, "PLD"),
                Member(30, "SGE"),
                Member(40, "WHM"),
                Member(50, "RPR"),
                Member(60, "BLM"),
            ]);
        var displayMembers = PartyCooldownRoster.CreateDisplayMembers(sorted, localEntityId: 30, excludeLocalPlayer: true);

        Sequence([10u, 20u, 40u, 50u, 60u], displayMembers.Select(member => member.EntityId).ToList());
    }

    private static PartyCooldownMemberSnapshot Member(uint entityId, string job)
        => new($"key-{entityId}", entityId, 0, $"member-{entityId}", $"m{entityId}", job, 0, string.Empty);
}
