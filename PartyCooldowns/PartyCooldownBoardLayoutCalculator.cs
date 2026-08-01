namespace FFXIVAura;

internal readonly record struct PartyCooldownBoardLayoutEnvironment(
    float DisplayHeight,
    float TextLineHeight,
    float AllianceGroupLabelWidth);

internal interface IPartyCooldownBoardLayoutRow
{
    string AllianceGroup { get; }

    int ItemCount { get; }
}

internal readonly record struct PartyCooldownBoardLayoutRow(
    string AllianceGroup,
    int ItemCount) : IPartyCooldownBoardLayoutRow;

internal readonly record struct PartyCooldownBoardRenderMetrics(
    float IconSize,
    float Gap,
    float Padding,
    float LabelGap,
    float TextLineHeight,
    float FontScale,
    float JobIconSize,
    float AllianceGroupWidth,
    float AllianceCellGap,
    float AllianceCellWidth,
    float LabelWidth,
    int AllianceGroupCount,
    int AllianceMemberColumnCount,
    int IconsPerLine,
    bool UseAllianceGrid,
    bool IsCompact,
    bool HasOverflow,
    int DrawnIconCount);

internal readonly record struct PartyCooldownBoardDisplayLayout(
    Vector2 Size,
    PartyCooldownBoardRenderMetrics Metrics,
    float RequiredHeight);

internal static class PartyCooldownBoardLayoutCalculator
{
    public static PartyCooldownBoardDisplayLayout Calculate(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        PartyCooldownBoardLayoutEnvironment environment)
        => CalculateCore(layoutBinding, rows, environment);

    public static PartyCooldownBoardDisplayLayout Calculate(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<PartyCooldownBoardLayoutRow> rows,
        PartyCooldownBoardLayoutEnvironment environment)
        => CalculateCore(layoutBinding, rows, environment);

    private static PartyCooldownBoardDisplayLayout CalculateCore<TRow>(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardLayoutEnvironment environment)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var configuredIconSize = Math.Clamp(
            layoutBinding.IconSize,
            OverlayLayoutLimits.MinIconSize,
            OverlayLayoutLimits.MaxIconSize);
        var targetIconsPerLine = GetPartyCooldownTargetIconsPerLine(rows);
        var maxDisplayHeight = GetPartyCooldownBoardMaxDisplayHeight(rows, environment);
        var layout = CreatePartyCooldownBoardDisplayLayout(
            layoutBinding,
            rows,
            configuredIconSize,
            targetIconsPerLine,
            compactAlliance: false,
            maxDisplayHeight,
            environment);
        if (layout.Metrics.AllianceGroupCount < 2
            || PartyCooldownBoardLayoutFits(
                rows,
                layout.Metrics,
                targetIconsPerLine,
                maxDisplayHeight))
        {
            return layout;
        }

        var minimumLayout = CreatePartyCooldownBoardDisplayLayout(
            layoutBinding,
            rows,
            PartyCooldownBoardLayout.MinAllianceIconSize,
            targetIconsPerLine,
            compactAlliance: true,
            maxDisplayHeight,
            environment);
        if (!PartyCooldownBoardLayoutFits(
                rows,
                minimumLayout.Metrics,
                targetIconsPerLine,
                maxDisplayHeight))
            return minimumLayout;

        var minimum = PartyCooldownBoardLayout.MinAllianceIconSize;
        var maximum = configuredIconSize;
        var best = minimumLayout;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var candidateIconSize = (minimum + maximum) * 0.5f;
            var candidate = CreatePartyCooldownBoardDisplayLayout(
                layoutBinding,
                rows,
                candidateIconSize,
                targetIconsPerLine,
                compactAlliance: true,
                maxDisplayHeight,
                environment);
            if (PartyCooldownBoardLayoutFits(
                    rows,
                    candidate.Metrics,
                    targetIconsPerLine,
                    maxDisplayHeight))
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

