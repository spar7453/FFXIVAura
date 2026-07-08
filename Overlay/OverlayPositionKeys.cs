namespace FFXIVAura;

internal static class OverlayPositionKeys
{
    private const string StatusPrefix = "status-";

    public static string AuraGroup(IconWindowConfig iconWindow)
        => $"{iconWindow.Id}:{iconWindow.Role}";

    public static string Aura(uint statusId)
        => $"{StatusPrefix}{statusId}";

    public static string WindowPrefix(string windowId)
        => $"{windowId.Trim()}:";

    public static string VisibleAbilityGroup(string windowId, string job)
        => $"{windowId}:{job}";

    public static string ReplaceWindowPrefix(string scopedKey, string sourceWindowId, string targetWindowId)
    {
        var source = sourceWindowId.Trim();
        var target = targetWindowId.Trim();
        if (source.Length == 0 || target.Length == 0)
            return scopedKey;

        var sourcePrefix = WindowPrefix(source);
        return scopedKey.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
            ? $"{target}:{scopedKey[sourcePrefix.Length..]}"
            : scopedKey;
    }

    public static bool TryParseAura(string key, out uint statusId)
    {
        statusId = 0;
        return key.StartsWith(StatusPrefix, StringComparison.OrdinalIgnoreCase)
               && uint.TryParse(key[StatusPrefix.Length..], out statusId)
               && statusId > 0;
    }
}
