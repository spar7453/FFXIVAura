using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraSearchIndexTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraSearchIndex matches cached search text", MatchesCachedSearchText),
        ("AuraSearchIndex matches additional search text", MatchesAdditionalSearchText),
        ("AuraSearchIndex filters internal names", FiltersInternalNames),
    ];

    private static void MatchesCachedSearchText()
    {
        var entries = new[]
        {
            new AuraSearchIndexEntry(10, "피의 갈증", 100, "피의 갈증", "10"),
            new AuraSearchIndexEntry(20, "원초의 혈기", 200, "Raw Intuition", "7531"),
        };

        var byActionName = AuraSearchIndex.Search(entries, "intuition").ToList();
        Equal(1, byActionName.Count);
        Equal(20u, byActionName[0].StatusId);

        var byId = AuraSearchIndex.Search(entries, "53").ToList();
        Equal(1, byId.Count);
        Equal(20u, byId[0].StatusId);
    }

    private static void MatchesAdditionalSearchText()
    {
        var entries = new[]
        {
            new AuraSearchIndexEntry(82, "천하무적 상태", 100, "천하무적", "30", "천하무적 상태 82"),
            new AuraSearchIndexEntry(1302, "천하무적 상태", 101, "진 천하무적", "31", "천하무적 상태 1302"),
        };

        var byStatusName = AuraSearchIndex.Search(entries, "상태").ToList();
        Equal(2, byStatusName.Count);

        var byStatusId = AuraSearchIndex.Search(entries, "1302").ToList();
        Equal(1, byStatusId.Count);
        Equal(1302u, byStatusId[0].StatusId);
    }

    private static void FiltersInternalNames()
    {
        True(AuraSearchIndex.IsSearchableStatusName("피의 갈증"), "normal Korean status names should be searchable");
        True(!AuraSearchIndex.IsSearchableStatusName("rsv_test"), "reserved status names should be hidden");
        True(!AuraSearchIndex.IsSearchableStatusName("_hidden"), "internal status names should be hidden");
        True(!AuraSearchIndex.IsSearchableStatusName("テスト"), "Japanese-only status names should be hidden");
    }
}
