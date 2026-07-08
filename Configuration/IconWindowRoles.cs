namespace FFXIVAura;

internal static class IconWindowRoles
{
    public static bool IsStandardAuraRole(IconWindowRole role)
        => role is IconWindowRole.PlayerBuffs
            or IconWindowRole.TargetDebuffs
            or IconWindowRole.PartyBuffs;

    public static bool IsPartyCooldownRole(IconWindowRole role)
        => role is IconWindowRole.PartyDefensives
            or IconWindowRole.PartyHealingCooldowns
            or IconWindowRole.PartySynergies;

    public static PartyCooldownCategory GetPartyCooldownCategory(IconWindowRole role)
        => role switch
        {
            IconWindowRole.PartyHealingCooldowns => PartyCooldownCategory.Healing,
            IconWindowRole.PartySynergies => PartyCooldownCategory.Synergy,
            _ => PartyCooldownCategory.Defensive,
        };
}
