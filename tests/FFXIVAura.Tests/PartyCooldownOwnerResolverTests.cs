using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownOwnerResolverTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownOwnerResolver keeps party source ids", KeepsPartySourceIds),
        ("PartyCooldownOwnerResolver maps owned objects to party owner", MapsOwnedObjectsToPartyOwner),
        ("PartyCooldownOwnerResolver falls back invalid source to owner", FallsBackInvalidSourceToOwner),
        ("PartyCooldownOwnerResolver rejects unknown sources", RejectsUnknownSources),
    ];

    private static void KeepsPartySourceIds()
    {
        var partyIds = new HashSet<uint> { 1001 };
        var resolved = PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId: 2001,
            sourceEntityId: 1001,
            partyIds,
            _ => 0);

        Equal(1001u, resolved);
    }

    private static void MapsOwnedObjectsToPartyOwner()
    {
        var partyIds = new HashSet<uint> { 1001 };
        var owners = new Dictionary<uint, uint>
        {
            [3001] = 1001,
        };

        var resolved = PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId: 2001,
            sourceEntityId: 3001,
            partyIds,
            entityId => owners.GetValueOrDefault(entityId));

        Equal(1001u, resolved);
    }

    private static void FallsBackInvalidSourceToOwner()
    {
        var partyIds = new HashSet<uint> { 1001 };
        var resolved = PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId: 1001,
            sourceEntityId: 0xE0000000,
            partyIds,
            _ => 0);

        Equal(1001u, resolved);
    }

    private static void RejectsUnknownSources()
    {
        var partyIds = new HashSet<uint> { 1001 };
        var resolved = PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId: 2001,
            sourceEntityId: 3001,
            partyIds,
            _ => 0);

        Equal(0u, resolved);
    }
}
