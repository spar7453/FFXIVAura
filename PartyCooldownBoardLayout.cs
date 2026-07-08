namespace FFXIVAura;

internal static class PartyCooldownBoardLayout
{
    public const int MaxIconsPerWrappedLine = 5;

    public static bool HideEmptyRows(PartyCooldownCategory category)
        => category == PartyCooldownCategory.Synergy;

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

    private static int SanitizeIconsPerLine(int iconsPerLine)
        => Math.Clamp(iconsPerLine, 1, MaxIconsPerWrappedLine);
}
