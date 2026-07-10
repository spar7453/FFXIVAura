namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private readonly record struct PartyCooldownBoardRenderMetrics(
        float IconSize,
        float Gap,
        float Padding,
        float LabelGap,
        float TextLineHeight,
        float JobIconSize,
        float AllianceGroupWidth,
        float AllianceCellGap,
        float AllianceCellWidth,
        float LabelWidth,
        int AllianceGroupCount,
        int AllianceMemberColumnCount,
        int IconsPerLine,
        bool UseAllianceGrid,
        int DrawnIconCount);

    private readonly record struct PartyCooldownBoardDisplayLayout(
        Vector2 Size,
        PartyCooldownBoardRenderMetrics Metrics);

    private static PartyCooldownBoardDisplayLayout GetPartyCooldownBoardDisplayLayout(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var configuredIconSize = Math.Clamp(iconWindow.IconSize, MinIconSize, MaxIconSize);
        var targetIconsPerLine = GetPartyCooldownTargetIconsPerLine(rows);
        var layout = CreatePartyCooldownBoardDisplayLayout(
            iconWindow,
            rows,
            configuredIconSize,
            targetIconsPerLine);
        if (layout.Metrics.AllianceGroupCount < 2
            || PartyCooldownBoardLayoutFits(rows, layout.Metrics, targetIconsPerLine))
        {
            return layout;
        }

        var minimumLayout = CreatePartyCooldownBoardDisplayLayout(
            iconWindow,
            rows,
            MinIconSize,
            targetIconsPerLine);
        if (!PartyCooldownBoardLayoutFits(rows, minimumLayout.Metrics, targetIconsPerLine))
            return minimumLayout;

        var minimum = MinIconSize;
        var maximum = configuredIconSize;
        var best = minimumLayout;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var candidateIconSize = (minimum + maximum) * 0.5f;
            var candidate = CreatePartyCooldownBoardDisplayLayout(
                iconWindow,
                rows,
                candidateIconSize,
                targetIconsPerLine);
            if (PartyCooldownBoardLayoutFits(rows, candidate.Metrics, targetIconsPerLine))
            {
                best = candidate;
                minimum = candidateIconSize;
            }
            else
            {
                maximum = candidateIconSize;
            }
        }

        return best;
    }

    private static PartyCooldownBoardDisplayLayout CreatePartyCooldownBoardDisplayLayout(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float iconSize,
        int targetIconsPerLine)
    {
        var width = GetPartyCooldownBoardDisplayWidth(iconWindow, rows, iconSize, targetIconsPerLine);
        var metrics = GetPartyCooldownBoardRenderMetrics(iconWindow, rows, width, iconSize);
        var requiredHeight = GetPartyCooldownBoardContentHeight(rows, metrics);
        var height = Math.Clamp(
            Math.Max(iconWindow.Height, requiredHeight),
            MinOverlayHeight,
            MaxOverlayHeight);
        return new PartyCooldownBoardDisplayLayout(new Vector2(width, height), metrics);
    }

    private static float GetPartyCooldownBoardDisplayWidth(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float iconSize,
        int targetIconsPerLine)
    {
        var allianceGroupCount = GetPartyCooldownAllianceGroupCount(rows);
        if (allianceGroupCount < 2)
            return iconWindow.Width;

        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var labelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var columnGap = Math.Max(8f, gap * 2f);
        var requiredWidth = PartyCooldownBoardLayout.GetRequiredAllianceGridWidth(
            labelWidth,
            iconSize,
            gap,
            columnGap,
            padding,
            targetIconsPerLine);
        return Math.Clamp(Math.Max(iconWindow.Width, requiredWidth + 1f), MinOverlayWidth, MaxOverlayWidth);
    }

    private static int GetPartyCooldownTargetIconsPerLine(IReadOnlyList<PartyCooldownMemberRow> rows)
    {
        var maximumItemCount = 1;
        foreach (var row in rows)
            maximumItemCount = Math.Max(maximumItemCount, row.Items.Count);

        return Math.Clamp(maximumItemCount, 1, PartyCooldownBoardLayout.MaxIconsPerWrappedLine);
    }

    private static bool PartyCooldownBoardLayoutFits(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics,
        int targetIconsPerLine)
        => metrics.UseAllianceGrid
           && metrics.IconsPerLine >= targetIconsPerLine
           && GetPartyCooldownBoardContentHeight(rows, metrics) <= MaxOverlayHeight + 0.5f;

    private static float GetPartyCooldownAllianceGroupWidth(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float labelGap)
        => rows.Any(row => !string.IsNullOrWhiteSpace(row.Member.AllianceGroup))
            ? Math.Max(16f, ImGui.CalcTextSize("C").X + labelGap)
            : 0f;

    private static PartyCooldownBoardRenderMetrics GetPartyCooldownBoardRenderMetrics(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        float? boardWidth = null,
        float? renderIconSize = null)
    {
        var width = boardWidth ?? iconWindow.Width;
        var iconSize = renderIconSize ?? iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var textLineHeight = ImGui.GetTextLineHeight() * Math.Clamp(iconWindow.FontScale, MinFontScale, MaxFontScale);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var regularAllianceGroupWidth = GetPartyCooldownAllianceGroupWidth(rows, labelGap);
        var regularLabelWidth = regularAllianceGroupWidth + jobIconSize + labelGap + nameWidth + labelGap;
        var regularIconAreaWidth = Math.Max(0f, width - padding * 2f - regularLabelWidth);
        var regularIconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(regularIconAreaWidth, iconSize, gap);
        var allianceGroupCount = GetPartyCooldownAllianceGroupCount(rows);
        var allianceColumnGap = Math.Max(8f, gap * 2f);
        var allianceAvailableWidth = Math.Max(0f, width - padding * 2f);
        var allianceColumnLabelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var useAllianceGrid = PartyCooldownBoardLayout.ShouldUseAllianceGrid(allianceGroupCount);
        var allianceMemberColumnCount = useAllianceGrid
            ? PartyCooldownBoardLayout.AllianceMemberColumnCount
            : 0;
        var allianceCellWidth = useAllianceGrid
            ? PartyCooldownBoardLayout.GetAllianceMemberCellWidth(allianceAvailableWidth, allianceColumnGap)
            : 0f;
        var allianceColumnIconAreaWidth = Math.Max(0f, allianceCellWidth - allianceColumnLabelWidth);
        var labelWidth = useAllianceGrid ? allianceColumnLabelWidth : regularLabelWidth;
        var iconsPerLine = useAllianceGrid
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
            textLineHeight,
            jobIconSize,
            useAllianceGrid ? 0f : regularAllianceGroupWidth,
            allianceColumnGap,
            allianceCellWidth,
            labelWidth,
            allianceGroupCount,
            allianceMemberColumnCount,
            iconsPerLine,
            useAllianceGrid,
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
        => metrics.TextLineHeight + Math.Max(4f, metrics.Gap);

    private static float GetPartyCooldownRowContentHeight(
        int itemCount,
        PartyCooldownBoardRenderMetrics metrics)
        => Math.Max(
            Math.Max(metrics.JobIconSize, metrics.TextLineHeight),
            PartyCooldownBoardLayout.GetRowContentHeight(
                itemCount,
                metrics.IconSize,
                metrics.Gap,
                metrics.IconsPerLine));

    private static float GetPartyCooldownBoardContentHeight(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        if (metrics.UseAllianceGrid)
            return GetPartyCooldownAllianceBoardContentHeight(rows, metrics);

        var height = metrics.Padding * 2f;
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                height += metrics.Gap;

            height += GetPartyCooldownRowContentHeight(rows[i].Items.Count, metrics);
        }

        return rows.Count == 0 ? 0f : height;
    }

    private static float GetPartyCooldownAllianceBoardContentHeight(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
    {
        var height = metrics.Padding * 2f;
        var renderedGroupCount = 0;
        var groupGap = Math.Max(8f, metrics.Gap * 2f);
        var headerHeight = GetPartyCooldownAllianceColumnHeaderHeight(metrics);
        for (var groupIndex = 0; groupIndex < PartyCooldownBoardLayout.AllianceColumnCount; groupIndex++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(groupIndex);
            var groupMemberCount = 0;
            var gridRowHeight = 0f;
            var groupContentHeight = headerHeight;
            foreach (var row in rows)
            {
                if (!string.Equals(row.Member.AllianceGroup, groupLabel, StringComparison.Ordinal))
                    continue;

                if (groupMemberCount > 0 && groupMemberCount % PartyCooldownBoardLayout.AllianceMemberColumnCount == 0)
                {
                    groupContentHeight += gridRowHeight + metrics.Gap;
                    gridRowHeight = 0f;
                }

                gridRowHeight = Math.Max(gridRowHeight, GetPartyCooldownRowContentHeight(row.Items.Count, metrics));
                groupMemberCount++;
            }

            if (groupMemberCount == 0)
                continue;

            groupContentHeight += gridRowHeight;
            if (renderedGroupCount > 0)
                height += groupGap;

            height += groupContentHeight;
            renderedGroupCount++;
        }

        return renderedGroupCount == 0 ? 0f : height;
    }
}
