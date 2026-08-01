namespace FFXIVAura;

public sealed partial class Plugin
{
    private bool EnsureOverlayPositionsForItems(
        IconWindowConfig iconWindow,
        string job,
        uint level,
        OverlayItemSet items,
        Vector2 areaSize)
    {
        if (iconWindow.Role == IconWindowRole.SkillCooldowns)
            return this.AutoAlignWhenVisibleSkillsChanged(iconWindow, job, level, items.Abilities, areaSize);

        return IconWindowRoles.IsStandardAuraRole(iconWindow.Role)
            && this.EnsureAuraIconPositions(iconWindow, items.Auras, areaSize);
    }

    private Vector2 GetOverlayAutoPosition(IconWindowConfig iconWindow, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        return OverlayLayout.GetAutoPosition(iconWindow.Alignment, index, visibleCount, areaSize, iconSize, gap);
    }

    private IEnumerable<Vector2> GetOverlayAutoPositionSlots(IconWindowConfig iconWindow, Vector2 areaSize)
    {
        return OverlayLayout.GetAutoPositionSlots(iconWindow.Alignment, areaSize, iconWindow.IconSize, iconWindow.Gap);
    }

    private Vector2 FindFreeOverlayAutoPosition(IconWindowConfig iconWindow, int preferredIndex, int visibleCount, IReadOnlyList<Vector2> occupied, Vector2 areaSize)
    {
        return OverlayLayout.FindFreeAutoPosition(iconWindow.Alignment, preferredIndex, visibleCount, occupied, areaSize, iconWindow.IconSize, iconWindow.Gap);
    }

    private Vector2 GetAuraAutoPosition(IconWindowConfig iconWindow, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        return OverlayLayout.GetCompactPosition(iconWindow.Alignment, index, visibleCount, areaSize, iconSize, gap);
    }

    private float GetAlignedRowStartX(IconWindowConfig iconWindow, float areaWidth, float rowWidth)
    {
        return OverlayLayout.GetAlignedRowStartX(iconWindow.Alignment, areaWidth, rowWidth);
    }

    private Vector2 ClampOverlayIconPosition(Vector2 position, Vector2 areaSize, float iconSize)
    {
        return OverlayLayout.ClampIconPosition(position, areaSize, iconSize);
    }

    private void NormalizeIconPositionsAfterResize(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        Vector2 areaSize)
    {
        foreach (var (positionJob, skillPositions) in iconWindow.IconPositionsByJob.ToList())
        {
            var orderedKeys = string.Equals(positionJob, job, StringComparison.OrdinalIgnoreCase)
                ? visible.Select(ability => ability.Id).ToList()
                : [];
            this.RemoveStaleOverlayIconPositions(skillPositions, positionJob);
            if (skillPositions.Count == 0 && orderedKeys.Count == 0)
            {
                iconWindow.IconPositionsByJob.Remove(positionJob);
                continue;
            }

            this.NormalizeIconPositions(skillPositions, orderedKeys, iconWindow, areaSize, positionJob);
        }

    }

    private bool EnsureAuraIconPositions(IconWindowConfig iconWindow, IReadOnlyList<AuraState> auras, Vector2 areaSize)
    {
        return UsesCompactAuraLayout(iconWindow)
            ? false
            : this.AddMissingAuraIconPositions(iconWindow, auras, areaSize);
    }

    private void NormalizeAuraIconPositionsAfterResize(
        IconWindowConfig iconWindow,
        IReadOnlyList<AuraState> auras,
        Vector2 areaSize)
    {
        if (UsesCompactAuraLayout(iconWindow))
            return;

        var auraGroupKey = OverlayPositionKeys.AuraGroup(iconWindow);
        var auraGroupPrefix = OverlayPositionKeys.WindowPrefix(iconWindow.Id);
        foreach (var (groupKey, auraPositions) in iconWindow.AuraPositionsByRole.ToList())
        {
            if (!groupKey.StartsWith(auraGroupPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var orderedKeys = string.Equals(groupKey, auraGroupKey, StringComparison.OrdinalIgnoreCase)
                ? auras.Select(aura => OverlayPositionKeys.Aura(aura.StatusId)).ToList()
                : [];
            this.RemoveStaleAuraIconPositions(iconWindow, auraPositions);
            if (auraPositions.Count == 0 && orderedKeys.Count == 0)
            {
                iconWindow.AuraPositionsByRole.Remove(groupKey);
                continue;
            }

            this.NormalizeIconPositions(auraPositions, orderedKeys, iconWindow, areaSize);
        }
    }

    private void NormalizeIconPositions(Dictionary<string, Vector2> positions, IReadOnlyList<string> orderedKeys, IconWindowConfig iconWindow, Vector2 areaSize, string? job = null)
    {
        var occupied = new List<Vector2>();
        var allKeys = positions.Keys.ToList();
        var slotCount = Math.Max(orderedKeys.Count, allKeys.Count);

        for (var index = 0; index < orderedKeys.Count; index++)
        {
            var key = orderedKeys[index];
            if (!this.TryGetIconPosition(positions, key, job, out var position))
                continue;

            var next = this.ClampOverlayIconPosition(position, areaSize, iconWindow.IconSize);
            if (occupied.Any(placed => OverlayIconPositionsOverlap(placed, next, iconWindow.IconSize)))
                next = this.FindFreeOverlayAutoPosition(iconWindow, index, slotCount, occupied, areaSize);

            this.SetIconPosition(positions, key, job, next);
            occupied.Add(next);
        }

        allKeys = positions.Keys.ToList();
        for (var index = 0; index < allKeys.Count; index++)
        {
            var key = allKeys[index];
            if (this.IsOrderedIconKey(key, orderedKeys, job))
                continue;

            var next = this.ClampOverlayIconPosition(positions[key], areaSize, iconWindow.IconSize);
            if (occupied.Any(placed => OverlayIconPositionsOverlap(placed, next, iconWindow.IconSize)))
                next = this.FindFreeOverlayAutoPosition(iconWindow, index, slotCount, occupied, areaSize);

            positions[key] = next;
            occupied.Add(next);
        }
    }

    private static bool OverlayIconPositionsOverlap(Vector2 first, Vector2 second, float iconSize)
    {
        return OverlayLayout.IconPositionsOverlap(first, second, iconSize);
    }

    private List<Vector2> GetOccupiedIconPositions(
        Dictionary<string, Vector2> positions,
        IEnumerable<string> keys,
        Vector2 areaSize,
        float iconSize,
        string? job = null)
    {
        var occupied = new List<Vector2>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (!seen.Add(key) || !this.TryGetIconPosition(positions, key, job, out var position))
                continue;

            occupied.Add(this.ClampOverlayIconPosition(position, areaSize, iconSize));
        }

        return occupied;
    }

    private bool IsOrderedIconKey(string key, IReadOnlyList<string> orderedKeys, string? job)
    {
        if (orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            return true;

        return job is not null && orderedKeys.Any(orderedKey => this.abilityCatalog.IdsMatch(key, orderedKey, job));
    }

    private bool TryGetIconPosition(Dictionary<string, Vector2> positions, string key, string? job, out Vector2 position)
    {
        return job is null
            ? positions.TryGetValue(key, out position)
            : this.TryGetOverlayIconPosition(positions, job, key, out position);
    }

    private void SetIconPosition(Dictionary<string, Vector2> positions, string key, string? job, Vector2 position)
    {
        if (job is null)
            positions[key] = position;
        else
            this.SetOverlayIconPosition(positions, job, key, position);
    }

}
