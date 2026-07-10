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

    public static string ContainerGroupLabel(
        int containerGroupIndex,
        int rawLocalGroupIndex,
        int displayLocalGroupIndex)
    {
        if (!IsValidGroupIndex(containerGroupIndex))
            return string.Empty;

        if (!IsValidGroupIndex(rawLocalGroupIndex) || !IsValidGroupIndex(displayLocalGroupIndex))
            return GroupLabel(containerGroupIndex);

        if (containerGroupIndex == rawLocalGroupIndex)
            return GroupLabel(displayLocalGroupIndex);

        var containerOffset = 0;
        var displayOffset = 0;
        for (var index = 0; index < Labels.Length; index++)
        {
            if (index != rawLocalGroupIndex)
            {
                if (index == containerGroupIndex)
                    break;

                containerOffset++;
            }
        }

        for (var index = 0; index < Labels.Length; index++)
        {
            if (index == displayLocalGroupIndex)
                continue;

            if (displayOffset == containerOffset)
                return GroupLabel(index);

            displayOffset++;
        }

        return GroupLabel(containerGroupIndex);
    }

    public static int ParseGroupIndexFromPartyTypeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return -1;

        var span = text.AsSpan().Trim();
        for (var index = span.Length - 1; index >= 0; index--)
        {
            var value = char.ToUpperInvariant(span[index]);
            if (value < 'A' || value > 'C')
                continue;

            var hasLeftBoundary = index == 0 || !char.IsLetterOrDigit(span[index - 1]);
            var hasRightBoundary = index == span.Length - 1 || !char.IsLetterOrDigit(span[index + 1]);
            if (hasLeftBoundary && hasRightBoundary)
                return value - 'A';
        }

        return -1;
    }

    public static bool IsLocalMember(
        uint memberEntityId,
        ulong memberContentId,
        uint localEntityId,
        ulong localContentId)
        => (localEntityId != 0 && memberEntityId == localEntityId)
           || (localContentId != 0 && memberContentId == localContentId);

    private static bool IsValidGroupIndex(int groupIndex)
        => groupIndex >= 0 && groupIndex < Labels.Length;

    private static string LabelForPartyId(int partyId)
        => Labels[partyId];
}
