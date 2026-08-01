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
        ("PartyCooldownOwnedObjectMatcher returns the matching party member", MatcherReturnsPartyMember),
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

    private static void MatcherReturnsPartyMember()
    {
        var matcher = new PartyCooldownOwnedObjectMatcher(
            new StubOwnedObjectReader(20),
            _ => { });
        PartyCooldownMemberSnapshot[] members =
        [
            new("member", 20, 0, 0, "Party Member", "P. Member", "SMN", 90, 0, string.Empty),
        ];

        True(
            matcher.TryFindMember(
                "Ruby Carbuncle",
                10,
                members,
                out var member,
                out var detail,
                out var ignoredReason),
            "owned object should resolve to its party owner");
        Equal(20u, member.EntityId);
        Equal(PartyCooldownIgnoredLogReason.None, ignoredReason);
        Equal("소환수/객체 소유자 매칭", detail);
    }

    private sealed class StubOwnedObjectReader(params uint[] ownerEntityIds) : IPartyCooldownOwnedObjectReader
    {
        public void AddMatchingOwnerEntityIds(string normalizedSourceName, ISet<uint> output)
        {
            foreach (var ownerEntityId in ownerEntityIds)
                output.Add(ownerEntityId);
        }
    }
}
