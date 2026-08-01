namespace FFXIVAura;

internal static class CooldownCoverGeometry
{
    public const int SegmentCount = 64;
    private static readonly Vector2[] Points = CreatePoints();

    public static ReadOnlySpan<Vector2> UnitCirclePoints => Points;

    public static int GetStartIndex(float elapsedRatio)
        => Math.Clamp((int)MathF.Floor(Math.Clamp(elapsedRatio, 0f, 1f) * SegmentCount), 0, SegmentCount);

    private static Vector2[] CreatePoints()
    {
        var points = new Vector2[SegmentCount + 1];
        for (var index = 0; index <= SegmentCount; index++)
        {
            var angle = (-MathF.PI * 0.5f) + (MathF.Tau * index / SegmentCount);
            points[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return points;
    }
}