    private static PartyCooldownBoardDisplayLayout CreatePartyCooldownBoardDisplayLayout<TRow>(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<TRow> rows,
        float iconSize,
        int targetIconsPerLine,
        bool compactAlliance,
        float maxDisplayHeight,
        PartyCooldownBoardLayoutEnvironment environment)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var renderGap = compactAlliance
            ? PartyCooldownBoardLayout.GetCompactAllianceGap(layoutBinding.Gap, layoutBinding.IconSize, iconSize)
            : Math.Max(2f, layoutBinding.Gap);
        var renderFontScale = compactAlliance
            ? PartyCooldownBoardLayout.GetCompactAllianceFontScale(layoutBinding.FontScale, layoutBinding.IconSize, iconSize)
            : Math.Clamp(
                layoutBinding.FontScale,
                OverlayLayoutLimits.MinFontScale,
                OverlayLayoutLimits.MaxFontScale);
        var width = GetPartyCooldownBoardDisplayWidth(
            layoutBinding,
            rows,
            iconSize,
            renderGap,
            targetIconsPerLine);
        var metrics = GetPartyCooldownBoardRenderMetrics(
            layoutBinding,
            rows,
            environment,
            width,
            iconSize,
            renderGap,
            renderFontScale,
            compactAlliance);
        var requiredHeight = GetPartyCooldownBoardContentHeight(rows, metrics);
        metrics = metrics with { HasOverflow = requiredHeight > maxDisplayHeight + 0.5f };
        var height = Math.Clamp(
            Math.Max(layoutBinding.Height, requiredHeight),
            OverlayLayoutLimits.MinHeight,
            maxDisplayHeight);
        return new PartyCooldownBoardDisplayLayout(new Vector2(width, height), metrics, requiredHeight);
    }

    private static float GetPartyCooldownBoardMaxDisplayHeight<TRow>(
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardLayoutEnvironment environment)
        where TRow : IPartyCooldownBoardLayoutRow
        => CountAllianceGroups(rows) >= 2
            ? PartyCooldownBoardLayout.GetAllianceBoardHeightLimit(
                OverlayLayoutLimits.MaxHeight,
                environment.DisplayHeight,
                OverlayLayoutLimits.WindowMargin)
            : OverlayLayoutLimits.MaxHeight;

    private static float GetPartyCooldownBoardDisplayWidth<TRow>(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<TRow> rows,
        float iconSize,
        float gap,
        int targetIconsPerLine)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var allianceGroupCount = CountAllianceGroups(rows);
        if (allianceGroupCount < 2)
            return layoutBinding.Width;

        var padding = Math.Max(2f, gap);
        var labelGap = Math.Max(3f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 14f, 32f);
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
        return Math.Clamp(
            Math.Max(layoutBinding.Width, requiredWidth + 1f),
            OverlayLayoutLimits.MinWidth,
            OverlayLayoutLimits.MaxWidth);
    }

    private static int GetPartyCooldownTargetIconsPerLine<TRow>(
        IReadOnlyList<TRow> rows)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var maximumItemCount = 1;
        foreach (var row in rows)
            maximumItemCount = Math.Max(maximumItemCount, row.ItemCount);

        return Math.Clamp(maximumItemCount, 1, PartyCooldownBoardLayout.MaxIconsPerWrappedLine);
    }

    private static bool PartyCooldownBoardLayoutFits<TRow>(
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardRenderMetrics metrics,
        int targetIconsPerLine,
        float maxDisplayHeight)
        where TRow : IPartyCooldownBoardLayoutRow
        => metrics.UseAllianceGrid
           && metrics.IconsPerLine >= targetIconsPerLine
           && GetPartyCooldownBoardContentHeight(rows, metrics) <= maxDisplayHeight + 0.5f;

    private static float GetPartyCooldownAllianceGroupWidth<TRow>(
        IReadOnlyList<TRow> rows,
        float labelGap,
        float allianceGroupLabelWidth)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.AllianceGroup))
                return Math.Max(16f, allianceGroupLabelWidth + labelGap);
        }

        return 0f;
    }

    private static PartyCooldownBoardRenderMetrics GetPartyCooldownBoardRenderMetrics<TRow>(
        IconWindowLayoutBinding layoutBinding,
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardLayoutEnvironment environment,
        float? boardWidth = null,
        float? renderIconSize = null,
        float? renderGap = null,
        float? renderFontScale = null,
        bool compactAlliance = false)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var width = boardWidth ?? layoutBinding.Width;
        var iconSize = renderIconSize ?? layoutBinding.IconSize;
        var gap = renderGap ?? Math.Max(2f, layoutBinding.Gap);
        var padding = compactAlliance ? Math.Max(2f, gap) : Math.Max(4f, gap);
        var labelGap = compactAlliance ? Math.Max(3f, gap) : Math.Max(4f, gap);
        var fontScale = renderFontScale
                        ?? Math.Clamp(
                            layoutBinding.FontScale,
                            OverlayLayoutLimits.MinFontScale,
                            OverlayLayoutLimits.MaxFontScale);
        var textLineHeight = environment.TextLineHeight * fontScale;
        var jobIconSize = Math.Clamp(iconSize * 0.72f, compactAlliance ? 14f : 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var regularAllianceGroupWidth = GetPartyCooldownAllianceGroupWidth(
            rows,
            labelGap,
            environment.AllianceGroupLabelWidth);
        var regularLabelWidth = regularAllianceGroupWidth + jobIconSize + labelGap + nameWidth + labelGap;
        var regularIconAreaWidth = Math.Max(0f, width - padding * 2f - regularLabelWidth);
        var regularIconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(regularIconAreaWidth, iconSize, gap);
        var allianceGroupCount = CountAllianceGroups(rows);
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
            drawnIconCount += row.ItemCount;

        return new PartyCooldownBoardRenderMetrics(
            iconSize,
            gap,
            padding,
            labelGap,
            textLineHeight,
            fontScale,
            jobIconSize,
            useAllianceGrid ? 0f : regularAllianceGroupWidth,
            allianceColumnGap,
            allianceCellWidth,
            labelWidth,
            allianceGroupCount,
            allianceMemberColumnCount,
            iconsPerLine,
            useAllianceGrid,
            compactAlliance,
            false,
            drawnIconCount);
    }

    public static int CountAllianceGroups(IReadOnlyList<PartyCooldownMemberRow> rows)
        => CountAllianceGroups<PartyCooldownMemberRow>(rows);

    private static int CountAllianceGroups<TRow>(
        IReadOnlyList<TRow> rows)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var count = 0;
        for (var group = 0; group < PartyCooldownBoardLayout.AllianceColumnCount; group++)
        {
            if (ContainsAllianceGroup(
                    rows,
                    PartyCooldownAllianceGroups.GroupLabel(group)))
                count++;
        }

        return count;
    }

    public static bool ContainsAllianceGroup(
        IReadOnlyList<PartyCooldownMemberRow> rows,
        string groupLabel)
        => ContainsAllianceGroup<PartyCooldownMemberRow>(rows, groupLabel);

    private static bool ContainsAllianceGroup<TRow>(
        IReadOnlyList<TRow> rows,
        string groupLabel)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        if (string.IsNullOrWhiteSpace(groupLabel))
            return false;

        foreach (var row in rows)
        {
            if (string.Equals(row.AllianceGroup, groupLabel, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static float GetAllianceColumnHeaderHeight(PartyCooldownBoardRenderMetrics metrics)
        => metrics.TextLineHeight + Math.Max(4f, metrics.Gap);

    public static float GetRowContentHeight(
        int itemCount,
        PartyCooldownBoardRenderMetrics metrics)
        => Math.Max(
            Math.Max(metrics.JobIconSize, metrics.TextLineHeight),
            PartyCooldownBoardLayout.GetRowContentHeight(
                itemCount,
                metrics.IconSize,
                metrics.Gap,
                metrics.IconsPerLine));

    private static float GetPartyCooldownBoardContentHeight<TRow>(
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        if (metrics.UseAllianceGrid)
            return GetPartyCooldownAllianceBoardContentHeight(rows, metrics);

        var height = metrics.Padding * 2f;
        var memberRowGap = PartyCooldownBoardLayout.GetMemberRowGap(metrics.IconSize, metrics.Gap, metrics.IsCompact);
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                height += memberRowGap;

            height += GetRowContentHeight(rows[i].ItemCount, metrics);
        }

        return rows.Count == 0 ? 0f : height;
    }

    private static float GetPartyCooldownAllianceBoardContentHeight<TRow>(
        IReadOnlyList<TRow> rows,
        PartyCooldownBoardRenderMetrics metrics)
        where TRow : IPartyCooldownBoardLayoutRow
    {
        var height = metrics.Padding * 2f;
        var renderedGroupCount = 0;
        var groupGap = Math.Max(8f, metrics.Gap * 2f);
        var headerHeight = GetAllianceColumnHeaderHeight(metrics);
        var memberRowGap = PartyCooldownBoardLayout.GetMemberRowGap(metrics.IconSize, metrics.Gap, metrics.IsCompact);
        for (var groupIndex = 0; groupIndex < PartyCooldownBoardLayout.AllianceColumnCount; groupIndex++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(groupIndex);
            var groupMemberCount = 0;
            var groupContentHeight = headerHeight;
            foreach (var row in rows)
            {
                if (!string.Equals(row.AllianceGroup, groupLabel, StringComparison.Ordinal))
                    continue;

                if (groupMemberCount > 0)
                    groupContentHeight += memberRowGap;

                groupContentHeight += GetRowContentHeight(row.ItemCount, metrics);
                groupMemberCount++;
            }

            if (groupMemberCount == 0)
                continue;

            if (renderedGroupCount > 0)
                height += groupGap;

            height += groupContentHeight;
            renderedGroupCount++;
        }

        return renderedGroupCount == 0 ? 0f : height;
    }
}
