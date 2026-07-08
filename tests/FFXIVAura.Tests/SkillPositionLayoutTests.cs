using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class SkillPositionLayoutTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("SkillPositionLayout builds stable visible keys", BuildsStableVisibleKeys),
        ("SkillPositionLayout detects hidden saved positions", DetectsHiddenSavedPositions),
        ("SkillPositionLayout reuses equivalent saved positions", ReusesEquivalentSavedPositions),
        ("SkillPositionLayout replaces equivalent keys on save", ReplacesEquivalentKeysOnSave),
        ("SkillPositionLayout aligns rows and tracked order", AlignsRowsAndTrackedOrder),
    ];

    private static void BuildsStableVisibleKeys()
    {
        Equal("70:a|b|c", SkillPositionLayout.BuildVisibleKey(70, ["a", "b", "c"]));
    }

    private static void DetectsHiddenSavedPositions()
    {
        var savedKeys = new[] { "low-skill", "level-locked", "unknown" };
        var visibleIds = new[] { "low-skill" };

        True(
            SkillPositionLayout.HasHiddenSavedPositions(savedKeys, visibleIds, EquivalentIdsMatch, IsKnownTracked),
            "known saved skills hidden by level sync should trigger realignment");

        True(
            !SkillPositionLayout.HasHiddenSavedPositions(["high-skill"], ["low-skill"], EquivalentIdsMatch, IsKnownTracked),
            "equivalent visible skills should keep the saved layout valid");

        True(
            !SkillPositionLayout.HasHiddenSavedPositions(["unknown"], visibleIds, EquivalentIdsMatch, IsKnownTracked),
            "unknown stale keys should not trigger level-sync realignment");
    }

    private static void ReusesEquivalentSavedPositions()
    {
        var positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
        {
            ["high-skill"] = new(11, 12),
        };

        var changed = SkillPositionLayout.AddMissingPositions(
            positions,
            ["low-skill"],
            Options(),
            EquivalentIdsMatch,
            IsKnownTracked);

        True(!changed, "equivalent saved positions should not create duplicate keys");
        Equal(1, positions.Count);
        True(!positions.ContainsKey("low-skill"), "existing equivalent key should be preserved until realignment/save");
        True(SkillPositionLayout.TryGetPosition(positions, "low-skill", EquivalentIdsMatch, out var position), "equivalent key should resolve");
        Vector(new Vector2(11, 12), position);
    }

    private static void ReplacesEquivalentKeysOnSave()
    {
        var positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
        {
            ["high-skill"] = new(11, 12),
            ["other"] = new(40, 40),
        };

        SkillPositionLayout.SetPosition(positions, "low-skill", new Vector2(20, 21), EquivalentIdsMatch);

        True(!positions.ContainsKey("high-skill"), "old equivalent key should be removed");
        Vector(new Vector2(20, 21), positions["low-skill"]);
        Vector(new Vector2(40, 40), positions["other"]);
    }

    private static void AlignsRowsAndTrackedOrder()
    {
        var positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
        {
            ["high-skill"] = new(0, 0),
            ["b"] = new(45, 0),
            ["c"] = new(0, 45),
        };
        var items = new[]
        {
            new SkillPositionLayoutItem("low-skill", 0, 1, new Vector2(0, 0)),
            new SkillPositionLayoutItem("b", 1, 0, new Vector2(45, 0)),
            new SkillPositionLayoutItem("c", 2, 2, new Vector2(0, 45)),
        };

        SkillPositionLayout.AlignPositions(positions, items, Options(), preferTrackedOrder: true, EquivalentIdsMatch);

        True(!positions.ContainsKey("high-skill"), "realignment should migrate equivalent saved keys to current visible ids");
        Vector(new Vector2(58, 8), positions["b"]);
        Vector(new Vector2(103, 8), positions["low-skill"]);
        Vector(new Vector2(80, 53), positions["c"]);
    }

    private static SkillPositionLayoutOptions Options()
        => new(IconAlignment.Center, new Vector2(200, 100), 40, 5);

    private static bool EquivalentIdsMatch(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
            return true;

        return IsLowHighPair(first, second) || IsLowHighPair(second, first);
    }

    private static bool IsLowHighPair(string first, string second)
        => string.Equals(first, "low-skill", StringComparison.OrdinalIgnoreCase)
           && string.Equals(second, "high-skill", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownTracked(string id)
        => !string.Equals(id, "unknown", StringComparison.OrdinalIgnoreCase);
}
