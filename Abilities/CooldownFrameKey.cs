namespace FFXIVAura;

internal readonly struct CooldownFrameKey : IEquatable<CooldownFrameKey>
{
    public CooldownFrameKey(string abilityId, uint actionId)
    {
        this.AbilityId = abilityId ?? string.Empty;
        this.ActionId = actionId;
    }

    public string AbilityId { get; }

    public uint ActionId { get; }

    public bool Equals(CooldownFrameKey other)
        => this.ActionId == other.ActionId
           && string.Equals(this.AbilityId, other.AbilityId, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj)
        => obj is CooldownFrameKey other && this.Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.AbilityId ?? string.Empty),
            this.ActionId);

    public static bool operator ==(CooldownFrameKey left, CooldownFrameKey right)
        => left.Equals(right);

    public static bool operator !=(CooldownFrameKey left, CooldownFrameKey right)
        => !left.Equals(right);
}
