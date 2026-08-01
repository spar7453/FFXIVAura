using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownTrackingSummaryBuilderTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownTrackingSummaryBuilder counts visible and statusless entries", CountsVisibleAndStatuslessEntries),
        ("PartyCooldownTrackingSummaryBuilder groups jobs case-insensitively", GroupsJobsCaseInsensitively),
    ];

    private static void CountsVisibleAndStatuslessEntries()
    {
        var darkMissionary = Definition("drk-missionary", "DRK", 1);
        var darkMind = Definition("drk-mind", "drk");
        var roleAction = Definition("role-action", "ROLE");
        var presetDefinitions = new[]
        {
            darkMissionary,
            darkMind,
            roleAction,
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            darkMind.Id,
        };

        var summary = PartyCooldownTrackingSummaryBuilder.Build(
            presetDefinitions,
            presetDefinitions,
            definition => excluded.Contains(definition.Id),
            definition => definition.StatusIds.Length > 0);

        Equal(3, summary.AvailableAtLevel);
        Equal(2, summary.VisibleAtLevel);
        Equal(1, summary.ExcludedInCategory);
        Equal(1, summary.StatuslessAtLevel);
    }

    private static void GroupsJobsCaseInsensitively()
    {
        var visible = Definition("drk-visible", "DRK", 1);
        var excluded = Definition("drk-excluded", "drk", 2);
        var roleAction = Definition("role-action", "ROLE", 3);

        var summary = PartyCooldownTrackingSummaryBuilder.Build(
            [visible, excluded, roleAction],
            [visible, roleAction],
            definition => definition.Id == excluded.Id,
            definition => definition.StatusIds.Length > 0);

        Equal(2, summary.PresetGroups.Count);
        Equal("DRK", summary.PresetGroups[0].Job);
        Equal(2, summary.PresetGroups[0].Definitions.Count);
        Equal(1, summary.PresetGroups[0].VisibleCount);
        Equal("ROLE", summary.PresetGroups[1].Job);
        Equal(1, summary.PresetGroups[1].VisibleCount);
    }

    private static PartyCooldownDefinition Definition(
        string id,
        string job,
        params uint[] statusIds)
        => new()
        {
            Id = id,
            Job = job,
            StatusIds = statusIds,
        };
}
