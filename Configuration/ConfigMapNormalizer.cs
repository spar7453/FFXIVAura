namespace FFXIVAura;

internal static class ConfigMapNormalizer
{
    public static bool NormalizeStatusIds(List<uint> statusIds)
    {
        var normalized = statusIds
            .Where(statusId => statusId > 0)
            .Distinct()
            .ToList();
        if (normalized.Count == statusIds.Count && normalized.SequenceEqual(statusIds))
            return false;

        statusIds.Clear();
        statusIds.AddRange(normalized);
        return true;
    }

    public static List<string> NormalizeStringList(List<string>? source, out bool changed)
    {
        changed = source is null;
        var normalized = new List<string>();
        if (source is null)
            return normalized;

        foreach (var rawValue in source)
        {
            var value = rawValue?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                changed = true;
                continue;
            }

            if (!string.Equals(rawValue, value, StringComparison.Ordinal))
                changed = true;

            if (normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                changed = true;
                continue;
            }

            normalized.Add(value);
        }

        if (!changed && normalized.SequenceEqual(source, StringComparer.OrdinalIgnoreCase))
            return source;

        changed = true;
        return normalized;
    }

    public static Dictionary<string, List<string>> NormalizeStringListMap(Dictionary<string, List<string>>? source, out bool changed)
    {
        changed = source is null || !Equals(source.Comparer, StringComparer.OrdinalIgnoreCase);
        var normalizedMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
            return normalizedMap;

        foreach (var (rawKey, values) in source)
        {
            var key = rawKey?.Trim();
            if (string.IsNullOrWhiteSpace(key) || values is null)
            {
                changed = true;
                continue;
            }

            if (!string.Equals(rawKey, key, StringComparison.Ordinal))
                changed = true;

            if (!normalizedMap.TryGetValue(key, out var normalizedValues))
            {
                normalizedValues = [];
                normalizedMap[key] = normalizedValues;
            }
            else
            {
                changed = true;
            }

            foreach (var rawValue in values)
            {
                var value = rawValue?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(rawValue, value, StringComparison.Ordinal))
                    changed = true;

                if (normalizedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    changed = true;
                    continue;
                }

                normalizedValues.Add(value);
            }

            if (normalizedValues.Count == 0)
            {
                normalizedMap.Remove(key);
                changed = true;
            }
        }

        if (!changed && StringListMapsEqual(source, normalizedMap))
            return source;

        changed = true;
        return normalizedMap;
    }

    public static Dictionary<string, Dictionary<string, Vector2>> NormalizeVector2Map(
        Dictionary<string, Dictionary<string, Vector2>>? source,
        Vector2 areaSize,
        float iconSize,
        out bool changed)
    {
        changed = source is null || !Equals(source.Comparer, StringComparer.OrdinalIgnoreCase);
        var normalizedMap = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
            return normalizedMap;

        foreach (var (rawOuterKey, positions) in source)
        {
            var outerKey = rawOuterKey?.Trim();
            if (string.IsNullOrWhiteSpace(outerKey) || positions is null)
            {
                changed = true;
                continue;
            }

            if (!string.Equals(rawOuterKey, outerKey, StringComparison.Ordinal))
                changed = true;

            if (!Equals(positions.Comparer, StringComparer.OrdinalIgnoreCase))
                changed = true;

            if (!normalizedMap.TryGetValue(outerKey, out var normalizedPositions))
            {
                normalizedPositions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
                normalizedMap[outerKey] = normalizedPositions;
            }
            else
            {
                changed = true;
            }

            foreach (var (rawInnerKey, position) in positions)
            {
                var innerKey = rawInnerKey?.Trim();
                if (string.IsNullOrWhiteSpace(innerKey) || !ConfigValueNormalizer.IsFinitePosition(position))
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(rawInnerKey, innerKey, StringComparison.Ordinal))
                    changed = true;

                var normalized = OverlayLayout.ClampIconPosition(position, areaSize, iconSize);
                if (Vector2.DistanceSquared(position, normalized) > 0.25f)
                    changed = true;

                if (normalizedPositions.ContainsKey(innerKey))
                    changed = true;

                normalizedPositions[innerKey] = normalized;
            }

            if (normalizedPositions.Count == 0)
            {
                normalizedMap.Remove(outerKey);
                changed = true;
            }
        }

        if (!changed && Vector2MapsEqual(source, normalizedMap))
            return source;

        changed = true;
        return normalizedMap;
    }

    private static bool StringListMapsEqual(Dictionary<string, List<string>> first, Dictionary<string, List<string>> second)
    {
        if (first.Count != second.Count)
            return false;

        foreach (var (key, values) in first)
        {
            if (!second.TryGetValue(key, out var otherValues)
                || values.Count != otherValues.Count
                || !values.SequenceEqual(otherValues, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Vector2MapsEqual(
        Dictionary<string, Dictionary<string, Vector2>> first,
        Dictionary<string, Dictionary<string, Vector2>> second)
    {
        if (first.Count != second.Count)
            return false;

        foreach (var (outerKey, positions) in first)
        {
            if (!second.TryGetValue(outerKey, out var otherPositions) || positions.Count != otherPositions.Count)
                return false;

            foreach (var (innerKey, position) in positions)
            {
                if (!otherPositions.TryGetValue(innerKey, out var otherPosition)
                    || Vector2.DistanceSquared(position, otherPosition) > 0.25f)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
