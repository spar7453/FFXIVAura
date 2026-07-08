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

        if (definition.IconId == 0)
            definition.IconId = ability?.IconId ?? action?.Icon ?? 0;

        return definition;
    }

    private void RebuildPartyCooldownDefinitionLookups()
    {
        this.partyCooldownDefinitionsByActionId.Clear();
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
        var members = frameSnapshot.Members;

        var rows = new List<PartyCooldownMemberRow>(members.Count);
        var candidateItemCount = 0;
        var hiddenByDisplayConditionCount = 0;
        var statuslessCandidateCount = 0;

        foreach (var member in members)
        {
            var items = new List<PartyCooldownDisplayItem>();
            foreach (var definition in this.GetPartyCooldownDefinitionsForMember(category, member.Job, level, iconWindow))
            {
                candidateItemCount++;
                if (!this.HasPartyCooldownStatusTracking(definition))
                    statuslessCandidateCount++;

                var item = this.BuildPartyCooldownDisplayItem(member, definition, frameSnapshot.TimestampUtc);

                var visibleItem = this.FilterPartyCooldownDisplayCondition(iconWindow, item);
                if (visibleItem is not null)
                    items.Add(visibleItem);
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

        var members = this.GetPartyCooldownMembers();
        this.RebuildPartyCooldownActiveStatusIndex(members);
        this.partyCooldownFrameSnapshot = new PartyCooldownFrameSnapshot(members, DateTime.UtcNow);
        return this.partyCooldownFrameSnapshot;
    }

    private HashSet<string> GetLivePartyCooldownRuntimeKeys(IReadOnlyList<PartyCooldownMemberSnapshot> members, uint level)
    {
        if (this.partyCooldownLiveRuntimeKeysFrameCache is not null)
            return this.partyCooldownLiveRuntimeKeysFrameCache;

        var liveRuntimeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in this.config.IconWindows)
        {
            if (!IconWindowRoles.IsPartyCooldownRole(window.Role))
                continue;

            var category = IconWindowRoles.GetPartyCooldownCategory(window.Role);
            foreach (var member in members)
            {
                foreach (var definition in this.GetPartyCooldownDefinitionsForMember(category, member.Job, level, window))
                    liveRuntimeKeys.Add(this.PartyCooldownRuntimeKey(member.Key, definition));
            }
        }

        this.partyCooldownLiveRuntimeKeysFrameCache = liveRuntimeKeys;
        return liveRuntimeKeys;
    }

    private float GetEstimatedPartyCooldownBoardHeight(IconWindowConfig iconWindow, uint level)
    {
        var category = IconWindowRoles.GetPartyCooldownCategory(iconWindow.Role);
        var members = this.GetPartyCooldownFrameSnapshot().Members;
        var iconSize = iconWindow.IconSize;
        var gap = Math.Max(2f, iconWindow.Gap);
        var padding = Math.Max(4f, gap);
        var labelGap = Math.Max(4f, gap);
        var jobIconSize = Math.Clamp(iconSize * 0.72f, 20f, 32f);
        var nameWidth = Math.Clamp(iconSize * 1.05f, 34f, 54f);
        var labelWidth = jobIconSize + labelGap + nameWidth + labelGap;
        var iconAreaWidth = Math.Max(0f, iconWindow.Width - padding * 2f - labelWidth);
        var iconsPerLine = PartyCooldownBoardLayout.GetIconLineCapacity(iconAreaWidth, iconSize, gap);
        var rowItemCounts = new List<int>(members.Count);

        foreach (var member in members)
        {
            var itemCount = this.GetPartyCooldownDefinitionsForMember(category, member.Job, level, iconWindow).Count();
            if (itemCount > 0 || !PartyCooldownBoardLayout.HideEmptyRows(category))
                rowItemCounts.Add(itemCount);
        }

        return PartyCooldownBoardLayout.GetBoardContentHeight(rowItemCounts, iconSize, gap, padding, iconsPerLine);
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
        var group = this.GetActionEquivalenceGroup(definition.ActionId);
        var actionCategory = this.GetActionCategory(definition.ActionId).RowId;
        if (group == 0 || actionCategory == 0)
            return string.Empty;

        return $"{definition.Job}:{definition.Category}:{actionCategory}:{group}";
    }

    private bool TryFindPartyCooldownMemberByEntityId(
        uint entityId,
        out PartyCooldownMemberSnapshot member)
    {
        foreach (var candidate in this.GetPartyCooldownMembers())
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

    private PartyCooldownDisplayItem BuildPartyCooldownDisplayItem(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        DateTime now)
    {
        var runtimeKey = this.PartyCooldownRuntimeKey(member.Key, definition);
        if (!this.partyCooldownRuntimeStates.TryGetValue(runtimeKey, out var runtime))
        {
            runtime = new PartyCooldownRuntimeState();
            this.partyCooldownRuntimeStates[runtimeKey] = runtime;
        }

        var activeRemaining = this.GetPartyCooldownActiveRemaining(member.EntityId, definition);
        if (activeRemaining > 0f)
        {
            var cooldownRemaining = EstimatePartyCooldownRemaining(definition, activeRemaining);
            runtime.CooldownEndsAtUtc = now.AddSeconds(cooldownRemaining);
            return new PartyCooldownDisplayItem(
                definition,
                PartyCooldownDisplayState.Active,
                activeRemaining,
                cooldownRemaining,
                Math.Max(definition.Cooldown, cooldownRemaining));
        }

        var remaining = Math.Max(0f, (float)(runtime.CooldownEndsAtUtc - now).TotalSeconds);
        return remaining > 0.05f
            ? new PartyCooldownDisplayItem(definition, PartyCooldownDisplayState.Cooldown, 0f, remaining, Math.Max(definition.Cooldown, remaining))
            : new PartyCooldownDisplayItem(definition, PartyCooldownDisplayState.Ready, 0f, 0f, Math.Max(definition.Cooldown, 0f));
    }

    private PartyCooldownDisplayItem? FilterPartyCooldownDisplayCondition(IconWindowConfig iconWindow, PartyCooldownDisplayItem item)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat() ? item : null,
            IconDisplayCondition.OutOfCombat => !this.IsInCombat() ? item : null,
            IconDisplayCondition.CoolingOnly => item.State is PartyCooldownDisplayState.Active or PartyCooldownDisplayState.Cooldown ? item : null,
            IconDisplayCondition.ReadyOnly => item.State == PartyCooldownDisplayState.Ready ? item : null,
            _ => item,
        };
    }

    private float GetPartyCooldownActiveRemaining(uint sourceEntityId, PartyCooldownDefinition definition)
    {
        var remaining = 0f;
        foreach (var statusId in this.ResolvePartyCooldownStatusIds(definition))
        {
            var key = PartyCooldownStatusKey(sourceEntityId, statusId);
            if (this.partyCooldownActiveStatusFrameCache.TryGetValue(key, out var status))
                remaining = Math.Max(remaining, status.Remaining);
        }

        return remaining;
    }

    private IReadOnlyList<PartyCooldownMemberSnapshot> GetPartyCooldownMembers()
    {
        var members = new List<PartyCooldownMemberSnapshot>(Math.Max(PartyList.Length, 1));
        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is null || member.EntityId == 0)
                continue;

            var classJobId = member.ClassJob.RowId;
            var job = JobInfo.Code(classJobId);
            members.Add(new PartyCooldownMemberSnapshot(
                PartyCooldownMemberKey(member.ContentId, member.EntityId, member.Name.ToString(), job),
                member.EntityId,
                (ushort)member.World.RowId,
                member.Name.ToString(),
                ShortPartyMemberName(member.Name.ToString()),
                job,
                JobInfo.IconId(classJobId)));
        }

        if (members.Count == 0 && ObjectTable.LocalPlayer is IBattleChara player)
        {
            var classJobId = PlayerState.ClassJob.RowId;
            var job = JobInfo.Code(classJobId);
            var name = player.Name.ToString();
            members.Add(new PartyCooldownMemberSnapshot(
                PartyCooldownMemberKey(0, player.EntityId, name, job),
                player.EntityId,
                (ushort)PlayerState.HomeWorld.RowId,
                name,
                ShortPartyMemberName(name),
                job,
                JobInfo.IconId(classJobId)));
        }

        var localEntityId = ObjectTable.LocalPlayer?.EntityId ?? 0;
        return PartyCooldownMemberOrdering.Sort(members, localEntityId);
    }

    private void RebuildPartyCooldownActiveStatusIndex(IReadOnlyList<PartyCooldownMemberSnapshot> members)
    {
        this.partyCooldownActiveStatusFrameCache.Clear();
        var memberEntityIds = new HashSet<uint>(members.Count);
        foreach (var member in members)
            memberEntityIds.Add(member.EntityId);

        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is null)
                continue;

            if (!this.TryReadStatusSnapshots(member.Statuses, "partyCooldownPartyMember", member.EntityId))
                continue;

            foreach (var status in this.statusSnapshotBuffer)
                this.AddPartyCooldownStatusSample(member.EntityId, status.SourceId, status.StatusId, status.RemainingTime, memberEntityIds);
        }

        if (ObjectTable.LocalPlayer is IBattleChara player && (PartyList.Length == 0 || memberEntityIds.Contains(player.EntityId)))
            this.AddPartyCooldownStatusSamplesFromCharacter(player, memberEntityIds);

        foreach (var gameObject in ObjectTable)
        {
            if (gameObject is IBattleChara battleChara)
                this.AddPartyCooldownStatusSamplesFromCharacter(battleChara, memberEntityIds);
        }

        if (TargetManager.Target is IBattleChara target)
            this.AddPartyCooldownStatusSamplesFromCharacter(target, memberEntityIds);
    }

    private void AddPartyCooldownStatusSamplesFromCharacter(IBattleChara character, HashSet<uint> partyEntityIds)
    {
        if (character.EntityId == 0)
            return;

        if (!this.TryReadStatusSnapshots(character.StatusList, "partyCooldownCharacter", character.EntityId))
            return;

        foreach (var status in this.statusSnapshotBuffer)
            this.AddPartyCooldownStatusSample(character.EntityId, status.SourceId, status.StatusId, status.RemainingTime, partyEntityIds);
    }

    private void AddPartyCooldownStatusSample(
        uint ownerEntityId,
        uint sourceEntityId,
        uint statusId,
        float remaining,
        HashSet<uint> partyEntityIds)
    {
        if (statusId == 0 || remaining <= 0f)
            return;

        var normalizedSourceId = this.ResolvePartyCooldownStatusSourceEntityId(
            ownerEntityId,
            sourceEntityId,
            partyEntityIds);
        if (normalizedSourceId == 0)
            return;

        var key = PartyCooldownStatusKey(normalizedSourceId, statusId);
        if (this.partyCooldownActiveStatusFrameCache.TryGetValue(key, out var existing) && existing.Remaining >= remaining)
            return;

        this.partyCooldownActiveStatusFrameCache[key] = new PartyCooldownActiveStatus(statusId, remaining);
    }

    private uint ResolvePartyCooldownStatusSourceEntityId(
        uint ownerEntityId,
        uint sourceEntityId,
        HashSet<uint> partyEntityIds)
        => PartyCooldownOwnerResolver.ResolveStatusSourceEntityId(
            ownerEntityId,
            sourceEntityId,
            partyEntityIds,
            this.GetPartyOwnedObjectOwnerEntityId);

    private uint GetPartyOwnedObjectOwnerEntityId(uint entityId)
    {
        if (!PartyCooldownOwnerResolver.IsValidEntityId(entityId))
            return 0;

        var gameObject = ObjectTable.SearchByEntityId(entityId);
        return gameObject is null ? 0 : gameObject.OwnerId;
    }

    private IReadOnlyList<uint> ResolvePartyCooldownStatusIds(PartyCooldownDefinition definition)
    {
        if (this.partyCooldownStatusIdsByActionId.TryGetValue(definition.ActionId, out var cached))
            return cached;

        var action = this.GetActionRow(definition.ActionId);
        cached = PartyCooldownStatusResolver.Resolve(definition.StatusIds, action?.StatusGainSelf.RowId ?? 0);
        this.partyCooldownStatusIdsByActionId[definition.ActionId] = cached;
        return cached;
    }

    private static float EstimatePartyCooldownRemaining(PartyCooldownDefinition definition, float activeRemaining)
    {
        if (definition.Cooldown <= 0f)
            return 0f;

        if (definition.Duration <= 0f)
            return definition.Cooldown;

        var elapsedSinceUse = Math.Clamp(definition.Duration - activeRemaining, 0f, definition.Duration);
        return Math.Max(0f, definition.Cooldown - elapsedSinceUse);
    }

    private void PrunePartyCooldownRuntime(HashSet<string> liveRuntimeKeys)
    {
        this.partyCooldownRuntimePruneBuffer.Clear();
        foreach (var key in this.partyCooldownRuntimeStates.Keys)
        {
            if (liveRuntimeKeys.Contains(key))
                continue;

            this.partyCooldownRuntimePruneBuffer.Add(key);
        }

        foreach (var key in this.partyCooldownRuntimePruneBuffer)
            this.partyCooldownRuntimeStates.Remove(key);
    }

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
