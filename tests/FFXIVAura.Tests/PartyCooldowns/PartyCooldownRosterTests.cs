using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownRosterTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownRoster excludes local player from display members", ExcludesLocalPlayerFromDisplayMembers),
        ("PartyCooldownRoster records alliance-ready diagnostics", RecordsAllianceReadyDiagnostics),
    ];

    private static void ExcludesLocalPlayerFromDisplayMembers()
    {
        var members = new[]
        {
            Member(10, "WAR"),
            Member(20, "WHM"),
            Member(30, "VPR"),
        };

        var displayMembers = PartyCooldownRoster.CreateDisplayMembers(members, localEntityId: 20, excludeLocalPlayer: true);

        Sequence([10u, 30u], displayMembers.Select(member => member.EntityId).ToList());
    }

    private static void RecordsAllianceReadyDiagnostics()
    {
        var members = new[]
        {
            Member(10, "WAR"),
            Member(20, "WHM"),
            Member(30, "VPR"),
        };
        var displayMembers = PartyCooldownRoster.CreateDisplayMembers(members, localEntityId: 20, excludeLocalPlayer: true);

        var diagnostics = PartyCooldownRoster.CreateDiagnostics(
            PartyCooldownRosterSource.Alliance,
            partyListLength: 8,
            members,
            displayMembers,
            localEntityId: 20,
            alliancePartyCount: 3,
            allianceMemberCount: 24,
            hasAllianceSource: true);

        Equal(PartyCooldownRosterSource.Alliance, diagnostics.Source);
        Equal(3, diagnostics.MemberCount);
        Equal(2, diagnostics.DisplayMemberCount);
        True(diagnostics.ExcludedLocalPlayer, "local player should be marked excluded");
        Equal(3, diagnostics.AlliancePartyCount);
        Equal(24, diagnostics.AllianceMemberCount);
        True(diagnostics.HasAllianceSource, "alliance source should be captured");
    }

    private static PartyCooldownMemberSnapshot Member(uint entityId, string job)
        => new($"key-{entityId}", entityId, 0, $"member-{entityId}", $"m{entityId}", job, 0);
}
