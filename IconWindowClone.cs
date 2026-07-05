namespace FFXIVAura;

internal static class IconWindowClone
{
    public static Dictionary<string, List<string>> CloneStringListMap(Dictionary<string, List<string>> source)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, values) in source)
        {
            var normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey) || values is null)
                continue;

            var normalizedValues = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedValues.Count > 0)
                result[normalizedKey] = normalizedValues;
        }

        return result;
    }

    public static Dictionary<string, Dictionary<string, Vector2>> CloneVector2Map(
        Dictionary<string, Dictionary<string, Vector2>> source)
    {
        var result = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, positions) in source)
        {
            var normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey) || positions is null)
                continue;

            var clonedPositions = ClonePositionDictionary(positions);
            if (clonedPositions.Count > 0)
                result[normalizedKey] = clonedPositions;
        }

        return result;
    }

    public static Dictionary<string, Dictionary<string, Vector2>> CloneAuraPositionsForWindow(
        Dictionary<string, Dictionary<string, Vector2>> source,
        string sourceWindowId,
        string targetWindowId)
    {
        var result = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sourceWindowId)
            || string.IsNullOrWhiteSpace(targetWindowId)
            || string.Equals(sourceWindowId.Trim(), targetWindowId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var sourcePrefix = OverlayPositionKeys.WindowPrefix(sourceWindowId);
        foreach (var pair in source)
        {
            if (!pair.Key.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) || pair.Value is null)
                continue;

            var clonedPositions = ClonePositionDictionary(pair.Value, key => OverlayPositionKeys.TryParseAura(key, out _));
            if (clonedPositions.Count > 0)
                result[OverlayPositionKeys.ReplaceWindowPrefix(pair.Key, sourceWindowId, targetWindowId)] = clonedPositions;
        }

        return result;
    }

    private static Dictionary<string, Vector2> ClonePositionDictionary(Dictionary<string, Vector2> positions, Func<string, bool>? keyFilter = null)
    {
        var clonedPositions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        foreach (var (innerKey, position) in positions)
        {
            var normalizedInnerKey = innerKey?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedInnerKey) || !ConfigValueNormalizer.IsFinitePosition(position))
                continue;

            if (keyFilter is not null && !keyFilter(normalizedInnerKey))
                continue;

            clonedPositions[normalizedInnerKey] = position;
        }

        return clonedPositions;
    }
}
