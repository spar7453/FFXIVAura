namespace FFXIVAura;

internal sealed class CooldownFrameService
{
    private readonly IGameActionRuntime runtime;
    private readonly Dictionary<CooldownFrameKey, CooldownState> cache = new();

    public CooldownFrameService(IGameActionRuntime runtime)
    {
        this.runtime = runtime;
    }

    public int CachedCount => this.cache.Count;

    public void BeginFrame()
        => this.cache.Clear();

    public bool TryGetCached(AbilityDefinition ability, out CooldownState state)
        => this.cache.TryGetValue(new CooldownFrameKey(ability.Id, ability.ActionId), out state);

    public CooldownState EvaluateAndCache(AbilityDefinition ability, in PlayerFrameContext playerContext)
    {
        var state = this.runtime.ReadCooldown(ability, playerContext);
        this.cache[new CooldownFrameKey(ability.Id, ability.ActionId)] = state;
        return state;
    }
}
