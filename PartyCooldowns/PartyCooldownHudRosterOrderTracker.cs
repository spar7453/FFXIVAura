namespace FFXIVAura;

internal readonly record struct PartyCooldownHudPartyMember(
    byte DisplayIndex,
    uint EntityId);

internal readonly record struct PartyCooldownHudRosterOrder(
    IReadOnlyList<uint> EntityIds,
    int LocalPartyCount,
    int AllianceMemberCount)
{
    public static PartyCooldownHudRosterOrder Empty { get; } = new(Array.Empty<uint>(), 0, 0);
}

internal sealed class PartyCooldownHudRosterOrderTracker
{
    public const int MaxLocalPartyMemberCount = 10;
    public const int MaxEntityOrderCount = 50;

    private uint[] lastCompleteAllianceOrder = [];
    private DateTime allianceOrderExpiresAtUtc = DateTime.MinValue;

    public PartyCooldownHudRosterOrder Resolve(
        ReadOnlySpan<PartyCooldownHudPartyMember> partyMembers,
        ReadOnlySpan<uint> raidMemberIds,
        bool isAlliance,
        DateTime nowUtc,
        TimeSpan retention,
        int completeAllianceMemberCount)
    {
        Span<PartyCooldownHudPartyMember> orderedPartyMembers = stackalloc PartyCooldownHudPartyMember[MaxLocalPartyMemberCount];
        var orderedPartyMemberCount = CopyInDisplayOrder(partyMembers, orderedPartyMembers);
        Span<uint> entityOrder = stackalloc uint[MaxEntityOrderCount];
        var entityOrderCount = 0;
        for (var index = 0; index < orderedPartyMemberCount; index++)
            AppendUniqueEntityId(entityOrder, ref entityOrderCount, orderedPartyMembers[index].EntityId);

        var localPartyCount = entityOrderCount;
        foreach (var entityId in raidMemberIds)
            AppendUniqueEntityId(entityOrder, ref entityOrderCount, entityId);

        var allianceMemberCount = this.ApplyAllianceOrderRetention(
            entityOrder,
            ref entityOrderCount,
            localPartyCount,
            isAlliance,
            nowUtc,
            retention,
            completeAllianceMemberCount);
        return new PartyCooldownHudRosterOrder(
            entityOrder[..entityOrderCount].ToArray(),
            localPartyCount,
            allianceMemberCount);
    }

    public void Reset()
    {
        this.lastCompleteAllianceOrder = [];
        this.allianceOrderExpiresAtUtc = DateTime.MinValue;
    }

    private int ApplyAllianceOrderRetention(
        Span<uint> entityOrder,
        ref int entityOrderCount,
        int localPartyCount,
        bool isAlliance,
        DateTime nowUtc,
        TimeSpan retention,
        int completeAllianceMemberCount)
    {
        var allianceMemberCount = entityOrderCount - localPartyCount;
        if (allianceMemberCount >= Math.Max(completeAllianceMemberCount, 1))
        {
            this.lastCompleteAllianceOrder = entityOrder[localPartyCount..entityOrderCount].ToArray();
            this.allianceOrderExpiresAtUtc = nowUtc.Add(retention < TimeSpan.Zero ? TimeSpan.Zero : retention);
        }
        else if (isAlliance
                 && nowUtc < this.allianceOrderExpiresAtUtc
                 && this.lastCompleteAllianceOrder.Length > 0)
        {
            foreach (var entityId in this.lastCompleteAllianceOrder)
                AppendUniqueEntityId(entityOrder, ref entityOrderCount, entityId);

            allianceMemberCount = entityOrderCount - localPartyCount;
        }
        else if (!isAlliance)
        {
            this.Reset();
        }

        return allianceMemberCount;
    }

    private static int CopyInDisplayOrder(
        ReadOnlySpan<PartyCooldownHudPartyMember> source,
        Span<PartyCooldownHudPartyMember> destination)
    {
        var count = 0;
        foreach (var member in source)
        {
            if (member.EntityId == 0 || count >= destination.Length)
                continue;

            destination[count] = member;
            var insertIndex = count;
            while (insertIndex > 0
                   && destination[insertIndex - 1].DisplayIndex > destination[insertIndex].DisplayIndex)
            {
                (destination[insertIndex - 1], destination[insertIndex]) = (destination[insertIndex], destination[insertIndex - 1]);
                insertIndex--;
            }

            count++;
        }

        return count;
    }

    private static void AppendUniqueEntityId(Span<uint> destination, ref int count, uint entityId)
    {
        if (entityId is 0 or 0xE0000000 || count >= destination.Length)
            return;

        for (var index = 0; index < count; index++)
        {
            if (destination[index] == entityId)
                return;
        }

        destination[count++] = entityId;
    }
}
