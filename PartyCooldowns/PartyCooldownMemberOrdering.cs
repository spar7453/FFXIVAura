namespace FFXIVAura;

internal static class PartyCooldownMemberOrdering
{
    public static IReadOnlyList<PartyCooldownMemberSnapshot> PreserveInGameOrder(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        IReadOnlyList<uint>? displayedEntityIds = null)
    {
        if (displayedEntityIds is null || displayedEntityIds.Count == 0)
            return members;

        var ordered = members.ToList();
        var destinationIndex = 0;
        foreach (var entityId in displayedEntityIds)
        {
            if (entityId == 0)
                continue;

            var sourceIndex = -1;
            for (var index = destinationIndex; index < ordered.Count; index++)
            {
                if (ordered[index].EntityId != entityId)
                    continue;

                sourceIndex = index;
                break;
            }

            if (sourceIndex < 0)
                continue;

            var member = ordered[sourceIndex];
            ordered.RemoveAt(sourceIndex);
            ordered.Insert(destinationIndex, member);
            destinationIndex++;
        }

        return ordered;
    }
}
