namespace FFXIVAura;

internal static class PartyCooldownMemberOrdering
{
    public static IReadOnlyList<PartyCooldownMemberSnapshot> Sort(
        IReadOnlyList<PartyCooldownMemberSnapshot> members,
        uint localEntityId)
    {
        return members
            .Select((member, index) => new SortEntry(member, index))
            .OrderBy(entry => IsLocalPlayer(entry.Member, localEntityId) ? 0 : 1)
            .ThenBy(entry => JobInfo.PartyRoleSortOrder(entry.Member.Job))
            .ThenBy(entry => entry.OriginalIndex)
            .Select(entry => entry.Member)
            .ToList();
    }

    private static bool IsLocalPlayer(PartyCooldownMemberSnapshot member, uint localEntityId)
        => localEntityId != 0 && member.EntityId == localEntityId;

    private readonly record struct SortEntry(PartyCooldownMemberSnapshot Member, int OriginalIndex);
}
