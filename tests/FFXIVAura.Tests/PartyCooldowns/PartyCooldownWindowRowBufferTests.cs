using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownWindowRowBufferTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownWindowRowBuffer reuses member item lists", ReusesMemberItemLists),
        ("PartyCooldownWindowRowBuffer prunes departed members", PrunesDepartedMembers),
    ];

    private static void ReusesMemberItemLists()
    {
        var buffer = new PartyCooldownWindowRowBuffer();
        buffer.BeginFrame(24);
        var first = buffer.GetMemberItems("member-1", 8);
        first.Add(default);
        buffer.EndFrame();

        buffer.BeginFrame(24);
        var second = buffer.GetMemberItems("member-1", 8);

        True(ReferenceEquals(first, second), "the member item list should be reused across frames");
        Equal(0, second.Count);
    }

    private static void PrunesDepartedMembers()
    {
        var buffer = new PartyCooldownWindowRowBuffer();
        buffer.BeginFrame(2);
        var departed = buffer.GetMemberItems("member-1", 2);
        buffer.EndFrame();

        buffer.BeginFrame(1);
        buffer.GetMemberItems("member-2", 2);
        buffer.EndFrame();

        buffer.BeginFrame(2);
        var recreated = buffer.GetMemberItems("member-1", 2);

        True(!ReferenceEquals(departed, recreated), "departed member buffers should be pruned");
    }
}
