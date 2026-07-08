using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class RuntimeScopeKeysTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("RuntimeScopeKeys match window scoped keys", MatchWindowScopedKeys),
        ("RuntimeScopeKeys build stable cache keys", BuildStableCacheKeys),
    ];

    private static void MatchWindowScopedKeys()
    {
        True(RuntimeScopeKeys.BelongsToWindow("win1", "win1"), "exact key should belong to the window");
        True(RuntimeScopeKeys.BelongsToWindow("win1:DRG", "win1"), "scoped key should belong to the window");
        True(!RuntimeScopeKeys.BelongsToWindow("win10:DRG", "win1"), "similarly-prefixed windows should not match");
        True(!RuntimeScopeKeys.BelongsToWindow("win1:DRG", ""), "empty window id should not match");
    }

    private static void BuildStableCacheKeys()
    {
        Equal("win1:PartyBuffs:True", RuntimeScopeKeys.AuraSeen("win1", IconWindowRole.PartyBuffs, true));
        Equal("jump:92", RuntimeScopeKeys.CooldownFrame("jump", 92));
        Equal("win1:DRG:jump", RuntimeScopeKeys.AbilityDrag("win1", "DRG", "jump"));
        Equal("win1:status-42", RuntimeScopeKeys.AuraDrag("win1", 42));
    }
}
