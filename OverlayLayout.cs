namespace FFXIVAura;

internal static class OverlayLayout
{
    public static Vector2 GetAutoPosition(
        IconAlignment alignment,
        int index,
        int visibleCount,
        Vector2 areaSize,
        float iconSize,
        float gap)
    {
        areaSize = NormalizeAreaSize(areaSize);
        iconSize = NormalizeIconSize(iconSize);
        gap = NormalizeGap(gap);

        var cell = iconSize + gap;
        var columns = Math.Max(1, (int)Math.Floor((areaSize.X + gap) / Math.Max(1f, cell)));
        var count = Math.Max(1, visibleCount);
        var rows = Math.Max(1, (int)Math.Ceiling(count / (float)columns));
        var row = index / columns;
        var column = index % columns;
        var itemsInRow = row == rows - 1 ? count - row * columns : columns;
        var rowWidth = Math.Max(0f, itemsInRow * iconSize + Math.Max(0, itemsInRow - 1) * gap);
        var blockHeight = Math.Max(0f, rows * iconSize + Math.Max(0, rows - 1) * gap);
        var x = GetAlignedRowStartX(alignment, areaSize.X, rowWidth) + column * cell;
        var y = Math.Max(0f, MathF.Round((areaSize.Y - blockHeight) * 0.5f)) + row * cell;
        return ClampIconPosition(new Vector2(x, y), areaSize, iconSize);
    }

    public static IEnumerable<Vector2> GetAutoPositionSlots(
        IconAlignment alignment,
        Vector2 areaSize,
        float iconSize,
        float gap)
    {
        areaSize = NormalizeAreaSize(areaSize);
        iconSize = NormalizeIconSize(iconSize);
        gap = NormalizeGap(gap);

        var cell = iconSize + gap;
        var columns = Math.Max(1, (int)Math.Floor((areaSize.X + gap) / Math.Max(1f, cell)));
        var rows = Math.Max(1, (int)Math.Floor((areaSize.Y + gap) / Math.Max(1f, cell)));
        var slotCount = Math.Max(1, columns * rows);

        for (var index = 0; index < slotCount; index++)
            yield return GetAutoPosition(alignment, index, slotCount, areaSize, iconSize, gap);
    }

    public static Vector2 FindFreeAutoPosition(
        IconAlignment alignment,
        int preferredIndex,
        int visibleCount,
        IReadOnlyList<Vector2> occupied,
        Vector2 areaSize,
        float iconSize,
        float gap)
    {
        areaSize = NormalizeAreaSize(areaSize);
        iconSize = NormalizeIconSize(iconSize);
        gap = NormalizeGap(gap);

        var slots = GetAutoPositionSlots(alignment, areaSize, iconSize, gap).ToList();
        if (slots.Count == 0)
            return Vector2.Zero;

        var startIndex = Math.Clamp(preferredIndex, 0, slots.Count - 1);
        for (var offset = 0; offset < slots.Count; offset++)
        {
            var candidate = slots[(startIndex + offset) % slots.Count];
            if (occupied.All(position => !IconPositionsOverlap(position, candidate, iconSize)))
                return candidate;
        }

        return GetAutoPosition(alignment, preferredIndex, visibleCount, areaSize, iconSize, gap);
    }

    public static float GetAlignedRowStartX(IconAlignment alignment, float areaWidth, float rowWidth)
    {
        var remaining = Math.Max(0f, areaWidth - rowWidth);
        return alignment switch
        {
            IconAlignment.Left => 0f,
            IconAlignment.Right => MathF.Round(remaining),
            _ => MathF.Round(remaining * 0.5f),
        };
    }

    public static Vector2 ClampIconPosition(Vector2 position, Vector2 areaSize, float iconSize)
    {
        areaSize = NormalizeAreaSize(areaSize);
        iconSize = NormalizeIconSize(iconSize);

        var x = IsFinite(position.X) ? position.X : 0f;
        var y = IsFinite(position.Y) ? position.Y : 0f;
        var maxX = Math.Max(0f, areaSize.X - iconSize);
        var maxY = Math.Max(0f, areaSize.Y - iconSize);

        return new Vector2(
            Math.Clamp(x, 0f, maxX),
            Math.Clamp(y, 0f, maxY));
    }

    public static bool IconPositionsOverlap(Vector2 first, Vector2 second, float iconSize)
    {
        return Math.Abs(first.X - second.X) < iconSize
               && Math.Abs(first.Y - second.Y) < iconSize;
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    private static Vector2 NormalizeAreaSize(Vector2 areaSize)
    {
        return new Vector2(
            IsFinite(areaSize.X) ? Math.Max(0f, areaSize.X) : 0f,
            IsFinite(areaSize.Y) ? Math.Max(0f, areaSize.Y) : 0f);
    }

    private static float NormalizeIconSize(float iconSize)
        => IsFinite(iconSize) && iconSize > 0f ? iconSize : 1f;

    private static float NormalizeGap(float gap)
        => IsFinite(gap) && gap > 0f ? gap : 0f;
}
