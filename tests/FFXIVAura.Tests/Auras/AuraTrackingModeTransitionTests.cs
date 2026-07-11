using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraTrackingModeTransitionTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Aura tracking mode converts exact aliases into one group", ConvertsExactAliasesIntoGroup),
        ("Aura tracking mode converts a group to the selected exact ID", ConvertsGroupToSelectedExactId),
        ("Aura tracking mode preserves unrelated tracked order", PreservesUnrelatedOrder),
    ];

    private static void ConvertsExactAliasesIntoGroup()
    {
        var tracked = new List<uint> { 10, 82, 1302, 20 };
        var exact = new List<uint> { 82, 1302 };

        var change = AuraTrackingModeTransitions.Apply(
            tracked,
            exact,
            82,
            exact: false,
            id => id is 82 or 1302);

        True(change.Changed, "separate exact aliases should be converted");
        Sequence([10u, 82u, 20u], tracked);
        Equal(0, exact.Count);
        Sequence([82u, 1302u], change.ReplacedStatusIds);
    }

    private static void ConvertsGroupToSelectedExactId()
    {
        var tracked = new List<uint> { 10, 82, 20 };
        var exact = new List<uint>();

        var change = AuraTrackingModeTransitions.Apply(
            tracked,
            exact,
            1302,
            exact: true,
            id => id is 82 or 1302);

        True(change.Changed, "the group representative should be replaced");
        Sequence([10u, 1302u, 20u], tracked);
        Sequence([1302u], exact);
        Sequence([82u], change.ReplacedStatusIds);
    }

    private static void PreservesUnrelatedOrder()
    {
        var tracked = new List<uint> { 1, 2, 3 };
        var exact = new List<uint> { 2 };

        var change = AuraTrackingModeTransitions.Apply(
            tracked,
            exact,
            2,
            exact: true,
            id => id == 2);

        True(!change.Changed, "an already exact status should not be rewritten");
        Sequence([1u, 2u, 3u], tracked);
        Sequence([2u], exact);
    }
}
