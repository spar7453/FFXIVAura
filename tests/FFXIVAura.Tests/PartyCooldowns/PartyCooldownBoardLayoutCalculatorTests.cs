using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownBoardLayoutCalculatorTests
{
    private static readonly PartyCooldownBoardLayoutEnvironment TallDisplay =
        new(DisplayHeight: 1320f, TextLineHeight: 18f, AllianceGroupLabelWidth: 9f);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownBoardLayoutCalculator preserves ordinary party layout", PreservesOrdinaryPartyLayout),
        ("PartyCooldownBoardLayoutCalculator compacts a full alliance board", CompactsFullAllianceBoard),
        ("PartyCooldownBoardLayoutCalculator reports unavoidable alliance overflow", ReportsUnavoidableAllianceOverflow),
        ("PartyCooldownBoardLayoutCalculator clamps configured visual limits", ClampsConfiguredVisualLimits),
        ("PartyCooldownBoardLayoutCalculator shares runtime and estimated row rules", SharesRuntimeAndEstimatedRowRules),
    ];

    private static void PreservesOrdinaryPartyLayout()
    {
        var binding = IconWindowLayoutBinding.Regular(CreateWindow());
        var rows = CreateRows(memberCount: 8, itemCount: 3);

        var layout = PartyCooldownBoardLayoutCalculator.Calculate(binding, rows, TallDisplay);

        True(!layout.Metrics.UseAllianceGrid, "ordinary parties should keep the linear row layout");
        True(!layout.Metrics.IsCompact, "ordinary parties should not use alliance compaction");
        Near(760f, layout.Size.X);
        Near(42f, layout.Metrics.IconSize);
        Equal(24, layout.Metrics.DrawnIconCount);
    }

    private static void CompactsFullAllianceBoard()
    {
        var binding = IconWindowLayoutBinding.Regular(CreateWindow());
        var rows = CreateRows(
            memberCount: 24,
            itemCount: 9,
            index => PartyCooldownAllianceGroups.GroupLabel(index / 8));

        var layout = PartyCooldownBoardLayoutCalculator.Calculate(binding, rows, TallDisplay);

        True(layout.Metrics.UseAllianceGrid, "alliance groups should use the dedicated board layout");
        True(layout.Metrics.IsCompact, "a full alliance board should compact to the available display height");
        True(!layout.Metrics.HasOverflow, "a tall display should fit the compact alliance board");
        Equal(3, layout.Metrics.AllianceGroupCount);
        Equal(1, layout.Metrics.AllianceMemberColumnCount);
        Equal(216, layout.Metrics.DrawnIconCount);
        True(
            layout.Metrics.IconSize >= PartyCooldownBoardLayout.MinAllianceIconSize
            && layout.Metrics.IconSize < 42f,
            $"alliance icon size should be compact but readable, got {layout.Metrics.IconSize}");
        var displayHeightLimit = PartyCooldownBoardLayout.GetAllianceBoardHeightLimit(
            OverlayLayoutLimits.MaxHeight,
            TallDisplay.DisplayHeight,
            OverlayLayoutLimits.WindowMargin);
        True(
            layout.Size.Y <= displayHeightLimit + 0.5f,
            $"alliance board should fit the display, got {layout.Size.Y}");
    }

    private static void ReportsUnavoidableAllianceOverflow()
    {
        var binding = IconWindowLayoutBinding.Regular(CreateWindow());
        var rows = CreateRows(
            memberCount: 24,
            itemCount: 9,
            index => PartyCooldownAllianceGroups.GroupLabel(index / 8));
        var shortDisplay = TallDisplay with { DisplayHeight = 720f };

        var layout = PartyCooldownBoardLayoutCalculator.Calculate(binding, rows, shortDisplay);

        True(layout.Metrics.IsCompact, "overflowing alliance boards should still use compact metrics");
        True(layout.Metrics.HasOverflow, "the calculator should report content that cannot fit");
        Near(PartyCooldownBoardLayout.MinAllianceIconSize, layout.Metrics.IconSize);
        Near(OverlayLayoutLimits.MaxHeight, layout.Size.Y);
    }

    private static void ClampsConfiguredVisualLimits()
    {
        var window = CreateWindow();
        window.IconSize = 200f;
        window.FontScale = 5f;
        var binding = IconWindowLayoutBinding.Regular(window);

        var layout = PartyCooldownBoardLayoutCalculator.Calculate(
            binding,
            CreateRows(memberCount: 1, itemCount: 1),
            TallDisplay);

        Near(OverlayLayoutLimits.MaxIconSize, layout.Metrics.IconSize);
        Near(OverlayLayoutLimits.MaxFontScale, layout.Metrics.FontScale);
    }

    private static void SharesRuntimeAndEstimatedRowRules()
    {
        var binding = IconWindowLayoutBinding.Regular(CreateWindow());
        var runtimeRows = CreateRows(
            memberCount: 24,
            itemCount: 7,
            index => PartyCooldownAllianceGroups.GroupLabel(index / 8));
        var estimatedRows = runtimeRows
            .Select(row => new PartyCooldownBoardLayoutRow(
                row.Member.AllianceGroup,
                row.Items.Count))
            .ToArray();

        var runtimeLayout = PartyCooldownBoardLayoutCalculator.Calculate(
            binding,
            runtimeRows,
            TallDisplay);
        var estimatedLayout = PartyCooldownBoardLayoutCalculator.Calculate(
            binding,
            estimatedRows,
            TallDisplay);

        Vector(runtimeLayout.Size, estimatedLayout.Size);
        Equal(runtimeLayout.Metrics, estimatedLayout.Metrics);
        Near(runtimeLayout.RequiredHeight, estimatedLayout.RequiredHeight);
    }

    private static IconWindowConfig CreateWindow()
        => new()
        {
            Width = 760f,
            Height = 170f,
            IconSize = 42f,
            Gap = 5f,
            FontScale = 1f,
        };

    private static IReadOnlyList<PartyCooldownMemberRow> CreateRows(
        int memberCount,
        int itemCount,
        Func<int, string>? allianceGroup = null)
    {
        var rows = new List<PartyCooldownMemberRow>(memberCount);
        for (var index = 0; index < memberCount; index++)
        {
            var member = new PartyCooldownMemberSnapshot(
                $"member-{index}",
                (uint)(index + 1),
                (ulong)(index + 1),
                1,
                $"Member {index + 1}",
                $"M{index + 1}",
                "PLD",
                90,
                62119,
                allianceGroup?.Invoke(index) ?? string.Empty);
            rows.Add(new PartyCooldownMemberRow(
                member,
                new PartyCooldownDisplayItem[itemCount]));
        }

        return rows;
    }
}
