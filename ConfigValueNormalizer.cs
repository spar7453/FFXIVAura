namespace FFXIVAura;

internal static class ConfigValueNormalizer
{
    public static float NormalizeScalar(float value, float fallback, float min, float max, bool allowZero = false)
    {
        if (!IsFiniteValue(value) || (allowZero ? value < 0f : value <= 0f))
            value = fallback;

        if (!IsFiniteValue(value) || (allowZero ? value < 0f : value <= 0f))
            value = min;

        return Math.Clamp(value, min, max);
    }

    public static float NormalizeDimension(float value, float fallback, float min, float max)
    {
        if (!IsFiniteValue(value) || value <= 0f)
            value = fallback;

        if (!IsFiniteValue(value) || value <= 0f)
            value = min;

        return Math.Clamp(value, min, max);
    }

    public static Vector2 NormalizePosition(Vector2 position, Vector2 fallback, Vector2 finalFallback)
    {
        if (!IsFinitePosition(position) || Math.Abs(position.X) > 20000f || Math.Abs(position.Y) > 20000f)
            position = fallback;

        return IsFinitePosition(position)
            ? position
            : finalFallback;
    }

    public static bool IsFinitePosition(Vector2 position)
        => IsFiniteValue(position.X) && IsFiniteValue(position.Y);

    public static bool IsFiniteValue(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
