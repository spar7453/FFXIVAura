using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class IconWindowCloneTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("IconWindowClone clones string maps safely", ClonesStringMapsSafely),
        ("IconWindowClone clones vector maps safely", ClonesVectorMapsSafely),
        ("IconWindowClone clones scoped aura positions", ClonesScopedAuraPositions),
        ("IconWindowClone skips invalid aura position keys", SkipsInvalidAuraPositionKeys),
        ("IconWindowClone ignores invalid aura clone scopes", IgnoresInvalidAuraCloneScopes),
        ("IconWindowClone preserves all window editor state", PreservesWindowEditorState),
    ];

    private static void ClonesStringMapsSafely()
    {
        var source = new Dictionary<string, List<string>>
        {
            [" DRG "] = [" jump ", "JUMP", "", " dive "],
            [" "] = ["ignored"],
        };

        var clone = IconWindowClone.CloneStringListMap(source);
        source[" DRG "][0] = "changed";

        True(clone.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "clone should ignore key case");
        True(clone.ContainsKey("DRG"), "trimmed key should exist");
        Sequence(["jump", "dive"], clone["DRG"]);
    }

    private static void ClonesVectorMapsSafely()
    {
        var source = new Dictionary<string, Dictionary<string, Vector2>>
        {
            [" DRG "] = new(StringComparer.OrdinalIgnoreCase)
            {
                [" jump "] = new Vector2(1, 2),
                ["bad"] = new Vector2(float.NaN, 0),
            },
        };

        var clone = IconWindowClone.CloneVector2Map(source);
        source[" DRG "][" jump "] = new Vector2(9, 9);

        True(clone.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "clone should ignore key case");
        True(clone.ContainsKey("DRG"), "trimmed outer key should exist");
        True(clone["DRG"].ContainsKey("jump"), "trimmed inner key should exist");
        Vector(new Vector2(1, 2), clone["DRG"]["jump"]);
        True(!clone["DRG"].ContainsKey("bad"), "invalid vector should be skipped");
    }

    private static void ClonesScopedAuraPositions()
    {
        var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                [" status-1 "] = new Vector2(1, 2),
                ["not-status"] = new Vector2(7, 8),
                ["bad"] = new Vector2(float.NaN, 0),
            },
            ["other:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-2"] = new Vector2(3, 4),
            },
        };

        var clone = IconWindowClone.CloneAuraPositionsForWindow(source, "old", "new");

        Equal(1, clone.Count);
        True(clone.ContainsKey("new:PartyBuffs"), "target group should be created");
        True(!clone.ContainsKey("other:PartyBuffs"), "unrelated groups should not be cloned");
        Vector(new Vector2(1, 2), clone["new:PartyBuffs"]["status-1"]);
        True(!clone["new:PartyBuffs"].ContainsKey("not-status"), "invalid aura keys should not be cloned");
        True(!clone["new:PartyBuffs"].ContainsKey("bad"), "invalid positions should not be cloned");
    }

    private static void SkipsInvalidAuraPositionKeys()
    {
        var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-0"] = new Vector2(1, 2),
                ["status-a"] = new Vector2(3, 4),
                ["status-2"] = new Vector2(5, 6),
            },
        };

        var clone = IconWindowClone.CloneAuraPositionsForWindow(source, "old", "new");
        Equal(1, clone["new:PartyBuffs"].Count);
        Vector(new Vector2(5, 6), clone["new:PartyBuffs"]["status-2"]);
    }

    private static void IgnoresInvalidAuraCloneScopes()
    {
        var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-1"] = new Vector2(1, 2),
            },
        };

        Equal(0, IconWindowClone.CloneAuraPositionsForWindow(source, "", "new").Count);
        Equal(0, IconWindowClone.CloneAuraPositionsForWindow(source, "old", " OLD ").Count);
    }

    private static void PreservesWindowEditorState()
    {
        var source = new IconWindowConfig
        {
            Id = "old",
            Name = "source",
            Position = new Vector2(10, 20),
            ActiveOrderRow = 3,
            OrderEditorHeight = 240,
            FourPlayerLayout = new IconWindowLayoutConfig
            {
                Position = new Vector2(30, 40),
            },
            ManualTrackingJobs = ["PLD"],
            TrackedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PLD"] = [],
            },
            TrackedStatusIds = [10],
            AuraPositionsByRole = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
            {
                ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["status-10"] = new Vector2(5, 6),
                },
            },
        };

        var clone = IconWindowClone.CloneForNewWindow(
            source,
            "new",
            "clone",
            new Vector2(100, 200));

        Equal("new", clone.Id);
        Equal("clone", clone.Name);
        Vector(new Vector2(100, 200), clone.Position);
        Equal(3, clone.ActiveOrderRow);
        Near(240, clone.OrderEditorHeight);
        True(clone.FourPlayerLayout is not null, "party layout should be cloned");
        True(!ReferenceEquals(source.FourPlayerLayout, clone.FourPlayerLayout), "party layout should be independent");
        Sequence(["PLD"], clone.ManualTrackingJobs);
        True(!clone.TrackedByJob.ContainsKey("PLD"), "empty tracked data should not be cloned");
        Sequence([10u], clone.TrackedStatusIds);
        True(clone.AuraPositionsByRole.ContainsKey("new:PartyBuffs"), "aura position scope should use the new window id");

        source.TrackedStatusIds.Add(20);
        source.FourPlayerLayout!.Position = Vector2.Zero;
        Sequence([10u], clone.TrackedStatusIds);
        Vector(new Vector2(30, 40), clone.FourPlayerLayout!.Position);
    }
}
