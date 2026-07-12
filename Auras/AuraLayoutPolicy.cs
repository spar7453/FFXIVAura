namespace FFXIVAura;

internal static class AuraLayoutPolicy
{
    public static bool UsesContinuousFlow(IconWindowRole role)
        => IconWindowRoles.IsStandardAuraRole(role);
}
