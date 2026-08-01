namespace FFXIVAura;

public sealed partial class Plugin
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
        if (!this.overlayFrameBuffersByWindow.TryGetValue(iconWindow.Id, out var buffers))
        {
            buffers = new OverlayFrameBuffers();
            this.overlayFrameBuffersByWindow[iconWindow.Id] = buffers;
        }

        buffers.Clear();
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
        {
            foreach (var ability in this.GetVisibleAbilities(job, level, iconWindow))
                buffers.LayoutAbilities.Add(ability);

            var displayAbilities = this.GetDisplayAbilitiesFromLayout(
                buffers.LayoutAbilities,
                buffers.DisplayAbilities,
                iconWindow);
            return new OverlayFrameModel(
                iconWindow,
                job,
                level,
                areaSize,
                new OverlayItemSet(displayAbilities, Array.Empty<AuraState>()),
                new OverlayItemSet(buffers.LayoutAbilities, Array.Empty<AuraState>()));
        }

        this.auraSearchService.UpdateCurrentSeenTimes(iconWindow);
        foreach (var aura in this.GetLayoutAuras(iconWindow))
        {
            buffers.LayoutAuras.Add(aura);
            if (this.ShouldDisplayAura(aura, iconWindow)
                && (aura.Present || ShouldShowMissingAura(iconWindow)))
            {
                buffers.DisplayAuras.Add(aura);
            }
        }

        return new OverlayFrameModel(
            iconWindow,
            job,
            level,
            areaSize,
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), buffers.DisplayAuras),
            new OverlayItemSet(Array.Empty<AbilityDefinition>(), buffers.LayoutAuras));
    }

    private IReadOnlyList<AbilityDefinition> GetDisplayAbilitiesFromLayout(
        IReadOnlyList<AbilityDefinition> layoutAbilities,
        List<AbilityDefinition> displayBuffer,
        IconWindowConfig iconWindow)
    {
        switch (iconWindow.DisplayCondition)
        {
            case IconDisplayCondition.Always:
                return layoutAbilities;
            case IconDisplayCondition.InCombat:
                return this.playerFrameContext.IsInCombat ? layoutAbilities : Array.Empty<AbilityDefinition>();
            case IconDisplayCondition.OutOfCombat:
                return !this.playerFrameContext.IsInCombat ? layoutAbilities : Array.Empty<AbilityDefinition>();
        }

        foreach (var ability in layoutAbilities)
        {
            if (this.ShouldDisplayAbility(ability, iconWindow))
                displayBuffer.Add(ability);
        }

        return displayBuffer;
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
        return this.auraSearchService.GetTrackedGroups(iconWindow)
            .Select(group => this.GetAuraState(iconWindow, group));
    }
}
