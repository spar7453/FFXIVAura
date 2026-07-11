namespace FFXIVAura;

internal static class StatusSourceOwnership
{
    public static bool IsFromPlayer(uint sourceEntityId, uint playerEntityId, Func<uint, uint> getOwnerEntityId)
    {
        if (!IsValidEntityId(sourceEntityId) || !IsValidEntityId(playerEntityId))
            return false;

        return sourceEntityId == playerEntityId || getOwnerEntityId(sourceEntityId) == playerEntityId;
    }

    private static bool IsValidEntityId(uint entityId)
        => entityId is not 0 and not 0xE0000000;
}
