namespace FFXIVAura;

internal readonly record struct SkillPositionLookup(
    IReadOnlyDictionary<string, Vector2>? TransientPositions,
    IReadOnlyDictionary<string, Vector2>? SavedPositions,
    Func<string, string, bool> IdsMatch);

public sealed partial class Plugin
{
    private bool AutoAlignWhenVisibleSkillsChanged(IconWindowConfig iconWindow, string job, uint level, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        var key = new SkillLayoutScopeKey(iconWindow.Id, job);
        var hasTrackedSkills = AbilityTrackingService.IsManualTracking(iconWindow, job)
                               && iconWindow.TrackedByJob.TryGetValue(job, out var tracked)
                               && tracked.Count > 0;
        var hasSavedPositions = iconWindow.IconPositionsByJob.TryGetValue(job, out var positions) && positions.Count > 0;
        var suppressAutoAlign = this.loginStabilizationState.ShouldSuppressSkillAutoAlign(DateTime.UtcNow);
        var visibleChanged = !this.visibleAbilityKeys.TryGetValue(key, out var previous)
                             || !previous.Matches(
                                 level,
                                 visible,
                                 iconWindow.Alignment,
                                 areaSize,
                                 iconWindow.IconSize,
                                 iconWindow.Gap,
                                 suppressAutoAlign,
                                 hasSavedPositions);
        if (!visibleChanged)
            return false;

        var layoutState = VisibleAbilityLayoutState.Create(
            level,
            visible,
            iconWindow.Alignment,
            areaSize,
            iconWindow.IconSize,
            iconWindow.Gap,
            suppressAutoAlign,
            hasSavedPositions);
        this.visibleAbilityKeys[key] = layoutState;

        if (suppressAutoAlign)
        {
            this.transientSkillPositionsByGroup.Remove(key);
            return hasSavedPositions && this.AddMissingOverlayIconPositions(iconWindow, job, layoutState.AbilityIds, areaSize);
        }

        if (!hasSavedPositions)
        {
            this.transientSkillPositionsByGroup.Remove(key);
            if (hasTrackedSkills && visible.Count > 0)
            {
                this.AlignOverlayIcons(iconWindow, job, level);
                return true;
            }

            return false;
        }

        var positionsChanged = this.AddMissingOverlayIconPositions(iconWindow, job, layoutState.AbilityIds, areaSize);
        positions = iconWindow.IconPositionsByJob.GetValueOrDefault(job);
        var shouldUseTransientLayout = positions is not null
                                       && visible.Count > 0
                                       && this.HasHiddenSavedSkillPositions(positions, job, layoutState.AbilityIds);
        if (!shouldUseTransientLayout)
        {
            this.transientSkillPositionsByGroup.Remove(key);
            return positionsChanged;
        }

        if (visibleChanged || positionsChanged || !this.transientSkillPositionsByGroup.ContainsKey(key))
            this.BuildTransientSkillLayout(iconWindow, job, visible, areaSize, key, positions!);

        return positionsChanged;
    }

    private void BuildTransientSkillLayout(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        Vector2 areaSize,
        SkillLayoutScopeKey key,
        IReadOnlyDictionary<string, Vector2> savedPositions)
    {
        var options = this.GetSkillPositionLayoutOptions(iconWindow, areaSize);
        var idsMatch = this.abilityCatalog.GetIdMatcher(job);
        var items = this.CreateSkillPositionLayoutItems(iconWindow, job, visible, savedPositions, options, idsMatch);
        this.transientSkillPositionsByGroup[key] = SkillPositionLayout.CreateAlignedVisibleCopy(
            savedPositions,
            items,
            options,
            preferTrackedOrder: false,
            idsMatch);
        this.SetBugDiagnosticEvent($"transientSkillAlign:{iconWindow.Id}:{job}:{visible.Count}");
    }

    private bool HasHiddenSavedSkillPositions(Dictionary<string, Vector2> positions, string job, IReadOnlyList<string> visibleIds)
    {
        return SkillPositionLayout.HasHiddenSavedPositions(
            positions.Keys,
            visibleIds,
            this.abilityCatalog.GetIdMatcher(job),
            key => this.abilityCatalog.FindTrackedDefinition(key, job) is not null);
    }

    private bool AddMissingOverlayIconPositions(IconWindowConfig iconWindow, string job, IReadOnlyList<string> visibleKeys, Vector2 areaSize)
    {
        if (!iconWindow.IconPositionsByJob.TryGetValue(job, out var positions))
        {
            positions = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            iconWindow.IconPositionsByJob[job] = positions;
        }

        var changed = SkillPositionLayout.AddMissingPositions(
            positions,
            visibleKeys,
            this.GetSkillPositionLayoutOptions(iconWindow, areaSize),
            this.abilityCatalog.GetIdMatcher(job),
            key => this.abilityCatalog.FindTrackedDefinition(key, job) is not null);

        if (positions.Count == 0)
            iconWindow.IconPositionsByJob.Remove(job);

        return changed;
    }

    private bool AddMissingOverlayIconPositions(IconWindowConfig iconWindow, string job, IReadOnlyList<AbilityDefinition> visible, Vector2 areaSize)
    {
        var visibleKeys = new string[visible.Count];
        for (var index = 0; index < visible.Count; index++)
            visibleKeys[index] = visible[index].Id;

        return this.AddMissingOverlayIconPositions(iconWindow, job, visibleKeys, areaSize);
    }

