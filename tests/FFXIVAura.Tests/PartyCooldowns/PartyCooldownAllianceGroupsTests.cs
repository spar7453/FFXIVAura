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
    ];

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
}
