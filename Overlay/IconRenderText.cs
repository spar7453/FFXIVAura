using System.Globalization;

namespace FFXIVAura;

internal static class IconRenderText
{
    private const int MaxCachedValue = 999;
    private static readonly string[] CachedIntegers = CreateIntegerCache();

    public static string FormatCooldown(float remaining)
    {
        if (!float.IsFinite(remaining) || remaining <= 0)
            return CachedIntegers[0];

        var rounded = Math.Ceiling(remaining);
        return rounded <= MaxCachedValue
            ? CachedIntegers[(int)rounded]
            : rounded.ToString("0", CultureInfo.InvariantCulture);
    }

    public static string FormatCharge(uint charges)
        => charges <= MaxCachedValue
            ? CachedIntegers[(int)charges]
            : charges.ToString(CultureInfo.InvariantCulture);

    public static string FormatCount(int count)
        => count is >= 0 and <= MaxCachedValue
            ? CachedIntegers[count]
            : count.ToString(CultureInfo.InvariantCulture);

    private static string[] CreateIntegerCache()
    {
        var values = new string[MaxCachedValue + 1];
        for (var index = 0; index < values.Length; index++)
            values[index] = index.ToString(CultureInfo.InvariantCulture);

        return values;
    }
}
