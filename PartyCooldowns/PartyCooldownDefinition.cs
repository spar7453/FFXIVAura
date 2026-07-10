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

internal enum PartyCooldownIgnoredLogReason
{
    None,
    Other,
    MemberNotFound,
    OwnerNotFound,
    CandidateMissing,
    NotUsableForJob,
    NotTrackedByWindow,
    Ambiguous,
}

internal sealed class PartyCooldownDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public uint ActionId { get; set; }
    public uint[] StatusIds { get; set; } = [];
    public string ReplacementGroup { get; set; } = string.Empty;
    public byte Level { get; set; }
    public float Cooldown { get; set; }
    public float Duration { get; set; }
    public byte Charges { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public uint IconId { get; set; }
}

internal sealed class PartyCooldownRuntimeState
{
    public Queue<DateTime> ChargeRecoveryEndsAtUtc { get; } = new();
    public uint MaxCharges { get; set; }
    public DateTime LastChargeRecoveryEndsAtUtc { get; set; } = DateTime.MinValue;
    public DateTime LastObservedUseAtUtc { get; set; } = DateTime.MinValue;
    public DateTime LastLogTrackedAtUtc { get; set; } = DateTime.MinValue;
    public bool ActiveObservedLastFrame { get; set; }
}

internal readonly record struct PartyCooldownMemberSnapshot(
    string Key,
    uint EntityId,
    ushort WorldId,
    string Name,
    string ShortName,
    string Job,
    uint JobIconId,
    string AllianceGroup);

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
    PartyCooldownIgnoredLogReason IgnoredReason,
    PartyCooldownRosterDiagnostics RosterDiagnostics,
    string Detail);

internal readonly record struct PartyCooldownActiveStatus(
    uint StatusId,
    float Remaining);

internal sealed record PartyCooldownDisplayItem(
    PartyCooldownDefinition Definition,
    PartyCooldownDisplayState State,
    float ActiveRemaining,
    float CooldownRemaining,
    float CooldownTotal,
    uint CurrentCharges,
    uint MaxCharges);

internal sealed record PartyCooldownMemberRow(
    PartyCooldownMemberSnapshot Member,
    IReadOnlyList<PartyCooldownDisplayItem> Items);

internal sealed record PartyCooldownFrameSnapshot(
    IReadOnlyList<PartyCooldownMemberSnapshot> Members,
    IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers,
    PartyCooldownRosterDiagnostics RosterDiagnostics,
    DateTime TimestampUtc);
