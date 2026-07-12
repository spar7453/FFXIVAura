using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownCrossRealmHeaderResolverTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Cross-realm header falls back to the HUD without a proxy", FallsBackToHudWithoutProxy),
        ("Cross-realm header ignores incomplete proxy groups", IgnoresIncompleteProxyGroups),
        ("Cross-realm header prioritizes HUD member and raw groups", PrioritizesHudMemberAndRawGroups),
        ("Cross-realm header normalizes invalid snapshot values", NormalizesInvalidSnapshotValues),
        ("Cross-realm header clamps unavailable group counts", ClampsUnavailableGroupCounts),
    ];

    private static void FallsBackToHudWithoutProxy()
    {
        var header = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(1, -1, true),
            snapshot: null);

        Equal(1, header.LocalGroupIndex);
        Equal(-1, header.RawLocalGroupIndex);
        Equal(1, header.HudLocalGroupIndex);
        Equal(-1, header.ObservedHudGroupIndex);
        True(header.UsedRetainedHudGroup, "the retained HUD diagnostic should be preserved");
        Equal(0, header.GroupCount);
    }

    private static void IgnoresIncompleteProxyGroups()
    {
        var header = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(2, 2, false),
            new PartyCooldownCrossRealmSnapshot(1, 0, 1));

        Equal(2, header.LocalGroupIndex);
        Equal(-1, header.RawLocalGroupIndex);
        Equal(2, header.HudLocalGroupIndex);
        Equal(1, header.GroupCount);
    }

    private static void PrioritizesHudMemberAndRawGroups()
    {
        var snapshot = new PartyCooldownCrossRealmSnapshot(3, 0, 1);
        var hud = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(2, 2, false),
            snapshot);
        var member = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(-1, -1, false),
            snapshot);
        var raw = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(-1, -1, false),
            snapshot with { MemberLocalGroupIndex = -1 });

        Equal(2, hud.LocalGroupIndex);
        Equal(1, member.LocalGroupIndex);
        Equal(0, raw.LocalGroupIndex);
        Equal(0, member.RawLocalGroupIndex);
        Equal(3, member.GroupCount);
    }

    private static void NormalizesInvalidSnapshotValues()
    {
        var header = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(8, 8, true),
            new PartyCooldownCrossRealmSnapshot(99, 7, 6));

        Equal(-1, header.LocalGroupIndex);
        Equal(-1, header.RawLocalGroupIndex);
        Equal(-1, header.HudLocalGroupIndex);
        Equal(8, header.ObservedHudGroupIndex);
        True(header.UsedRetainedHudGroup, "diagnostic metadata should survive normalization");
        Equal(3, header.GroupCount);
    }

    private static void ClampsUnavailableGroupCounts()
    {
        var header = PartyCooldownCrossRealmHeaderResolver.Resolve(
            new PartyCooldownAllianceGroupResolution(-1, -1, false),
            new PartyCooldownCrossRealmSnapshot(-5, 0, 1));

        Equal(-1, header.LocalGroupIndex);
        Equal(-1, header.RawLocalGroupIndex);
        Equal(0, header.GroupCount);
    }
}
