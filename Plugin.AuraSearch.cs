namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AuraSeenHistoryLimit = 512;

    private IReadOnlyList<AuraSearchDisplayResult> SearchStatuses(string search, IconWindowConfig iconWindow)
    {
        var query = search?.Trim() ?? string.Empty;
        var currentStatusIds = this.UpdateCurrentAuraSeenTimes(iconWindow);
        var currentStatusIdSet = currentStatusIds.ToHashSet();
        var candidates = new List<AuraSearchDisplayResult>();
        this.AddCurrentStatusSearchResults(candidates, query, iconWindow, currentStatusIds);
        if (iconWindow.AuraSearchActiveOnly)
            return AuraSearchDisplayResults.MergeAndSort(candidates, query);

        this.AddRecentStatusSearchResults(candidates, query, iconWindow, currentStatusIdSet);
        if (query.Length > 0)
        {
            this.AddActionGrantedStatusSearchResults(candidates, query);
            this.AddAllStatusSearchResults(candidates, query);
        }

        return AuraSearchDisplayResults.MergeAndSort(candidates, query);
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
                SourceActionNames: sourceActionNames));
        }
    }

    private void AddActionGrantedStatusSearchResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var entry in this.GetActionGrantedStatusSearchIndex())
        {
            if (!AuraSearchIndex.Matches(entry, query))
                continue;

            results.Add(new AuraSearchDisplayResult(
                entry.StatusId,
                entry.Name,
                entry.IconId,
                IsCurrent: false,
                WasRecentlySeen: false,
                FromAction: true,
                FromStatusSheet: false,
                SeenAtUtc: DateTime.MinValue,
                SourceActionNames: entry.PrimarySearchText));
        }
    }

    private void AddAllStatusSearchResults(List<AuraSearchDisplayResult> results, string query)
    {
        foreach (var result in AuraSearchIndex.Search(this.GetAllStatusSearchIndex(), query))
        {
            results.Add(new AuraSearchDisplayResult(
                result.StatusId,
                result.Name,
                result.IconId,
                IsCurrent: false,
                WasRecentlySeen: false,
                FromAction: false,
                FromStatusSheet: true,
                SeenAtUtc: DateTime.MinValue));
        }
    }

    private IReadOnlyList<AuraSearchIndexEntry> GetActionGrantedStatusSearchIndex()
    {
        if (this.actionGrantedStatusSearchIndexBuilt)
            return this.actionGrantedStatusSearchIndex;

        this.actionGrantedStatusSearchIndex.Clear();
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
        {
            this.actionGrantedStatusSearchIndexBuilt = true;
            return this.actionGrantedStatusSearchIndex;
        }

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

            this.actionGrantedStatusSearchIndex.Add(new AuraSearchIndexEntry(
                statusId,
                definition.Name,
                definition.IconId,
                actionName,
                action.RowId.ToString(),
                $"{definition.Name} {statusId}"));
        }

        this.actionGrantedStatusSearchIndexBuilt = true;
        return this.actionGrantedStatusSearchIndex;
    }

    private IReadOnlyList<AuraSearchIndexEntry> GetAllStatusSearchIndex()
    {
        if (this.allStatusSearchIndexBuilt)
            return this.allStatusSearchIndex;

        this.allStatusSearchIndex.Clear();
        var sheet = DataManager.GetExcelSheet<GameStatus>();
        if (sheet is null)
        {
            this.allStatusSearchIndexBuilt = true;
            return this.allStatusSearchIndex;
        }

        foreach (var status in sheet)
        {
            if (status.RowId == 0 || status.Icon == 0)
                continue;

            var name = status.Name.ExtractText();
            if (!this.IsSearchableStatusName(name))
                continue;

            this.statusDefinitionCache.TryAdd(status.RowId, (name, status.Icon));
            this.allStatusSearchIndex.Add(new AuraSearchIndexEntry(
                status.RowId,
                name,
                status.Icon,
                name,
                status.RowId.ToString()));
        }

        this.allStatusSearchIndexBuilt = true;
        return this.allStatusSearchIndex;
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
                SourceActionNames: sourceActionNames));
        }
    }

    private IReadOnlyList<uint> UpdateCurrentAuraSeenTimes(IconWindowConfig iconWindow)
    {
        var currentStatusIds = this.GetCurrentStatusIds(iconWindow)
            .Distinct()
            .ToList();
        this.UpdateAuraSeenTimes(iconWindow, currentStatusIds);
        return currentStatusIds;
    }

    private string GetActionGrantedSourceNames(uint statusId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var names = new List<string>();
        foreach (var entry in this.GetActionGrantedStatusSearchIndex())
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
    {
        switch (role)
        {
            case IconWindowRole.TargetDebuffs:
                if (TargetManager.Target is IBattleChara target)
                {
                    foreach (var status in target.StatusList)
                    {
                        if (status.StatusId > 0)
                            yield return status.StatusId;
                    }
                }

                break;

            case IconWindowRole.PartyBuffs:
                for (var i = 0; i < PartyList.Length; i++)
                {
                    var member = PartyList[i];
                    if (member is null)
                        continue;

                    foreach (var status in member.Statuses)
                    {
                        if (status.StatusId > 0 && (!ownOnly || this.IsStatusFromSelf(status.SourceId)))
                            yield return status.StatusId;
                    }
                }

                break;

            default:
                if (ObjectTable.LocalPlayer is IBattleChara player)
                {
                    foreach (var status in player.StatusList)
                    {
                        if (status.StatusId > 0)
                            yield return status.StatusId;
                    }
                }

                break;
        }
    }

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
