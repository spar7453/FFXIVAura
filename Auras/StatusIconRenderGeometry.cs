namespace FFXIVAura;

internal static class StatusIconRenderGeometry
{
    private const float VisibleHeightRatio = 7f / 8f;

    public static Vector2 UvMin => Vector2.Zero;

    public static Vector2 UvMax => new(1f, VisibleHeightRatio);
}
