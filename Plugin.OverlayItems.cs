namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private enum OverlayItemVisibility
    {
        Layout,
        Display,
    }

    private readonly record struct OverlayItemSet(
        IReadOnlyList<AbilityDefinition> Abilities,
        IReadOnlyList<AuraState> Auras);

    private OverlayItemSet GetOverlayItems(IconWindowConfig iconWindow, string job, uint level, OverlayItemVisibility visibility)
    {
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            return new OverlayItemSet(
                this.GetOverlayAbilities(iconWindow, job, level, visibility).ToList(),
                Array.Empty<AuraState>());

        return new OverlayItemSet(
            Array.Empty<AbilityDefinition>(),
            this.GetOverlayAuras(iconWindow, visibility).ToList());
    }

    private IEnumerable<AbilityDefinition> GetOverlayAbilities(
        IconWindowConfig iconWindow,
        string job,
        uint level,
        OverlayItemVisibility visibility)
    {
        return visibility == OverlayItemVisibility.Display
            ? this.GetDisplayAbilities(job, level, iconWindow)
            : this.GetVisibleAbilities(job, level, iconWindow);
    }

    private IEnumerable<AuraState> GetOverlayAuras(IconWindowConfig iconWindow, OverlayItemVisibility visibility)
    {
        if (visibility == OverlayItemVisibility.Display)
            return this.GetDisplayAuras(iconWindow);

        return iconWindow.TrackedStatusIds
            .Distinct()
            .Select(statusId => this.GetAuraState(iconWindow, statusId));
    }
}
