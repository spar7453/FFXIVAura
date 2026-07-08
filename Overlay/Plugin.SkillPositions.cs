namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private bool AutoAlignWhenVisibleSkillsChanged(IconWindowConfig iconWindow, string job, uint level, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        var key = OverlayPositionKeys.VisibleAbilityGroup(iconWindow.Id, job);
        var visibleIds = visible.Select(ability => ability.Id).ToList();
        var visibleKey = SkillPositionLayout.BuildVisibleKey(level, visibleIds);
        var hasTrackedSkills = iconWindow.TrackedByJob.TryGetValue(job, out var tracked) && tracked.Count > 0;
        var hasSavedPositions = iconWindow.IconPositionsByJob.TryGetValue(job, out var positions) && positions.Count > 0;
        var suppressAutoAlign = this.loginStabilizationState.ShouldSuppressSkillAutoAlign(DateTime.UtcNow);
        var shouldRealignLevelFilteredSkills = positions is not null
                                               && hasSavedPositions
                                               && visible.Count > 0
                                               && this.HasHiddenSavedSkillPositions(positions, job, visibleIds);
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

        if (suppressAutoAlign)
            return hasSavedPositions && this.AddMissingOverlayIconPositions(iconWindow, job, visible, areaSize);

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

    private bool HasHiddenSavedSkillPositions(Dictionary<string, Vector2> positions, string job, IReadOnlyList<string> visibleIds)
    {
        return SkillPositionLayout.HasHiddenSavedPositions(
            positions.Keys,
            visibleIds,
            (first, second) => this.TrackedAbilityIdsMatch(first, second, job),
            key => this.FindTrackedAbilityDefinition(key, job) is not null);
    }

    private bool AddMissingOverlayIconPositions(IconWindowConfig iconWindow, string job, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.IconPositionsByJob[job] = positions;
        }

        var visibleKeys = visible.Select(ability => ability.Id).ToList();
        var changed = SkillPositionLayout.AddMissingPositions(
            positions,
            visibleKeys,
            this.GetSkillPositionLayoutOptions(iconWindow, areaSize),
            (first, second) => this.TrackedAbilityIdsMatch(first, second, job),
            key => this.FindTrackedAbilityDefinition(key, job) is not null);

        if (positions.Count == 0)
            iconWindow.IconPositionsByJob.Remove(job);

        return changed;
    }

    private Vector2 GetOverlayIconPosition(IconWindowConfig iconWindow, string job, AbilityDefinition ability, int index, int visibleCount, Vector2 areaSize, float iconSize, float gap)
    {
        if (iconWindow.IconPositionsByJob.TryGetValue(job, out var positions)
            && SkillPositionLayout.TryGetPosition(positions, ability.Id, (first, second) => this.TrackedAbilityIdsMatch(first, second, job), out var saved))
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

        SkillPositionLayout.SetPosition(positions, abilityId, position, (first, second) => this.TrackedAbilityIdsMatch(first, second, job));
    }

    private void AlignOverlayIcons(IconWindowConfig iconWindow, string job, uint level, bool preferTrackedOrder = false, bool respectDisplayCondition = false)
    {
        this.SetBugDiagnosticEvent($"alignIcons:{iconWindow.Id}:{job}:{level}:tracked={preferTrackedOrder}:display={respectDisplayCondition}");
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
        SkillPositionLayout.RemoveStalePositions(positions, key => this.FindTrackedAbilityDefinition(key, job) is not null);
        if (visible.Count == 0)
        {
            if (positions.Count == 0)
                iconWindow.IconPositionsByJob.Remove(job);
            else
                iconWindow.IconPositionsByJob[job] = positions;

            return;
        }

        var options = this.GetSkillPositionLayoutOptions(iconWindow, areaSize);
        var items = visible
            .Select((ability, index) => new SkillPositionLayoutItem(
                ability.Id,
                index,
                this.GetTrackedOrder(iconWindow, job, ability.Id),
                SkillPositionLayout.GetPosition(
                    positions,
                    ability.Id,
                    index,
                    visible.Count,
                    options,
                    (first, second) => this.TrackedAbilityIdsMatch(first, second, job))))
            .ToList();
        SkillPositionLayout.AlignPositions(
            positions,
            items,
            options,
            preferTrackedOrder,
            (first, second) => this.TrackedAbilityIdsMatch(first, second, job));
        iconWindow.IconPositionsByJob[job] = positions;
    }

    private List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>> GetOverlayPositionRows(
        IconWindowConfig iconWindow,
        string job,
        List<AbilityDefinition> visible,
        Vector2 areaSize)
    {
        var options = this.GetSkillPositionLayoutOptions(iconWindow, areaSize);
        var positions = iconWindow.IconPositionsByJob.TryGetValue(job, out var existing)
            ? existing
            : new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        var items = visible
            .Select((ability, index) => new SkillPositionLayoutItem(
                ability.Id,
                index,
                this.GetTrackedOrder(iconWindow, job, ability.Id),
                SkillPositionLayout.GetPosition(
                    positions,
                    ability.Id,
                    index,
                    visible.Count,
                    options,
                    (first, second) => this.TrackedAbilityIdsMatch(first, second, job))))
            .ToList();

        return SkillPositionLayout.GetRows(items, iconWindow.IconSize)
            .Select(row => row
                .Select(item => (visible[item.Index], item.Index, item.Position))
                .ToList())
            .ToList();
    }

    private bool RemoveStaleOverlayIconPositions(Dictionary<string, Vector2> positions, string job)
    {
        return SkillPositionLayout.RemoveStalePositions(positions, key => this.FindTrackedAbilityDefinition(key, job) is not null);
    }

    private bool TryGetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, out Vector2 position)
    {
        return SkillPositionLayout.TryGetPosition(positions, abilityId, (first, second) => this.TrackedAbilityIdsMatch(first, second, job), out position);
    }

    private void SetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, Vector2 position)
    {
        SkillPositionLayout.SetPosition(positions, abilityId, position, (first, second) => this.TrackedAbilityIdsMatch(first, second, job));
    }

    private SkillPositionLayoutOptions GetSkillPositionLayoutOptions(IconWindowConfig iconWindow, Vector2 areaSize)
        => new(iconWindow.Alignment, areaSize, iconWindow.IconSize, iconWindow.Gap);
}
