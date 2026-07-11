namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AuraSeenHistoryLimit = 512;
    private static readonly TimeSpan AuraStatusIndexRetryDelay = TimeSpan.FromSeconds(1);

    private IReadOnlyList<AuraSearchDisplayResult> SearchStatuses(string search, IconWindowConfig iconWindow)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraSearch);
        try
        {
            var query = search?.Trim() ?? string.Empty;
            this.UpdateCurrentAuraSeenTimes(iconWindow, this.auraSearchCurrentStatusIdBuffer);
            this.auraSearchCurrentStatusIdSetBuffer.Clear();
            foreach (var statusId in this.auraSearchCurrentStatusIdBuffer)
                this.auraSearchCurrentStatusIdSetBuffer.Add(statusId);

            var cacheKey = this.CreateAuraSearchResultCacheKey(iconWindow, query);
            if (this.auraSearchResultCacheByWindow.TryGetValue(iconWindow.Id, out var cached)
                && cached.Key == cacheKey)
            {
                this.auraSearchResultCacheHitCount++;
                return cached.Results;
            }

            this.auraSearchResultCacheMissCount++;
            var candidates = new List<AuraSearchDisplayResult>();
            this.AddCurrentStatusSearchResults(candidates, query, iconWindow, this.auraSearchCurrentStatusIdBuffer);
            IReadOnlyList<AuraSearchDisplayResult> results;
            if (iconWindow.AuraSearchActiveOnly)
            {
                results = this.FinalizeAuraSearchResults(candidates, query, iconWindow);
            }
            else
            {
                this.AddRecentStatusSearchResults(candidates, query, iconWindow, this.auraSearchCurrentStatusIdSetBuffer);
                if (query.Length > 0)
                {
                    this.AddActionGrantedStatusSearchResults(candidates, query);
                    this.AddAllStatusSearchResults(candidates, query);
                }

                results = this.FinalizeAuraSearchResults(candidates, query, iconWindow);
            }

            if (this.CanCacheAuraSearchResults(iconWindow, query))
            {
                cacheKey = this.CreateAuraSearchResultCacheKey(iconWindow, query);
                if (this.auraSearchResultCacheByWindow.Count >= AuraSearchResultCacheLimit
                    && !this.auraSearchResultCacheByWindow.ContainsKey(iconWindow.Id))
                {
                    this.auraSearchResultCacheByWindow.Clear();
                }

                this.auraSearchResultCacheByWindow[iconWindow.Id] = new AuraSearchResultCacheEntry(cacheKey, results);
            }
            else
            {
                this.auraSearchResultCacheByWindow.Remove(iconWindow.Id);
            }

            return results;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraSearch, profileStart);
        }
    }

    private AuraSearchResultCacheKey CreateAuraSearchResultCacheKey(IconWindowConfig iconWindow, string query)
    {
        var scopeKey = RuntimeScopeKeys.AuraSeen(iconWindow);
        return new AuraSearchResultCacheKey(
            query,
            iconWindow.Role,
            iconWindow.AuraSearchActiveOnly,
            iconWindow.AuraSearchShowIndividualIds,
            iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.PartyAurasOwnOnly,
            this.auraSearchStateRevisionByScope.GetValueOrDefault(scopeKey),
            this.statusIdentityIndexState.Generation,
            this.actionGrantedStatusSearchIndexState.Generation);
    }

    private bool CanCacheAuraSearchResults(IconWindowConfig iconWindow, string query)
        => this.statusIdentityIndexState.IsBuilt
           && (iconWindow.AuraSearchActiveOnly
               || query.Length == 0
               || (this.allStatusSearchIndexBuilt
                   && this.actionGrantedStatusSearchIndexState.IsBuilt));

    private IReadOnlyList<AuraSearchDisplayResult> FinalizeAuraSearchResults(
        IEnumerable<AuraSearchDisplayResult> candidates,
        string query,
        IconWindowConfig iconWindow)
    {
        var separateSameNameIds = iconWindow.AuraSearchShowIndividualIds
                                  || uint.TryParse(query, out _);
        var results = AuraSearchDisplayResults.MergeAndSort(candidates, query, separateSameNameIds);
        for (var index = 0; index < results.Count; index++)
        {
            var knownCount = this.GetStatusIdsByGroup(results[index].GroupKey).Count;
            if (knownCount > results[index].SameNameCount)
                results[index] = results[index].WithSameNameCount(knownCount);
        }

        return results;
    }

    private void AddCurrentStatusSearchResults(List<AuraSearchDisplayResult> results, string query, IconWindowConfig iconWindow, IReadOnlyList<uint> currentStatusIds)
    {
        foreach (var statusId in currentStatusIds)
        {
            var definition = this.GetStatusDefinition(statusId);
            if (!this.IsSearchableStatusName(definition.Name))
                continue;

            var sourceActionNames = this.GetActionGrantedSourceNames(statusId, query);
            if (query.Length > 0
                && !this.MatchesStatusSearch(statusId, definition.Name, query)
                && string.IsNullOrEmpty(sourceActionNames))
            {
                continue;
            }

            results.Add(new AuraSearchDisplayResult(
                statusId,
                definition.Name,
                definition.IconId,
                IsCurrent: true,
                WasRecentlySeen: true,
                FromAction: !string.IsNullOrEmpty(sourceActionNames),
                FromStatusSheet: false,
                SeenAtUtc: this.GetAuraSeenTime(iconWindow, statusId),
                SourceActionNames: sourceActionNames,
                StatusCategory: definition.StatusCategory));
        }
    }

    private void AddActionGrantedStatusSearchResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var entry in this.GetActionGrantedStatusSearchMatches(query))
        {
            results.Add(new AuraSearchDisplayResult(
                entry.StatusId,
                entry.Name,
                entry.IconId,
                IsCurrent: false,
                WasRecentlySeen: false,
                FromAction: true,
                FromStatusSheet: false,
                SeenAtUtc: DateTime.MinValue,
                SourceActionNames: entry.PrimarySearchText,
                StatusCategory: entry.StatusCategory));
        }
    }

    private void AddAllStatusSearchResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var result in this.GetAllStatusSearchMatches(query))
        {
            results.Add(new AuraSearchDisplayResult(
                result.StatusId,
                result.Name,
                result.IconId,
                IsCurrent: false,
                WasRecentlySeen: false,
                FromAction: false,
                FromStatusSheet: true,
                SeenAtUtc: DateTime.MinValue,
                StatusCategory: result.StatusCategory));
        }
    }

    private IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedStatusSearchIndex()
    {
        if (this.actionGrantedStatusSearchIndexState.IsBuilt)
            return this.actionGrantedStatusSearchIndex;

        if (!this.EnsureStatusIdentityIndex())
            return this.actionGrantedStatusSearchIndex;

        var nowUtc = DateTime.UtcNow;
        if (!this.actionGrantedStatusSearchIndexState.ShouldAttempt(nowUtc))
            return this.actionGrantedStatusSearchIndex;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraIndexBuild);
        try
        {
            var sheet = DataManager.GetExcelSheet<GameAction>();
            if (sheet is null)
            {
                this.actionGrantedStatusSearchIndexState.MarkFailed(nowUtc, AuraStatusIndexRetryDelay);
                return this.actionGrantedStatusSearchIndex;
            }

            var nextIndex = new List<AuraSearchIndexEntry>();
            var nextIndexByStatusId = new Dictionary<uint, List<AuraSearchIndexEntry>>();
            foreach (var action in sheet)
            {
                if (action.RowId == 0 || action.StatusGainSelf.RowId == 0)
                    continue;

                var actionName = action.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                var statusId = action.StatusGainSelf.RowId;
                var definition = this.GetStatusDefinition(statusId);
                if (!this.IsSearchableStatusName(definition.Name))
                    continue;

                var entry = new AuraSearchIndexEntry(
                    statusId,
                    definition.Name,
                    definition.IconId,
                    actionName,
                    action.RowId.ToString(),
                    $"{definition.Name} {statusId}",
                    definition.StatusCategory);
                nextIndex.Add(entry);
                if (!nextIndexByStatusId.TryGetValue(statusId, out var statusEntries))
                {
                    statusEntries = [];
                    nextIndexByStatusId[statusId] = statusEntries;
                }

                statusEntries.Add(entry);
            }

            this.actionGrantedStatusSearchIndex.Clear();
            this.actionGrantedStatusSearchIndex.AddRange(nextIndex);
            this.actionGrantedStatusSearchIndexByStatusId.Clear();
            foreach (var (statusId, entries) in nextIndexByStatusId)
                this.actionGrantedStatusSearchIndexByStatusId[statusId] = entries;

            this.actionGrantedAuraSearchQueryCache.Clear();
            this.actionGrantedStatusSearchIndexState.MarkSucceeded();
        }
        catch (Exception ex)
        {
            this.actionGrantedStatusSearchIndexState.MarkFailed(nowUtc, AuraStatusIndexRetryDelay);
            Log.Debug(ex, "Failed to build the action-granted aura search index.");
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }

        return this.actionGrantedStatusSearchIndex;
    }

    private IReadOnlyList<AuraSearchIndexEntry> GetAllStatusSearchIndex()
    {
        if (this.allStatusSearchIndexBuilt)
            return this.allStatusSearchIndex;

        if (!this.EnsureStatusIdentityIndex())
            return this.allStatusSearchIndex;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraIndexBuild);
        try
        {
            var nextSearchIndex = new List<AuraSearchIndexEntry>(this.statusIdentityStatusIds.Count);
            foreach (var statusId in this.statusIdentityStatusIds)
            {
                if (!this.statusDefinitionCache.TryGetValue(statusId, out var definition)
                    || definition.IconId == 0)
                {
                    continue;
                }

                nextSearchIndex.Add(new AuraSearchIndexEntry(
                    statusId,
                    definition.Name,
                    definition.IconId,
                    definition.Name,
                    statusId.ToString(),
                    StatusCategory: definition.StatusCategory));
            }

            this.allStatusSearchIndex.Clear();
            this.allStatusSearchIndex.AddRange(nextSearchIndex);
            this.allStatusSearchQueryCache.Clear();
            this.allStatusSearchIndexBuilt = true;
            return this.allStatusSearchIndex;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }
    }

    private IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedStatusSearchMatches(string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
            return Array.Empty<AuraSearchIndexEntry>();

        if (this.actionGrantedAuraSearchQueryCache.TryGetValue(normalizedQuery, out var cached))
            return cached;

        var index = this.GetActionGrantedStatusSearchIndex();
        if (!this.actionGrantedStatusSearchIndexState.IsBuilt)
            return Array.Empty<AuraSearchIndexEntry>();

        var matches = index
            .Where(entry => AuraSearchIndex.Matches(entry, normalizedQuery))
            .ToArray();
        AddAuraSearchQueryCacheEntry(this.actionGrantedAuraSearchQueryCache, normalizedQuery, matches);
        return matches;
    }

    private IReadOnlyList<AuraSearchResult> GetAllStatusSearchMatches(string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
            return Array.Empty<AuraSearchResult>();

        if (this.allStatusSearchQueryCache.TryGetValue(normalizedQuery, out var cached))
            return cached;

        var index = this.GetAllStatusSearchIndex();
        if (!this.allStatusSearchIndexBuilt)
            return Array.Empty<AuraSearchResult>();

        var matches = AuraSearchIndex.Search(index, normalizedQuery).ToArray();
        AddAuraSearchQueryCacheEntry(this.allStatusSearchQueryCache, normalizedQuery, matches);
        return matches;
    }

    private static void AddAuraSearchQueryCacheEntry<T>(
        Dictionary<string, IReadOnlyList<T>> cache,
        string query,
        IReadOnlyList<T> results)
    {
        if (cache.Count >= AuraSearchQueryCacheLimit)
            cache.Clear();

        cache[query] = results;
    }

    private bool EnsureStatusIdentityIndex()
    {
        if (this.statusIdentityIndexState.IsBuilt)
            return true;

        var nowUtc = DateTime.UtcNow;
        if (!this.statusIdentityIndexState.ShouldAttempt(nowUtc))
            return false;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraIndexBuild);
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is null)
            {
                this.statusIdentityIndexState.MarkFailed(nowUtc, AuraStatusIndexRetryDelay);
                return false;
            }

            var nextDefinitions = new Dictionary<uint, AuraStatusDefinition>();
            var nextStatusIds = new List<uint>();
            var nextStatusIdsByGroup = new Dictionary<AuraStatusGroupKey, List<uint>>();
            foreach (var status in sheet)
            {
                if (status.RowId == 0)
                    continue;

                var name = status.Name.ExtractText();
                if (!this.IsSearchableStatusName(name))
                    continue;

                var definition = new AuraStatusDefinition(name, status.Icon, status.StatusCategory);
                nextDefinitions[status.RowId] = definition;
                nextStatusIds.Add(status.RowId);
                if (!nextStatusIdsByGroup.TryGetValue(definition.GroupKey, out var statusIds))
                {
                    statusIds = [];
                    nextStatusIdsByGroup[definition.GroupKey] = statusIds;
                }

                statusIds.Add(status.RowId);
            }

            foreach (var (statusId, definition) in nextDefinitions)
                this.statusDefinitionCache[statusId] = definition;

            this.statusIdentityStatusIds.Clear();
            this.statusIdentityStatusIds.AddRange(nextStatusIds);
            this.statusIdsByGroupIndex.Clear();
            foreach (var (key, statusIds) in nextStatusIdsByGroup)
                this.statusIdsByGroupIndex[key] = statusIds;

            this.statusIdentityIndexState.MarkSucceeded();
            this.trackedAuraGroupCache.Clear();
            this.allStatusSearchIndex.Clear();
            this.allStatusSearchQueryCache.Clear();
            this.allStatusSearchIndexBuilt = false;
            return true;
        }
        catch (Exception ex)
        {
            this.statusIdentityIndexState.MarkFailed(nowUtc, AuraStatusIndexRetryDelay);
            Log.Debug(ex, "Failed to build the aura status identity index.");
            return false;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }
    }

    private IReadOnlyList<uint> GetStatusIdsByGroup(AuraStatusGroupKey key)
    {
        if (!key.IsValid)
            return Array.Empty<uint>();

        _ = this.EnsureStatusIdentityIndex();
        return this.statusIdsByGroupIndex.TryGetValue(key, out var statusIds)
            ? statusIds
            : Array.Empty<uint>();
    }

    private void AddRecentStatusSearchResults(List<AuraSearchDisplayResult> results, string query, IconWindowConfig iconWindow, HashSet<uint> currentStatusIds)
    {
        if (!this.auraFirstSeenByScope.TryGetValue(RuntimeScopeKeys.AuraSeen(iconWindow), out var seenTimes))
            return;

        foreach (var (statusId, seenAt) in seenTimes)
        {
            if (currentStatusIds.Contains(statusId))
                continue;

            var definition = this.GetStatusDefinition(statusId);
            if (!this.IsSearchableStatusName(definition.Name))
                continue;

            var sourceActionNames = this.GetActionGrantedSourceNames(statusId, query);
            if (query.Length > 0
                && !this.MatchesStatusSearch(statusId, definition.Name, query)
                && string.IsNullOrEmpty(sourceActionNames))
            {
                continue;
            }

            results.Add(new AuraSearchDisplayResult(
                statusId,
                definition.Name,
                definition.IconId,
                IsCurrent: false,
                WasRecentlySeen: true,
                FromAction: !string.IsNullOrEmpty(sourceActionNames),
                FromStatusSheet: false,
                SeenAtUtc: seenAt,
                SourceActionNames: sourceActionNames,
                StatusCategory: definition.StatusCategory));
        }
    }

    private void UpdateCurrentAuraSeenTimes(IconWindowConfig iconWindow, List<uint> output)
    {
        output.Clear();
        foreach (var statusId in this.GetCurrentStatusIds(iconWindow))
            output.Add(statusId);

        this.UpdateAuraSeenTimes(iconWindow, output);
    }

    private string GetActionGrantedSourceNames(uint statusId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        _ = this.GetActionGrantedStatusSearchIndex();
        if (!this.actionGrantedStatusSearchIndexByStatusId.TryGetValue(statusId, out var statusEntries))
            return string.Empty;

        var names = new List<string>();
        foreach (var entry in statusEntries)
        {
            if (entry.StatusId != statusId || !AuraSearchIndex.Matches(entry, query))
                continue;

            if (!names.Contains(entry.PrimarySearchText, StringComparer.CurrentCultureIgnoreCase))
                names.Add(entry.PrimarySearchText);
        }

        return string.Join(" / ", names);
    }

    private void UpdateAuraSeenTimes(IconWindowConfig iconWindow, IReadOnlyCollection<uint> currentStatusIds)
    {
        var scopeKey = RuntimeScopeKeys.AuraSeen(iconWindow);
        if (!this.visibleAurasByScope.TryGetValue(scopeKey, out var previous))
        {
            previous = [];
            this.visibleAurasByScope[scopeKey] = previous;
        }

        if (!this.auraFirstSeenByScope.TryGetValue(scopeKey, out var seenTimes))
        {
            seenTimes = new Dictionary<uint, DateTime>();
            this.auraFirstSeenByScope[scopeKey] = seenTimes;
        }

        if (AuraSearchStatusSets.HaveSameMembers(previous, currentStatusIds))
            return;

        var now = DateTime.UtcNow;
        foreach (var statusId in currentStatusIds)
        {
            if (!previous.Contains(statusId))
                seenTimes[statusId] = now;
        }

        previous.Clear();
        foreach (var statusId in currentStatusIds)
            previous.Add(statusId);

        this.PruneAuraSeenTimes(seenTimes);
        this.auraSearchStateRevisionByScope[scopeKey] = this.auraSearchStateRevisionByScope.GetValueOrDefault(scopeKey) + 1;
    }

    private void PruneAuraSeenTimes(Dictionary<uint, DateTime> seenTimes)
    {
        if (seenTimes.Count <= AuraSeenHistoryLimit)
            return;

        var staleStatusIds = seenTimes
            .OrderByDescending(pair => pair.Value)
            .Skip(AuraSeenHistoryLimit)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var statusId in staleStatusIds)
            seenTimes.Remove(statusId);
    }

    private DateTime GetAuraSeenTime(IconWindowConfig iconWindow, uint statusId)
    {
        return this.auraFirstSeenByScope.TryGetValue(RuntimeScopeKeys.AuraSeen(iconWindow), out var seenTimes)
               && seenTimes.TryGetValue(statusId, out var seenAt)
            ? seenAt
            : DateTime.MinValue;
    }

    private IEnumerable<uint> GetCurrentStatusIds(IconWindowConfig iconWindow)
        => this.GetCurrentStatusIds(iconWindow.Role, iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.PartyAurasOwnOnly);

    private IEnumerable<uint> GetCurrentStatusIds(IconWindowRole role, bool ownOnly)
        => role switch
        {
            IconWindowRole.TargetDebuffs => this.GetTargetAuraFrameIndex().Keys,
            IconWindowRole.PartyBuffs => this.GetPartyAuraFrameIndex(ownOnly).Keys,
            _ => this.GetPlayerAuraFrameIndex().Keys,
        };

    private bool MatchesStatusSearch(uint statusId, string name, string query)
    {
        return name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || statusId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSearchableStatusName(string name)
    {
        return AuraSearchIndex.IsSearchableStatusName(name);
    }
}
