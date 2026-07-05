namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool AutoAlignWhenVisibleSkillsChanged(IconWindowConfig iconWindow, string job, uint level, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        var key = OverlayPositionKeys.VisibleAbilityGroup(iconWindow.Id, job);
        var visibleKey = $"{level}:{string.Join("|", visible.Select(ability => ability.Id))}";
        var hasTrackedSkills = iconWindow.TrackedByJob.TryGetValue(job, out var tracked) && tracked.Count > 0;
        var hasSavedPositions = iconWindow.IconPositionsByJob.TryGetValue(job, out var positions) && positions.Count > 0;
        var shouldRealignLevelFilteredSkills = positions is not null
                                               && hasSavedPositions
                                               && visible.Count > 0
                                               && this.HasHiddenSavedSkillPositions(positions, job, visible);
        if (this.visibleAbilityKeys.TryGetValue(key, out var previous) && string.Equals(previous, visibleKey, StringComparison.Ordinal))
        {
            if (hasSavedPositions)
                return this.AddMissingOverlayIconPositions(iconWindow, job, visible, areaSize);

            if (hasTrackedSkills && visible.Count > 0)
            {
                this.AlignOverlayIcons(iconWindow, job, level);
                return true;
            }

            return false;
        }

        this.visibleAbilityKeys[key] = visibleKey;
        if (shouldRealignLevelFilteredSkills)
        {
            this.AlignOverlayIcons(iconWindow, job, level);
            return true;
        }

        if (!hasSavedPositions)
        {
            if (hasTrackedSkills && visible.Count > 0)
            {
                this.AlignOverlayIcons(iconWindow, job, level);
                return true;
            }

            return false;
        }

        return this.AddMissingOverlayIconPositions(iconWindow, job, visible, areaSize);
    }

    private bool HasHiddenSavedSkillPositions(Dictionary<string, Vector2> positions, string job, IReadOnlyList<AbilityDefinition> visible)
    {
        var visibleIds = visible.Select(ability => ability.Id).ToList();
        foreach (var key in positions.Keys)
        {
            if (visibleIds.Any(visibleId => this.TrackedAbilityIdsMatch(key, visibleId, job)))
                continue;

            if (this.FindTrackedAbilityDefinition(key, job) is not null)
                return true;
        }

        return false;
    }

    private bool AddMissingOverlayIconPositions(IconWindowConfig iconWindow, string job, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.IconPositionsByJob[job] = positions;
        }

        var changed = this.RemoveStaleOverlayIconPositions(positions, job);
        var visibleKeys = visible.Select(ability => ability.Id).ToList();
        var occupied = this.GetOccupiedIconPositions(positions, visibleKeys, areaSize, iconWindow.IconSize, job);

        for (var index = 0; index < visible.Count; index++)
        {
            var ability = visible[index];
            if (this.TryGetOverlayIconPosition(positions, job, ability.Id, out _))
                continue;

            var position = this.FindFreeOverlayAutoPosition(iconWindow, index, visible.Count, occupied, areaSize);
            this.SetOverlayIconPosition(positions, job, ability.Id, position);
            occupied.Add(position);
            changed = true;
        }

        if (positions.Count == 0)
            iconWindow.IconPositionsByJob.Remove(job);

        return changed;
    }

    private Vector2 GetOverlayIconPosition(IconWindowConfig iconWindow, string job, AbilityDefinition ability, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions)
            && this.TryGetOverlayIconPosition(positions, job, ability.Id, out var saved))
        {
            return this.ClampOverlayIconPosition(saved, areaSize, iconSize);
        }

        return this.GetOverlayAutoPosition(iconWindow, index, visibleCount, areaSize, iconSize, gap);
    }

    private void SetOverlayIconPosition(IconWindowConfig iconWindow, string job, string abilityId, Vector2 position)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.IconPositionsByJob[job] = positions;
        }

        this.SetOverlayIconPosition(positions, job, abilityId, position);
    }

    private void AlignOverlayIcons(IconWindowConfig iconWindow, string job, uint level, bool preferTrackedOrder = false, bool respectDisplayCondition = false)
    {
        var visible = this.GetOverlayAbilities(
                iconWindow,
                job,
                level,
                respectDisplayCondition ? OverlayItemVisibility.Display : OverlayItemVisibility.Layout)
            .ToList();
        var areaSize = new Vector2(iconWindow.Width, iconWindow.Height);
        var positions = iconWindow.IconPositionsByJob.TryGetValue(job, out var existing)
            ? new Dictionary<string, Vector2>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        this.RemoveStaleOverlayIconPositions(positions, job);
        if (visible.Count == 0)
        {
            if (positions.Count == 0)
                iconWindow.IconPositionsByJob.Remove(job);
            else
                iconWindow.IconPositionsByJob[job] = positions;

            return;
        }

        var rows = this.GetOverlayPositionRows(iconWindow, job, visible, areaSize);
        var orderedKeys = visible.Select(ability => ability.Id).ToList();

        var orderedRows = rows
            .OrderBy(row => row.Average(item => item.Position.Y))
            .ToList();
        var verticalGap = Math.Max(0f, iconWindow.Gap);
        var rowStep = iconWindow.IconSize + verticalGap;
        var totalHeight = Math.Max(0f, orderedRows.Count * iconWindow.IconSize + Math.Max(0, orderedRows.Count - 1) * verticalGap);
        var startY = Math.Max(0f, MathF.Round((areaSize.Y - totalHeight) * 0.5f));

        for (var rowIndex = 0; rowIndex < orderedRows.Count; rowIndex++)
        {
            var row = orderedRows[rowIndex];
            var items = (preferTrackedOrder
                    ? row
                        .OrderBy(item => this.GetTrackedOrder(iconWindow, job, item.Ability.Id))
                        .ThenBy(item => item.Position.X)
                    : row
                        .OrderBy(item => item.Position.X)
                        .ThenBy(item => this.GetTrackedOrder(iconWindow, job, item.Ability.Id)))
                .ThenBy(item => item.Index)
                .ToList();
            var rowWidth = Math.Max(0f, items.Count * iconWindow.IconSize + Math.Max(0, items.Count - 1) * iconWindow.Gap);
            var startX = this.GetAlignedRowStartX(iconWindow, areaSize.X, rowWidth);
            var y = Math.Clamp(MathF.Round(startY + rowIndex * rowStep), 0f, Math.Max(0f, areaSize.Y - iconWindow.IconSize));

            for (var column = 0; column < items.Count; column++)
            {
                this.SetOverlayIconPosition(positions, job, items[column].Ability.Id, this.ClampOverlayIconPosition(
                    new Vector2(startX + column * (iconWindow.IconSize + iconWindow.Gap), y),
                    areaSize,
                    iconWindow.IconSize));
            }
        }

        this.NormalizeIconPositions(positions, orderedKeys, iconWindow, areaSize, job);
        iconWindow.IconPositionsByJob[job] = positions;
    }

    private List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>> GetOverlayPositionRows(
        IconWindowConfig iconWindow,
        string job,
        List<AbilityDefinition> visible,
        Vector2 areaSize)
    {
        var rowThreshold = Math.Max(6f, MathF.Round(iconWindow.IconSize * 0.55f));
        var items = visible
            .Select((ability, index) => (
                Ability: ability,
                Index: index,
                Position: this.GetOverlayIconPosition(iconWindow, job, ability, index, visible.Count, areaSize, iconWindow.IconSize, iconWindow.Gap)))
            .OrderBy(item => item.Position.Y)
            .ThenBy(item => item.Position.X)
            .ThenBy(item => item.Index)
            .ToList();

        var rows = new List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>>();
        foreach (var item in items)
        {
            var lastRow = rows.Count > 0 ? rows[^1] : null;
            if (lastRow is null || Math.Abs(item.Position.Y - lastRow.Average(rowItem => rowItem.Position.Y)) > rowThreshold)
            {
                rows.Add([item]);
                continue;
            }

            lastRow.Add(item);
        }

        return rows;
    }

    private bool RemoveStaleOverlayIconPositions(Dictionary<string, Vector2> positions, string job)
    {
        var changed = false;
        foreach (var key in positions.Keys.ToList())
        {
            if (this.FindTrackedAbilityDefinition(key, job) is not null)
                continue;

            positions.Remove(key);
            changed = true;
        }

        return changed;
    }

    private bool TryGetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, out Vector2 position)
    {
        if (positions.TryGetValue(abilityId, out position))
            return true;

        foreach (var (key, saved) in positions)
        {
            if (!this.TrackedAbilityIdsMatch(key, abilityId, job))
                continue;

            position = saved;
            return true;
        }

        position = default;
        return false;
    }

    private void SetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, Vector2 position)
    {
        foreach (var key in positions.Keys.ToList())
        {
            if (string.Equals(key, abilityId, StringComparison.OrdinalIgnoreCase)
                || !this.TrackedAbilityIdsMatch(key, abilityId, job))
            {
                continue;
            }

            positions.Remove(key);
        }

        positions[abilityId] = position;
    }
}
