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
        ("PartyCooldownBoardLayout enables alliance columns only when wide enough", PartyCooldownBoardLayoutEnablesAllianceColumnsOnlyWhenWideEnough),
        ("PartyCooldownBoardLayout builds member-scoped icon interaction ids", PartyCooldownBoardLayoutBuildsMemberScopedIconInteractionIds),
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

    private static void PartyCooldownBoardLayoutHidesEmptyOptionalPartyRows()
    {
        True(PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Synergy), "synergy board should skip jobs without visible synergy icons");
        True(PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Healing), "healing board should skip jobs without visible healing icons");
        True(!PartyCooldownBoardLayout.HideEmptyRows(PartyCooldownCategory.Defensive), "defensive board should keep party-member rows");
    }

    private static void PartyCooldownBoardLayoutEnablesAllianceColumnsOnlyWhenWideEnough()
    {
        True(
            PartyCooldownBoardLayout.ShouldUseAllianceColumns(
                allianceGroupCount: 3,
                availableWidth: 900,
                iconSize: 40,
                gap: 3,
                labelWidth: 82,
                columnGap: 8),
            "wide alliance boards should use A/B/C columns");
        True(
            !PartyCooldownBoardLayout.ShouldUseAllianceColumns(
                allianceGroupCount: 3,
                availableWidth: 500,
                iconSize: 40,
                gap: 3,
                labelWidth: 82,
                columnGap: 8),
            "narrow alliance boards should keep the linear fallback");
        Near(294.67f, PartyCooldownBoardLayout.GetAllianceColumnWidth(900, 8, 3), tolerance: 0.01f);
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
