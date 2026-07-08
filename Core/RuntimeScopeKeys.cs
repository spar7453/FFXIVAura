namespace FFXIVAura;

internal static class RuntimeScopeKeys
{
    public static string AuraSeen(IconWindowConfig iconWindow)
        => AuraSeen(
            iconWindow.Id,
            iconWindow.Role,
            iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.PartyAurasOwnOnly);

    public static string AuraSeen(string windowId, IconWindowRole role, bool ownOnly)
        => $"{windowId}:{role}:{ownOnly}";

    public static string CooldownFrame(string abilityId, uint actionId)
        => $"{abilityId}:{actionId}";

    public static string AbilityDrag(string windowId, string job, string abilityId)
        => $"{windowId}:{job}:{abilityId}";

    public static string AuraDrag(string windowId, uint statusId)
        => $"{windowId}:{OverlayPositionKeys.Aura(statusId)}";

    public static bool BelongsToWindow(string scopeKey, string windowId)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(windowId))
            return false;

        return string.Equals(scopeKey, windowId, StringComparison.OrdinalIgnoreCase)
               || scopeKey.StartsWith(OverlayPositionKeys.WindowPrefix(windowId), StringComparison.OrdinalIgnoreCase);
    }
}
