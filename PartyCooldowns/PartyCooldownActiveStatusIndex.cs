namespace FFXIVAura;

internal readonly record struct PartyCooldownActiveStatusObservation(
    float Remaining,
    ObservedStatusObservation Observation,
    bool CanConfirmRefresh);

internal readonly record struct PartyCooldownActiveStatusIndexDiagnostics(
    int StatusCount,
    int PartyListSourceCount,
    int PartyListRecipientCount,
    int ObjectSourceCount,
    int ObjectRecipientCount,
    int LiveStatusCount,
    int FallbackStatusCount,
    bool AbsenceConfirmed,
    int LiveOwnerCount);

internal sealed class PartyCooldownActiveStatusIndex
{
    private readonly TimeSpan cacheDuration;
    private readonly Dictionary<ulong, PartyCooldownActiveStatus> statuses = new();
    private readonly HashSet<uint> liveOwnerEntityIds = [];
    private DateTime builtAtUtc = DateTime.MinValue;
    private ulong rosterHash;
    private bool absenceConfirmed;

    public PartyCooldownActiveStatusIndex(TimeSpan cacheDuration)
    {
        if (cacheDuration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cacheDuration));

        this.cacheDuration = cacheDuration;
    }

    public bool BeginRefresh(ulong nextRosterHash, DateTime nowUtc)
    {
        if (nextRosterHash == this.rosterHash
            && this.builtAtUtc != DateTime.MinValue
            && nowUtc - this.builtAtUtc < this.cacheDuration)
        {
            return false;
        }

        this.rosterHash = nextRosterHash;
        this.builtAtUtc = nowUtc;
        this.statuses.Clear();
        this.liveOwnerEntityIds.Clear();
        this.absenceConfirmed = false;
        return true;
    }

    public void MarkLiveOwner(uint ownerEntityId)
    {
        if (ownerEntityId != 0)
            this.liveOwnerEntityIds.Add(ownerEntityId);
    }

    public void AddSample(
        uint sourceEntityId,
        uint statusId,
        float remaining,
        bool fromPartyList,
        bool onSourceMember,
        StatusSnapshotOrigin origin)
    {
        if (sourceEntityId == 0 || statusId == 0 || remaining <= 0f)
            return;

        var key = CreateStatusKey(sourceEntityId, statusId);
        var candidate = new PartyCooldownActiveStatus(
            statusId,
            remaining,
            PartyCooldownStatusSampleSelector.GetPriority(fromPartyList, onSourceMember),
            origin);
        if (this.statuses.TryGetValue(key, out var existing)
            && !PartyCooldownStatusSampleSelector.IsPreferredDuplicateSample(candidate, existing))
        {
            return;
        }

        this.statuses[key] = candidate;
    }

    public void CompleteRefresh(IEnumerable<uint> expectedOwnerEntityIds)
    {
        ArgumentNullException.ThrowIfNull(expectedOwnerEntityIds);
        this.absenceConfirmed = true;
        foreach (var ownerEntityId in expectedOwnerEntityIds)
        {
            if (this.liveOwnerEntityIds.Contains(ownerEntityId))
                continue;

            this.absenceConfirmed = false;
            break;
        }
    }

    public PartyCooldownActiveStatusObservation GetObservation(
        uint sourceEntityId,
        IReadOnlyList<uint> statusIds,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(statusIds);
        PartyCooldownActiveStatus? selected = null;
        var cacheAgeSeconds = this.builtAtUtc == DateTime.MinValue
            ? 0f
            : Math.Max(0f, (float)(nowUtc - this.builtAtUtc).TotalSeconds);
        foreach (var statusId in statusIds)
        {
            var key = CreateStatusKey(sourceEntityId, statusId);
            if (!this.statuses.TryGetValue(key, out var status))
                continue;

            var adjusted = status with { Remaining = Math.Max(0f, status.Remaining - cacheAgeSeconds) };
            if (adjusted.Remaining > 0f
                && (selected is null || PartyCooldownStatusSampleSelector.IsPreferredAcrossStatusIds(adjusted, selected.Value)))
            {
                selected = adjusted;
            }
        }

        if (selected is null)
        {
            return new PartyCooldownActiveStatusObservation(
                0f,
                this.absenceConfirmed
                    ? ObservedStatusObservation.ConfirmedAbsent
                    : ObservedStatusObservation.Unavailable,
                CanConfirmRefresh: false);
        }

        return new PartyCooldownActiveStatusObservation(
            selected.Value.Remaining,
            ObservedStatusObservation.Present,
            selected.Value.Origin == StatusSnapshotOrigin.Live);
    }

    public PartyCooldownActiveStatusIndexDiagnostics CreateDiagnostics()
    {
        var partyListSourceCount = 0;
        var partyListRecipientCount = 0;
        var objectSourceCount = 0;
        var objectRecipientCount = 0;
        var liveStatusCount = 0;
        var fallbackStatusCount = 0;
        foreach (var status in this.statuses.Values)
        {
            switch (status.Priority)
            {
                case PartyCooldownStatusSamplePriority.PartyListSource:
                    partyListSourceCount++;
                    break;
                case PartyCooldownStatusSamplePriority.PartyListRecipient:
                    partyListRecipientCount++;
                    break;
                case PartyCooldownStatusSamplePriority.ObjectSource:
                    objectSourceCount++;
                    break;
                case PartyCooldownStatusSamplePriority.ObjectRecipient:
                    objectRecipientCount++;
                    break;
            }

            if (status.Origin == StatusSnapshotOrigin.Live)
                liveStatusCount++;
            else if (status.Origin == StatusSnapshotOrigin.Fallback)
                fallbackStatusCount++;
        }

        return new PartyCooldownActiveStatusIndexDiagnostics(
            this.statuses.Count,
            partyListSourceCount,
            partyListRecipientCount,
            objectSourceCount,
            objectRecipientCount,
            liveStatusCount,
            fallbackStatusCount,
            this.absenceConfirmed,
            this.liveOwnerEntityIds.Count);
    }

    public void Reset()
    {
        this.statuses.Clear();
        this.liveOwnerEntityIds.Clear();
        this.builtAtUtc = DateTime.MinValue;
        this.rosterHash = 0;
        this.absenceConfirmed = false;
    }

    private static ulong CreateStatusKey(uint sourceEntityId, uint statusId)
        => ((ulong)sourceEntityId << 32) | statusId;
}
