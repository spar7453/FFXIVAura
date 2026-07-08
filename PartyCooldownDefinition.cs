namespace FFXIVAura;

internal enum PartyCooldownCategory
{
    Defensive,
    Healing,
    Synergy,
}

internal enum PartyCooldownDisplayState
{
    Ready,
    Active,
    Cooldown,
}

internal sealed class PartyCooldownDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public uint ActionId { get; set; }
    public uint[] StatusIds { get; set; } = [];
    public byte Level { get; set; }
    public float Cooldown { get; set; }
    public float Duration { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint IconId { get; set; }
}

internal sealed class PartyCooldownRuntimeState
{
    public DateTime CooldownEndsAtUtc { get; set; } = DateTime.MinValue;
    public DateTime LastLogTrackedAtUtc { get; set; } = DateTime.MinValue;
}

internal readonly record struct PartyCooldownMemberSnapshot(
    string Key,
    uint EntityId,
    ushort WorldId,
    string Name,
    string ShortName,
    string Job,
    uint JobIconId);

internal readonly record struct PartyCooldownObservedAction(
    uint ActionId,
    string ActionName,
    string MatchSource,
    int ParameterIndex);

internal sealed record PartyCooldownLogObservation(
    DateTime TimestampUtc,
    uint LogMessageId,
    string SourceName,
    ushort SourceWorldId,
    string MemberName,
    uint ActionId,
    string ActionName,
    string MatchSource,
    int ParameterIndex,
    string Result,
    string Detail);

internal readonly record struct PartyCooldownActiveStatus(
    uint StatusId,
    float Remaining);

internal sealed record PartyCooldownDisplayItem(
    PartyCooldownDefinition Definition,
    PartyCooldownDisplayState State,
    float ActiveRemaining,
    float CooldownRemaining,
    float CooldownTotal);

internal sealed record PartyCooldownMemberRow(
    PartyCooldownMemberSnapshot Member,
    IReadOnlyList<PartyCooldownDisplayItem> Items);

internal sealed record PartyCooldownFrameSnapshot(
    IReadOnlyList<PartyCooldownMemberSnapshot> Members,
    DateTime TimestampUtc);
