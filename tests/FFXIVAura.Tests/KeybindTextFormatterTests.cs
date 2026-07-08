using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class KeybindTextFormatterTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("KeybindTextFormatter formats modifiers", FormatsModifiers),
        ("KeybindTextFormatter maps game glyphs", MapsGameGlyphs),
        ("KeybindTextFormatter strips unknown glyphs", StripsUnknownGlyphs),
    ];

    private static void FormatsModifiers()
    {
        var text = KeybindTextFormatter.Format("[Shift+1]", out var unknown);
        Equal("s1", text);
        True(!unknown, "known modifier text should not be marked unknown");

        text = KeybindTextFormatter.Format("Ctrl-Alt+5", out unknown);
        Equal("ca5", text);
        True(!unknown, "known modifier text should not be marked unknown");

        text = KeybindTextFormatter.Format("Shift+q", out unknown);
        Equal("sq", text);
        True(!unknown, "letter keys should be preserved");
    }

    private static void MapsGameGlyphs()
    {
        var text = KeybindTextFormatter.Format("\u00a7\u00a2\u00aa\u00ba", out var unknown);
        Equal("scan", text);
        True(!unknown, "known game glyphs should not be marked unknown");
    }

    private static void StripsUnknownGlyphs()
    {
        var text = KeybindTextFormatter.Format("Shift+?", out var unknown);
        Equal("s", text);
        True(unknown, "unknown glyph should be reported");
    }
}
