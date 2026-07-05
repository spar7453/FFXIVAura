using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraStatusFrameIndexTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraStatusFrameIndex tracks best and own status", TracksBestAndOwnStatus),
        ("AuraStatusFrameIndex ignores invalid status ids", IgnoresInvalidStatusIds),
    ];

    private static void TracksBestAndOwnStatus()
    {
        var index = new Dictionary<uint, CharacterAuraAggregate>();

        AuraStatusFrameIndex.AddStatus(index, new CharacterAuraStatusSample(42, 30, 100, false));
        AuraStatusFrameIndex.AddStatus(index, new CharacterAuraStatusSample(42, 10, 200, true));
        AuraStatusFrameIndex.AddStatus(index, new CharacterAuraStatusSample(42, 20, 300, true));

        var all = index[42].Select(preferOwnStatus: false);
        Near(30, all.Remaining);
        Equal((ushort)100, all.Param);
        Equal(3, all.Count);
        Equal(2, all.OwnCount);
        True(!all.FromSelf, "non-own longest status should be selected without own preference");

        var own = index[42].Select(preferOwnStatus: true);
        Near(20, own.Remaining);
        Equal((ushort)300, own.Param);
        Equal(3, own.Count);
        Equal(2, own.OwnCount);
        True(own.FromSelf, "own status should be selected when requested");
    }

    private static void IgnoresInvalidStatusIds()
    {
        var index = new Dictionary<uint, CharacterAuraAggregate>();

        AuraStatusFrameIndex.AddStatus(index, new CharacterAuraStatusSample(0, 30, 100, true));

        Equal(0, index.Count);
    }
}
