using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownRosterMemberCollectorTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Party cooldown member factory creates stable identities", CreatesStableIdentities),
        ("Party cooldown roster collector replaces entityless cross-realm members", ReplacesEntitylessCrossRealmMembers),
        ("Party cooldown roster collector rejects duplicate entities", RejectsDuplicateEntities),
        ("Party cooldown roster collector rolls back rejected entity reservations", RollsBackRejectedEntityReservations),
    ];

    private static void CreatesStableIdentities()
    {
        var contentMember = PartyCooldownMemberSnapshotFactory.Create(10, 100, 1, "Alpha", 19, 90);
        var entityMember = PartyCooldownMemberSnapshotFactory.Create(20, 0, 1, "Beta", 19, 80);
        var namedMember = PartyCooldownMemberSnapshotFactory.Create(0, 0, 1, "Gamma", 19, 70);

        Equal("content-100", contentMember.Key);
        Equal("entity-20", entityMember.Key);
        Equal($"{namedMember.Job}:Gamma", namedMember.Key);
        Equal("Al", contentMember.ShortName);
        Equal(90u, contentMember.Level);
        Equal(90u, contentMember.ResolveEffectiveLevel(100));
        Equal(90u, contentMember.ResolveEffectiveLevel(15));
        Equal(100u, default(PartyCooldownMemberSnapshot).ResolveEffectiveLevel(100));
        Equal("??", PartyCooldownMemberSnapshotFactory.CreateShortName("   "));
    }

    private static void ReplacesEntitylessCrossRealmMembers()
    {
        var collector = new PartyCooldownRosterMemberCollector(4);
        var crossRealm = PartyCooldownMemberSnapshotFactory.Create(0, 100, 1, "Alpha", 19, 50);
        var partyList = PartyCooldownMemberSnapshotFactory.Create(10, 100, 1, "Alpha", 19, 60);

        True(collector.AddCrossRealm(crossRealm, "B"), "the entityless cross-realm member should be accepted");
        True(collector.AddPartyList(partyList, "A"), "the party-list identity should replace the entityless member");

        Equal(1, collector.Members.Count);
        Equal(10u, collector.Members[0].EntityId);
        Equal(60u, collector.Members[0].Level);
        Equal("B", collector.Members[0].AllianceGroup);
    }

    private static void RejectsDuplicateEntities()
    {
        var collector = new PartyCooldownRosterMemberCollector(4);
        var first = PartyCooldownMemberSnapshotFactory.Create(10, 100, 1, "Alpha", 19, 90);
        var duplicateEntity = PartyCooldownMemberSnapshotFactory.Create(10, 200, 1, "Beta", 19, 90);

        True(collector.AddPartyList(first, "A"), "the first entity should be accepted");
        True(!collector.AddPartyList(duplicateEntity, "A"), "the duplicate entity should be rejected");
        Equal(1, collector.Members.Count);
    }

    private static void RollsBackRejectedEntityReservations()
    {
        var collector = new PartyCooldownRosterMemberCollector(4);
        var first = PartyCooldownMemberSnapshotFactory.Create(10, 100, 1, "Alpha", 19, 90);
        var duplicateKey = PartyCooldownMemberSnapshotFactory.Create(20, 100, 1, "Alpha", 19, 90);
        var next = PartyCooldownMemberSnapshotFactory.Create(20, 200, 1, "Beta", 19, 90);

        True(collector.AddCrossRealm(first, "A"), "the first identity should be accepted");
        True(!collector.AddCrossRealm(duplicateKey, "A"), "the duplicate key should be rejected");
        True(collector.AddCrossRealm(next, "B"), "the rejected entity id should be reusable");
        Equal(2, collector.Members.Count);
    }
}
