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

    private readonly record struct OverlayFrameItemSets(
        OverlayItemSet Display,
        OverlayItemSet Layout);

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

    private OverlayFrameItemSets GetOverlayFrameItems(IconWindowConfig iconWindow, string job, uint level)
    {
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
        {
            var layoutAbilities = this.GetVisibleAbilities(job, level, iconWindow).ToList();
            var displayAbilities = iconWindow.DisplayCondition == IconDisplayCondition.Always
                ? layoutAbilities
                : layoutAbilities.Where(ability => this.ShouldDisplayAbility(ability, iconWindow)).ToList();
            return new OverlayFrameItemSets(
                new OverlayItemSet(displayAbilities, Array.Empty<AuraState>()),
                new OverlayItemSet(layoutAbilities, Array.Empty<AuraState>()));
        }

        var layoutAuras = this.GetLayoutAuras(iconWindow).ToList();
        var displayAuras = layoutAuras
            .Where(aura => this.ShouldDisplayAura(aura, iconWindow))
            .Where(aura => aura.Present || ShouldShowMissingAura(iconWindow))
            .ToList();
        return new OverlayFrameItemSets(
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), displayAuras),
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), layoutAuras));
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

        return this.GetLayoutAuras(iconWindow);
    }

    private IEnumerable<AuraState> GetLayoutAuras(IconWindowConfig iconWindow)
    {
        return iconWindow.TrackedStatusIds
            .Distinct()
            .Select(statusId => this.GetAuraState(iconWindow, statusId));
    }
}
