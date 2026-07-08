namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private Vector2 GetAuraIconPosition(IconWindowConfig iconWindow, AuraState aura, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (UsesCompactAuraLayout(iconWindow))
            return this.GetAuraAutoPosition(iconWindow, index, visibleCount, areaSize, iconSize, gap);

        if (iconWindow.AuraPositionsByRole.TryGetValue(OverlayPositionKeys.AuraGroup(iconWindow), out var positions)
            && positions.TryGetValue(OverlayPositionKeys.Aura(aura.StatusId), out var saved))
        {
            return this.ClampOverlayIconPosition(saved, areaSize, iconSize);
        }

        return this.GetOverlayAutoPosition(iconWindow, index, visibleCount, areaSize, iconSize, gap);
    }

    private static bool UsesCompactAuraLayout(IconWindowConfig iconWindow)
        => IconWindowRoles.IsStandardAuraRole(iconWindow.Role)
           && iconWindow.DisplayCondition == IconDisplayCondition.CoolingOnly;

    private bool AddMissingAuraIconPositions(IconWindowConfig iconWindow, IReadOnlyList<AuraState> auras, Vector2 areaSize)
    {
        var groupKey = OverlayPositionKeys.AuraGroup(iconWindow);
        if (!iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.AuraPositionsByRole[groupKey] = positions;
        }

        var changed = this.RemoveStaleAuraIconPositions(iconWindow, positions);
        var visibleKeys = auras.Select(aura => OverlayPositionKeys.Aura(aura.StatusId)).ToList();
        var occupied = this.GetOccupiedIconPositions(positions, visibleKeys, areaSize, iconWindow.IconSize);

        for (var index = 0; index < auras.Count; index++)
        {
            var key = OverlayPositionKeys.Aura(auras[index].StatusId);
            if (positions.ContainsKey(key))
                continue;

            var position = this.FindFreeOverlayAutoPosition(iconWindow, index, auras.Count, occupied, areaSize);
            positions[key] = position;
            occupied.Add(position);
            changed = true;
        }

        if (positions.Count == 0)
            iconWindow.AuraPositionsByRole.Remove(groupKey);

        return changed;
    }

    private void SetAuraIconPosition(IconWindowConfig iconWindow, uint statusId, Vector2 position)
    {
        var groupKey = OverlayPositionKeys.AuraGroup(iconWindow);
        if (!iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.AuraPositionsByRole[groupKey] = positions;
        }

        positions[OverlayPositionKeys.Aura(statusId)] = position;
    }

    private void AlignAuraIcons(IconWindowConfig iconWindow)
    {
        if (UsesCompactAuraLayout(iconWindow))
            return;

        var groupKey = OverlayPositionKeys.AuraGroup(iconWindow);
        var auras = this.GetOverlayAuras(iconWindow, OverlayItemVisibility.Layout).ToList();
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var positions = iconWindow.AuraPositionsByRole.TryGetValue(groupKey, out var existing)
            ? new Dictionary<string, Vector2>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        this.RemoveStaleAuraIconPositions(iconWindow, positions);
        if (auras.Count == 0)
        {
            if (positions.Count == 0)
                iconWindow.AuraPositionsByRole.Remove(groupKey);
            else
                iconWindow.AuraPositionsByRole[groupKey] = positions;

            return;
        }

        var orderedKeys = auras.Select(aura => OverlayPositionKeys.Aura(aura.StatusId)).ToList();
        for (var index = 0; index < auras.Count; index++)
        {
            var position = this.GetOverlayAutoPosition(iconWindow, index, auras.Count, areaSize, iconWindow.IconSize, iconWindow.Gap);
            positions[OverlayPositionKeys.Aura(auras[index].StatusId)] = position;
        }

        this.NormalizeIconPositions(positions, orderedKeys, iconWindow, areaSize);
        iconWindow.AuraPositionsByRole[groupKey] = positions;
    }

    private bool RemoveStaleAuraIconPositions(IconWindowConfig iconWindow, Dictionary<string, Vector2> positions)
    {
        var trackedStatusIds = iconWindow.TrackedStatusIds.ToHashSet();
        var changed = false;
        foreach (var key in positions.Keys.ToList())
        {
            if (OverlayPositionKeys.TryParseAura(key, out var statusId) && trackedStatusIds.Contains(statusId))
                continue;

            positions.Remove(key);
            changed = true;
        }

        return changed;
    }

}
