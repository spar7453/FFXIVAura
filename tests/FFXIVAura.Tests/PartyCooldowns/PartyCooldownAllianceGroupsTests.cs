using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownAllianceGroupsTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownAllianceGroups hides labels outside alliance", HidesLabelsOutsideAlliance),
        ("PartyCooldownAllianceGroups labels exact alliance groups", LabelsExactAllianceGroups),
        ("PartyCooldownAllianceGroups labels local party from group index", LabelsLocalPartyFromGroupIndex),
        ("PartyCooldownAllianceGroups labels alliance slots around local party", LabelsAllianceSlotsAroundLocalParty),
        ("PartyCooldownAllianceGroups maps normalized containers to displayed groups", MapsNormalizedContainersToDisplayedGroups),
        ("PartyCooldownAllianceGroups parses the HUD party title", ParsesHudPartyTitle),
        ("PartyCooldownAllianceGroups matches local member by stable content id", MatchesLocalMemberByContentId),
    ];

    private static void MatchesLocalMemberByContentId()
    {
        True(PartyCooldownAllianceGroups.IsLocalMember(10, 100, 10, 200), "entity id should match");
        True(PartyCooldownAllianceGroups.IsLocalMember(0, 100, 10, 100), "content id should match while entity id is unavailable");
        True(!PartyCooldownAllianceGroups.IsLocalMember(0, 100, 10, 200), "unrelated members should not match");
    }

    private static void HidesLabelsOutsideAlliance()
    {
        Equal(string.Empty, PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: false, localGroupIndex: 0));
    }

    private static void LabelsExactAllianceGroups()
    {
        Equal("A", PartyCooldownAllianceGroups.GroupLabel(0));
        Equal("B", PartyCooldownAllianceGroups.GroupLabel(1));
        Equal("C", PartyCooldownAllianceGroups.GroupLabel(2));
        Equal(string.Empty, PartyCooldownAllianceGroups.GroupLabel(3));
    }

    private static void LabelsLocalPartyFromGroupIndex()
    {
        Equal("A", PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localGroupIndex: 0));
        Equal("B", PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localGroupIndex: 1));
        Equal("C", PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localGroupIndex: 2));
        Equal(string.Empty, PartyCooldownAllianceGroups.OwnPartyLabel(isAlliance: true, localGroupIndex: 99));
    }

    private static void LabelsAllianceSlotsAroundLocalParty()
    {
        Equal("B", PartyCooldownAllianceGroups.AllianceSlotLabel(0, localGroupIndex: 0));
        Equal("B", PartyCooldownAllianceGroups.AllianceSlotLabel(7, localGroupIndex: 0));
        Equal("C", PartyCooldownAllianceGroups.AllianceSlotLabel(8, localGroupIndex: 0));

        Equal("A", PartyCooldownAllianceGroups.AllianceSlotLabel(0, localGroupIndex: 1));
        Equal("C", PartyCooldownAllianceGroups.AllianceSlotLabel(8, localGroupIndex: 1));

        Equal("A", PartyCooldownAllianceGroups.AllianceSlotLabel(0, localGroupIndex: 2));
        Equal("B", PartyCooldownAllianceGroups.AllianceSlotLabel(8, localGroupIndex: 2));
        Equal(string.Empty, PartyCooldownAllianceGroups.AllianceSlotLabel(16, localGroupIndex: 2));
    }

    private static void MapsNormalizedContainersToDisplayedGroups()
    {
        Equal("B", PartyCooldownAllianceGroups.ContainerGroupLabel(0, rawLocalGroupIndex: 0, displayLocalGroupIndex: 1));
        Equal("A", PartyCooldownAllianceGroups.ContainerGroupLabel(1, rawLocalGroupIndex: 0, displayLocalGroupIndex: 1));
        Equal("C", PartyCooldownAllianceGroups.ContainerGroupLabel(2, rawLocalGroupIndex: 0, displayLocalGroupIndex: 1));

        Equal("A", PartyCooldownAllianceGroups.ContainerGroupLabel(0, rawLocalGroupIndex: 1, displayLocalGroupIndex: 1));
        Equal("B", PartyCooldownAllianceGroups.ContainerGroupLabel(1, rawLocalGroupIndex: 1, displayLocalGroupIndex: 1));
        Equal("C", PartyCooldownAllianceGroups.ContainerGroupLabel(2, rawLocalGroupIndex: 1, displayLocalGroupIndex: 1));
    }

    private static void ParsesHudPartyTitle()
    {
        Equal(1, PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText("연합 파티 B"));
        Equal(2, PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText("Alliance Party C "));
        Equal(-1, PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText("대규모 파티"));
        Equal(-1, PartyCooldownAllianceGroups.ParseGroupIndexFromPartyTypeText("CAB"));
    }
}
