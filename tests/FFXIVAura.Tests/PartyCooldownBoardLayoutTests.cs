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
        ("PartyCooldownBoardLayout hides empty synergy rows only", PartyCooldownBoardLayoutHidesEmptySynergyRowsOnly),
    ];

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

    private static void PartyCooldownBoardLayoutHidesEmptySynergyRowsOnly()
    {
        True(PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Synergy), "synergy board should skip jobs without visible synergy icons");
        True(!PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Defensive), "defensive board should keep party-member rows");
        True(!PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Healing), "healing board should keep party-member rows");
    }
}
