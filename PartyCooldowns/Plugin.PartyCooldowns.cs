namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void LoadPartyCooldowns()
    {
        try
        {
            var json = PluginDataFiles.ReadText(
                PluginInterface.AssemblyLocation.DirectoryName!,
                Path.Combine("Data", "party_cooldowns.json"),
                PluginDataFiles.PartyCooldownsResourceName);
            var loaded = JsonSerializer.Deserialize<List<PartyCooldownDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (loaded is null)
                return;

            this.partyCooldownDefinitions.AddRange(
                loaded
                    .Where(definition => definition.ActionId > 0)
                    .Select(this.NormalizePartyCooldownDefinition)
                    .Where(definition => definition.IconId > 0));
            this.RebuildPartyCooldownDefinitionLookups();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load FFXIVAura party cooldown data.");
        }
    }

    private PartyCooldownDefinition NormalizePartyCooldownDefinition(PartyCooldownDefinition definition)
    {
        var ability = this.abilities.FirstOrDefault(ability => ability.ActionId == definition.ActionId);
        var action = this.GetActionRow(definition.ActionId);

        definition.Id = definition.Id.Trim();
        definition.Category = definition.Category.Trim();
        definition.Job = definition.Job.Trim().ToUpperInvariant();
        definition.Name = definition.Name.Trim();
        definition.ReplacementGroup = definition.ReplacementGroup.Trim();
        definition.StatusIds = definition.StatusIds
            .Where(statusId => statusId > 0)
            .Distinct()
            .Order()
            .ToArray();

        if (string.IsNullOrWhiteSpace(definition.Id))
            definition.Id = ability?.Id ?? $"action-{definition.ActionId}";

        if (string.IsNullOrWhiteSpace(definition.Job))
            definition.Job = ability?.Job?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(definition.Name))
            definition.Name = action?.Name.ExtractText() ?? ability?.Name ?? $"Action {definition.ActionId}";

        if (definition.Level == 0)
            definition.Level = ability?.Level ?? (byte)Math.Min(action?.ClassJobLevel ?? 1, byte.MaxValue);

        if (definition.Cooldown <= 0f)
            definition.Cooldown = ability?.Cooldown ?? ((action?.Recast100ms ?? 0) / 10f);

        definition.Charges = Math.Max(
            definition.Charges,
            Math.Max(ability?.Charges ?? (byte)1, action?.MaxCharges ?? (byte)1));

        if (definition.IconId == 0)
            definition.IconId = ability?.IconId ?? action?.Icon ?? 0;

        return definition;
    }

    private void RebuildPartyCooldownDefinitionLookups()
    {
        this.partyCooldownDefinitionsByActionId.Clear();
        this.partyCooldownMaxChargesByActionAndLevel.Clear();
        this.partyCooldownDefinitionsByName.Clear();
        this.partyCooldownDefinitionsByCategory.Clear();
        this.partyCooldownEffectiveDefinitionsByScope.Clear();
        this.partyCooldownPresetDefinitionsByCategory.Clear();
        this.partyCooldownEffectiveDefinitionsByCategoryAndLevel.Clear();
        this.partyCooldownCanonicalDefinitionIdById.Clear();

        foreach (var definition in this.partyCooldownDefinitions
                     .OrderBy(definition => string.Equals(definition.Job, "ROLE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                     .ThenBy(definition => definition.Level)
                     .ThenByDescending(definition => definition.Cooldown))
        {
            if (definition.ActionId > 0)
                this.partyCooldownDefinitionsByActionId.TryAdd(definition.ActionId, definition);

            if (Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var category))
            {
                if (!this.partyCooldownDefinitionsByCategory.TryGetValue(category, out var categoryDefinitions))
                {
                    categoryDefinitions = [];
                    this.partyCooldownDefinitionsByCategory[category] = categoryDefinitions;
                }

                categoryDefinitions.Add(definition);
            }

            var name = PartyCooldownLogMatcher.NormalizeActionName(definition.Name);
            if (name.Length == 0)
                continue;

            if (!this.partyCooldownDefinitionsByName.TryGetValue(name, out var definitions))
            {
                definitions = [];
                this.partyCooldownDefinitionsByName[name] = definitions;
            }

            definitions.Add(definition);
        }

        this.RebuildPartyCooldownCanonicalDefinitionIds();
    }

    private void RebuildPartyCooldownCanonicalDefinitionIds()
    {
        foreach (var pair in PartyCooldownDefinitionIdentity.BuildCanonicalDefinitionIds(
                     this.partyCooldownDefinitions,
                     this.GetPartyCooldownEquivalenceKey))
            this.partyCooldownCanonicalDefinitionIdById[pair.Key] = pair.Value;
    }

    private IReadOnlyList<PartyCooldownMemberRow> BuildPartyCooldownRows(IconWindowConfig iconWindow, uint level)
    {
        var category = IconWindowRoles.GetPartyCooldownCategory(iconWindow.Role);
        var frameSnapshot = this.GetPartyCooldownFrameSnapshot();
        var members = frameSnapshot.DisplayMembers;

        var rows = new List<PartyCooldownMemberRow>(members.Count);
        var candidateItemCount = 0;
        var hiddenByDisplayConditionCount = 0;
        var statuslessCandidateCount = 0;

        foreach (var member in members)
        {
            var definitions = this.GetEffectivePartyCooldownDefinitionsForMember(category, member.Job, level);
            var items = new List<PartyCooldownDisplayItem>(definitions.Count);
            foreach (var definition in definitions)
            {
                if (this.IsPartyCooldownExcluded(iconWindow, definition))
                    continue;

                candidateItemCount++;
                if (!this.HasPartyCooldownStatusTracking(definition))
                    statuslessCandidateCount++;

                var item = this.BuildPartyCooldownDisplayItem(member, definition, level, frameSnapshot.TimestampUtc);

                var visibleItem = this.FilterPartyCooldownDisplayCondition(iconWindow, item);
                if (visibleItem is { } visibleValue)
                    items.Add(visibleValue);
                else
                    hiddenByDisplayConditionCount++;
            }

            if (items.Count > 0 || !PartyCooldownBoardLayout.HideEmptyRows(category))
                rows.Add(new PartyCooldownMemberRow(member, items));
        }

        this.RememberPartyCooldownWindowDiagnostics(
            iconWindow,
            rows,
            candidateItemCount,
            hiddenByDisplayConditionCount,
            statuslessCandidateCount);
        this.PrunePartyCooldownRuntime(this.GetLivePartyCooldownRuntimeKeys(members, level));
        return rows;
    }

    private PartyCooldownFrameSnapshot GetPartyCooldownFrameSnapshot()
    {
        if (this.partyCooldownFrameSnapshot is not null)
            return this.partyCooldownFrameSnapshot;

        var roster = this.GetPartyCooldownRoster();
        var members = roster.Members;
        var displayMembers = this.GetPartyCooldownDisplayMembers(members);
        this.RebuildPartyCooldownActiveStatusIndex(displayMembers);
        this.partyCooldownFrameSnapshot = new PartyCooldownFrameSnapshot(
            members,
            displayMembers,
            this.CreatePartyCooldownRosterDiagnostics(roster, displayMembers),
            DateTime.UtcNow);
        return this.partyCooldownFrameSnapshot;
    }

    private HashSet<string> GetLivePartyCooldownRuntimeKeys(IReadOnlyList<PartyCooldownMemberSnapshot> members, uint level)
    {
        if (this.partyCooldownLiveRuntimeKeysFrameCacheValid)
            return this.partyCooldownLiveRuntimeKeysFrameCache;

        this.partyCooldownLiveRuntimeKeysFrameCache.Clear();
        foreach (var window in this.config.IconWindows)
        {
            if (!IconWindowRoles.IsPartyCooldownRole(window.Role))
                continue;

            var category = IconWindowRoles.GetPartyCooldownCategory(window.Role);
            foreach (var member in members)
            {
                foreach (var definition in this.GetPartyCooldownDefinitionsForMember(category, member.Job, level, window))
                    this.partyCooldownLiveRuntimeKeysFrameCache.Add(this.PartyCooldownRuntimeKey(member.Key, definition));
            }
        }

        this.partyCooldownLiveRuntimeKeysFrameCacheValid = true;
        return this.partyCooldownLiveRuntimeKeysFrameCache;
    }

    private float GetEstimatedPartyCooldownBoardHeight(
        IconWindowConfig iconWindow,
        IconWindowLayoutBinding layoutBinding,
        uint level)
    {
        var category = IconWindowRoles.GetPartyCooldownCategory(iconWindow.Role);
        var members = this.GetPartyCooldownFrameSnapshot().DisplayMembers;
        var iconSize = layoutBinding.IconSize;
        var gap = Math.Max(2f, layoutBinding.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var allianceGroupWidth = members.Any(member => !string.IsNullOrWhiteSpace(member.AllianceGroup))
            ? Math.Max(16f, ImGui.CalcTextSize("C").X + labelGap)
            : 0f;
        var regularLabelWidth = allianceGroupWidth + jobIconSize + labelGap + nameWidth + labelGap;
        var regularIconAreaWidth = Math.Max(0f, layoutBinding.Width - padding * 2f - regularLabelWidth);
        var regularIconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(regularIconAreaWidth, iconSize, gap);
        var columnLabelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var columnGap = Math.Max(8f, gap * 2f);
        var availableWidth = Math.Max(0f, layoutBinding.Width - padding * 2f);
        var rowItemCounts = new List<int>(members.Count);
        var allianceRowItemCounts = new List<(string AllianceGroup, int ItemCount)>(members.Count);
        var allianceGroupCount = 0;

        foreach (var member in members)
        {
            var itemCount = this.GetPartyCooldownDefinitionsForMember(category, member.Job, level, iconWindow).Count();
            if (itemCount > 0 || !PartyCooldownBoardLayout.HideEmptyRows(category))
            {
                rowItemCounts.Add(itemCount);
                allianceRowItemCounts.Add((member.AllianceGroup, itemCount));
            }
        }

        for (var group = 0; group < PartyCooldownBoardLayout.AllianceColumnCount; group++)
        {
            var groupLabel = PartyCooldownAllianceGroups.GroupLabel(group);
            if (allianceRowItemCounts.Any(row => string.Equals(row.AllianceGroup, groupLabel, StringComparison.Ordinal)))
                allianceGroupCount++;
        }

        if (PartyCooldownBoardLayout.ShouldUseAllianceGrid(allianceGroupCount))
        {
            var columnWidth = PartyCooldownBoardLayout.GetAllianceMemberCellWidth(availableWidth, columnGap);
            var columnIconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(
                Math.Max(0f, columnWidth - columnLabelWidth),
                iconSize,
                gap);
            return PartyCooldownBoardLayout.GetAllianceGridBoardContentHeight(
                allianceRowItemCounts,
                iconSize,
                gap,
                padding,
                ImGui.GetTextLineHeight() + Math.Max(4f, gap),
                columnIconsPerLine);
        }

        return PartyCooldownBoardLayout.GetBoardContentHeight(rowItemCounts, iconSize, gap, padding, regularIconsPerLine);
    }

    private IEnumerable<PartyCooldownDefinition> GetPartyCooldownDefinitionsForMember(
        PartyCooldownCategory category,
        string job,
        uint level,
        IconWindowConfig iconWindow)
    {
        foreach (var definition in this.GetEffectivePartyCooldownDefinitionsForMember(category, job, level))
        {
            if (this.IsPartyCooldownExcluded(iconWindow, definition))
                continue;

            yield return definition;
        }
    }

    private IReadOnlyList<PartyCooldownDefinition> GetEffectivePartyCooldownDefinitionsForMember(
        PartyCooldownCategory category,
        string job,
        uint level)
    {
        var cacheKey = PartyCooldownEffectiveDefinitionScopeKey(category, job, level);
        if (this.partyCooldownEffectiveDefinitionsByScope.TryGetValue(cacheKey, out var cached))
            return cached;

        var candidates = this.GetPartyCooldownDefinitionsForCategory(category)
            .Where(definition => this.IsPartyCooldownForJob(definition, job));
        var effectiveDefinitions = PartyCooldownDefinitionSelector.SelectEffectiveForLevel(
            candidates,
            level,
            this.GetPartyCooldownEquivalenceKey);
        this.partyCooldownEffectiveDefinitionsByScope[cacheKey] = effectiveDefinitions;
        return effectiveDefinitions;
    }

    private IReadOnlyList<PartyCooldownDefinition> GetPartyCooldownDefinitionsForCategory(PartyCooldownCategory category)
        => this.partyCooldownDefinitionsByCategory.TryGetValue(category, out var definitions)
            ? definitions
            : Array.Empty<PartyCooldownDefinition>();

    private IReadOnlyList<PartyCooldownDefinition> GetPartyCooldownPresetDefinitionsForCategory(PartyCooldownCategory category)
    {
        if (this.partyCooldownPresetDefinitionsByCategory.TryGetValue(category, out var cached))
            return cached;

        var presetDefinitions = PartyCooldownDefinitionSelector
            .SelectCanonicalReplacements(this.GetPartyCooldownDefinitionsForCategory(category), this.GetPartyCooldownEquivalenceKey)
            .OrderBy(definition => GetPartyCooldownJobSortOrder(definition.Job))
            .ThenBy(definition => definition.Level)
            .ThenByDescending(definition => definition.Cooldown)
            .ToList();
        this.partyCooldownPresetDefinitionsByCategory[category] = presetDefinitions;
        return presetDefinitions;
    }

    private IReadOnlyList<PartyCooldownDefinition> GetPartyCooldownEffectiveDefinitionsForCategory(
        PartyCooldownCategory category,
        uint level)
    {
        var cacheKey = $"{category}:{level}";
        if (this.partyCooldownEffectiveDefinitionsByCategoryAndLevel.TryGetValue(cacheKey, out var cached))
            return cached;

        var definitionsByJob = new Dictionary<string, List<PartyCooldownDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in this.GetPartyCooldownDefinitionsForCategory(category))
        {
            if (!definitionsByJob.TryGetValue(definition.Job, out var definitions))
            {
                definitions = [];
                definitionsByJob[definition.Job] = definitions;
            }

            definitions.Add(definition);
        }

        var effectiveDefinitions = new List<PartyCooldownDefinition>();
        foreach (var definitions in definitionsByJob.Values)
        {
            effectiveDefinitions.AddRange(PartyCooldownDefinitionSelector.SelectEffectiveForLevel(
                definitions,
                level,
                this.GetPartyCooldownEquivalenceKey));
        }

        effectiveDefinitions.Sort(ComparePartyCooldownDefinitionForUi);
        this.partyCooldownEffectiveDefinitionsByCategoryAndLevel[cacheKey] = effectiveDefinitions;
        return effectiveDefinitions;
    }

    private bool IsPartyCooldownTrackedByAnyWindow(PartyCooldownDefinition definition, string job, uint level)
    {
        if (!this.IsPartyCooldownForJob(definition, job))
            return false;

        foreach (var window in this.config.IconWindows)
        {
            if (!IconWindowRoles.IsPartyCooldownRole(window.Role))
                continue;

            var category = IconWindowRoles.GetPartyCooldownCategory(window.Role);
            if (!IsPartyCooldownCategory(definition, category))
                continue;

            foreach (var visibleDefinition in this.GetPartyCooldownDefinitionsForMember(category, job, level, window))
            {
                if (this.PartyCooldownDefinitionsMatch(definition, visibleDefinition))
                    return true;
            }
        }

        return false;
    }

    private bool PartyCooldownDefinitionsMatch(PartyCooldownDefinition first, PartyCooldownDefinition second)
    {
        if (first.ActionId == second.ActionId
            || string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstKey = this.GetPartyCooldownEquivalenceKey(first);
        return !string.IsNullOrEmpty(firstKey)
               && string.Equals(firstKey, this.GetPartyCooldownEquivalenceKey(second), StringComparison.OrdinalIgnoreCase);
    }

    private PartyCooldownDefinition ResolveEffectivePartyCooldownDefinition(
        PartyCooldownDefinition definition,
        string job,
        uint level)
    {
        if (!Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var category))
            return definition;

        return this.GetEffectivePartyCooldownDefinitionsForMember(category, job, level)
                   .FirstOrDefault(candidate => this.PartyCooldownDefinitionsMatch(definition, candidate))
               ?? definition;
    }

    private string GetPartyCooldownEquivalenceKey(PartyCooldownDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.ReplacementGroup))
            return $"{definition.Job}:{definition.Category}:{definition.ReplacementGroup}";

        var group = this.GetActionEquivalenceGroup(definition.ActionId);
        var actionCategory = this.GetActionCategory(definition.ActionId).RowId;
        if (group == 0 || actionCategory == 0)
            return string.Empty;

        return $"{definition.Job}:{definition.Category}:{actionCategory}:{group}";
    }

    private static bool TryFindPartyCooldownMemberByEntityId(
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        uint entityId,
        out PartyCooldownMemberSnapshot member)
    {
        foreach (var candidate in displayMembers)
        {
            if (candidate.EntityId == entityId)
            {
                member = candidate;
                return true;
            }
        }

        member = default;
        return false;
    }

    private bool IsPartyCooldownForJob(PartyCooldownDefinition definition, string job)
    {
        if (string.Equals(definition.Job, job, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(definition.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            return false;

        var roleAbility = this.abilities.FirstOrDefault(ability => ability.ActionId == definition.ActionId);
        return roleAbility is not null && JobInfo.CanUseRoleAction(job, roleAbility);
    }

    private static bool IsPartyCooldownCategory(PartyCooldownDefinition definition, PartyCooldownCategory category)
        => Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out var parsed)
           && parsed == category;

    private bool IsPartyCooldownExcluded(IconWindowConfig iconWindow, PartyCooldownDefinition definition)
        => PartyCooldownDefinitionIdentity.IsExcluded(
            iconWindow.ExcludedPartyCooldownIds,
            this.partyCooldownCanonicalDefinitionIdById,
            definition);

    private void SetPartyCooldownExcluded(IconWindowConfig iconWindow, PartyCooldownDefinition definition, bool excluded)
    {
        var normalizedId = this.GetPartyCooldownCanonicalDefinitionId(definition).Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
            return;

        this.RemovePartyCooldownExclusions(iconWindow, [definition]);
        if (excluded)
            iconWindow.ExcludedPartyCooldownIds.Add(normalizedId);
    }

    private void RemovePartyCooldownExclusions(IconWindowConfig iconWindow, IEnumerable<PartyCooldownDefinition> definitions)
        => PartyCooldownDefinitionIdentity.RemoveExclusions(
            iconWindow.ExcludedPartyCooldownIds,
            this.partyCooldownCanonicalDefinitionIdById,
            definitions);

    private string GetPartyCooldownCanonicalDefinitionId(PartyCooldownDefinition definition)
        => PartyCooldownDefinitionIdentity.GetCanonicalDefinitionId(
            this.partyCooldownCanonicalDefinitionIdById,
            definition);

    private static string PartyCooldownMemberKey(ulong contentId, uint entityId, string name, string job)
    {
        if (contentId != 0)
            return $"content-{contentId}";

        if (entityId != 0)
            return $"entity-{entityId}";

        return $"{job}:{name}";
    }

    private string PartyCooldownRuntimeKey(string memberKey, PartyCooldownDefinition definition)
        => PartyCooldownDefinitionIdentity.RuntimeKey(
            memberKey,
            this.partyCooldownCanonicalDefinitionIdById,
            definition);

    private static ulong PartyCooldownStatusKey(uint sourceEntityId, uint statusId)
        => ((ulong)sourceEntityId << 32) | statusId;

    private static string PartyCooldownEffectiveDefinitionScopeKey(PartyCooldownCategory category, string job, uint level)
        => $"{category}:{job.Trim()}:{level}";

    private static bool IsValidPartyCooldownEntityId(uint entityId)
        => PartyCooldownOwnerResolver.IsValidEntityId(entityId);

    private static string ShortPartyMemberName(string name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? "??" : name.Trim();
        return normalized.Length <= 2 ? normalized : normalized[..2];
    }
}
