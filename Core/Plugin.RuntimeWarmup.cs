namespace FFXIVAura;

public sealed partial class Plugin
{
    private void WarmOverlayRuntimeData()
    {
        var job = this.playerFrameContext.Job;
        var level = this.playerFrameContext.EffectiveLevel;
        if (!string.IsNullOrWhiteSpace(job)
            && this.config.IconWindows.Any(window => window.Role == IconWindowRole.SkillCooldowns))
        {
            var candidates = this.abilityCatalog.GetJobCandidates(job, level);
            this.WarmOverlayActionTooltip(candidates);
        }

        if (this.config.IconWindows.Any(window => IconWindowRoles.IsStandardAuraRole(window.Role)
                                                  && window.TrackedStatusIds.Count > 0))
        {
            _ = this.auraCatalog.EnsureStatusIdentityIndex();
        }
    }

    private void WarmOverlayActionTooltip(IReadOnlyList<AbilityDefinition> candidates)
    {
        if (!this.config.ShowTooltips)
            return;

        foreach (var ability in candidates)
        {
            if (ability.ActionId == 0 || this.tooltipContentService.ContainsActionModel(ability.ActionId))
                continue;

            _ = this.tooltipContentService.GetActionModel(
                ability.ActionId,
                ability.Name,
                ability.IconId,
                string.Empty);
            return;
        }
    }
}
