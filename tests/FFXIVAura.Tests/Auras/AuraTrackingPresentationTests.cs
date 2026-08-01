using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraTrackingPresentationTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraTrackingPresentation labels tracked groups", LabelsTrackedGroups),
        ("AuraTrackingPresentation groups same-name search labels", GroupsSameNameSearchLabels),
        ("AuraTrackingPresentation exposes IDs for ID searches and views", ExposesIdsForIdSearchesAndViews),
        ("AuraTrackingPresentation maps tracking actions", MapsTrackingActions),
    ];

    private static void LabelsTrackedGroups()
    {
        var key = AuraStatusGroupKey.Create("천하무적", 100, 1);

        Equal(
            "천하무적  상태 82  ID만",
            AuraTrackingPresentation.GetTrackedLabel(
                new AuraStatusGroup(82, "천하무적", 100, key, true, [82])));
        Equal(
            "천하무적  상태 ID 2개",
            AuraTrackingPresentation.GetTrackedLabel(
                new AuraStatusGroup(82, "천하무적", 100, key, false, [82, 1302])));
        Equal(
            "천하무적  상태 82",
            AuraTrackingPresentation.GetTrackedLabel(
                new AuraStatusGroup(82, "천하무적", 100, key, false, [82])));
    }

    private static void GroupsSameNameSearchLabels()
    {
        var result = Result(sameNameCount: 3);

        Equal(
            "천하무적  상태 ID 3개",
            AuraTrackingPresentation.GetSearchResultLabel(
                result,
                showIndividualIds: false,
                search: "천하무적"));
    }

    private static void ExposesIdsForIdSearchesAndViews()
    {
        var result = Result(sameNameCount: 3);

        Equal(
            "천하무적  상태 82",
            AuraTrackingPresentation.GetSearchResultLabel(
                result,
                showIndividualIds: true,
                search: "천하무적"));
        Equal(
            "천하무적  상태 82",
            AuraTrackingPresentation.GetSearchResultLabel(
                result,
                showIndividualIds: false,
                search: " 82 "));
    }

    private static void MapsTrackingActions()
    {
        var cases = new[]
        {
            (
                Exact: true,
                Coverage: AuraTrackingCoverage.Group,
                Expected: new AuraTrackingButtonModel("ID로 전환", true, true, true)),
            (
                Exact: false,
                Coverage: AuraTrackingCoverage.Exact,
                Expected: new AuraTrackingButtonModel("그룹으로", true, true, false)),
            (
                Exact: true,
                Coverage: AuraTrackingCoverage.Exact,
                Expected: new AuraTrackingButtonModel("ID 추가됨", false, false, true)),
            (
                Exact: false,
                Coverage: AuraTrackingCoverage.Group,
                Expected: new AuraTrackingButtonModel("추가됨", false, false, false)),
            (
                Exact: true,
                Coverage: AuraTrackingCoverage.None,
                Expected: new AuraTrackingButtonModel("ID 추가", true, false, true)),
            (
                Exact: false,
                Coverage: AuraTrackingCoverage.None,
                Expected: new AuraTrackingButtonModel("추가", true, false, false)),
        };

        foreach (var item in cases)
        {
            Equal(
                item.Expected,
                AuraTrackingPresentation.GetTrackingButton(item.Exact, item.Coverage));
        }
    }

    private static AuraSearchDisplayResult Result(int sameNameCount)
        => new(
            82,
            "천하무적",
            100,
            IsCurrent: true,
            WasRecentlySeen: true,
            FromAction: false,
            FromStatusSheet: false,
            SeenAtUtc: DateTime.UtcNow,
            SameNameCount: sameNameCount);
}
