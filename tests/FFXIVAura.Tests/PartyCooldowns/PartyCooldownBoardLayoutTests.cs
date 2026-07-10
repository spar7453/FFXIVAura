using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownBoardLayoutTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownBoardLayout wraps nine icons into two lines", PartyCooldownBoardLayoutWrapsNineIconsIntoTwoLines),
        ("PartyCooldownBoardLayout falls back when the board is narrow", PartyCooldownBoardLayoutFallsBackWhenTheBoardIsNarrow),
        ("PartyCooldownBoardLayout aligns partial wrapped rows", PartyCooldownBoardLayoutAlignsPartialWrappedRows),
        ("PartyCooldownBoardLayout computes required board height", PartyCooldownBoardLayoutComputesRequiredBoardHeight),
        ("PartyCooldownBoardLayout expands short boards without shrinking", PartyCooldownBoardLayoutExpandsShortBoardsWithoutShrinking),
        ("PartyCooldownBoardLayout hides empty optional party rows", PartyCooldownBoardLayoutHidesEmptyOptionalPartyRows),
        ("PartyCooldownBoardLayout enables dedicated alliance grid", PartyCooldownBoardLayoutEnablesDedicatedAllianceGrid),
        ("PartyCooldownBoardLayout computes alliance grid board height", PartyCooldownBoardLayoutComputesAllianceGridBoardHeight),
        ("PartyCooldownBoardLayout computes four-member alliance grid width", PartyCooldownBoardLayoutComputesAllianceGridWidth),
        ("PartyCooldownBoardLayout fits a full alliance board at minimum icon size", PartyCooldownBoardLayoutFitsFullAllianceAtMinimumIconSize),
        ("PartyCooldownBoardLayout builds member-scoped icon interaction ids", PartyCooldownBoardLayoutBuildsMemberScopedIconInteractionIds),
    ];

    private static void PartyCooldownBoardLayoutComputesAllianceGridWidth()
    {
        var width = PartyCooldownBoardLayout.GetRequiredAllianceGridWidth(
            labelWidth: 79f,
            iconSize: 40f,
            gap: 3f,
            cellGap: 8f,
            padding: 4f);

        Near(1196f, width);
        Near(291f, PartyCooldownBoardLayout.GetAllianceMemberCellWidth(width - 8f, 8f));
    }

    private static void PartyCooldownBoardLayoutFitsFullAllianceAtMinimumIconSize()
    {
        var rows = new List<(string AllianceGroup, int ItemCount)>();
        foreach (var group in new[] { "A", "B", "C" })
        {
            for (var member = 0; member < 8; member++)
                rows.Add((group, 9));
        }

        var height = PartyCooldownBoardLayout.GetAllianceGridBoardContentHeight(
            rows,
            iconSize: 24,
            gap: 16,
            padding: 16,
            headerHeight: 42,
            iconsPerLine: 5);

        True(height <= 900, $"minimum-size alliance board should fit the overlay height limit, got {height}");
    }

    private static void PartyCooldownBoardLayoutWrapsNineIconsIntoTwoLines()
    {
        Equal(2, PartyCooldownBoardLayout.GetLineCount(9));
        Equal(5, PartyCooldownBoardLayout.GetLineItemCount(9, 0));
        Equal(4, PartyCooldownBoardLayout.GetLineItemCount(9, 1));
        Near(42 * 2 + 5, PartyCooldownBoardLayout.GetIconContentHeight(9, 42, 5));
    }

    private static void PartyCooldownBoardLayoutFallsBackWhenTheBoardIsNarrow()
    {
        Equal(3, PartyCooldownBoardLayout.GetIconLineCapacity(34, 10, 2));
        Equal(3, PartyCooldownBoardLayout.GetLineCount(9, 3));
        Equal(3, PartyCooldownBoardLayout.GetLineItemCount(9, 2, 3));
    }

    private static void PartyCooldownBoardLayoutAlignsPartialWrappedRows()
    {
        Near(0, PartyCooldownBoardLayout.GetLineStartOffset(IconAlignment.Left, 4, 10, 2));
        Near(6, PartyCooldownBoardLayout.GetLineStartOffset(IconAlignment.Center, 4, 10, 2));
        Near(12, PartyCooldownBoardLayout.GetLineStartOffset(IconAlignment.Right, 4, 10, 2));
    }

    private static void PartyCooldownBoardLayoutComputesRequiredBoardHeight()
    {
        Near(74, PartyCooldownBoardLayout.GetBoardContentHeight([9, 2], 20, 3, 4, 5));
    }

    private static void PartyCooldownBoardLayoutExpandsShortBoardsWithoutShrinking()
    {
        Near(74, PartyCooldownBoardLayout.GetExpandedBoardHeight(40, [9, 2], 20, 3, 4, 5, 40, 900));
        Near(120, PartyCooldownBoardLayout.GetExpandedBoardHeight(120, [9, 2], 20, 3, 4, 5, 40, 900));
        Near(60, PartyCooldownBoardLayout.GetExpandedBoardHeight(40, [9, 2], 20, 3, 4, 5, 40, 60));
    }

    private static void PartyCooldownBoardLayoutHidesEmptyOptionalPartyRows()
    {
        True(PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Synergy), "synergy board should skip jobs without visible synergy icons");
        True(PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Healing), "healing board should skip jobs without visible healing icons");
        True(!PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Defensive), "defensive board should keep party-member rows");
    }

    private static void PartyCooldownBoardLayoutEnablesDedicatedAllianceGrid()
    {
        True(PartyCooldownBoardLayout.ShouldUseAllianceGrid(3), "24-player alliance boards should always use the dedicated grid");
        True(PartyCooldownBoardLayout.ShouldUseAllianceGrid(2), "partially loaded alliance boards should keep the dedicated grid");
        True(!PartyCooldownBoardLayout.ShouldUseAllianceGrid(1), "ordinary parties should keep linear rows");
    }

    private static void PartyCooldownBoardLayoutComputesAllianceGridBoardHeight()
    {
        var rows = new (string AllianceGroup, int ItemCount)[]
        {
            ("A", 9),
            ("A", 2),
            ("B", 1),
            ("C", 5),
            ("C", 5),
            ("C", 5),
        };

        Near(137, PartyCooldownBoardLayout.GetAllianceGridBoardContentHeight(rows, 20, 3, 4, 10, 5));
    }

    private static void PartyCooldownBoardLayoutBuildsMemberScopedIconInteractionIds()
    {
        var first = PartyCooldownBoardLayout.BuildIconInteractionId("main", "content-1", "rampart", 7531);
        var second = PartyCooldownBoardLayout.BuildIconInteractionId("main", "content-2", "rampart", 7531);
        var otherWindow = PartyCooldownBoardLayout.BuildIconInteractionId("other", "content-1", "rampart", 7531);

        True(!string.Equals(first, second, StringComparison.Ordinal), "same cooldown on different party members should not share an ImGui id");
        True(!string.Equals(first, otherWindow, StringComparison.Ordinal), "same cooldown in different windows should not share an ImGui id");
    }
}
