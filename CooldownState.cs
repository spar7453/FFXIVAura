namespace FFXIVAura;

public readonly record struct CooldownState(
    uint DisplayActionId,
    uint DisplayIconId,
    float Total,
    float Remaining,
    uint CurrentCharges,
    uint MaxCharges,
    bool Highlighted,
    bool Adjusted,
    bool ConditionUnavailable)
{
    public bool IsCooling => Remaining > 0.05f;
    public float Progress => Total <= 0.05f ? 0f : Math.Clamp(Remaining / Total, 0f, 1f);
}
