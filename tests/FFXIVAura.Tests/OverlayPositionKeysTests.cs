using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class OverlayPositionKeysTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("OverlayPositionKeys parse aura keys", ParseAuraKeys),
        ("OverlayPositionKeys reject invalid aura keys", RejectInvalidAuraKeys),
        ("OverlayPositionKeys replace window prefixes", ReplaceWindowPrefixes),
    ];

    private static void ParseAuraKeys()
    {
        Equal("status-42", OverlayPositionKeys.Aura(42));
        True(OverlayPositionKeys.TryParseAura("STATUS-42", out var statusId), "aura key should parse case-insensitively");
        Equal(42u, statusId);
        True(!OverlayPositionKeys.TryParseAura("skill-42", out _), "non-aura key should not parse");
    }

    private static void RejectInvalidAuraKeys()
    {
        True(!OverlayPositionKeys.TryParseAura("status-0", out _), "zero status id should not parse");
        True(!OverlayPositionKeys.TryParseAura("status-", out _), "empty status id should not parse");
        True(!OverlayPositionKeys.TryParseAura("status-a", out _), "non-numeric status id should not parse");
    }

    private static void ReplaceWindowPrefixes()
    {
        Equal("new:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("old:PartyBuffs", "old", "new"));
        Equal("new:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("old:PartyBuffs", " old ", " new "));
        Equal("other:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("other:PartyBuffs", "old", "new"));
    }
}
