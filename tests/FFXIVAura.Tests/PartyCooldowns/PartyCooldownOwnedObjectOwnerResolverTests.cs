using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownOwnedObjectOwnerResolverTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownOwnedObjectOwnerResolver excludes local collisions", ExcludesLocalCollisions),
        ("PartyCooldownOwnedObjectOwnerResolver resolves one party owner", ResolvesOnePartyOwner),
        ("PartyCooldownOwnedObjectOwnerResolver rejects ambiguous party owners", RejectsAmbiguousPartyOwners),
        ("PartyCooldownOwnedObjectOwnerResolver ignores unrelated owners", IgnoresUnrelatedOwners),
        ("PartyCooldownOwnedObjectOwnerResolver ignores invalid local ids", IgnoresInvalidLocalIds),
        ("PartyCooldownOwnedObjectOwnerResolver ignores invalid owner ids", IgnoresInvalidOwnerIds),
    ];

    private static void ExcludesLocalCollisions()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            10,
            new HashSet<uint> { 10, 20 },
            new HashSet<uint> { 20 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.LocalPlayer, result.Kind);
        Equal(10u, result.OwnerEntityId);
    }

    private static void ResolvesOnePartyOwner()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            10,
            new HashSet<uint> { 20 },
            new HashSet<uint> { 20, 30 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.PartyMember, result.Kind);
        Equal(20u, result.OwnerEntityId);
    }

    private static void RejectsAmbiguousPartyOwners()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            10,
            new HashSet<uint> { 20, 30 },
            new HashSet<uint> { 20, 30 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.Ambiguous, result.Kind);
    }

    private static void IgnoresUnrelatedOwners()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            10,
            new HashSet<uint> { 40 },
            new HashSet<uint> { 20, 30 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.None, result.Kind);
    }

    private static void IgnoresInvalidLocalIds()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            0xE0000000,
            new HashSet<uint> { 0xE0000000, 20 },
            new HashSet<uint> { 20 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.PartyMember, result.Kind);
        Equal(20u, result.OwnerEntityId);
    }

    private static void IgnoresInvalidOwnerIds()
    {
        var result = PartyCooldownOwnedObjectOwnerResolver.Resolve(
            10,
            new HashSet<uint> { 0, 0xE0000000 },
            new HashSet<uint> { 0, 0xE0000000 });

        Equal(PartyCooldownOwnedObjectOwnerMatchKind.None, result.Kind);
        Equal(0u, result.OwnerEntityId);
    }
}
