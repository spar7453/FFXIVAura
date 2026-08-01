namespace FFXIVAura;

internal readonly record struct AuraSearchRuntimeDiagnostics(
    int ResultCacheCount,
    long ResultCacheHitCount,
    long ResultCacheMissCount,
    int VisibleScopeCount,
    int VisibleStatusCount,
    int FirstSeenScopeCount,
    int FirstSeenStatusCount,
    int RevisionScopeCount,
    int TrackedGroupCacheCount);

internal interface IAuraTrackedGroupProvider
{
    IReadOnlyList<AuraStatusGroup> GetTrackedGroups(IconWindowConfig iconWindow);

    void InvalidateTrackedGroups(IconWindowConfig iconWindow);
}

internal sealed class AuraSearchService : IAuraTrackedGroupProvider
{
    private const int SeenHistoryLimit = 512;
    private const int ResultCacheLimit = 4;
    private readonly IAuraCatalog catalog;
    private readonly IAuraStatusFrameSource auraFrameService;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly Dictionary<string, HashSet<uint>> visibleAurasByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<uint, DateTime>> firstSeenByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> stateRevisionByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AuraSearchResultCacheEntry> resultCacheByWindow = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AuraStatusGroupCacheEntry> trackedGroupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<uint> currentStatusIdBuffer = [];
    private readonly HashSet<uint> currentStatusIdSetBuffer = [];
    private long resultCacheHitCount;
    private long resultCacheMissCount;

    public AuraSearchService(
        IAuraCatalog catalog,
        IAuraStatusFrameSource auraFrameService,
        PerformanceProfiler performanceProfiler)
    {
        this.catalog = catalog;
        this.auraFrameService = auraFrameService;
        this.performanceProfiler = performanceProfiler;
    }

