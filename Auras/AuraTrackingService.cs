namespace FFXIVAura;

internal enum AuraTrackingCoverage
{
    None,
    Exact,
    Group,
}

internal sealed class AuraTrackingService
{
    private readonly IAuraCatalog auraCatalog;
    private readonly IAuraTrackedGroupProvider trackedGroupProvider;

    public AuraTrackingService(
        IAuraCatalog auraCatalog,
        IAuraTrackedGroupProvider trackedGroupProvider)
    {
        this.auraCatalog = auraCatalog;
        this.trackedGroupProvider = trackedGroupProvider;
    }

    public bool Track(IconWindowConfig iconWindow, uint statusId, bool exact)
    {
        if (statusId == 0 || this.GetCoverage(iconWindow, statusId, exact) != AuraTrackingCoverage.None)
            return false;

        iconWindow.TrackedStatusIds.Add(statusId);
        if (exact)
            iconWindow.ExactTrackedStatusIds.Add(statusId);

        this.trackedGroupProvider.InvalidateTrackedGroups(iconWindow);
        return true;
    }

    public void Untrack(IconWindowConfig iconWindow, AuraStatusGroup group)
    {
        var exactStatusIds = iconWindow.ExactTrackedStatusIds.ToHashSet();
        var positionStatusIds = group.MemberStatusIds.ToHashSet();
        if (!group.IsExact)
            positionStatusIds.ExceptWith(exactStatusIds);

        var removedStatusIds = iconWindow.TrackedStatusIds
            .Where(id => group.IsExact
                ? id == group.StatusId && exactStatusIds.Contains(id)
                : !exactStatusIds.Contains(id)
                  && (positionStatusIds.Contains(id)
                      || AuraStatusGroups.HaveSameIdentity(this.auraCatalog.GetDefinition(id).GroupKey, group.Key)))
            .ToList();
        if (removedStatusIds.Count == 0)
            return;

        iconWindow.TrackedStatusIds.RemoveAll(id => removedStatusIds.Contains(id));
        iconWindow.ExactTrackedStatusIds.RemoveAll(id => removedStatusIds.Contains(id));
        this.trackedGroupProvider.InvalidateTrackedGroups(iconWindow);
        foreach (var removedStatusId in removedStatusIds)
            positionStatusIds.Add(removedStatusId);

        var groupPrefix = OverlayPositionKeys.WindowPrefix(iconWindow.Id);
        foreach (var groupKey in iconWindow.AuraPositionsByRole.Keys.ToList())
        {
            if (!groupKey.StartsWith(groupPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var positions))
                continue;

            foreach (var groupStatusId in positionStatusIds)
                positions.Remove(OverlayPositionKeys.Aura(groupStatusId));

            if (positions.Count == 0)
                iconWindow.AuraPositionsByRole.Remove(groupKey);
        }
    }

    public AuraTrackingCoverage GetCoverage(IconWindowConfig iconWindow, uint statusId, bool exact)
    {
        if (statusId == 0)
            return AuraTrackingCoverage.None;

        var definition = this.auraCatalog.GetDefinition(statusId);
        foreach (var group in this.trackedGroupProvider.GetTrackedGroups(iconWindow))
        {
            var sameStatus = group.StatusId == statusId;
            var sameIdentity = AuraStatusGroups.HaveSameIdentity(group.Key, definition.GroupKey);
            if (exact)
            {
                if (group.IsExact && sameStatus)
                    return AuraTrackingCoverage.Exact;

                if (!group.IsExact && (sameStatus || sameIdentity))
                    return AuraTrackingCoverage.Group;

                continue;
            }

            if (sameStatus || sameIdentity)
                return group.IsExact ? AuraTrackingCoverage.Exact : AuraTrackingCoverage.Group;
        }

        return AuraTrackingCoverage.None;
    }

    public bool SetMode(IconWindowConfig iconWindow, uint statusId, bool exact)
    {
        if (statusId == 0)
            return false;

        var selectedKey = this.auraCatalog.GetDefinition(statusId).GroupKey;
        var change = AuraTrackingModeTransitions.Apply(
            iconWindow.TrackedStatusIds,
            iconWindow.ExactTrackedStatusIds,
            statusId,
            exact,
            trackedStatusId => AuraStatusGroups.HaveSameIdentity(
                this.auraCatalog.GetDefinition(trackedStatusId).GroupKey,
                selectedKey));
        if (!change.Changed)
            return false;

        TransferPositionToStatus(iconWindow, change.ReplacedStatusIds, statusId);
        this.trackedGroupProvider.InvalidateTrackedGroups(iconWindow);
        return true;
    }

    private static void TransferPositionToStatus(
        IconWindowConfig iconWindow,
        IReadOnlyList<uint> replacedStatusIds,
        uint statusId)
    {
        var groupPrefix = OverlayPositionKeys.WindowPrefix(iconWindow.Id);
        var destinationKey = OverlayPositionKeys.Aura(statusId);
        foreach (var (scopeKey, positions) in iconWindow.AuraPositionsByRole)
        {
            if (!scopeKey.StartsWith(groupPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            Vector2? preservedPosition = positions.TryGetValue(destinationKey, out var selectedPosition)
                ? selectedPosition
                : null;
            foreach (var replacedStatusId in replacedStatusIds)
            {
                var positionKey = OverlayPositionKeys.Aura(replacedStatusId);
                if (preservedPosition is null && positions.TryGetValue(positionKey, out var position))
                    preservedPosition = position;

                positions.Remove(positionKey);
            }

            if (preservedPosition is not null)
                positions[destinationKey] = preservedPosition.Value;
        }
    }
}
