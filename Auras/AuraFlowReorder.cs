namespace FFXIVAura;

internal static class AuraFlowReorder
{
    public static int FindNearestVisibleIndex(
        IconAlignment alignment,
        Vector2 pointerPosition,
        int visibleCount,
        Vector2 areaSize,
        float iconSize,
        float gap)
    {
        if (visibleCount <= 0)
            return -1;

        var nearestIndex = 0;
        var nearestDistanceSquared = float.MaxValue;
        var iconCenterOffset = new Vector2(iconSize * 0.5f);
        for (var index = 0; index < visibleCount; index++)
        {
            var position = OverlayLayout.GetCompactPosition(
                alignment,
                index,
                visibleCount,
                areaSize,
                iconSize,
                gap);
            var distanceSquared = Vector2.DistanceSquared(pointerPosition, position + iconCenterOffset);
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestIndex = index;
            nearestDistanceSquared = distanceSquared;
        }

        return nearestIndex;
    }

    public static bool MoveTrackedStatus(List<uint> trackedStatusIds, uint sourceStatusId, uint targetStatusId)
    {
        ArgumentNullException.ThrowIfNull(trackedStatusIds);
        if (sourceStatusId == 0 || targetStatusId == 0 || sourceStatusId == targetStatusId)
            return false;

        var sourceIndex = trackedStatusIds.IndexOf(sourceStatusId);
        var targetIndex = trackedStatusIds.IndexOf(targetStatusId);
        if (sourceIndex < 0 || targetIndex < 0)
            return false;

        trackedStatusIds.RemoveAt(sourceIndex);
        trackedStatusIds.Insert(Math.Clamp(targetIndex, 0, trackedStatusIds.Count), sourceStatusId);
        return true;
    }
}
