namespace FFXIVAura;

internal readonly record struct OverlayControlLayout(
    bool TopControlsBelow,
    bool SplitTopControls,
    bool BottomControlsAbove,
    bool SplitBottomControls,
    int BottomBelowRowOffset,
    int BottomAboveRowOffset);

internal static class OverlayControlGeometry
{
    private const float RowGap = 4f;
    private const float SplitTolerance = 8f;

    public static OverlayControlLayout CreateLayout(
        Vector2 areaOrigin,
        Vector2 areaSize,
        Vector2 displaySize,
        float controlHeight,
        float roleWidth,
        float conditionWidth,
        float nameWidth,
        float alignmentWidth)
    {
        var splitTopControls = areaSize.X < roleWidth + conditionWidth + SplitTolerance;
        var topRows = splitTopControls ? 2 : 1;
        var topControlsBelow = areaOrigin.Y < GetRowsHeight(topRows, controlHeight);

        var splitBottomControls = areaSize.X < nameWidth + alignmentWidth + SplitTolerance;
        var bottomRows = splitBottomControls ? 2 : 1;
        var bottomControlsAbove = false;
        if (displaySize.Y > 0f)
        {
            var bottomSpace = displaySize.Y - (areaOrigin.Y + areaSize.Y);
            var bottomSpaceNeeded = GetRowsHeight(bottomRows, controlHeight);
            var aboveRowsAlreadyUsed = topControlsBelow ? 0 : topRows;
            var aboveSpaceNeeded = GetRowsHeight(aboveRowsAlreadyUsed + bottomRows, controlHeight);
            bottomControlsAbove = bottomSpace < bottomSpaceNeeded
                                  && (areaOrigin.Y >= aboveSpaceNeeded || areaOrigin.Y > bottomSpace);
        }

        return new OverlayControlLayout(
            topControlsBelow,
            splitTopControls,
            bottomControlsAbove,
            splitBottomControls,
            topControlsBelow ? topRows : 0,
            topControlsBelow ? 0 : topRows);
    }

    public static float GetRowsHeight(int rows, float rowHeight)
        => rows <= 0 ? 0f : rows * (rowHeight + RowGap);

    public static float GetTopControlY(Vector2 areaOrigin, Vector2 areaSize, float windowHeight, int row, OverlayControlLayout layout)
    {
        return layout.TopControlsBelow
            ? GetBelowControlY(areaOrigin, areaSize, windowHeight, row)
            : GetAboveControlY(areaOrigin, windowHeight, row);
    }

    public static float GetBottomControlY(Vector2 areaOrigin, Vector2 areaSize, float windowHeight, int row, OverlayControlLayout layout)
    {
        return layout.BottomControlsAbove
            ? GetAboveControlY(areaOrigin, windowHeight, layout.BottomAboveRowOffset + row)
            : GetBelowControlY(areaOrigin, areaSize, windowHeight, layout.BottomBelowRowOffset + row);
    }

    public static Vector2 ClampWindowPositionToDisplay(
        Vector2 position,
        Vector2 size,
        Vector2 invalidFallback,
        Vector2 displaySize,
        float margin)
    {
        if (!IsFinitePosition(position))
            position = invalidFallback;

        if (!IsFinitePosition(size))
            size = Vector2.Zero;

        margin = IsFiniteValue(margin) ? Math.Max(0f, margin) : 0f;
        if (displaySize.X <= 0f || displaySize.Y <= 0f)
            return position;

        var maxX = Math.Max(margin, displaySize.X - Math.Max(0f, size.X) - margin);
        var maxY = Math.Max(margin, displaySize.Y - Math.Max(0f, size.Y) - margin);
        return new Vector2(
            Math.Clamp(position.X, margin, maxX),
            Math.Clamp(position.Y, margin, maxY));
    }

    private static float GetAboveControlY(Vector2 areaOrigin, float windowHeight, int row)
        => areaOrigin.Y - (row + 1) * (windowHeight + RowGap);

    private static float GetBelowControlY(Vector2 areaOrigin, Vector2 areaSize, float windowHeight, int row)
        => areaOrigin.Y + areaSize.Y + RowGap + row * (windowHeight + RowGap);

    private static bool IsFinitePosition(Vector2 position)
        => IsFiniteValue(position.X) && IsFiniteValue(position.Y);

    private static bool IsFiniteValue(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
