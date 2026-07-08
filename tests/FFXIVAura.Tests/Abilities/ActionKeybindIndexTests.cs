using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class ActionKeybindIndexTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("ActionKeybindIndex preserves first keybind", PreservesFirstKeybind),
        ("ActionKeybindIndex resolves adjusted ids", ResolvesAdjustedIds),
    ];

    private static void PreservesFirstKeybind()
    {
        var index = new ActionKeybindIndex();

        index.Register(10, 110, "1");
        index.Register(10, 110, "2");

        Equal(2, index.Count);
        Equal("1", index.Find(10, 0, 0, 0));
        Equal("1", index.Find(0, 110, 0, 0));
    }

    private static void ResolvesAdjustedIds()
    {
        var index = new ActionKeybindIndex();

        index.Register(10, 110, "q");

        Equal("q", index.Find(1, 2, 10, 0));
        Equal("q", index.Find(1, 2, 0, 110));
        Equal(string.Empty, index.Find(1, 2, 3, 4));
    }
}
