namespace FFXIVAura;

internal readonly record struct PartyAuraStatusSample(
    uint StatusId,
    float Remaining,
    ushort Param,
    bool FromSelf);

internal readonly record struct PartyMemberAuraState(
    float Remaining,
    ushort Param,
    bool FromSelf);

internal readonly record struct PartyAuraAggregate(
    float Remaining,
    ushort Param,
    int Count,
    int OwnCount,
    bool FromSelf);

internal readonly record struct PartyAuraGroupAggregate(
    int Count,
    int OwnCount);

internal static class PartyAuraAggregator
{
    public static void AddMemberStatus(
        Dictionary<uint, PartyMemberAuraState> memberAuras,
        PartyAuraStatusSample sample,
        bool ownOnly)
    {
        if (sample.StatusId == 0 || (ownOnly && !sample.FromSelf))
            return;

        var remaining = Math.Max(0f, sample.Remaining);
        if (!memberAuras.TryGetValue(sample.StatusId, out var existing))
        {
            memberAuras[sample.StatusId] = new PartyMemberAuraState(remaining, sample.Param, sample.FromSelf);
            return;
        }

        memberAuras[sample.StatusId] = new PartyMemberAuraState(
            remaining > existing.Remaining ? remaining : existing.Remaining,
            remaining > existing.Remaining ? sample.Param : existing.Param,
            existing.FromSelf || sample.FromSelf);
    }

    public static void MergeMemberAuras(
        Dictionary<uint, PartyMemberAuraState> memberAuras,
        Dictionary<uint, PartyAuraAggregate> aggregateAuras)
    {
        foreach (var (statusId, memberAura) in memberAuras)
        {
            if (!aggregateAuras.TryGetValue(statusId, out var existing))
            {
                aggregateAuras[statusId] = new PartyAuraAggregate(
                    memberAura.Remaining,
                    memberAura.Param,
                    1,
                    memberAura.FromSelf ? 1 : 0,
                    memberAura.FromSelf);
                continue;
            }

            aggregateAuras[statusId] = new PartyAuraAggregate(
                memberAura.Remaining > existing.Remaining ? memberAura.Remaining : existing.Remaining,
                memberAura.Remaining > existing.Remaining ? memberAura.Param : existing.Param,
                existing.Count + 1,
                existing.OwnCount + (memberAura.FromSelf ? 1 : 0),
                existing.FromSelf || memberAura.FromSelf);
        }
    }

    public static void AddMemberAuraGroup(
        Dictionary<AuraStatusGroupKey, bool> memberGroups,
        AuraStatusGroupKey key,
        bool fromSelf)
    {
        if (!key.IsValid)
            return;

        memberGroups[key] = memberGroups.TryGetValue(key, out var existingFromSelf)
            ? existingFromSelf || fromSelf
            : fromSelf;
    }

    public static void MergeMemberAuraGroups(
        Dictionary<AuraStatusGroupKey, bool> memberGroups,
        Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> aggregateGroups)
    {
        foreach (var (key, fromSelf) in memberGroups)
        {
            if (!aggregateGroups.TryGetValue(key, out var existing))
            {
                aggregateGroups[key] = new PartyAuraGroupAggregate(1, fromSelf ? 1 : 0);
                continue;
            }

            aggregateGroups[key] = new PartyAuraGroupAggregate(
                existing.Count + 1,
                existing.OwnCount + (fromSelf ? 1 : 0));
        }
    }
}
