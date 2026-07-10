namespace FFXIVAura;

internal readonly record struct SkillPositionLayoutOptions(
    IconAlignment Alignment,
    Vector2 AreaSize,
    float IconSize,
    float Gap);

internal readonly record struct SkillPositionLayoutItem(
    string Id,
    int Index,
    int Order,
    Vector2 Position);

internal static class SkillPositionLayout
{
    public static string BuildVisibleKey(uint level, IEnumerable<string> visibleIds)
        => $"{level}:{string.Join("|", visibleIds)}";

    public static string BuildVisibleLayoutKey(
        uint level,
        IEnumerable<string> visibleIds,
        IconAlignment alignment,
        Vector2 areaSize,
        float iconSize,
        float gap)
        => FormattableString.Invariant(
            $"{BuildVisibleKey(level, visibleIds)}:{alignment}:{areaSize.X:R}x{areaSize.Y:R}:{iconSize:R}:{gap:R}");

    public static bool HasHiddenSavedPositions(
        IEnumerable<string> savedKeys,
        IReadOnlyList<string> visibleIds,
        Func<string, string, bool> idsMatch,
        Func<string, bool> isTracked)
    {
        foreach (var key in savedKeys)
        {
            if (visibleIds.Any(visibleId => idsMatch(key, visibleId)))
                continue;

            if (isTracked(key))
                return true;
        }

        return false;
    }

    public static bool RemoveStalePositions(Dictionary<string, Vector2> positions, Func<string, bool> isTracked)
    {
        var changed = false;
        foreach (var key in positions.Keys.ToList())
        {
            if (isTracked(key))
                continue;

            positions.Remove(key);
            changed = true;
        }

        return changed;
    }

    public static bool AddMissingPositions(
        Dictionary<string, Vector2> positions,
        IReadOnlyList<string> visibleIds,
        SkillPositionLayoutOptions options,
        Func<string, string, bool> idsMatch,
        Func<string, bool> isTracked)
    {
        var changed = RemoveStalePositions(positions, isTracked);
        var occupied = GetOccupiedPositions(positions, visibleIds, options, idsMatch);

        for (var index = 0; index < visibleIds.Count; index++)
        {
            var id = visibleIds[index];
            if (TryGetPosition(positions, id, idsMatch, out _))
                continue;

            var position = FindFreePosition(options, index, visibleIds.Count, occupied);
            SetPosition(positions, id, position, idsMatch);
            occupied.Add(position);
            changed = true;
        }

        return changed;
    }

    public static Vector2 GetPosition(
        IReadOnlyDictionary<string, Vector2> positions,
        string id,
        int index,
        int visibleCount,
        SkillPositionLayoutOptions options,
        Func<string, string, bool> idsMatch)
    {
        if (TryGetPosition(positions, id, idsMatch, out var saved))
            return OverlayLayout.ClampIconPosition(saved, options.AreaSize, options.IconSize);

        return OverlayLayout.GetAutoPosition(options.Alignment, index, visibleCount, options.AreaSize, options.IconSize, options.Gap);
    }

    public static bool TryGetPosition(
        IReadOnlyDictionary<string, Vector2> positions,
        string id,
        Func<string, string, bool> idsMatch,
        out Vector2 position)
    {
        if (positions.TryGetValue(id, out position))
            return true;

        foreach (var (key, saved) in positions)
        {
            if (!idsMatch(key, id))
                continue;

            position = saved;
            return true;
        }

        position = default;
        return false;
    }

    public static void SetPosition(
        Dictionary<string, Vector2> positions,
        string id,
        Vector2 position,
        Func<string, string, bool> idsMatch)
    {
        foreach (var key in positions.Keys.ToList())
        {
            if (string.Equals(key, id, StringComparison.OrdinalIgnoreCase) || !idsMatch(key, id))
                continue;

            positions.Remove(key);
        }

        positions[id] = position;
    }

    public static void SetPositionInLayouts(
        Dictionary<string, Vector2> savedPositions,
        Dictionary<string, Vector2>? activePositions,
        string id,
        Vector2 position,
        Func<string, string, bool> idsMatch)
    {
        SetPosition(savedPositions, id, position, idsMatch);
        if (activePositions is not null && !ReferenceEquals(savedPositions, activePositions))
            SetPosition(activePositions, id, position, idsMatch);
    }

    public static void AlignPositions(
        Dictionary<string, Vector2> positions,
        IReadOnlyList<SkillPositionLayoutItem> visibleItems,
        SkillPositionLayoutOptions options,
        bool preferTrackedOrder,
        Func<string, string, bool> idsMatch)
    {
        if (visibleItems.Count == 0)
            return;

        var orderedRows = GetRows(visibleItems, options.IconSize)
            .OrderBy(AverageY)
            .ToList();
        var verticalGap = Math.Max(0f, options.Gap);
        var rowStep = options.IconSize + verticalGap;
        var totalHeight = Math.Max(0f, orderedRows.Count * options.IconSize + Math.Max(0, orderedRows.Count - 1) * verticalGap);
        var startY = Math.Max(0f, MathF.Round((options.AreaSize.Y - totalHeight) * 0.5f));

        for (var rowIndex = 0; rowIndex < orderedRows.Count; rowIndex++)
        {
            var row = orderedRows[rowIndex];
            var items = (preferTrackedOrder
                    ? row
                        .OrderBy(item => item.Order)
                        .ThenBy(item => item.Position.X)
                    : row
                        .OrderBy(item => item.Position.X)
                        .ThenBy(item => item.Order))
                .ThenBy(item => item.Index)
                .ToList();
            var rowWidth = Math.Max(0f, items.Count * options.IconSize + Math.Max(0, items.Count - 1) * options.Gap);
            var startX = OverlayLayout.GetAlignedRowStartX(options.Alignment, options.AreaSize.X, rowWidth);
            var y = Math.Clamp(MathF.Round(startY + rowIndex * rowStep), 0f, Math.Max(0f, options.AreaSize.Y - options.IconSize));

            for (var column = 0; column < items.Count; column++)
            {
                SetPosition(
                    positions,
                    items[column].Id,
                    OverlayLayout.ClampIconPosition(
                        new Vector2(startX + column * (options.IconSize + options.Gap), y),
                        options.AreaSize,
                        options.IconSize),
                    idsMatch);
            }
        }

        NormalizePositions(positions, visibleItems.Select(item => item.Id).ToList(), options, idsMatch);
    }

