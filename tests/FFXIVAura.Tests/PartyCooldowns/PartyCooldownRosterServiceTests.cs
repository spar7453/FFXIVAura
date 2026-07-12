using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownRosterServiceTests
{
    private static readonly DateTime TimestampUtc = new(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(100);

    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Party cooldown roster service owns cache boundaries", OwnsCacheBoundaries),
        ("Party cooldown roster service orders party members and excludes local player", OrdersMembersAndExcludesLocalPlayer),
        ("Party cooldown roster service prefers a complete cross-realm roster", PrefersCompleteCrossRealmRoster),
        ("Party cooldown roster service builds a solo fallback", BuildsSoloFallback),
    ];

    private static void OwnsCacheBoundaries()
    {
        var reader = new FakePartyRosterReader
        {
            Source = CreateSource(new PartyListHeader(0, false, 0), new PartyRosterLocalIdentity(10, 100)),
            LocalPlayer = CreateMember(10, 100, "Local"),
        };
        var service = new PartyCooldownRosterService(reader, CacheDuration);

        _ = service.GetRoster(forceRefresh: false, TimestampUtc);
        _ = service.GetRoster(forceRefresh: false, TimestampUtc.AddMilliseconds(99));
        _ = service.GetRoster(forceRefresh: false, TimestampUtc.AddMilliseconds(100));
        _ = service.GetRoster(forceRefresh: true, TimestampUtc.AddMilliseconds(100));

        Equal(3, reader.SourceReadCount);
        Equal(1L, service.CacheHitCount);
        Equal(3L, service.CacheMissCount);

        service.ResetRuntimeState();
        _ = service.GetRoster(forceRefresh: false, TimestampUtc.AddMilliseconds(101));
        Equal(1, reader.ResetCount);
        Equal(4, reader.SourceReadCount);
    }

    private static void OrdersMembersAndExcludesLocalPlayer()
    {
        var reader = new FakePartyRosterReader
        {
            Source = CreateSource(new PartyListHeader(3, false, 1), new PartyRosterLocalIdentity(10, 100)),
            PartySlotMembers =
            [
                CreateMember(20, 200, "Second"),
                CreateMember(10, 100, "Local"),
                CreateMember(30, 300, "Third"),
            ],
            HudRosterOrder = new PartyCooldownHudRosterOrder([10, 30, 20], 3, 0),
        };
        var service = new PartyCooldownRosterService(reader, CacheDuration);

        var roster = service.GetRoster(forceRefresh: false, TimestampUtc);

        Sequence([10u, 30u, 20u], roster.Members.Select(member => member.EntityId).ToList());
        Sequence([30u, 20u], roster.DisplayMembers.Select(member => member.EntityId).ToList());
        Equal(PartyCooldownRosterSource.PartyList, roster.Diagnostics.Source);
        Equal(PartyCooldownRosterReadMode.PartySlots, roster.Diagnostics.ReadMode);
        True(roster.Diagnostics.ExcludedLocalPlayer, "the local player should be excluded from display members");
    }

    private static void PrefersCompleteCrossRealmRoster()
    {
        var crossRealmMembers = new List<PartyCooldownCrossRealmMemberRead>(24);
        for (var groupIndex = 0; groupIndex < 3; groupIndex++)
        {
            for (var slotIndex = 0; slotIndex < 8; slotIndex++)
            {
                var id = (uint)(100 + (groupIndex * 8) + slotIndex);
                crossRealmMembers.Add(new PartyCooldownCrossRealmMemberRead(
                    CreateMember(id, id + 1000, $"Member{id}"),
                    groupIndex));
            }
        }

        var reader = new FakePartyRosterReader
        {
            Source = new PartyCooldownRosterSourceRead(
                new PartyListHeader(8, true, 1),
                new PartyCooldownCrossRealmHeader(1, 1, 1, 1, false, 3),
                crossRealmMembers,
                default),
        };
        var service = new PartyCooldownRosterService(reader, CacheDuration);

        var roster = service.GetRoster(forceRefresh: false, TimestampUtc);

        Equal(24, roster.Members.Count);
        Equal(0, reader.PartySlotReadCount);
        Equal(0, reader.FlatAllianceReadCount);
        Equal(PartyCooldownRosterReadMode.CrossRealmAlliance, roster.Diagnostics.ReadMode);
        Equal(24, roster.Diagnostics.AllianceMemberCount);
        Equal(8, roster.Diagnostics.AllianceGroupAMemberCount);
        Equal(8, roster.Diagnostics.AllianceGroupBMemberCount);
        Equal(8, roster.Diagnostics.AllianceGroupCMemberCount);
    }

    private static void BuildsSoloFallback()
    {
        var reader = new FakePartyRosterReader
        {
            Source = CreateSource(new PartyListHeader(0, false, 0), new PartyRosterLocalIdentity(10, 100)),
            LocalPlayer = CreateMember(10, 100, "Local"),
        };
        var service = new PartyCooldownRosterService(reader, CacheDuration);

        var roster = service.GetRoster(forceRefresh: false, TimestampUtc);

        Equal(1, roster.Members.Count);
        Equal(0, roster.DisplayMembers.Count);
        Equal(PartyCooldownRosterSource.SoloFallback, roster.Diagnostics.Source);
        Equal(PartyCooldownRosterReadMode.SoloFallback, roster.Diagnostics.ReadMode);
    }

    private static PartyCooldownRosterSourceRead CreateSource(
        PartyListHeader header,
        PartyRosterLocalIdentity localIdentity)
        => new(
            header,
            PartyCooldownCrossRealmHeader.Empty,
            Array.Empty<PartyCooldownCrossRealmMemberRead>(),
            localIdentity);

    private static PartyCooldownMemberSnapshot CreateMember(uint entityId, ulong contentId, string name)
        => PartyCooldownMemberSnapshotFactory.Create(entityId, contentId, 1, name, 19);

    private sealed class FakePartyRosterReader : IPartyRosterReader
    {
        public PartyCooldownRosterSourceRead Source { get; set; }

        public IReadOnlyList<PartyCooldownMemberSnapshot> PartySlotMembers { get; set; } = [];

        public IReadOnlyList<PartyCooldownFlatAllianceMemberRead> FlatAllianceMembers { get; set; } = [];

        public PartyCooldownMemberSnapshot? LocalPlayer { get; set; }

        public PartyCooldownHudRosterOrder HudRosterOrder { get; set; } = PartyCooldownHudRosterOrder.Empty;

        public int SourceReadCount { get; private set; }

        public int PartySlotReadCount { get; private set; }

        public int FlatAllianceReadCount { get; private set; }

        public int ResetCount { get; private set; }

        public PartyListHeader ReadPartyListHeader()
            => this.Source.PartyListHeader;

        public PartyCooldownRosterSourceRead ReadSource()
        {
            this.SourceReadCount++;
            return this.Source;
        }

        public IReadOnlyList<PartyCooldownMemberSnapshot> ReadPartySlotMembers(int partySlotCount)
        {
            this.PartySlotReadCount++;
            return this.PartySlotMembers;
        }

        public IReadOnlyList<PartyCooldownFlatAllianceMemberRead> ReadFlatAllianceMembers()
        {
            this.FlatAllianceReadCount++;
            return this.FlatAllianceMembers;
        }

        public PartyCooldownMemberSnapshot? ReadLocalPlayer()
            => this.LocalPlayer;

        public PartyCooldownHudRosterOrder ReadHudRosterOrder(bool isAlliance)
            => this.HudRosterOrder;

        public void Reset()
            => this.ResetCount++;
    }
}
