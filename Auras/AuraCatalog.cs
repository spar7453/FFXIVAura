using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace FFXIVAura;

internal readonly record struct AuraCatalogDiagnostics(
    int StatusDefinitionCount,
    bool StatusIdentityIndexBuilt,
    int StatusIdentityIndexGeneration,
    int StatusIdentityGroupCount,
    bool AllStatusSearchIndexBuilt,
    int AllStatusSearchEntryCount,
    int AllStatusSearchQueryCacheCount,
    bool ActionGrantedIndexBuilt,
    int ActionGrantedIndexGeneration,
    int ActionGrantedEntryCount,
    int ActionGrantedStatusBucketCount,
    int ActionGrantedQueryCacheCount);

internal interface IAuraCatalog
{
    int StatusIdentityGeneration { get; }

    int ActionGrantedGeneration { get; }

    bool CanCacheSearch(bool activeOnly, string query);

    AuraStatusDefinition GetDefinition(uint statusId);

    bool EnsureStatusIdentityIndex();

    IReadOnlyList<uint> GetStatusIdsByGroup(AuraStatusGroupKey key);

    IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedMatches(string query);

    IReadOnlyList<AuraSearchResult> GetAllStatusMatches(string query);

    string GetActionGrantedSourceNames(uint statusId, string query);

    AuraCatalogDiagnostics CreateDiagnostics();
}

internal sealed class AuraCatalog : IAuraCatalog
{
    private const int QueryCacheLimit = 16;
    private static readonly TimeSpan IndexRetryDelay = TimeSpan.FromSeconds(1);
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly Dictionary<uint, AuraStatusDefinition> statusDefinitions = new();
    private readonly List<uint> statusIdentityIds = [];
    private readonly Dictionary<AuraStatusGroupKey, List<uint>> statusIdsByGroup = new();
    private readonly AuraStatusIndexBuildState statusIdentityIndexState = new();
    private readonly List<AuraSearchIndexEntry> actionGrantedSearchIndex = [];
    private readonly Dictionary<uint, List<AuraSearchIndexEntry>> actionGrantedIndexByStatusId = new();
    private readonly Dictionary<string, IReadOnlyList<AuraSearchIndexEntry>> actionGrantedQueryCache = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly AuraStatusIndexBuildState actionGrantedIndexState = new();
    private readonly List<AuraSearchIndexEntry> allStatusSearchIndex = [];
    private readonly Dictionary<string, IReadOnlyList<AuraSearchResult>> allStatusQueryCache = new(StringComparer.CurrentCultureIgnoreCase);
    private bool allStatusSearchIndexBuilt;

    public AuraCatalog(
        IDataManager dataManager,
        IPluginLog log,
        PerformanceProfiler performanceProfiler)
    {
        this.dataManager = dataManager;
        this.log = log;
        this.performanceProfiler = performanceProfiler;
    }

    public int StatusIdentityGeneration => this.statusIdentityIndexState.Generation;

    public int ActionGrantedGeneration => this.actionGrantedIndexState.Generation;

    public bool CanCacheSearch(bool activeOnly, string query)
        => this.statusIdentityIndexState.IsBuilt
           && (activeOnly
               || query.Length == 0
               || (this.allStatusSearchIndexBuilt && this.actionGrantedIndexState.IsBuilt));

    public AuraStatusDefinition GetDefinition(uint statusId)
    {
        if (this.statusDefinitions.TryGetValue(statusId, out var cached))
            return cached;

        var definition = new AuraStatusDefinition($"Status {statusId}", 0u, 0);
        var shouldCache = false;
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText();
                definition = new AuraStatusDefinition(
                    string.IsNullOrWhiteSpace(name) ? $"Status {statusId}" : name,
                    row.Icon,
                    row.StatusCategory);
                shouldCache = true;
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read status {statusId}.");
        }

        if (shouldCache)
            this.statusDefinitions[statusId] = definition;

