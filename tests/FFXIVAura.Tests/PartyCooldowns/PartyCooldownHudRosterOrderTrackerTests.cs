using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownHudRosterOrderTrackerTests
{
    private static readonly DateTime TimestampUtc = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(3);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("HUD roster order sorts and deduplicates live members", SortsAndDeduplicatesLiveMembers),
        ("HUD roster order retains a complete alliance briefly", RetainsCompleteAllianceBriefly),
        ("HUD roster order expires at the retention boundary", ExpiresAtRetentionBoundary),
        ("HUD roster order clears retained members outside alliance", ClearsRetainedMembersOutsideAlliance),
        ("HUD roster order replaces retention with newer complete data", ReplacesRetentionWithNewerCompleteData),
    ];

    private static void SortsAndDeduplicatesLiveMembers()
    {
        var tracker = new PartyCooldownHudRosterOrderTracker();
        PartyCooldownHudPartyMember[] partyMembers =
        [
            new(2, 200),
            new(0, 100),
            new(1, 150),
            new(3, 0),
            new(4, 0xE0000000),
        ];
        uint[] raidMemberIds = [300, 150, 0, 0xE0000000, 400, 300];

        var order = tracker.Resolve(
            partyMembers,
            raidMemberIds,
            isAlliance: true,
            TimestampUtc,
            Retention,
            completeAllianceMemberCount: 16);

        Sequence([100u, 150u, 200u, 300u, 400u], order.EntityIds);
        Equal(3, order.LocalPartyCount);
        Equal(2, order.AllianceMemberCount);
    }

    private static void RetainsCompleteAllianceBriefly()
    {
        var tracker = CreateTrackerWithCompleteOrder([200, 201, 202, 203]);

        var order = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [200, 201],
            isAlliance: true,
            TimestampUtc.AddSeconds(1),
            Retention,
            completeAllianceMemberCount: 4);

        Sequence([100u, 200u, 201u, 202u, 203u], order.EntityIds);
        Equal(1, order.LocalPartyCount);
        Equal(4, order.AllianceMemberCount);
    }

    private static void ExpiresAtRetentionBoundary()
    {
        var tracker = CreateTrackerWithCompleteOrder([200, 201, 202, 203]);

        var order = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [200],
            isAlliance: true,
            TimestampUtc.AddSeconds(3),
            Retention,
            completeAllianceMemberCount: 4);

        Sequence([100u, 200u], order.EntityIds);
        Equal(1, order.AllianceMemberCount);
    }

    private static void ClearsRetainedMembersOutsideAlliance()
    {
        var tracker = CreateTrackerWithCompleteOrder([200, 201, 202, 203]);
        _ = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [200],
            isAlliance: false,
            TimestampUtc.AddSeconds(1),
            Retention,
            completeAllianceMemberCount: 4);

        var order = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [],
            isAlliance: true,
            TimestampUtc.AddSeconds(2),
            Retention,
            completeAllianceMemberCount: 4);

        Sequence([100u], order.EntityIds);
        Equal(0, order.AllianceMemberCount);
    }

    private static void ReplacesRetentionWithNewerCompleteData()
    {
        var tracker = CreateTrackerWithCompleteOrder([200, 201, 202, 203]);
        _ = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [300, 301, 302, 303],
            isAlliance: true,
            TimestampUtc.AddSeconds(1),
            Retention,
            completeAllianceMemberCount: 4);

        var order = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            [300],
            isAlliance: true,
            TimestampUtc.AddSeconds(2),
            Retention,
            completeAllianceMemberCount: 4);

        Sequence([100u, 300u, 301u, 302u, 303u], order.EntityIds);
    }

    private static PartyCooldownHudRosterOrderTracker CreateTrackerWithCompleteOrder(uint[] allianceEntityIds)
    {
        var tracker = new PartyCooldownHudRosterOrderTracker();
        _ = tracker.Resolve(
            [new PartyCooldownHudPartyMember(0, 100)],
            allianceEntityIds,
            isAlliance: true,
            TimestampUtc,
            Retention,
            completeAllianceMemberCount: allianceEntityIds.Length);
        return tracker;
    }
}
