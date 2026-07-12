namespace FFXIVAura;

internal static class PartyCooldownMemberOrdering
{
    public static IReadOnlyList<PartyCooldownMemberSnapshot> PreserveInGameOrder(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        IReadOnlyList<uint>? displayedEntityIds = null)
    {
        var ordered = members.ToList();
        ApplyInGameOrder(ordered, displayedEntityIds);
        return ordered;
    }

    public static void ApplyInGameOrder(
        List<PartyCooldownMemberSnapshot> members,
        IReadOnlyList<uint>? displayedEntityIds = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (displayedEntityIds is null || displayedEntityIds.Count == 0)
            return;

        var destinationIndex = 0;
        foreach (var entityId in displayedEntityIds)
        {
            if (entityId == 0)
                continue;

            var sourceIndex = -1;
            for (var index = destinationIndex; index < members.Count; index++)
            {
                if (members[index].EntityId != entityId)
                    continue;

                sourceIndex = index;
                break;
            }

            if (sourceIndex < 0)
                continue;

            var member = members[sourceIndex];
            members.RemoveAt(sourceIndex);
            members.Insert(destinationIndex, member);
            destinationIndex++;
        }
    }
}