        return definition;
    }

    public bool EnsureStatusIdentityIndex()
    {
        if (this.statusIdentityIndexState.IsBuilt)
            return true;

        var nowUtc = DateTime.UtcNow;
        if (!this.statusIdentityIndexState.ShouldAttempt(nowUtc))
            return false;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraIndexBuild);
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameStatus>();
            if (sheet is null)
            {
                this.statusIdentityIndexState.MarkFailed(nowUtc, IndexRetryDelay);
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
                if (!AuraSearchIndex.IsSearchableStatusName(name))
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
                this.statusDefinitions[statusId] = definition;

            this.statusIdentityIds.Clear();
            this.statusIdentityIds.AddRange(nextStatusIds);
            this.statusIdsByGroup.Clear();
            foreach (var (key, statusIds) in nextStatusIdsByGroup)
                this.statusIdsByGroup[key] = statusIds;

            this.statusIdentityIndexState.MarkSucceeded();
            this.allStatusSearchIndex.Clear();
            this.allStatusQueryCache.Clear();
            this.allStatusSearchIndexBuilt = false;
            return true;
        }
        catch (Exception ex)
        {
            this.statusIdentityIndexState.MarkFailed(nowUtc, IndexRetryDelay);
            this.log.Debug(ex, "Failed to build the aura status identity index.");
            return false;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }
    }

    public IReadOnlyList<uint> GetStatusIdsByGroup(AuraStatusGroupKey key)
    {
        if (!key.IsValid)
            return Array.Empty<uint>();

        _ = this.EnsureStatusIdentityIndex();
        return this.statusIdsByGroup.TryGetValue(key, out var statusIds)
            ? statusIds
            : Array.Empty<uint>();
    }

    public IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedMatches(string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
            return Array.Empty<AuraSearchIndexEntry>();

        if (this.actionGrantedQueryCache.TryGetValue(normalizedQuery, out var cached))
            return cached;

        var index = this.GetActionGrantedSearchIndex();
        if (!this.actionGrantedIndexState.IsBuilt)
            return Array.Empty<AuraSearchIndexEntry>();

        var matches = index
            .Where(entry => AuraSearchIndex.Matches(entry, normalizedQuery))
            .ToArray();
        AddQueryCacheEntry(this.actionGrantedQueryCache, normalizedQuery, matches);
        return matches;
    }

    public IReadOnlyList<AuraSearchResult> GetAllStatusMatches(string query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
            return Array.Empty<AuraSearchResult>();

        if (this.allStatusQueryCache.TryGetValue(normalizedQuery, out var cached))
            return cached;

        var index = this.GetAllStatusSearchIndex();
        if (!this.allStatusSearchIndexBuilt)
            return Array.Empty<AuraSearchResult>();

        var matches = AuraSearchIndex.Search(index, normalizedQuery).ToArray();
        AddQueryCacheEntry(this.allStatusQueryCache, normalizedQuery, matches);
        return matches;
    }

    public string GetActionGrantedSourceNames(uint statusId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        _ = this.GetActionGrantedSearchIndex();
        if (!this.actionGrantedIndexByStatusId.TryGetValue(statusId, out var statusEntries))
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

    public AuraCatalogDiagnostics CreateDiagnostics()
        => new(
            this.statusDefinitions.Count,
            this.statusIdentityIndexState.IsBuilt,
            this.statusIdentityIndexState.Generation,
            this.statusIdsByGroup.Count,
            this.allStatusSearchIndexBuilt,
            this.allStatusSearchIndex.Count,
            this.allStatusQueryCache.Count,
            this.actionGrantedIndexState.IsBuilt,
            this.actionGrantedIndexState.Generation,
            this.actionGrantedSearchIndex.Count,
            this.actionGrantedIndexByStatusId.Count,
            this.actionGrantedQueryCache.Count);

    private IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedSearchIndex()
    {
        if (this.actionGrantedIndexState.IsBuilt)
            return this.actionGrantedSearchIndex;

        if (!this.EnsureStatusIdentityIndex())
            return this.actionGrantedSearchIndex;

        var nowUtc = DateTime.UtcNow;
        if (!this.actionGrantedIndexState.ShouldAttempt(nowUtc))
            return this.actionGrantedSearchIndex;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.AuraIndexBuild);
        try
        {
            var sheet = this.dataManager.GetExcelSheet<GameAction>();
            if (sheet is null)
            {
                this.actionGrantedIndexState.MarkFailed(nowUtc, IndexRetryDelay);
                return this.actionGrantedSearchIndex;
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
                var definition = this.GetDefinition(statusId);
                if (!AuraSearchIndex.IsSearchableStatusName(definition.Name))
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

            this.actionGrantedSearchIndex.Clear();
            this.actionGrantedSearchIndex.AddRange(nextIndex);
            this.actionGrantedIndexByStatusId.Clear();
            foreach (var (statusId, entries) in nextIndexByStatusId)
                this.actionGrantedIndexByStatusId[statusId] = entries;

            this.actionGrantedQueryCache.Clear();
            this.actionGrantedIndexState.MarkSucceeded();
        }
        catch (Exception ex)
        {
            this.actionGrantedIndexState.MarkFailed(nowUtc, IndexRetryDelay);
            this.log.Debug(ex, "Failed to build the action-granted aura search index.");
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }

        return this.actionGrantedSearchIndex;
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
            var nextSearchIndex = new List<AuraSearchIndexEntry>(this.statusIdentityIds.Count);
            foreach (var statusId in this.statusIdentityIds)
            {
                if (!this.statusDefinitions.TryGetValue(statusId, out var definition)
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
            this.allStatusQueryCache.Clear();
            this.allStatusSearchIndexBuilt = true;
            return this.allStatusSearchIndex;
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.AuraIndexBuild, profileStart);
        }
    }

    private static void AddQueryCacheEntry<T>(
        Dictionary<string, IReadOnlyList<T>> cache,
        string query,
        IReadOnlyList<T> results)
    {
        if (cache.Count >= QueryCacheLimit)
            cache.Clear();

        cache[query] = results;
    }
}
