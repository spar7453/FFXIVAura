namespace FFXIVAura;

public sealed partial class Plugin
{
    private CooldownState GetCooldown(AbilityDefinition ability)
    {
        if (this.cooldownFrameService.TryGetCached(ability, out var cached))
            return cached;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.Cooldown);
        var state = this.cooldownFrameService.EvaluateAndCache(ability, this.playerFrameContext);
        this.performanceProfiler.EndSection(PerformanceProfileSection.Cooldown, profileStart);
        this.performanceStats.CountCooldownCalculation();
        return state;
    }
}
