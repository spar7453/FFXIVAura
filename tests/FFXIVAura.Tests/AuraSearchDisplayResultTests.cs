using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraSearchDisplayResultTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraSearchDisplayResults merges sources and same-name counts", MergesSourcesAndSameNameCounts),
        ("AuraSearchDisplayResults sorts current and exact matches first", SortsCurrentAndExactMatchesFirst),
        ("AuraSearchDisplayResultFormatter builds source tags", BuildsSourceTags),
    ];

    private static void MergesSourcesAndSameNameCounts()
    {
        var seenAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc);
        var results = AuraSearchDisplayResults.MergeAndSort(
            [
                new AuraSearchDisplayResult(10, "보호막", 100, IsCurrent: true, WasRecentlySeen: true, FromAction: false, FromStatusSheet: false, SeenAtUtc: seenAt),
                new AuraSearchDisplayResult(10, "보호막", 100, IsCurrent: false, WasRecentlySeen: false, FromAction: true, FromStatusSheet: false, SeenAtUtc: DateTime.MinValue, SourceActionNames: "수호"),
                new AuraSearchDisplayResult(20, "보호막", 200, IsCurrent: false, WasRecentlySeen: false, FromAction: false, FromStatusSheet: true, SeenAtUtc: DateTime.MinValue),
            ],
            "보호막");

        Equal(2, results.Count);
        Equal(10u, results[0].StatusId);
        True(results[0].IsCurrent, "current flag should be preserved");
        True(results[0].FromAction, "action source should merge into the active result");
        Equal("수호", results[0].SourceActionNames);
        Equal(2, results[0].SameNameCount);
        Equal(2, results[1].SameNameCount);
    }

    private static void SortsCurrentAndExactMatchesFirst()
    {
        var results = AuraSearchDisplayResults.MergeAndSort(
            [
                new AuraSearchDisplayResult(30, "다른 이름", 300, IsCurrent: true, WasRecentlySeen: true, FromAction: false, FromStatusSheet: false, SeenAtUtc: DateTime.UtcNow),
                new AuraSearchDisplayResult(20, "천하무적", 200, IsCurrent: false, WasRecentlySeen: false, FromAction: false, FromStatusSheet: true, SeenAtUtc: DateTime.MinValue),
                new AuraSearchDisplayResult(10, "천하무적", 100, IsCurrent: true, WasRecentlySeen: true, FromAction: false, FromStatusSheet: false, SeenAtUtc: DateTime.UtcNow),
            ],
            "천하무적");

        Equal(10u, results[0].StatusId);
        Equal(20u, results[1].StatusId);
        Equal(30u, results[2].StatusId);
    }

    private static void BuildsSourceTags()
    {
        var text = AuraSearchDisplayResultFormatter.GetTagText(new AuraSearchDisplayResult(
            10,
            "보호막",
            100,
            IsCurrent: true,
            WasRecentlySeen: true,
            FromAction: true,
            FromStatusSheet: true,
            SeenAtUtc: DateTime.UtcNow,
            SourceActionNames: "수호",
            SameNameCount: 3));

        Equal("활성 · 스킬: 수호 · 동명 3개", text);
    }
}
