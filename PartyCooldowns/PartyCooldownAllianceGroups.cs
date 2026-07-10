namespace FFXIVAura;

internal static class PartyCooldownAllianceGroups
{
    private static readonly string[] Labels = ["A", "B", "C"];

    public static string OwnPartyLabel(bool isAlliance, int localGroupIndex)
        => isAlliance ? GroupLabel(localGroupIndex) : string.Empty;

    public static string GroupLabel(int groupIndex)
        => groupIndex >= 0 && groupIndex < Labels.Length ? LabelForPartyId(groupIndex) : string.Empty;

    public static string AllianceSlotLabel(int allianceSlotIndex, int localGroupIndex)
    {
        if (localGroupIndex < 0 || localGroupIndex >= Labels.Length || allianceSlotIndex < 0 || allianceSlotIndex >= 16)
            return string.Empty;

        var slotPartyOffset = allianceSlotIndex / 8;
        var offset = 0;
        for (var groupIndex = 0; groupIndex < Labels.Length; groupIndex++)
        {
            if (groupIndex == localGroupIndex)
                continue;

            if (offset == slotPartyOffset)
                return GroupLabel(groupIndex);

            offset++;
        }

        return string.Empty;
    }

    private static string LabelForPartyId(int partyId)
        => Labels[partyId];
}
