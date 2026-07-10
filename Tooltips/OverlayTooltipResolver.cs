namespace FFXIVAura;

internal enum OverlayTooltipCandidateKind
{
    None,
    Ability,
    Aura,
    PartyCooldown,
}

internal readonly record struct OverlayTooltipCandidate(
    OverlayTooltipCandidateKind Kind,
    AbilityDefinition? Ability,
    AuraState Aura,
    PartyCooldownDefinition? PartyCooldown)
{
    public static OverlayTooltipCandidate None => default;

    public bool HasValue => this.Kind != OverlayTooltipCandidateKind.None;

    public static OverlayTooltipCandidate ForAbility(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        return new OverlayTooltipCandidate(OverlayTooltipCandidateKind.Ability, ability, default, null);
    }

    public static OverlayTooltipCandidate ForAura(AuraState aura)
    {
        return new OverlayTooltipCandidate(OverlayTooltipCandidateKind.Aura, null, aura, null);
    }

    public static OverlayTooltipCandidate ForPartyCooldown(PartyCooldownDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new OverlayTooltipCandidate(OverlayTooltipCandidateKind.PartyCooldown, null, default, definition);
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
