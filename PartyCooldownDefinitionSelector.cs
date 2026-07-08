namespace FFXIVAura;

internal static class PartyCooldownDefinitionSelector
{
    public static IReadOnlyList<PartyCooldownDefinition> SelectEffectiveForLevel(
        IEnumerable<PartyCooldownDefinition> definitions,
        uint level,
        Func<PartyCooldownDefinition, string> getEquivalenceKey)
        => SelectHighestReplacements(definitions, definition => definition.Level <= level, getEquivalenceKey);

    public static IReadOnlyList<PartyCooldownDefinition> SelectCanonicalReplacements(
        IEnumerable<PartyCooldownDefinition> definitions,
        Func<PartyCooldownDefinition, string> getEquivalenceKey)
        => SelectHighestReplacements(definitions, _ => true, getEquivalenceKey);

    private static IReadOnlyList<PartyCooldownDefinition> SelectHighestReplacements(
        IEnumerable<PartyCooldownDefinition> definitions,
        Func<PartyCooldownDefinition, bool> canUseDefinition,
        Func<PartyCooldownDefinition, string> getEquivalenceKey)
    {
        var selected = new List<PartyCooldownDefinition>();
        var selectedByEquivalence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!canUseDefinition(definition))
                continue;

            var equivalenceKey = getEquivalenceKey(definition);
            if (string.IsNullOrWhiteSpace(equivalenceKey))
            {
                selected.Add(definition);
                continue;
            }

            if (!selectedByEquivalence.TryGetValue(equivalenceKey, out var existingIndex))
            {
                selectedByEquivalence[equivalenceKey] = selected.Count;
                selected.Add(definition);
                continue;
            }

            if (IsHigherLevelReplacement(definition, selected[existingIndex]))
                selected[existingIndex] = definition;
        }

        return selected;
    }

    private static bool IsHigherLevelReplacement(PartyCooldownDefinition candidate, PartyCooldownDefinition current)
        => candidate.Level > current.Level
           || (candidate.Level == current.Level && candidate.ActionId > current.ActionId);
}
