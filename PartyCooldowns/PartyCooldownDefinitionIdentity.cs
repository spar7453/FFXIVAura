namespace FFXIVAura;

internal static class PartyCooldownDefinitionIdentity
{
    public static Dictionary<string, string> BuildCanonicalDefinitionIds(
        IEnumerable<PartyCooldownDefinition> definitions,
        Func<PartyCooldownDefinition, string> getEquivalenceKey)
    {
        var canonicalIdsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var definitionsByEquivalence = new Dictionary<string, List<PartyCooldownDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var definitionId = definition.Id.Trim();
            if (definitionId.Length == 0)
                continue;

            var equivalenceKey = getEquivalenceKey(definition);
            if (string.IsNullOrWhiteSpace(equivalenceKey))
            {
                canonicalIdsById[definitionId] = definitionId;
                continue;
            }

            if (!definitionsByEquivalence.TryGetValue(equivalenceKey, out var equivalentDefinitions))
            {
                equivalentDefinitions = [];
                definitionsByEquivalence[equivalenceKey] = equivalentDefinitions;
            }

            equivalentDefinitions.Add(definition);
        }

        foreach (var equivalentDefinitions in definitionsByEquivalence.Values)
        {
            var canonicalDefinition = PartyCooldownDefinitionSelector
                .SelectCanonicalReplacements(equivalentDefinitions, getEquivalenceKey)
                .FirstOrDefault();
            if (canonicalDefinition is null)
                continue;

            var canonicalDefinitionId = canonicalDefinition.Id.Trim();
            if (canonicalDefinitionId.Length == 0)
                continue;

            foreach (var definition in equivalentDefinitions)
            {
                var definitionId = definition.Id.Trim();
                if (definitionId.Length > 0)
                    canonicalIdsById[definitionId] = canonicalDefinitionId;
            }
        }

        return canonicalIdsById;
    }

    public static bool IsExcluded(
        IReadOnlyCollection<string> excludedIds,
        IReadOnlyDictionary<string, string> canonicalIdsById,
        PartyCooldownDefinition definition)
    {
        if (excludedIds.Count == 0)
            return false;

        var canonicalDefinitionId = GetCanonicalDefinitionId(canonicalIdsById, definition);
        foreach (var excludedId in excludedIds)
        {
            var normalizedExcludedId = excludedId.Trim();
            if (normalizedExcludedId.Length == 0)
                continue;

            if (string.Equals(normalizedExcludedId, definition.Id.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedExcludedId, canonicalDefinitionId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (canonicalIdsById.TryGetValue(normalizedExcludedId, out var excludedCanonicalId)
                && string.Equals(excludedCanonicalId, canonicalDefinitionId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void RemoveExclusions(
        List<string> excludedIds,
        IReadOnlyDictionary<string, string> canonicalIdsById,
        IEnumerable<PartyCooldownDefinition> definitions)
    {
        var removalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            removalIds.Add(definition.Id.Trim());
            removalIds.Add(GetCanonicalDefinitionId(canonicalIdsById, definition));
        }

        excludedIds.RemoveAll(excludedId =>
            removalIds.Contains(excludedId.Trim())
            || (canonicalIdsById.TryGetValue(excludedId.Trim(), out var excludedCanonicalId)
                && removalIds.Contains(excludedCanonicalId)));
    }

    public static string RuntimeKey(
        string memberKey,
        IReadOnlyDictionary<string, string> canonicalIdsById,
        PartyCooldownDefinition definition)
        => $"{memberKey}:{GetCanonicalDefinitionId(canonicalIdsById, definition)}";

    public static string GetCanonicalDefinitionId(
        IReadOnlyDictionary<string, string> canonicalIdsById,
        PartyCooldownDefinition definition)
        => canonicalIdsById.TryGetValue(definition.Id.Trim(), out var canonicalDefinitionId)
            ? canonicalDefinitionId
            : definition.Id.Trim();
}
