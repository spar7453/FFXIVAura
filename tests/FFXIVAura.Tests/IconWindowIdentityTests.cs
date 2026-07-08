using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class IconWindowIdentityTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("IconWindowIdentity creates stable unique ids", CreatesStableUniqueIds),
        ("IconWindowIdentity gets the next available number", GetsNextAvailableNumber),
        ("IconWindowIdentity reuses deleted window numbers", ReusesDeletedWindowNumbers),
        ("IconWindowIdentity wraps available window numbers", WrapsAvailableWindowNumbers),
        ("IconWindowIdentity trims window ids", TrimsWindowIds),
        ("IconWindowIdentity remaps aura position groups", RemapsAuraPositionGroups),
        ("IconWindowIdentity merges existing remap targets", MergesExistingRemapTargets),
        ("IconWindowIdentity ignores case-only remaps", IgnoresCaseOnlyRemaps),
    ];

    private static void CreatesStableUniqueIds()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "win1" };
        var next = 1;
        var id = IconWindowIdentity.CreateUniqueId(used, ref next);

        Equal("win2", id);
        Equal(3, next);
        True(used.Contains("win2"), "new id should be reserved");
    }

    private static void GetsNextAvailableNumber()
    {
        var number = IconWindowIdentity.GetNextAvailableNumber(["win1", "win2", "win4"], 2);
        Equal(3, number);
    }

    private static void ReusesDeletedWindowNumbers()
    {
        var number = IconWindowIdentity.GetNextAvailableNumber(["win1"], 2);
        Equal(2, number);
    }

    private static void WrapsAvailableWindowNumbers()
    {
        var number = IconWindowIdentity.GetNextAvailableNumber(["win1", "win3"], 9999);
        Equal(2, number);
    }

    private static void TrimsWindowIds()
    {
        Equal(7, IconWindowIdentity.GetNumber(" win7 "));
        Equal("창 7", IconWindowIdentity.GetDefaultName(" win7 "));

        var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-1"] = new Vector2(1, 2),
            },
        };

        IconWindowIdentity.RemapAuraPositionGroups(positions, " old ", " new ");
        True(positions.ContainsKey("new:PartyBuffs"), "trimmed target window id should be used");
    }

    private static void RemapsAuraPositionGroups()
    {
        var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-1"] = new Vector2(1, 2),
            },
            ["other:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-2"] = new Vector2(3, 4),
            },
        };

        IconWindowIdentity.RemapAuraPositionGroups(positions, "old", "new");
        True(!positions.ContainsKey("old:PartyBuffs"), "old group should be removed");
        True(positions.ContainsKey("new:PartyBuffs"), "new group should be created");
        True(positions.ContainsKey("other:PartyBuffs"), "unrelated group should stay");
    }

    private static void MergesExistingRemapTargets()
    {
        var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-1"] = new Vector2(1, 2),
                ["status-2"] = new Vector2(3, 4),
            },
            ["new:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-2"] = new Vector2(30, 40),
                ["status-3"] = new Vector2(5, 6),
            },
        };

        IconWindowIdentity.RemapAuraPositionGroups(positions, "old", "new");
        True(!positions.ContainsKey("old:PartyBuffs"), "old group should be removed");
        Vector(new Vector2(1, 2), positions["new:PartyBuffs"]["status-1"]);
        Vector(new Vector2(30, 40), positions["new:PartyBuffs"]["status-2"]);
        Vector(new Vector2(5, 6), positions["new:PartyBuffs"]["status-3"]);
    }

    private static void IgnoresCaseOnlyRemaps()
    {
        var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Win1:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["status-1"] = new Vector2(1, 2),
            },
        };

        IconWindowIdentity.RemapAuraPositionGroups(positions, "Win1", "win1");
        Equal(1, positions.Count);
        True(positions.ContainsKey("Win1:PartyBuffs"), "case-only remap should not remove the existing group");
        Vector(new Vector2(1, 2), positions["Win1:PartyBuffs"]["status-1"]);
    }
}
