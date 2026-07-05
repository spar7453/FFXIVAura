namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private const int AuraSeenHistoryLimit = 512;

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchStatuses(string search, IconWindowConfig iconWindow)
    {
        var query = search?.Trim() ?? string.Empty;
        var currentStatusIds = this.GetCurrentStatusIds(iconWindow)
            .Distinct()
            .ToList();
        this.UpdateAuraSeenTimes(iconWindow, currentStatusIds);
        if (iconWindow.AuraSearchActiveOnly)
        {
            foreach (var result in this.SearchCurrentStatuses(query, iconWindow, currentStatusIds))
                yield return result;

            yield break;
        }

        var hasIdQuery = uint.TryParse(query, out var idQuery);
        var results = this.SearchRecentStatuses(query, iconWindow);
        if (query.Length > 0)
            results = results
                .Concat(this.SearchActionGrantedStatuses(query))
                .Concat(this.SearchAllStatuses(query));

        foreach (var result in results
                     .GroupBy(result => result.StatusId)
                     .Select(group => group.First())
                     .OrderByDescending(row => hasIdQuery && row.StatusId == idQuery)
                     .ThenByDescending(row => query.Length > 0 && row.Name.Equals(query, StringComparison.CurrentCultureIgnoreCase))
                     .ThenByDescending(row => query.Length > 0 && row.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
                     .ThenByDescending(row => this.GetAuraSeenTime(iconWindow, row.StatusId))
                     .ThenBy(row => row.StatusId))
        {
            yield return result;
        }
    }

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchActionGrantedStatuses(string query)
    {
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            yield break;

        foreach (var action in sheet)
        {
            if (action.RowId == 0 || action.StatusGainSelf.RowId == 0)
                continue;

            var actionName = action.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(actionName))
                continue;

            if (!actionName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                && !action.RowId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var statusId = action.StatusGainSelf.RowId;
            var definition = this.GetStatusDefinition(statusId);
            if (!this.IsSearchableStatusName(definition.Name))
                continue;

            yield return (statusId, definition.Name, definition.IconId);
        }
    }

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchAllStatuses(string query)
    {
        var hasIdQuery = uint.TryParse(query, out var idQuery);
        var sheet = DataManager.GetExcelSheet<GameStatus>();
        if (sheet is null)
            yield break;

        foreach (var status in sheet)
        {
            if (status.RowId == 0 || status.Icon == 0)
                continue;

            var name = status.Name.ExtractText();
            if (!this.IsSearchableStatusName(name))
                continue;

            if (!((hasIdQuery && status.RowId == idQuery) || this.MatchesStatusSearch(status.RowId, name, query)))
                continue;

            yield return (status.RowId, name, status.Icon);
        }
    }

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchRecentStatuses(string query, IconWindowConfig iconWindow)
    {
        if (!this.auraFirstSeenByScope.TryGetValue(RuntimeScopeKeys.AuraSeen(iconWindow), out var seenTimes))
            yield break;

        foreach (var statusId in seenTimes.Keys)
        {
            var definition = this.GetStatusDefinition(statusId);
            if (!this.IsSearchableStatusName(definition.Name))
                continue;

            if (query.Length > 0 && !this.MatchesStatusSearch(statusId, definition.Name, query))
                continue;

            yield return (statusId, definition.Name, definition.IconId);
        }
    }

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchCurrentStatuses(string query, IconWindowConfig iconWindow, IReadOnlyCollection<uint>? currentStatusIds = null)
    {
        currentStatusIds ??= this.GetCurrentStatusIds(iconWindow)
            .Distinct()
            .ToList();
        this.UpdateAuraSeenTimes(iconWindow, currentStatusIds);

        return currentStatusIds
            .Select(statusId =>
            {
                var definition = this.GetStatusDefinition(statusId);
                return (StatusId: statusId, definition.Name, definition.IconId);
            })
            .Where(result => this.IsSearchableStatusName(result.Name))
            .Where(result => query.Length == 0 || this.MatchesStatusSearch(result.StatusId, result.Name, query))
            .OrderByDescending(result => result.Name.Equals(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenByDescending(result => result.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenByDescending(result => this.GetAuraSeenTime(iconWindow, result.StatusId))
            .ThenBy(result => result.StatusId);
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
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = name.Trim();
        return !trimmed.StartsWith("_", StringComparison.Ordinal)
               && !trimmed.StartsWith("rsv_", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("_rsv", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains('\uFF1D')
               && !trimmed.Contains('=')
               && !trimmed.Contains('\u25CB')
               && !trimmed.Contains('\u25CF')
               && !ContainsJapaneseKana(trimmed);
    }

    private static bool ContainsJapaneseKana(string text)
    {
        return text.Any(ch => (ch >= '\u3040' && ch <= '\u30ff') || (ch >= '\u31f0' && ch <= '\u31ff'));
    }
}
