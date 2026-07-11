namespace FFXIVAura;

internal readonly record struct AuraStatusGroupKey(
    string NormalizedName,
    uint IconId,
    byte StatusCategory)
{
    public bool IsValid => !string.IsNullOrEmpty(this.NormalizedName);

    public static AuraStatusGroupKey Create(string name, uint iconId, byte statusCategory)
        => new(
            (name?.Trim() ?? string.Empty).ToUpperInvariant(),
            iconId,
            statusCategory);
}

internal readonly record struct AuraStatusDefinition
{
    public AuraStatusDefinition(string name, uint iconId, byte statusCategory)
    {
        this.Name = name;
        this.IconId = iconId;
        this.StatusCategory = statusCategory;
        this.GroupKey = AuraStatusGroupKey.Create(name, iconId, statusCategory);
    }

    public string Name { get; }

    public uint IconId { get; }

    public byte StatusCategory { get; }

    public AuraStatusGroupKey GroupKey { get; }
}

internal sealed record AuraStatusGroup(
    uint StatusId,
    string Name,
    uint IconId,
    AuraStatusGroupKey Key,
    bool IsExact,
    IReadOnlyList<uint> MemberStatusIds)
{
    public bool Contains(uint statusId)
        => this.MemberStatusIds.Contains(statusId);
}

internal sealed record AuraStatusGroupCacheEntry(
    uint[] TrackedStatusIds,
    uint[] ExactTrackedStatusIds,
    int StatusIndexGeneration,
    IReadOnlyList<AuraStatusGroup> Groups)
{
    public bool Matches(
        IReadOnlyList<uint> trackedStatusIds,
        IReadOnlyList<uint> exactTrackedStatusIds,
        int statusIndexGeneration)
    {
        if (this.StatusIndexGeneration != statusIndexGeneration
            || this.TrackedStatusIds.Length != trackedStatusIds.Count
            || this.ExactTrackedStatusIds.Length != exactTrackedStatusIds.Count)
        {
            return false;
        }

        for (var index = 0; index < this.TrackedStatusIds.Length; index++)
        {
            if (this.TrackedStatusIds[index] != trackedStatusIds[index])
                return false;
        }

        for (var index = 0; index < this.ExactTrackedStatusIds.Length; index++)
        {
            if (this.ExactTrackedStatusIds[index] != exactTrackedStatusIds[index])
                return false;
        }

        return true;
    }
}

internal static class AuraStatusGroups
{
    public static List<AuraStatusGroup> Build(
        IReadOnlyList<uint> trackedStatusIds,
        IReadOnlyCollection<uint> exactTrackedStatusIds,
        Func<uint, AuraStatusDefinition> getDefinition,
        Func<AuraStatusGroupKey, IReadOnlyList<uint>> getStatusIdsByGroup)
    {
        ArgumentNullException.ThrowIfNull(trackedStatusIds);
        ArgumentNullException.ThrowIfNull(exactTrackedStatusIds);
        ArgumentNullException.ThrowIfNull(getDefinition);
        ArgumentNullException.ThrowIfNull(getStatusIdsByGroup);

        var groups = new List<AuraStatusGroup>(trackedStatusIds.Count);
        var seenStatusIds = new HashSet<uint>();
        var seenGroups = new HashSet<AuraStatusGroupKey>();
        var exactStatusIds = exactTrackedStatusIds.ToHashSet();
        foreach (var statusId in trackedStatusIds)
        {
            if (statusId == 0 || !seenStatusIds.Add(statusId))
                continue;

            var definition = getDefinition(statusId);
            var name = definition.Name?.Trim() ?? string.Empty;
            var key = definition.GroupKey;
            var isExact = exactStatusIds.Contains(statusId);
            if (!isExact && key.IsValid && !seenGroups.Add(key))
                continue;

            var memberStatusIds = isExact || !key.IsValid
                ? [statusId]
                : getStatusIdsByGroup(key)
                    .Append(statusId)
                    .Where(id => id > 0)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();
            groups.Add(new AuraStatusGroup(
                statusId,
                name.Length > 0 ? name : $"Status {statusId}",
                definition.IconId,
                key,
                isExact,
                memberStatusIds));
        }

        return groups;
    }

    public static bool HaveSameIdentity(AuraStatusGroupKey first, AuraStatusGroupKey second)
        => first.IsValid && first == second;
}

internal readonly record struct AuraStatusGroupActiveState(
    uint StatusId,
    uint IconId,
    float Remaining,
    ushort Param,
    int Count,
    int OwnCount,
    bool FromSelf);

internal struct AuraStatusGroupStateBuilder
{
    private readonly AuraStatusGroup group;
    private uint activeStatusId;
    private uint activeIconId;
    private float remaining;
    private ushort param;
    private int count;
    private int ownCount;
    private bool present;
    private bool fromSelf;
    private bool selectedFromSelf;

    public AuraStatusGroupStateBuilder(AuraStatusGroup group)
    {
        this.group = group ?? throw new ArgumentNullException(nameof(group));
    }

    public void Add(AuraStatusGroupActiveState state)
    {
        if (state.StatusId == 0)
            return;

        var remaining = Math.Max(0f, state.Remaining);
        var shouldSelect = !this.present
                           || remaining > this.remaining
                           || (Math.Abs(remaining - this.remaining) < 0.001f
                               && state.FromSelf
                               && !this.selectedFromSelf)
                           || (Math.Abs(remaining - this.remaining) < 0.001f
                               && state.FromSelf == this.selectedFromSelf
                               && state.StatusId < this.activeStatusId);
        if (shouldSelect)
        {
            this.activeStatusId = state.StatusId;
            this.activeIconId = state.IconId;
            this.remaining = remaining;
            this.param = state.Param;
            this.selectedFromSelf = state.FromSelf;
        }

        this.count += Math.Max(0, state.Count);
        this.ownCount += Math.Max(0, state.OwnCount);
        this.fromSelf |= state.FromSelf;
        this.present = true;
    }

    public readonly AuraState Build()
    {
        if (!this.present)
        {
            return new AuraState(
                this.group.StatusId,
                this.group.Name,
                this.group.IconId,
                0f,
                0,
                0,
                0,
                false,
                false);
        }

        return new AuraState(
            this.group.StatusId,
            this.group.Name,
            this.activeIconId == 0 ? this.group.IconId : this.activeIconId,
            this.remaining,
            this.param,
            this.count,
            this.ownCount,
            true,
            this.fromSelf,
            this.activeStatusId);
    }
}
