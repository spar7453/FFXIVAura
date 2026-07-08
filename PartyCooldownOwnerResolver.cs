namespace FFXIVAura;

internal static class PartyCooldownOwnerResolver
{
    public static uint ResolveStatusSourceEntityId(
        uint ownerEntityId,
        uint sourceEntityId,
        IReadOnlySet<uint> partyEntityIds,
        Func<uint, uint> getOwnerEntityId)
    {
        var candidateSourceId = IsValidEntityId(sourceEntityId)
            ? sourceEntityId
            : ownerEntityId;

        if (partyEntityIds.Contains(candidateSourceId))
            return candidateSourceId;

        var ownerId = getOwnerEntityId(candidateSourceId);
        if (partyEntityIds.Contains(ownerId))
            return ownerId;

        if (candidateSourceId == ownerEntityId)
            return 0;

        ownerId = getOwnerEntityId(ownerEntityId);
        return partyEntityIds.Contains(ownerId) ? ownerId : 0;
    }

    public static bool IsValidEntityId(uint entityId)
        => entityId is not 0 and not 0xE0000000;
}