    private SkillPositionLookup CreateSkillPositionLookup(IconWindowConfig iconWindow, string job)
    {
        this.transientSkillPositionsByGroup.TryGetValue(
            new SkillLayoutScopeKey(iconWindow.Id, job),
            out var transientPositions);
        iconWindow.IconPositionsByJob.TryGetValue(job, out var savedPositions);
        return new SkillPositionLookup(
            transientPositions,
            savedPositions,
            this.abilityCatalog.GetIdMatcher(job));
    }

    private Vector2 GetOverlayIconPosition(
        in SkillPositionLookup lookup,
        IconWindowConfig iconWindow,
        AbilityDefinition ability,
        int index,
        int visibleCount,
        Vector2 areaSize,
        float iconSize,
        float gap)
    {
        if (lookup.TransientPositions is not null
            && SkillPositionLayout.TryGetPosition(lookup.TransientPositions, ability.Id, lookup.IdsMatch, out var transient))
        {
            return this.ClampOverlayIconPosition(transient, areaSize, iconSize);
        }

        if (lookup.SavedPositions is not null
            && SkillPositionLayout.TryGetPosition(lookup.SavedPositions, ability.Id, lookup.IdsMatch, out var saved))
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

        var transientKey = new SkillLayoutScopeKey(iconWindow.Id, job);
        this.transientSkillPositionsByGroup.TryGetValue(transientKey, out var transientPositions);
        SkillPositionLayout.SetPositionInLayouts(
            positions,
            transientPositions,
            abilityId,
            position,
            this.abilityCatalog.GetIdMatcher(job));
    }

    private void AlignOverlayIcons(IconWindowConfig iconWindow, string job, uint level, bool preferTrackedOrder = false, bool respectDisplayCondition = false)
    {
        this.transientSkillPositionsByGroup.Remove(new SkillLayoutScopeKey(iconWindow.Id, job));
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
        SkillPositionLayout.RemoveStalePositions(positions, key => this.abilityCatalog.FindTrackedDefinition(key, job) is not null);
        if (visible.Count == 0)
        {
            if (positions.Count == 0)
                iconWindow.IconPositionsByJob.Remove(job);
            else
                iconWindow.IconPositionsByJob[job] = positions;

            return;
        }

        var options = this.GetSkillPositionLayoutOptions(iconWindow, areaSize);
        var idsMatch = this.abilityCatalog.GetIdMatcher(job);
        var items = this.CreateSkillPositionLayoutItems(iconWindow, job, visible, positions, options, idsMatch);
        SkillPositionLayout.AlignPositions(
            positions,
            items,
            options,
            preferTrackedOrder,
            idsMatch);
        iconWindow.IconPositionsByJob[job] = positions;
    }

    private List<List<(AbilityDefinition Ability, int Index, Vector2 Position)>> GetOverlayPositionRows(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        Vector2 areaSize)
    {
        var options = this.GetSkillPositionLayoutOptions(iconWindow, areaSize);
        var positions = iconWindow.IconPositionsByJob.TryGetValue(job, out var existing)
            ? existing
            : new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        var items = this.CreateSkillPositionLayoutItems(
            iconWindow,
            job,
            visible,
            positions,
            options,
            this.abilityCatalog.GetIdMatcher(job));

        return SkillPositionLayout.GetRows(items, iconWindow.IconSize)
            .Select(row => row
                .Select(item => (visible[item.Index], item.Index, item.Position))
                .ToList())
            .ToList();
    }

    private bool RemoveStaleOverlayIconPositions(Dictionary<string, Vector2> positions, string job)
    {
        return SkillPositionLayout.RemoveStalePositions(positions, key => this.abilityCatalog.FindTrackedDefinition(key, job) is not null);
    }

    private bool TryGetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, out Vector2 position)
    {
        return SkillPositionLayout.TryGetPosition(positions, abilityId, this.abilityCatalog.GetIdMatcher(job), out position);
    }

    private void SetOverlayIconPosition(Dictionary<string, Vector2> positions, string job, string abilityId, Vector2 position)
    {
        SkillPositionLayout.SetPosition(positions, abilityId, position, this.abilityCatalog.GetIdMatcher(job));
    }

    private List<SkillPositionLayoutItem> CreateSkillPositionLayoutItems(
        IconWindowConfig iconWindow,
        string job,
        IReadOnlyList<AbilityDefinition> visible,
        IReadOnlyDictionary<string, Vector2> positions,
        SkillPositionLayoutOptions options,
        Func<string, string, bool> idsMatch)
    {
        var items = new List<SkillPositionLayoutItem>(visible.Count);
        for (var index = 0; index < visible.Count; index++)
        {
            var ability = visible[index];
            items.Add(new SkillPositionLayoutItem(
                ability.Id,
                index,
                this.GetTrackedOrder(iconWindow, job, ability.Id),
                SkillPositionLayout.GetPosition(
                    positions,
                    ability.Id,
                    index,
                    visible.Count,
                    options,
                    idsMatch)));
        }

        return items;
    }

    private SkillPositionLayoutOptions GetSkillPositionLayoutOptions(IconWindowConfig iconWindow, Vector2 areaSize)
        => new(iconWindow.Alignment, areaSize, iconWindow.IconSize, iconWindow.Gap);
}
