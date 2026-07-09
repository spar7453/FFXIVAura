namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private readonly record struct PartyCooldownBoardRenderMetrics(
        float IconSize,
        float Gap,
        float Padding,
        float LabelGap,
        float JobIconSize,
        float AllianceGroupWidth,
        float AllianceColumnGap,
        float AllianceColumnWidth,
        float LabelWidth,
        int AllianceGroupCount,
        int AllianceColumnCount,
        int IconsPerLine,
        bool UseAllianceColumns,
        int DrawnIconCount);

    private bool EnsurePartyCooldownBoardHeight(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var metrics = GetPartyCooldownBoardRenderMetrics(iconWindow, rows);
        var nextHeight = GetPartyCooldownExpandedBoardHeight(iconWindow.Height, rows, metrics);

        if (nextHeight <= iconWindow.Height + 0.5f)
            return false;

        iconWindow.Height = nextHeight;
        this.SetBugDiagnosticEvent($"partyCooldownHeightExpanded:{iconWindow.Id}:{nextHeight:0}");
        return true;
    }

    private static float GetPartyCooldownAllianceGroupWidth(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float labelGap)
        => rows.Any(row => !string.IsNullOrWhiteSpace(row.Member.AllianceGroup))
            ? Math.Max(16f, ImGui.CalcTextSize("C").X + labelGap)
            : 0f;

    private static PartyCooldownBoardRenderMetrics GetPartyCooldownBoardRenderMetrics(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var iconSize = iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var regularAllianceGroupWidth = GetPartyCooldownAllianceGroupWidth(rows, labelGap);
        var regularLabelWidth = regularAllianceGroupWidth + jobIconSize + labelGap + nameWidth + labelGap;
        var regularIconAreaWidth = Math.Max(0f, iconWindow.Width - padding * 2f - regularLabelWidth);
        var regularIconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(regularIconAreaWidth, iconSize, gap);
        var allianceGroupCount = GetPartyCooldownAllianceGroupCount(rows);
        var allianceColumnGap = Math.Max(8f, gap * 2f);
        var allianceAvailableWidth = Math.Max(0f, iconWindow.Width - padding * 2f);
        var allianceColumnLabelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var useAllianceColumns = PartyCooldownBoardLayout.ShouldUseAllianceColumns(
            allianceGroupCount,
            allianceAvailableWidth,
            iconSize,
            gap,
            allianceColumnLabelWidth,
            allianceColumnGap);
        var allianceColumnCount = useAllianceColumns
            ? Math.Clamp(allianceGroupCount, 2, PartyCooldownBoardLayout.AllianceColumnCount)
            : 0;
        var allianceColumnWidth = useAllianceColumns
            ? PartyCooldownBoardLayout.GetAllianceColumnWidth(allianceAvailableWidth, allianceColumnGap, allianceColumnCount)
            : 0f;
        var allianceColumnIconAreaWidth = Math.Max(0f, allianceColumnWidth - allianceColumnLabelWidth);
        var labelWidth = useAllianceColumns ? allianceColumnLabelWidth : regularLabelWidth;
        var iconsPerLine = useAllianceColumns
            ? PartyCooldownBoardLayout.GetIconLineCapacity(allianceColumnIconAreaWidth, iconSize, gap)
            : regularIconsPerLine;
        var drawnIconCount = 0;
        foreach (var row in rows)
            drawnIconCount += row.Items.Count;

        return new PartyCooldownBoardRenderMetrics(
            iconSize,
            gap,
            padding,
            labelGap,
            jobIconSize,
            useAllianceColumns ? 0f : regularAllianceGroupWidth,
            allianceColumnGap,
            allianceColumnWidth,
            labelWidth,
            allianceGroupCount,
            allianceColumnCount,
            iconsPerLine,
            useAllianceColumns,
            drawnIconCount);
    }

    private static int GetPartyCooldownAllianceGroupCount(IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var count = 0;
        for (var group = 0; group < PartyCooldownBoardLayout.AllianceColumnCount; group++)
        {
            if (ContainsPartyCooldownAllianceGroup(rows, PartyCooldownAllianceGroups.GroupLabel(group)))
                count++;
        }

        return count;
    }

    private static bool ContainsPartyCooldownAllianceGroup(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        string groupLabel)
    {
        if (string.IsNullOrWhiteSpace(groupLabel))
            return false;

        foreach (var row in rows)
        {
            if (string.Equals(row.Member.AllianceGroup, groupLabel, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static float GetPartyCooldownAllianceColumnHeaderHeight(PartyCooldownBoardRenderMetrics metrics)
        => ImGui.GetTextLineHeight() + Math.Max(4f, metrics.Gap);

    private static float GetPartyCooldownExpandedBoardHeight(
        float currentHeight,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        var requiredHeight = GetPartyCooldownBoardContentHeight(rows, metrics);
        return Math.Clamp(Math.Max(currentHeight, requiredHeight), MinOverlayHeight, MaxOverlayHeight);
    }

    private static float GetPartyCooldownBoardContentHeight(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        if (metrics.UseAllianceColumns)
            return GetPartyCooldownAllianceBoardContentHeight(rows, metrics);

        var height = metrics.Padding * 2f;
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                height += metrics.Gap;

            height += PartyCooldownBoardLayout.GetRowContentHeight(
                rows[i].Items.Count,
                metrics.IconSize,
                metrics.Gap,
                metrics.IconsPerLine);
        }

        return rows.Count == 0 ? 0f : height;
    }

    private static float GetPartyCooldownAllianceBoardContentHeight(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        var maxColumnHeight = 0f;
        var headerHeight = GetPartyCooldownAllianceColumnHeaderHeight(metrics);
        for (var group = 0; group < PartyCooldownBoardLayout.AllianceColumnCount; group++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(group);
            if (!ContainsPartyCooldownAllianceGroup(rows, groupLabel))
                continue;

            var columnHeight = headerHeight;
            var rowCount = 0;
            foreach (var row in rows)
            {
                if (!string.Equals(row.Member.AllianceGroup, groupLabel, StringComparison.Ordinal))
                    continue;

                if (rowCount > 0)
                    columnHeight += metrics.Gap;

                columnHeight += PartyCooldownBoardLayout.GetRowContentHeight(
                    row.Items.Count,
                    metrics.IconSize,
                    metrics.Gap,
                    metrics.IconsPerLine);
                rowCount++;
            }

            maxColumnHeight = Math.Max(maxColumnHeight, columnHeight);
        }

        return maxColumnHeight <= 0f ? 0f : metrics.Padding * 2f + maxColumnHeight;
    }
}
