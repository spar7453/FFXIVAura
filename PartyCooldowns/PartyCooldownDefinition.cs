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

internal enum PartyCooldownUseObservationSource
{
    Unknown,
    CombatLog,
    ActiveStatus,
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
    LocalPlayerExcluded,
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
    public PartyCooldownUseObservationSource LastObservedUseSource { get; set; }
    public DateTime LastLogTrackedAtUtc { get; set; } = DateTime.MinValue;
    public bool ActiveObservedLastFrame { get; set; }
    public DateTime ActiveStatusLastSeenAtUtc { get; set; } = DateTime.MinValue;
    public ObservedStatusTimerState ActiveTimer { get; } = new();
    public DateTime ActiveTimerLastUseAtUtc { get; set; } = DateTime.MinValue;
}

internal readonly record struct PartyCooldownRuntimeKey(
    string MemberKey,
    string DefinitionId);

internal sealed class PartyCooldownRuntimeKeyComparer : IEqualityComparer<PartyCooldownRuntimeKey>
{
    public static PartyCooldownRuntimeKeyComparer Instance { get; } = new();

    public bool Equals(PartyCooldownRuntimeKey x, PartyCooldownRuntimeKey y)
        => string.Equals(x.MemberKey, y.MemberKey, StringComparison.OrdinalIgnoreCase)
           && string.Equals(x.DefinitionId, y.DefinitionId, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(PartyCooldownRuntimeKey value)
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.MemberKey ?? string.Empty),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.DefinitionId ?? string.Empty));
}

internal readonly record struct PartyCooldownMemberSnapshot(
    string Key,
    uint EntityId,
    ulong ContentId,
    ushort WorldId,
    string Name,
    string ShortName,
    string Job,
    uint Level,
    uint JobIconId,
    string AllianceGroup)
{
    // A non-zero member level is authoritative. The local effective level is only a
    // missing-data fallback, not a cap: another member can legitimately be above it.
    public uint ResolveEffectiveLevel(uint missingLevelFallback)
        => this.Level > 0 ? this.Level : missingLevelFallback;
}

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
    float Remaining,
    PartyCooldownStatusSamplePriority Priority,
    StatusSnapshotOrigin Origin = StatusSnapshotOrigin.Live);

internal readonly record struct PartyCooldownDisplayItem(
    PartyCooldownDefinition Definition,
    PartyCooldownDisplayState State,
    float ActiveRemaining,
    float CooldownRemaining,
    float CooldownTotal,
    uint CurrentCharges,
    uint MaxCharges);

internal readonly record struct PartyCooldownMemberRow(
    PartyCooldownMemberSnapshot Member,
    IReadOnlyList<PartyCooldownDisplayItem> Items) : IPartyCooldownBoardLayoutRow
{
    public string AllianceGroup => this.Member.AllianceGroup;

    public int ItemCount => this.Items.Count;
}

internal readonly struct PartyCooldownDefinitionScopeKey : IEquatable<PartyCooldownDefinitionScopeKey>
{
    public PartyCooldownDefinitionScopeKey(PartyCooldownCategory category, string job, uint level)
    {
        this.Category = category;
        this.Job = job?.Trim() ?? string.Empty;
        this.Level = level;
    }

    public PartyCooldownCategory Category { get; }

    public string Job { get; }

    public uint Level { get; }

    public bool Equals(PartyCooldownDefinitionScopeKey other)
        => this.Category == other.Category
           && this.Level == other.Level
           && string.Equals(this.Job, other.Job, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj)
        => obj is PartyCooldownDefinitionScopeKey other && this.Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(
            this.Category,
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.Job ?? string.Empty),
            this.Level);
}

internal readonly record struct PartyCooldownCategoryLevelKey(
    PartyCooldownCategory Category,
    uint Level);

internal readonly record struct PartyCooldownFrameSnapshot(
    IReadOnlyList<PartyCooldownMemberSnapshot> Members,
    IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers,
    PartyCooldownRosterDiagnostics RosterDiagnostics,
    DateTime TimestampUtc);