    public IReadOnlyList<AuraSearchDisplayResult> Search(string search, IconWindowConfig iconWindow)
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraSearch);
        try
        {
            var query = search?.Trim() ?? string.Empty;
            this.UpdateCurrentSeenTimes(iconWindow, this.currentStatusIdBuffer);
            this.currentStatusIdSetBuffer.Clear();
            foreach (var statusId in this.currentStatusIdBuffer)
                this.currentStatusIdSetBuffer.Add(statusId);

            var cacheKey = this.CreateResultCacheKey(iconWindow, query);
            if (this.resultCacheByWindow.TryGetValue(iconWindow.Id, out var cached)
                && cached.Key == cacheKey)
            {
                this.resultCacheHitCount++;
                return cached.Results;
            }

            this.resultCacheMissCount++;
            var candidates = new List<AuraSearchDisplayResult>();
            this.AddCurrentStatusResults(candidates, query, iconWindow, this.currentStatusIdBuffer);
            IReadOnlyList<AuraSearchDisplayResult> results;
            if (iconWindow.AuraSearchActiveOnly)
            {
                results = this.FinalizeResults(candidates, query, iconWindow);
            }
            else
            {
                this.AddRecentStatusResults(candidates, query, iconWindow, this.currentStatusIdSetBuffer);
                if (query.Length > 0)
                {
                    this.AddActionGrantedStatusResults(candidates, query);
                    this.AddAllStatusResults(candidates, query);
                }

                results = this.FinalizeResults(candidates, query, iconWindow);
            }

            if (this.catalog.CanCacheSearch(iconWindow.AuraSearchActiveOnly, query))
            {
                cacheKey = this.CreateResultCacheKey(iconWindow, query);
                if (this.resultCacheByWindow.Count >= ResultCacheLimit
                    && !this.resultCacheByWindow.ContainsKey(iconWindow.Id))
                {
                    this.resultCacheByWindow.Clear();
                }

                this.resultCacheByWindow[iconWindow.Id] = new AuraSearchResultCacheEntry(cacheKey, results);
            }
            else
            {
                this.resultCacheByWindow.Remove(iconWindow.Id);
            }

            return results;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraSearch, profileStart);
        }
    }

    public IReadOnlyList<AuraStatusGroup> GetTrackedGroups(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            return Array.Empty<AuraStatusGroup>();

        var statusIndexBuilt = this.catalog.EnsureStatusIdentityIndex();
        var statusIndexGeneration = this.catalog.StatusIdentityGeneration;
        if (statusIndexBuilt
            && this.trackedGroupCache.TryGetValue(iconWindow.Id, out var cached)
            && cached.Matches(
                iconWindow.TrackedStatusIds,
                iconWindow.ExactTrackedStatusIds,
                statusIndexGeneration))
        {
            return cached.Groups;
        }

        var groups = AuraStatusGroups.Build(
            iconWindow.TrackedStatusIds,
            iconWindow.ExactTrackedStatusIds,
            this.catalog.GetDefinition,
            key => statusIndexBuilt
                ? this.catalog.GetStatusIdsByGroup(key)
                : iconWindow.TrackedStatusIds
                    .Where(statusId => !iconWindow.ExactTrackedStatusIds.Contains(statusId)
                                       && this.catalog.GetDefinition(statusId).GroupKey == key)
                    .ToArray());
        if (statusIndexBuilt)
        {
            this.trackedGroupCache[iconWindow.Id] = new AuraStatusGroupCacheEntry(
                iconWindow.TrackedStatusIds.ToArray(),
                iconWindow.ExactTrackedStatusIds.ToArray(),
                statusIndexGeneration,
                groups);
        }
        else
        {
            this.trackedGroupCache.Remove(iconWindow.Id);
        }

        return groups;
    }

    public void InvalidateTrackedGroups(IconWindowConfig iconWindow)
        => this.trackedGroupCache.Remove(iconWindow.Id);

    public void UpdateCurrentSeenTimes(IconWindowConfig iconWindow, List<uint> output)
    {
        output.Clear();
        foreach (var statusId in this.GetCurrentStatusIds(iconWindow))
            output.Add(statusId);

        this.UpdateSeenTimes(iconWindow, output);
    }

    public void UpdateCurrentSeenTimes(IconWindowConfig iconWindow)
        => this.UpdateCurrentSeenTimes(iconWindow, this.currentStatusIdBuffer);

    public void RemoveWindow(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            return;

        RemoveScopedKeys(this.visibleAurasByScope, windowId);
        RemoveScopedKeys(this.firstSeenByScope, windowId);
        RemoveScopedKeys(this.stateRevisionByScope, windowId);
        this.resultCacheByWindow.Remove(windowId);
        this.trackedGroupCache.Remove(windowId);
    }

    public void PruneWindows(IReadOnlyCollection<string> windowIds)
    {
        PruneScopedKeys(this.visibleAurasByScope, windowIds);
        PruneScopedKeys(this.firstSeenByScope, windowIds);
        PruneScopedKeys(this.stateRevisionByScope, windowIds);
        PruneWindowKeys(this.resultCacheByWindow, windowIds);
        PruneWindowKeys(this.trackedGroupCache, windowIds);
    }

    public void ResetWorldRuntimeState()
    {
        this.visibleAurasByScope.Clear();
        this.firstSeenByScope.Clear();
        this.stateRevisionByScope.Clear();
        this.resultCacheByWindow.Clear();
        this.currentStatusIdBuffer.Clear();
        this.currentStatusIdSetBuffer.Clear();
    }

    public AuraSearchRuntimeDiagnostics CreateDiagnostics()
        => new(
            this.resultCacheByWindow.Count,
            this.resultCacheHitCount,
            this.resultCacheMissCount,
            this.visibleAurasByScope.Count,
            this.visibleAurasByScope.Values.Sum(statusIds => statusIds.Count),
            this.firstSeenByScope.Count,
            this.firstSeenByScope.Values.Sum(statuses => statuses.Count),
            this.stateRevisionByScope.Count,
            this.trackedGroupCache.Count);

    private AuraSearchResultCacheKey CreateResultCacheKey(IconWindowConfig iconWindow, string query)
    {
        var scopeKey = RuntimeScopeKeys.AuraSeen(iconWindow);
        return new AuraSearchResultCacheKey(
            query,
            iconWindow.Role,
            iconWindow.AuraSearchActiveOnly,
            iconWindow.AuraSearchShowIndividualIds,
            iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.PartyAurasOwnOnly,
            this.stateRevisionByScope.GetValueOrDefault(scopeKey),
            this.catalog.StatusIdentityGeneration,
            this.catalog.ActionGrantedGeneration);
    }

    private IReadOnlyList<AuraSearchDisplayResult> FinalizeResults(
        IEnumerable<AuraSearchDisplayResult> candidates,
        string query,
        IconWindowConfig iconWindow)
    {
        var separateSameNameIds = iconWindow.AuraSearchShowIndividualIds
                                  || uint.TryParse(query, out _);
        var results = AuraSearchDisplayResults.MergeAndSort(candidates, query, separateSameNameIds);
        for (var index = 0; index < results.Count; index++)
        {
            var knownCount = this.catalog.GetStatusIdsByGroup(results[index].GroupKey).Count;
            if (knownCount > results[index].SameNameCount)
                results[index] = results[index].WithSameNameCount(knownCount);
        }

        return results;
    }

    private void AddCurrentStatusResults(
        List<AuraSearchDisplayResult> results,
        string query,
        IconWindowConfig iconWindow,
        IReadOnlyList<uint> currentStatusIds)
    {
        foreach (var statusId in currentStatusIds)
        {
            var definition = this.catalog.GetDefinition(statusId);
            if (!AuraSearchIndex.IsSearchableStatusName(definition.Name))
                continue;

            var sourceActionNames = this.catalog.GetActionGrantedSourceNames(statusId, query);
            if (query.Length > 0
                && !MatchesStatusSearch(statusId, definition.Name, query)
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
                SeenAtUtc: this.GetSeenTime(iconWindow, statusId),
                SourceActionNames: sourceActionNames,
                StatusCategory: definition.StatusCategory));
        }
    }

    private void AddActionGrantedStatusResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var entry in this.catalog.GetActionGrantedMatches(query))
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

    private void AddAllStatusResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var result in this.catalog.GetAllStatusMatches(query))
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

    private void AddRecentStatusResults(
        List<AuraSearchDisplayResult> results,
        string query,
        IconWindowConfig iconWindow,
        HashSet<uint> currentStatusIds)
    {
        if (!this.firstSeenByScope.TryGetValue(RuntimeScopeKeys.AuraSeen(iconWindow), out var seenTimes))
            return;

        foreach (var (statusId, seenAt) in seenTimes)
        {
            if (currentStatusIds.Contains(statusId))
                continue;

            var definition = this.catalog.GetDefinition(statusId);
            if (!AuraSearchIndex.IsSearchableStatusName(definition.Name))
                continue;

            var sourceActionNames = this.catalog.GetActionGrantedSourceNames(statusId, query);
            if (query.Length > 0
                && !MatchesStatusSearch(statusId, definition.Name, query)
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

    private void UpdateSeenTimes(IconWindowConfig iconWindow, IReadOnlyCollection<uint> currentStatusIds)
    {
        var scopeKey = RuntimeScopeKeys.AuraSeen(iconWindow);
        if (!this.visibleAurasByScope.TryGetValue(scopeKey, out var previous))
        {
            previous = [];
            this.visibleAurasByScope[scopeKey] = previous;
        }

        if (!this.firstSeenByScope.TryGetValue(scopeKey, out var seenTimes))
        {
            seenTimes = new Dictionary<uint, DateTime>();
            this.firstSeenByScope[scopeKey] = seenTimes;
        }

        if (AuraSearchStatusSets.HaveSameMembers(previous, currentStatusIds))
            return;

        var nowUtc = DateTime.UtcNow;
        foreach (var statusId in currentStatusIds)
        {
            if (!previous.Contains(statusId))
                seenTimes[statusId] = nowUtc;
        }

        previous.Clear();
        foreach (var statusId in currentStatusIds)
            previous.Add(statusId);

        PruneSeenTimes(seenTimes);
        this.stateRevisionByScope[scopeKey] = this.stateRevisionByScope.GetValueOrDefault(scopeKey) + 1;
    }

    private DateTime GetSeenTime(IconWindowConfig iconWindow, uint statusId)
        => this.firstSeenByScope.TryGetValue(RuntimeScopeKeys.AuraSeen(iconWindow), out var seenTimes)
           && seenTimes.TryGetValue(statusId, out var seenAt)
            ? seenAt
            : DateTime.MinValue;

    private IEnumerable<uint> GetCurrentStatusIds(IconWindowConfig iconWindow)
        => iconWindow.Role switch
        {
            IconWindowRole.TargetDebuffs => this.auraFrameService.GetTargetStatusIds(),
            IconWindowRole.PartyBuffs => this.auraFrameService.GetPartyStatusIds(iconWindow.PartyAurasOwnOnly),
            _ => this.auraFrameService.GetPlayerStatusIds(),
        };

    private static bool MatchesStatusSearch(uint statusId, string name, string query)
        => name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
           || statusId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

    private static void PruneSeenTimes(Dictionary<uint, DateTime> seenTimes)
    {
        if (seenTimes.Count <= SeenHistoryLimit)
            return;

        var staleStatusIds = seenTimes
            .OrderByDescending(pair => pair.Value)
            .Skip(SeenHistoryLimit)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var statusId in staleStatusIds)
            seenTimes.Remove(statusId);
    }

    private static void RemoveScopedKeys<TValue>(Dictionary<string, TValue> map, string windowId)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (RuntimeScopeKeys.BelongsToWindow(key, windowId))
                map.Remove(key);
        }
    }

    private static void PruneScopedKeys<TValue>(
        Dictionary<string, TValue> map,
        IReadOnlyCollection<string> windowIds)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (windowIds.Any(windowId => RuntimeScopeKeys.BelongsToWindow(key, windowId)))
                continue;

            map.Remove(key);
        }
    }

    private static void PruneWindowKeys<TValue>(
        Dictionary<string, TValue> map,
        IReadOnlyCollection<string> windowIds)
    {
        foreach (var key in map.Keys.ToList())
        {
            if (!windowIds.Contains(key, StringComparer.OrdinalIgnoreCase))
                map.Remove(key);
        }
    }
}
