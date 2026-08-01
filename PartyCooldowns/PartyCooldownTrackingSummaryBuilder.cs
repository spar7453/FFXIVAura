namespace FFXIVAura;

internal sealed record PartyCooldownPresetGroupSummary(
    string Job,
    IReadOnlyList<PartyCooldownDefinition> Definitions,
    int VisibleCount);

internal sealed record PartyCooldownTrackingSummary(
    int AvailableAtLevel,
    int VisibleAtLevel,
    int ExcludedInCategory,
    int StatuslessAtLevel,
    IReadOnlyList<PartyCooldownPresetGroupSummary> PresetGroups);

internal static class PartyCooldownTrackingSummaryBuilder
{
    public static PartyCooldownTrackingSummary Build(
        IReadOnlyList<PartyCooldownDefinition> presetDefinitions,
        IReadOnlyList<PartyCooldownDefinition> effectiveDefinitions,
        Func<PartyCooldownDefinition, bool> isExcluded,
        Func<PartyCooldownDefinition, bool> hasStatusTracking)
    {
        ArgumentNullException.ThrowIfNull(presetDefinitions);
        ArgumentNullException.ThrowIfNull(effectiveDefinitions);
        ArgumentNullException.ThrowIfNull(isExcluded);
        ArgumentNullException.ThrowIfNull(hasStatusTracking);

        var visibleAtLevel = 0;
        var statuslessAtLevel = 0;
        foreach (var definition in effectiveDefinitions)
        {
            if (isExcluded(definition))
                continue;

            visibleAtLevel++;
            if (!hasStatusTracking(definition))
                statuslessAtLevel++;
        }

        var presetGroups = presetDefinitions
            .GroupBy(definition => definition.Job, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var definitions = group.ToList();
                var visibleCount = definitions.Count(definition => !isExcluded(definition));
                return new PartyCooldownPresetGroupSummary(
                    group.Key,
                    definitions,
                    visibleCount);
            })
            .ToList();

        return new PartyCooldownTrackingSummary(
            effectiveDefinitions.Count,
            visibleAtLevel,
            presetDefinitions.Count(isExcluded),
            statuslessAtLevel,
            presetGroups);
    }
}
