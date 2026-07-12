using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownDefinitionIdentityTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownDefinitionIdentity builds canonical ids", BuildsCanonicalIds),
        ("PartyCooldownDefinitionIdentity matches legacy lower exclusions", MatchesLegacyLowerExclusions),
        ("PartyCooldownDefinitionIdentity removes equivalent exclusions", RemovesEquivalentExclusions),
        ("PartyCooldownDefinitionIdentity builds stable runtime keys", BuildsStableRuntimeKeys),
        ("PartyCooldown runtime keys remain case insensitive", RuntimeKeysRemainCaseInsensitive),
        ("PartyCooldownDefinitionIdentity trims legacy ids", TrimsLegacyIds),
    ];

    private static void BuildsCanonicalIds()
    {
        var definitions = Definitions();
        var canonicalIds = PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(definitions, EquivalenceKey);

        Equal("guardian", canonicalIds["sentinel"]);
        Equal("guardian", canonicalIds["guardian"]);
        Equal("hallowed-ground", canonicalIds["hallowed-ground"]);
    }

    private static void MatchesLegacyLowerExclusions()
    {
        var definitions = Definitions();
        var canonicalIds = PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(definitions, EquivalenceKey);

        True(
            PartyCooldownDefinitionIdentity.IsExcluded(["sentinel"], canonicalIds, definitions[1]),
            "legacy lower replacement exclusion should hide the canonical row");
        True(
            PartyCooldownDefinitionIdentity.IsExcluded(["guardian"], canonicalIds, definitions[0]),
            "canonical exclusion should hide lower effective replacements");
        True(
            !PartyCooldownDefinitionIdentity.IsExcluded(["sentinel"], canonicalIds, definitions[2]),
            "unrelated definitions should not be excluded");
    }

    private static void RemovesEquivalentExclusions()
    {
        var definitions = Definitions();
        var canonicalIds = PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(definitions, EquivalenceKey);
        var excludedIds = new List<string> { "sentinel", "guardian", "hallowed-ground" };

        PartyCooldownDefinitionIdentity.RemoveExclusions(excludedIds, canonicalIds, [definitions[1]]);

        Sequence(["hallowed-ground"], excludedIds);
    }

    private static void BuildsStableRuntimeKeys()
    {
        var definitions = Definitions();
        var canonicalIds = PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(definitions, EquivalenceKey);

        Equal(
            PartyCooldownDefinitionIdentity.RuntimeKey("member-1", canonicalIds, definitions[0]),
            PartyCooldownDefinitionIdentity.RuntimeKey("member-1", canonicalIds, definitions[1]));
        Equal("member-1:guardian", PartyCooldownDefinitionIdentity.RuntimeKey("member-1", canonicalIds, definitions[1]));
    }

    private static void RuntimeKeysRemainCaseInsensitive()
    {
        var first = new PartyCooldownRuntimeKey("Content-10", "Guardian");
        var second = new PartyCooldownRuntimeKey("content-10", "guardian");

        True(PartyCooldownRuntimeKeyComparer.Instance.Equals(first, second), "runtime key casing should not split cooldown state");
        Equal(
            PartyCooldownRuntimeKeyComparer.Instance.GetHashCode(first),
            PartyCooldownRuntimeKeyComparer.Instance.GetHashCode(second));
    }

    private static void TrimsLegacyIds()
    {
        var definitions = Definitions();
        var canonicalIds = PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(definitions, EquivalenceKey);
        var excludedIds = new List<string> { " sentinel " };

        True(
            PartyCooldownDefinitionIdentity.IsExcluded(excludedIds, canonicalIds, definitions[1]),
            "trimmed legacy exclusion should hide canonical replacement");

        PartyCooldownDefinitionIdentity.RemoveExclusions(excludedIds, canonicalIds, [definitions[1]]);
        Sequence([], excludedIds);
    }

    private static PartyCooldownDefinition[] Definitions()
        =>
        [
            Definition("sentinel", 17, 38),
            Definition("guardian", 36920, 92),
            Definition("hallowed-ground", 30, 50),
        ];

    private static PartyCooldownDefinition Definition(string id, uint actionId, byte level)
        => new()
        {
            Id = id,
            Category = "Defensive",
            Job = "PLD",
            ActionId = actionId,
            Level = level,
            Name = id,
            IconId = actionId,
            Cooldown = 120f,
        };

    private static string EquivalenceKey(PartyCooldownDefinition definition)
        => definition.Id is "sentinel" or "guardian" ? "PLD:Defensive:tank-30" : string.Empty;
}
