namespace FFXIVAura;

internal static class OverlayTooltipPositioning
{
    private static readonly Vector2 MouseOffset = new(18f, 18f);

    public static Vector2 GetPositionAtMouse(
        Vector2 mousePosition,
        Vector2 displaySize,
        Vector2 tooltipSize)
    {
        var preferred = mousePosition + MouseOffset;
        if (displaySize.X <= 0f || displaySize.Y <= 0f || tooltipSize.X <= 0f || tooltipSize.Y <= 0f)
            return preferred;

        Span<Vector2> candidates =
        [
            preferred,
            new Vector2(mousePosition.X - tooltipSize.X - MouseOffset.X, mousePosition.Y + MouseOffset.Y),
            new Vector2(mousePosition.X + MouseOffset.X, mousePosition.Y - tooltipSize.Y - MouseOffset.Y),
            mousePosition - tooltipSize - MouseOffset,
        ];
        foreach (var candidate in candidates)
        {
            if (FitsDisplay(candidate, displaySize, tooltipSize))
                return candidate;
        }

        return ClampPosition(preferred, displaySize, tooltipSize);
    }

    private static bool FitsDisplay(Vector2 position, Vector2 displaySize, Vector2 tooltipSize)
        => position.X >= 0f
           && position.Y >= 0f
           && position.X + tooltipSize.X <= displaySize.X
           && position.Y + tooltipSize.Y <= displaySize.Y;

    private static Vector2 ClampPosition(Vector2 position, Vector2 displaySize, Vector2 tooltipSize)
        => new(
            Math.Clamp(position.X, 0f, Math.Max(0f, displaySize.X - tooltipSize.X)),
            Math.Clamp(position.Y, 0f, Math.Max(0f, displaySize.Y - tooltipSize.Y)));
}
