using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyAuraTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyAuraAggregator counts party members once", PartyAuraAggregatorCountsPartyMembersOnce),
        ("PartyAuraAggregator builds own-only aggregates", PartyAuraAggregatorBuildsOwnOnlyAggregates),
    ];

    private static void PartyAuraAggregatorCountsPartyMembersOnce()
    {
        var member = new Dictionary<uint, PartyMemberAuraState>();
        var aggregate = new Dictionary<uint, PartyAuraAggregate>();

        PartyAuraAggregator.AddMemberStatus(member, new PartyAuraStatusSample(42, 10, 100, false), ownOnly: false);
        PartyAuraAggregator.AddMemberStatus(member, new PartyAuraStatusSample(42, 5, 200, true), ownOnly: false);
        PartyAuraAggregator.MergeMemberAuras(member, aggregate);

        Equal(1, aggregate[42].Count);
        Equal(1, aggregate[42].OwnCount);
        Near(10, aggregate[42].Remaining);
        Equal((ushort)100, aggregate[42].Param);
        True(aggregate[42].FromSelf, "member should still count as own when a shorter own status exists");
    }

    private static void PartyAuraAggregatorBuildsOwnOnlyAggregates()
    {
        var memberOne = new Dictionary<uint, PartyMemberAuraState>();
        var memberTwo = new Dictionary<uint, PartyMemberAuraState>();
        var aggregate = new Dictionary<uint, PartyAuraAggregate>();

        PartyAuraAggregator.AddMemberStatus(memberOne, new PartyAuraStatusSample(42, 30, 100, false), ownOnly: true);
        PartyAuraAggregator.AddMemberStatus(memberOne, new PartyAuraStatusSample(42, 20, 200, true), ownOnly: true);
        PartyAuraAggregator.AddMemberStatus(memberTwo, new PartyAuraStatusSample(42, 40, 300, true), ownOnly: true);
        PartyAuraAggregator.MergeMemberAuras(memberOne, aggregate);
        PartyAuraAggregator.MergeMemberAuras(memberTwo, aggregate);

        Equal(2, aggregate[42].Count);
        Equal(2, aggregate[42].OwnCount);
        Near(40, aggregate[42].Remaining);
        Equal((ushort)300, aggregate[42].Param);
        True(aggregate[42].FromSelf, "own-only aggregate should be marked from self");
    }
}
