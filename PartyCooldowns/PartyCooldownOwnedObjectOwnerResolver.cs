namespace FFXIVAura;

internal enum PartyCooldownOwnedObjectOwnerMatchKind
{
    None,
    LocalPlayer,
    PartyMember,
    Ambiguous,
}

internal readonly record struct PartyCooldownOwnedObjectOwnerMatch(
    PartyCooldownOwnedObjectOwnerMatchKind Kind,
    uint OwnerEntityId);

internal static class PartyCooldownOwnedObjectOwnerResolver
{
    public static PartyCooldownOwnedObjectOwnerMatch Resolve(
        uint localPlayerEntityId,
        IReadOnlySet<uint> matchingOwnerEntityIds,
        IReadOnlySet<uint> partyMemberEntityIds)
    {
        if (localPlayerEntityId != 0 && matchingOwnerEntityIds.Contains(localPlayerEntityId))
        {
            return new PartyCooldownOwnedObjectOwnerMatch(
                PartyCooldownOwnedObjectOwnerMatchKind.LocalPlayer,
                localPlayerEntityId);
        }

        var matchedOwnerEntityId = 0u;
        foreach (var ownerEntityId in matchingOwnerEntityIds)
        {
            if (!PartyCooldownOwnerResolver.IsValidEntityId(ownerEntityId)
                || !partyMemberEntityIds.Contains(ownerEntityId))
            {
                continue;
            }

            if (matchedOwnerEntityId != 0 && matchedOwnerEntityId != ownerEntityId)
            {
                return new PartyCooldownOwnedObjectOwnerMatch(
                    PartyCooldownOwnedObjectOwnerMatchKind.Ambiguous,
                    0);
            }

            matchedOwnerEntityId = ownerEntityId;
        }

        return matchedOwnerEntityId == 0
            ? new PartyCooldownOwnedObjectOwnerMatch(PartyCooldownOwnedObjectOwnerMatchKind.None, 0)
            : new PartyCooldownOwnedObjectOwnerMatch(
                PartyCooldownOwnedObjectOwnerMatchKind.PartyMember,
                matchedOwnerEntityId);
    }
}
