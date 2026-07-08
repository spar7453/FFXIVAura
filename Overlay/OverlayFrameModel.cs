namespace FFXIVAura;

internal readonly record struct OverlayItemSet(
    IReadOnlyList<AbilityDefinition> Abilities,
    IReadOnlyList<AuraState> Auras)
{
    public int Count => this.Abilities.Count + this.Auras.Count;

    public bool IsEmpty => this.Count == 0;
}

internal readonly record struct OverlayFrameModel(
    IconWindowConfig Window,
    string Job,
    uint Level,
    Vector2 AreaSize,
    OverlayItemSet Display,
    OverlayItemSet Layout)
{
    public IReadOnlyList<AbilityDefinition> DisplayAbilities => this.Display.Abilities;

    public IReadOnlyList<AuraState> DisplayAuras => this.Display.Auras;

    public IReadOnlyList<AbilityDefinition> LayoutAbilities => this.Layout.Abilities;

    public IReadOnlyList<AuraState> LayoutAuras => this.Layout.Auras;

    public bool HasDisplayItems => !this.Display.IsEmpty;
}
