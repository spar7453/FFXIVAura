namespace FFXIVAura;

internal sealed class OverlayFrameBuffers
{
    public List<AbilityDefinition> LayoutAbilities { get; } = [];

    public List<AbilityDefinition> DisplayAbilities { get; } = [];

    public List<AuraState> LayoutAuras { get; } = [];

    public List<AuraState> DisplayAuras { get; } = [];

    public void Clear()
    {
        this.LayoutAbilities.Clear();
        this.DisplayAbilities.Clear();
        this.LayoutAuras.Clear();
        this.DisplayAuras.Clear();
    }
}
