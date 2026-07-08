namespace FFXIVAura;

internal static class IconWindowIdentity
{
    public static void RemapAuraPositionGroups(
        Dictionary<string, Dictionary<string, Vector2>> auraPositionsByRole,
        string previousWindowId,
        string nextWindowId)
    {
        var previous = previousWindowId.Trim();
        var next = nextWindowId.Trim();
        if (previous.Length == 0
            || next.Length == 0
            || string.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousPrefix = OverlayPositionKeys.WindowPrefix(previous);
        foreach (var key in auraPositionsByRole.Keys.ToList())
        {
            if (!key.StartsWith(previousPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var nextKey = OverlayPositionKeys.ReplaceWindowPrefix(key, previous, next);
            var sourcePositions = auraPositionsByRole[key];
            if (!auraPositionsByRole.TryGetValue(nextKey, out var targetPositions) || targetPositions is null)
            {
                auraPositionsByRole[nextKey] = sourcePositions;
            }
            else
            {
                foreach (var (positionKey, position) in sourcePositions)
                {
                    if (!targetPositions.ContainsKey(positionKey))
                        targetPositions[positionKey] = position;
                }
            }

            auraPositionsByRole.Remove(key);
        }
    }

    public static string CreateUniqueId(HashSet<string> usedWindowIds, ref int nextWindowNumber)
    {
        var number = Math.Max(1, nextWindowNumber);
        while (number < 10000)
        {
            var id = $"win{number}";
            number++;
            if (!usedWindowIds.Add(id))
                continue;

            nextWindowNumber = number;
            return id;
        }

        var fallback = $"win{Guid.NewGuid():N}";
        usedWindowIds.Add(fallback);
        return fallback;
    }

    public static int GetNumber(string windowId)
    {
        var normalized = windowId.Trim();
        return normalized.StartsWith("win", StringComparison.OrdinalIgnoreCase)
               && int.TryParse(normalized[3..], out var number)
            ? number
            : 0;
    }

    public static int GetNextAvailableNumber(IEnumerable<string> windowIds, int windowCounter)
    {
        var used = windowIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(GetNumber)
            .Where(number => number > 0)
            .ToHashSet();

        for (var number = 1; number < 10000; number++)
        {
            if (!used.Contains(number))
                return number;
        }

        return Math.Max(1, Math.Max(windowCounter + 1, used.Count + 1));
    }

    public static bool IsBrokenName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var trimmed = name.Trim();
        if (string.Equals(trimmed, "win", StringComparison.OrdinalIgnoreCase)
            || (trimmed.Length > 3
                && trimmed.StartsWith("win", StringComparison.OrdinalIgnoreCase)
                && trimmed[3..].All(char.IsDigit)))
        {
            return true;
        }

        return false;
    }

    public static string GetDefaultName(string windowId)
    {
        var normalized = windowId.Trim();
        if (normalized.StartsWith("win", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[3..], out var number)
            && number > 0)
            return $"\uCC3D {number}";

        return "\uCC3D";
    }
}
