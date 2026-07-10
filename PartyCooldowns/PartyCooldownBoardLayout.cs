namespace FFXIVAura;

internal static class PartyCooldownBoardLayout
{
    public const int MaxIconsPerWrappedLine = 5;
    public const int AllianceColumnCount = 3;
    public const int AllianceMemberColumnCount = 1;
    public const float MinAllianceIconSize = 16f;

    public static bool HideEmptyRows(PartyCooldownCategory category)
        => category is PartyCooldownCategory.Healing or PartyCooldownCategory.Synergy;

    public static string BuildIconInteractionId(
        string windowId,
        string memberKey,
        string definitionId,
        uint actionId)
        => $"##party-cooldown-{windowId}-{memberKey}-{definitionId}-{actionId}";

    public static int GetIconLineCapacity(float availableWidth, float iconSize, float gap)
    {
        if (availableWidth <= 0f || iconSize <= 0f)
            return 1;

        var capacity = (int)MathF.Floor((availableWidth + gap) / (iconSize + gap));
        return Math.Clamp(capacity, 1, MaxIconsPerWrappedLine);
    }

    public static int GetLineCount(int itemCount, int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var capacity = SanitizeIconsPerLine(iconsPerLine);
        return Math.Max(1, (Math.Max(0, itemCount) + capacity - 1) / capacity);
    }

    public static int GetLineStartIndex(int lineIndex, int iconsPerLine = MaxIconsPerWrappedLine)
        => Math.Max(0, lineIndex) * SanitizeIconsPerLine(iconsPerLine);

    public static int GetLineItemCount(int itemCount, int lineIndex, int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var capacity = SanitizeIconsPerLine(iconsPerLine);
        var remaining = Math.Max(0, itemCount) - GetLineStartIndex(lineIndex, capacity);
        return Math.Clamp(remaining, 0, capacity);
    }

    public static float GetIconAreaWidth(float iconSize, float gap, int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var capacity = SanitizeIconsPerLine(iconsPerLine);
        return capacity * iconSize + Math.Max(0, capacity - 1) * gap;
    }

    public static float GetLineWidth(int lineItemCount, float iconSize, float gap)
    {
        if (lineItemCount <= 0)
            return 0f;

        return lineItemCount * iconSize + Math.Max(0, lineItemCount - 1) * gap;
    }

    public static float GetLineStartOffset(IconAlignment alignment, int lineItemCount, float iconSize, float gap, int iconsPerLine = MaxIconsPerWrappedLine)
        => OverlayLayout.GetAlignedRowStartX(
            alignment,
            GetIconAreaWidth(iconSize, gap, iconsPerLine),
            GetLineWidth(lineItemCount, iconSize, gap));

    public static float GetAllianceMemberCellWidth(float availableWidth, float cellGap)
        => Math.Max(0f, availableWidth);

    public static float GetRequiredAllianceGridWidth(
        float labelWidth,
        float iconSize,
        float gap,
        float cellGap,
        float padding,
        int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var cellWidth = Math.Max(0f, labelWidth) + GetIconAreaWidth(iconSize, gap, iconsPerLine);
        return Math.Max(0f, padding) * 2f + cellWidth;
    }

    public static bool ShouldUseAllianceGrid(int allianceGroupCount)
        => allianceGroupCount >= 2;

    public static float GetCompactAllianceGap(float configuredGap, float configuredIconSize, float renderIconSize)
    {
        var scale = configuredIconSize <= 0f ? 1f : Math.Clamp(renderIconSize / configuredIconSize, 0f, 1f);
        return Math.Clamp(Math.Max(0f, configuredGap) * scale, 0.75f, 1.25f);
    }

    public static float GetCompactAllianceFontScale(float configuredFontScale, float configuredIconSize, float renderIconSize)
    {
        var scale = configuredIconSize <= 0f ? 1f : Math.Clamp(renderIconSize / configuredIconSize, 0f, 1f);
        return Math.Clamp(Math.Max(0f, configuredFontScale) * scale, 0.65f, 0.7f);
    }

    public static float GetIconContentHeight(int itemCount, float iconSize, float gap, int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var lineCount = GetLineCount(itemCount, iconsPerLine);
        return lineCount * iconSize + Math.Max(0, lineCount - 1) * gap;
    }

    public static float GetRowContentHeight(int itemCount, float iconSize, float gap, int iconsPerLine = MaxIconsPerWrappedLine)
        => Math.Max(iconSize, GetIconContentHeight(itemCount, iconSize, gap, iconsPerLine));

    public static float GetBoardContentHeight(IEnumerable<int> rowItemCounts, float iconSize, float gap, float padding, int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var height = padding * 2f;
        var rowCount = 0;
        foreach (var itemCount in rowItemCounts)
        {
            if (rowCount > 0)
                height += gap;

            height += GetRowContentHeight(itemCount, iconSize, gap, iconsPerLine);
            rowCount++;
        }

        return rowCount == 0 ? 0f : height;
    }

    public static float GetAllianceGridBoardContentHeight(
        IReadOnlyList<(string AllianceGroup, int ItemCount)> rows,
        float iconSize,
        float gap,
        float padding,
        float headerHeight,
        int iconsPerLine = MaxIconsPerWrappedLine)
    {
        var height = Math.Max(0f, padding) * 2f;
        var renderedGroupCount = 0;
        var groupGap = Math.Max(8f, gap * 2f);
        for (var groupIndex = 0; groupIndex < AllianceColumnCount; groupIndex++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(groupIndex);
            var groupMemberCount = 0;
            var groupContentHeight = Math.Max(0f, headerHeight);
            foreach (var row in rows)
            {
                if (!string.Equals(row.AllianceGroup, groupLabel, StringComparison.Ordinal))
                    continue;

                if (groupMemberCount > 0)
                    groupContentHeight += gap;

                groupContentHeight += GetRowContentHeight(row.ItemCount, iconSize, gap, iconsPerLine);
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

    public static float GetExpandedBoardHeight(
        float currentHeight,
        IEnumerable<int> rowItemCounts,
        float iconSize,
        float gap,
        float padding,
        int iconsPerLine,
        float minHeight,
        float maxHeight)
    {
        var requiredHeight = GetBoardContentHeight(rowItemCounts, iconSize, gap, padding, iconsPerLine);
        return Math.Clamp(Math.Max(currentHeight, requiredHeight), minHeight, maxHeight);
    }

    private static int SanitizeIconsPerLine(int iconsPerLine)
        => Math.Clamp(iconsPerLine, 1, MaxIconsPerWrappedLine);
}
