namespace FFXIVAura;

internal readonly record struct AuraTrackingModeChange(
    bool Changed,
    uint StatusId,
    IReadOnlyList<uint> ReplacedStatusIds);

internal static class AuraTrackingModeTransitions
{
    public static AuraTrackingModeChange Apply(
        List<uint> trackedStatusIds,
        List<uint> exactTrackedStatusIds,
        uint statusId,
        bool exact,
        Func<uint, bool> hasSameIdentity)
    {
        ArgumentNullException.ThrowIfNull(trackedStatusIds);
        ArgumentNullException.ThrowIfNull(exactTrackedStatusIds);
        ArgumentNullException.ThrowIfNull(hasSameIdentity);
        if (statusId == 0)
            return default;

        var replacedStatusIds = trackedStatusIds
            .Where(id => id == statusId || hasSameIdentity(id))
            .Distinct()
            .ToArray();
        var alreadyInRequestedMode = replacedStatusIds.Length == 1
                                     && replacedStatusIds[0] == statusId
                                     && exactTrackedStatusIds.Contains(statusId) == exact;
        if (alreadyInRequestedMode)
            return new AuraTrackingModeChange(false, statusId, replacedStatusIds);

        var insertIndex = trackedStatusIds.FindIndex(id => id == statusId || hasSameIdentity(id));
        if (insertIndex < 0)
            insertIndex = trackedStatusIds.Count;

        trackedStatusIds.RemoveAll(id => id == statusId || hasSameIdentity(id));
        exactTrackedStatusIds.RemoveAll(id => id == statusId || hasSameIdentity(id));
        trackedStatusIds.Insert(Math.Clamp(insertIndex, 0, trackedStatusIds.Count), statusId);
        if (exact)
            exactTrackedStatusIds.Add(statusId);

        return new AuraTrackingModeChange(true, statusId, replacedStatusIds);
    }
}
