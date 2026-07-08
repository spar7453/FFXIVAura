namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private enum OverlayItemVisibility
    {
        Layout,
        Display,
    }

    private OverlayItemSet GetOverlayItems(IconWindowConfig iconWindow, string job, uint level, OverlayItemVisibility visibility)
    {
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            return new OverlayItemSet(
                this.GetOverlayAbilities(iconWindow, job, level, visibility).ToList(),
                Array.Empty<AuraState>());

        if (IconWindowRoles.IsStandardAuraRole(iconWindow.Role))
            return new OverlayItemSet(
                Array.Empty<AbilityDefinition>(),
                this.GetOverlayAuras(iconWindow, visibility).ToList());

        return new OverlayItemSet(Array.Empty<AbilityDefinition>(), Array.Empty<AuraState>());
    }

    private OverlayFrameModel BuildOverlayFrameModel(IconWindowConfig iconWindow, string job, uint level)
    {
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
        {
            var layoutAbilities = this.GetVisibleAbilities(job, level, iconWindow).ToList();
            var displayAbilities = this.GetDisplayAbilitiesFromLayout(layoutAbilities, iconWindow);
            return new OverlayFrameModel(
                iconWindow,
                job,
                level,
                areaSize,
                new OverlayItemSet(displayAbilities, Array.Empty<AuraState>()),
                new OverlayItemSet(layoutAbilities, Array.Empty<AuraState>()));
        }

        this.UpdateCurrentAuraSeenTimes(iconWindow);
        var layoutAuras = this.GetLayoutAuras(iconWindow).ToList();
        var displayAuras = layoutAuras
            .Where(aura => this.ShouldDisplayAura(aura, iconWindow))
            .Where(aura => aura.Present || ShouldShowMissingAura(iconWindow))
            .ToList();
        return new OverlayFrameModel(
            iconWindow,
            job,
            level,
            areaSize,
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), displayAuras),
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), layoutAuras));
    }

    private IReadOnlyList<AbilityDefinition> GetDisplayAbilitiesFromLayout(
        IReadOnlyList<AbilityDefinition> layoutAbilities,
        IconWindowConfig iconWindow)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.Always => layoutAbilities,
            IconDisplayCondition.InCombat => this.IsInCombat() ? layoutAbilities : Array.Empty<AbilityDefinition>(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat() ? layoutAbilities : Array.Empty<AbilityDefinition>(),
            _ => layoutAbilities.Where(ability => this.ShouldDisplayAbility(ability, iconWindow)).ToList(),
        };
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
