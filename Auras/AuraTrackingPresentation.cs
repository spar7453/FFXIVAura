namespace FFXIVAura;

internal readonly record struct AuraTrackingButtonModel(
    string Label,
    bool Enabled,
    bool ChangesMode,
    bool Exact);

internal static class AuraTrackingPresentation
{
    public static string GetTrackedLabel(AuraStatusGroup group)
        => group.IsExact
            ? $"{group.Name}  상태 {group.StatusId}  ID만"
            : group.MemberStatusIds.Count > 1
                ? $"{group.Name}  상태 ID {group.MemberStatusIds.Count}개"
                : $"{group.Name}  상태 {group.StatusId}";

    public static string GetSearchResultLabel(
        AuraSearchDisplayResult result,
        bool showIndividualIds,
        string? search)
    {
        var grouped = result.SameNameCount > 1
                      && !showIndividualIds
                      && !uint.TryParse(search?.Trim(), out _);
        return grouped
            ? $"{result.Name}  상태 ID {result.SameNameCount}개"
            : $"{result.Name}  상태 {result.StatusId}";
    }

    public static AuraTrackingButtonModel GetTrackingButton(
        bool exact,
        AuraTrackingCoverage coverage)
    {
        if (exact && coverage == AuraTrackingCoverage.Group)
            return new AuraTrackingButtonModel("ID로 전환", true, true, true);

        if (!exact && coverage == AuraTrackingCoverage.Exact)
            return new AuraTrackingButtonModel("그룹으로", true, true, false);

        if (coverage != AuraTrackingCoverage.None)
        {
            return new AuraTrackingButtonModel(
                exact ? "ID 추가됨" : "추가됨",
                false,
                false,
                exact);
        }

        return new AuraTrackingButtonModel(
            exact ? "ID 추가" : "추가",
            true,
            false,
            exact);
    }
}
