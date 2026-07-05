namespace FFXIVAura;

internal enum OverlayTooltipCandidateKind
{
    None,
    Ability,
    Aura,
}

internal readonly record struct OverlayTooltipCandidate(
    OverlayTooltipCandidateKind Kind,
    AbilityDefinition? Ability,
    AuraState Aura)
{
    public static OverlayTooltipCandidate None => default;

    public bool HasValue => this.Kind != OverlayTooltipCandidateKind.None;

    public static OverlayTooltipCandidate ForAbility(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        return new OverlayTooltipCandidate(OverlayTooltipCandidateKind.Ability, ability, default);
    }

    public static OverlayTooltipCandidate ForAura(AuraState aura)
    {
        return new OverlayTooltipCandidate(OverlayTooltipCandidateKind.Aura, null, aura);
    }
}

internal sealed class OverlayTooltipResolver
{
    private OverlayTooltipCandidate candidate = OverlayTooltipCandidate.None;

    public void Clear()
    {
        this.candidate = OverlayTooltipCandidate.None;
    }

    public void Register(OverlayTooltipCandidate nextCandidate)
    {
        if (!nextCandidate.HasValue)
            return;

        this.candidate = nextCandidate;
    }

    public bool TryConsume(out OverlayTooltipCandidate consumed)
    {
        consumed = this.candidate;
        this.Clear();
        return consumed.HasValue;
    }
}
