namespace FFXIVAura;

internal static class PartyCooldownMemberOrdering
{
    public static IReadOnlyList<PartyCooldownMemberSnapshot> PreserveInGameOrder(
        IReadOnlyList<PartyCooldownMemberSnapshot> members)
        => members.ToList();
}