    public static Dictionary<string, Vector2> CreateAlignedVisibleCopy(
        IReadOnlyDictionary<string, Vector2> savedPositions,
        IReadOnlyList<SkillPositionLayoutItem> visibleItems,
        SkillPositionLayoutOptions options,
        bool preferTrackedOrder,
        Func<string, string, bool> idsMatch)
    {
        var working = new Dictionary<string, Vector2>(savedPositions, StringComparer.OrdinalIgnoreCase);
        AlignPositions(working, visibleItems, options, preferTrackedOrder, idsMatch);

        var result = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in visibleItems)
        {
            if (TryGetPosition(working, item.Id, idsMatch, out var position))
                result[item.Id] = position;
        }

        return result;
    }

    private static List<Vector2> GetOccupiedPositions(
        IReadOnlyDictionary<string, Vector2> positions,
        IEnumerable<string> ids,
        SkillPositionLayoutOptions options,
        Func<string, string, bool> idsMatch)
    {
        var occupied = new List<Vector2>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (!seen.Add(id) || !TryGetPosition(positions, id, idsMatch, out var position))
                continue;

            occupied.Add(OverlayLayout.ClampIconPosition(position, options.AreaSize, options.IconSize));
        }

        return occupied;
    }

    private static Vector2 FindFreePosition(
        SkillPositionLayoutOptions options,
        int preferredIndex,
        int visibleCount,
        IReadOnlyList<Vector2> occupied)
    {
        return OverlayLayout.FindFreeAutoPosition(
            options.Alignment,
            preferredIndex,
            visibleCount,
            occupied,
            options.AreaSize,
            options.IconSize,
            options.Gap);
    }

    private static void NormalizePositions(
        Dictionary<string, Vector2> positions,
        IReadOnlyList<string> orderedKeys,
        SkillPositionLayoutOptions options,
        Func<string, string, bool> idsMatch)
    {
        var occupied = new List<Vector2>();
        var allKeys = positions.Keys.ToList();
        var slotCount = Math.Max(orderedKeys.Count, allKeys.Count);

        for (var index = 0; index < orderedKeys.Count; index++)
        {
            var key = orderedKeys[index];
            if (!TryGetPosition(positions, key, idsMatch, out var position))
                continue;

            var next = OverlayLayout.ClampIconPosition(position, options.AreaSize, options.IconSize);
            if (occupied.Any(placed => OverlayLayout.IconPositionsOverlap(placed, next, options.IconSize)))
                next = FindFreePosition(options, index, slotCount, occupied);

            SetPosition(positions, key, next, idsMatch);
            occupied.Add(next);
        }

        allKeys = positions.Keys.ToList();
        for (var index = 0; index < allKeys.Count; index++)
        {
            var key = allKeys[index];
            if (IsOrderedKey(key, orderedKeys, idsMatch))
                continue;

            var next = OverlayLayout.ClampIconPosition(positions[key], options.AreaSize, options.IconSize);
            if (occupied.Any(placed => OverlayLayout.IconPositionsOverlap(placed, next, options.IconSize)))
                next = FindFreePosition(options, index, slotCount, occupied);

            positions[key] = next;
            occupied.Add(next);
        }
    }

    public static List<List<SkillPositionLayoutItem>> GetRows(
        IReadOnlyList<SkillPositionLayoutItem> items,
        float iconSize)
    {
        var rowThreshold = Math.Max(6f, MathF.Round(iconSize * 0.55f));
        var rows = new List<List<SkillPositionLayoutItem>>();
        foreach (var item in items
                     .OrderBy(item => item.Position.Y)
                     .ThenBy(item => item.Position.X)
                     .ThenBy(item => item.Index))
        {
            var lastRow = rows.Count > 0 ? rows[^1] : null;
            if (lastRow is null || Math.Abs(item.Position.Y - AverageY(lastRow)) > rowThreshold)
            {
                rows.Add([item]);
                continue;
            }

            lastRow.Add(item);
        }

        return rows;
    }

    private static float AverageY(IReadOnlyList<SkillPositionLayoutItem> row)
        => row.Count == 0 ? 0f : row.Sum(item => item.Position.Y) / row.Count;

    private static bool IsOrderedKey(string key, IReadOnlyList<string> orderedKeys, Func<string, string, bool> idsMatch)
    {
        if (orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            return true;

        return orderedKeys.Any(orderedKey => idsMatch(key, orderedKey));
    }
}
