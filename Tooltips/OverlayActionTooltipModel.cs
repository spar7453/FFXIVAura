namespace FFXIVAura;

internal readonly record struct OverlayActionTooltipModel(
    uint ActionId,
    uint IconId,
    string Name,
    string Category,
    string CastTime,
    string RecastTime,
    string Range,
    string EffectRange,
    string Description,
    byte UnlockLevel,
    byte MaxCharges);

internal static class OverlayActionTooltipFormatting
{
    public static string FormatCastTime(ushort value100ms)
        => value100ms == 0 ? "즉시 발동" : FormatDuration(value100ms);

    public static string FormatRecastTime(ushort value100ms)
        => value100ms == 0 ? "-" : FormatDuration(value100ms);

    public static string FormatDistance(int value)
        => $"{Math.Max(0, value)}m";

    public static bool ShouldShowMaxCharges(byte maxCharges, string description)
        => maxCharges > 1
           && !description.Contains("최대 누적", StringComparison.OrdinalIgnoreCase);

    private static string FormatDuration(ushort value100ms)
        => $"{value100ms / 10f:0.00}초";
}
