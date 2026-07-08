using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownMemberOrderingTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownMemberOrdering puts local player then tank healer dps", PutsLocalPlayerThenTankHealerDps),
        ("PartyCooldownMemberOrdering preserves order inside role groups", PreservesOrderInsideRoleGroups),
    ];

    private static void PutsLocalPlayerThenTankHealerDps()
    {
        var sorted = PartyCooldownMemberOrdering.Sort(
            [
                Member(10, "DNC"),
                Member(20, "WHM"),
                Member(30, "SAM"),
                Member(40, "WAR"),
                Member(50, "JOB"),
                Member(60, "PLD"),
            ],
            localEntityId: 30);

        Sequence([30u, 40u, 60u, 20u, 10u, 50u], sorted.Select(member => member.EntityId).ToList());
    }

    private static void PreservesOrderInsideRoleGroups()
    {
        var sorted = PartyCooldownMemberOrdering.Sort(
            [
                Member(10, "GNB"),
                Member(20, "PLD"),
                Member(30, "SGE"),
                Member(40, "WHM"),
                Member(50, "RPR"),
                Member(60, "BLM"),
            ],
            localEntityId: 0);

        Sequence([10u, 20u, 30u, 40u, 50u, 60u], sorted.Select(member => member.EntityId).ToList());
    }

    private static PartyCooldownMemberSnapshot Member(uint entityId, string job)
        => new($"key-{entityId}", entityId, 0, $"member-{entityId}", $"m{entityId}", job, 0);
}
