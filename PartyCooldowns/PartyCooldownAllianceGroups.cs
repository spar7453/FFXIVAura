namespace FFXIVAura;

internal static class PartyCooldownAllianceGroups
{
    private static readonly string[] Labels = ["A", "B", "C"];

    public static string OwnPartyLabel(bool isAlliance, int partyId)
        => isAlliance ? LabelForPartyId(NormalizePartyId(partyId, fallback: 0)) : string.Empty;

    public static string GroupLabel(int groupIndex)
        => groupIndex >= 0 && groupIndex < Labels.Length ? LabelForPartyId(groupIndex) : string.Empty;

    public static string AllianceSlotLabel(int allianceSlotIndex, int localPartyId)
    {
        var local = NormalizePartyId(localPartyId, fallback: 0);
        var slotPartyOffset = Math.Clamp(allianceSlotIndex / 8, 0, 1);
        var offset = 0;
        for (var partyId = 0; partyId < Labels.Length; partyId++)
        {
            if (partyId == local)
                continue;

            if (offset == slotPartyOffset)
                return LabelForPartyId(partyId);

            offset++;
        }

        return string.Empty;
    }

    private static int NormalizePartyId(int partyId, int fallback)
        => partyId >= 0 && partyId < Labels.Length ? partyId : fallback;

    private static string LabelForPartyId(int partyId)
        => Labels[Math.Clamp(partyId, 0, Labels.Length - 1)];
}
