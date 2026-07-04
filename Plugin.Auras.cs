namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void DrawAuraTrackerEditor(IconWindowConfig iconWindow)
    {
        ImGui.TextUnformatted("\uCD94\uC801 \uBC84\uD504/\uB514\uBC84\uD504");

        var search = iconWindow.AuraSearch ?? string.Empty;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##FFXIVAuraAuraSearch", "\uBC84\uD504/\uB514\uBC84\uD504 \uC774\uB984 \uB610\uB294 ID \uAC80\uC0C9", ref search, 80))
        {
            iconWindow.AuraSearch = search;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("\uAC80\uC0C9"))
            this.auraSearchWindowVisible = true;

        if (!string.IsNullOrWhiteSpace(iconWindow.AuraSearch))
        {
            ImGui.SameLine();
            if (ImGui.Button("\uAC80\uC0C9 \uC9C0\uC6B0\uAE30"))
            {
                iconWindow.AuraSearch = string.Empty;
                this.QueueConfigSave();
            }
        }

        var activeOnly = iconWindow.AuraSearchActiveOnly;
        if (ImGui.Checkbox("\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uB9CC \uAC80\uC0C9", ref activeOnly))
        {
            iconWindow.AuraSearchActiveOnly = activeOnly;
            this.QueueConfigSave();
        }

        this.DrawAuraSearchWindow(iconWindow);

        ImGui.SetNextItemWidth(120f);
        ImGui.InputInt("\uC0C1\uD0DC ID", ref this.pendingStatusId);
        ImGui.SameLine();
        if (ImGui.Button("\uC0C1\uD0DC \uCD94\uAC00") && this.pendingStatusId > 0)
        {
            var id = (uint)this.pendingStatusId;
            if (!iconWindow.TrackedStatusIds.Contains(id))
            {
                iconWindow.TrackedStatusIds.Add(id);
                this.QueueConfigSave();
            }
        }

        ImGui.BeginChild("FFXIVAuraTrackedAuraList", new Vector2(560f, 220f), true);
        for (var i = 0; i < iconWindow.TrackedStatusIds.Count; i++)
        {
            var statusId = iconWindow.TrackedStatusIds[i];
            var definition = this.GetStatusDefinition(statusId);
            ImGui.PushID($"aura-{statusId}");
            this.DrawStatusListIcon(definition.IconId, 22f);
            ImGui.SameLine(0f, 8f);
            ImGui.TextUnformatted($"{definition.Name}  \uC0C1\uD0DC {statusId}");
            ImGui.SameLine();
            if (ImGui.SmallButton("\uC0AD\uC81C"))
            {
                iconWindow.TrackedStatusIds.RemoveAt(i);
                this.QueueConfigSave();
                ImGui.PopID();
                break;
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawAuraSearchWindow(IconWindowConfig iconWindow)
    {
        if (!this.auraSearchWindowVisible)
            return;

        ImGui.SetNextWindowSize(new Vector2(620f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("\uBC84\uD504/\uB514\uBC84\uD504 \uAC80\uC0C9", ref this.auraSearchWindowVisible))
        {
            ImGui.End();
            return;
        }

        var search = iconWindow.AuraSearch ?? string.Empty;
        ImGui.SetNextItemWidth(360f);
        if (ImGui.InputTextWithHint("##FFXIVAuraAuraSearchWindowInput", "\uC774\uB984 \uB610\uB294 ID", ref search, 80))
        {
            iconWindow.AuraSearch = search;
            this.QueueConfigSave();
        }

        ImGui.SameLine();
        if (ImGui.Button("\uC9C0\uC6B0\uAE30"))
        {
            iconWindow.AuraSearch = string.Empty;
            this.QueueConfigSave();
        }

        var activeOnly = iconWindow.AuraSearchActiveOnly;
        if (ImGui.Checkbox("\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uB9CC", ref activeOnly))
        {
            iconWindow.AuraSearchActiveOnly = activeOnly;
            this.QueueConfigSave();
        }

        ImGui.Separator();
        this.DrawAuraSearchResults(iconWindow, new Vector2(590f, 340f));
        ImGui.End();
    }

    private void DrawAuraSearchResults(IconWindowConfig iconWindow, Vector2 size)
    {
        var limit = string.IsNullOrWhiteSpace(iconWindow.AuraSearch) ? 200 : 80;
        var results = this.SearchStatuses(iconWindow.AuraSearch, iconWindow)
            .Take(limit)
            .ToList();

        ImGui.BeginChild("FFXIVAuraAuraSearchResults", size, true);
        if (results.Count == 0)
        {
            ImGui.TextUnformatted(iconWindow.AuraSearchActiveOnly
                ? "\uD604\uC7AC \uBCF4\uC774\uB294 \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
                : "\uCD5C\uADFC \uAC10\uC9C0\uB41C \uBC84\uD504/\uB514\uBC84\uD504\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
        }

        foreach (var result in results)
        {
            var alreadyTracked = iconWindow.TrackedStatusIds.Contains(result.StatusId);
            ImGui.PushID($"status-search-{result.StatusId}");
            this.DrawStatusListIcon(result.IconId, 22f);
            ImGui.SameLine(0f, 8f);
            ImGui.TextUnformatted($"{result.Name}  \uC0C1\uD0DC {result.StatusId}");
            ImGui.SameLine();
            if (alreadyTracked)
            {
                ImGui.BeginDisabled();
                ImGui.SmallButton("\uCD94\uAC00\uB428");
                ImGui.EndDisabled();
            }
            else if (ImGui.SmallButton("\uCD94\uAC00"))
            {
                iconWindow.TrackedStatusIds.Add(result.StatusId);
                this.QueueConfigSave();
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }
    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchStatuses(string search, IconWindowConfig iconWindow)
    {
        var query = search.Trim();
        var currentStatusIds = this.GetCurrentStatusIds(iconWindow.Role)
            .Distinct()
            .ToList();
        this.UpdateAuraSeenTimes(iconWindow.Role, currentStatusIds);
        if (iconWindow.AuraSearchActiveOnly)
        {
            foreach (var result in this.SearchCurrentStatuses(query, iconWindow.Role, currentStatusIds))
                yield return result;

            yield break;
        }

        var hasIdQuery = uint.TryParse(query, out var idQuery);
        var results = this.SearchRecentStatuses(query, iconWindow.Role);
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
                     .ThenByDescending(row => this.GetAuraSeenTime(iconWindow.Role, row.StatusId))
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

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchRecentStatuses(string query, IconWindowRole role)
    {
        if (!this.auraFirstSeenByRole.TryGetValue(role, out var seenTimes))
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

    private IEnumerable<(uint StatusId, string Name, uint IconId)> SearchCurrentStatuses(string query, IconWindowRole role, IReadOnlyCollection<uint>? currentStatusIds = null)
    {
        currentStatusIds ??= this.GetCurrentStatusIds(role)
            .Distinct()
            .ToList();
        this.UpdateAuraSeenTimes(role, currentStatusIds);

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
            .ThenByDescending(result => this.GetAuraSeenTime(role, result.StatusId))
            .ThenBy(result => result.StatusId);
    }

    private void UpdateAuraSeenTimes(IconWindowRole role, IReadOnlyCollection<uint> currentStatusIds)
    {
        if (!this.visibleAurasByRole.TryGetValue(role, out var previous))
        {
            previous = [];
            this.visibleAurasByRole[role] = previous;
        }

        if (!this.auraFirstSeenByRole.TryGetValue(role, out var seenTimes))
        {
            seenTimes = new Dictionary<uint, DateTime>();
            this.auraFirstSeenByRole[role] = seenTimes;
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
    }

    private DateTime GetAuraSeenTime(IconWindowRole role, uint statusId)
    {
        return this.auraFirstSeenByRole.TryGetValue(role, out var seenTimes)
               && seenTimes.TryGetValue(statusId, out var seenAt)
            ? seenAt
            : DateTime.MinValue;
    }

    private IEnumerable<uint> GetCurrentStatusIds(IconWindowRole role)
        => this.GetCurrentStatusIds(role, ownOnly: false);

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
               && !trimmed.Contains('＝')
               && !trimmed.Contains('=')
               && !trimmed.Contains('○')
               && !trimmed.Contains('●')
               && !ContainsJapaneseKana(trimmed);
    }

    private static bool ContainsJapaneseKana(string text)
    {
        return text.Any(ch => (ch >= '\u3040' && ch <= '\u30ff') || (ch >= '\u31f0' && ch <= '\u31ff'));
    }

    private IEnumerable<AuraState> GetDisplayAuras(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            yield break;

        foreach (var statusId in iconWindow.TrackedStatusIds.Distinct())
        {
            var aura = this.GetAuraState(iconWindow, statusId);
            if (!aura.Present && !iconWindow.ShowMissingAuras)
                continue;

            if (!this.ShouldDisplayAura(aura, iconWindow))
                continue;

            yield return aura;
        }
    }

    private bool ShouldDisplayAura(AuraState aura, IconWindowConfig iconWindow)
    {
        return iconWindow.DisplayCondition switch
        {
            IconDisplayCondition.InCombat => this.IsInCombat(),
            IconDisplayCondition.OutOfCombat => !this.IsInCombat(),
            IconDisplayCondition.CoolingOnly => aura.Present,
            IconDisplayCondition.ReadyOnly => !aura.Present,
            _ => true,
        };
    }

    private AuraState GetAuraState(IconWindowConfig iconWindow, uint statusId)
    {
        var definition = this.GetStatusDefinition(statusId);
        var active = iconWindow.Role switch
        {
            IconWindowRole.TargetDebuffs => this.FindStatusOnTarget(statusId),
            IconWindowRole.PartyBuffs => this.FindStatusOnParty(statusId, iconWindow.PartyAurasOwnOnly),
            _ => this.FindStatusOnPlayer(statusId),
        };

        if (active is null)
            return new AuraState(statusId, definition.Name, definition.IconId, 0f, 0, 0, 0, false, false);

        return new AuraState(
            statusId,
            definition.Name,
            definition.IconId,
            Math.Max(0f, active.Value.Remaining),
            active.Value.Param,
            active.Value.Count,
            active.Value.OwnCount,
            true,
            active.Value.FromSelf);
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnPlayer(uint statusId)
    {
        return ObjectTable.LocalPlayer is IBattleChara chara
            ? this.FindStatus(chara, statusId, preferOwnStatus: false)
            : null;
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnTarget(uint statusId)
    {
        return TargetManager.Target is IBattleChara chara
            ? this.FindStatus(chara, statusId, preferOwnStatus: true)
            : null;
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatusOnParty(uint statusId, bool ownOnly)
    {
        (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? best = null;
        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is null)
                continue;

            foreach (var status in member.Statuses)
            {
                if (status.StatusId != statusId)
                    continue;

                var fromSelf = this.IsStatusFromSelf(status.SourceId);
                if (ownOnly && !fromSelf)
                    continue;

                var remaining = Math.Max(0f, status.RemainingTime);
                if (best is null)
                {
                    best = (remaining, status.Param, 1, fromSelf ? 1 : 0, fromSelf);
                    continue;
                }

                var current = best.Value;
                current.Count++;
                if (fromSelf)
                    current.OwnCount++;

                if (remaining > current.Remaining)
                {
                    current.Remaining = remaining;
                    current.Param = status.Param;
                    current.FromSelf = fromSelf;
                }
                else
                {
                    current.FromSelf |= fromSelf;
                }

                best = current;
            }
        }

        return best;
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatus(IBattleChara chara, uint statusId, bool preferOwnStatus)
    {
        (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? fallback = null;
        foreach (var status in chara.StatusList)
        {
            if (status.StatusId != statusId)
                continue;

            var fromSelf = this.IsStatusFromSelf(status.SourceId);
            var candidate = (
                Remaining: Math.Max(0f, status.RemainingTime),
                Param: status.Param,
                Count: 1,
                OwnCount: fromSelf ? 1 : 0,
                FromSelf: fromSelf);
            if (candidate.FromSelf || !preferOwnStatus)
                return candidate;

            fallback ??= candidate;
        }

        return fallback;
    }

    private bool IsStatusFromSelf(uint sourceId)
    {
        return ObjectTable.LocalPlayer is not null && sourceId == ObjectTable.LocalPlayer.EntityId;
    }

    private (string Name, uint IconId) GetStatusDefinition(uint statusId)
    {
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText();
                return (string.IsNullOrWhiteSpace(name) ? $"Status {statusId}" : name, row.Icon);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read status {statusId}.");
        }

        return ($"Status {statusId}", 0);
    }

    private void DrawAuraIcon(AuraState aura, float size, IconWindowConfig iconWindow)
    {
        var lookup = new GameIconLookup(aura.IconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        var pos = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var grayscaleTexture = !aura.Present ? this.GetGrayscaleIconTexture(aura.IconId) : null;
        ImGui.Image((grayscaleTexture ?? texture).Handle, new Vector2(size, size));
        var max = pos + new Vector2(size, size);

        if (!aura.Present)
        {
            if (grayscaleTexture is null)
                this.DrawUnavailableIconTint(draw, pos, max);

            return;
        }

        if (aura.Remaining > 0.05f)
            this.DrawTimerText(draw, pos, max, aura.Remaining);

        if (aura.Param > 1)
            this.DrawChargeText(draw, pos, max, aura.Param);

        if (iconWindow.Role == IconWindowRole.PartyBuffs && iconWindow.ShowPartyAuraCount && aura.Count > 0)
            this.DrawAuraCountText(draw, pos, max, aura.Count);

        if (aura.FromSelf)
            draw.AddRect(pos, max, ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 1f, 0.95f)), 3f, ImDrawFlags.None, 1.5f);
    }

    private void DrawAuraCountText(ImDrawListPtr draw, Vector2 min, Vector2 max, int count)
    {
        var text = count.ToString();
        using (this.keybindFont.Push())
        {
            var size = ImGui.CalcTextSize(text);
            var pos = new Vector2(max.X - size.X - 2f, min.Y + 1f);
            this.DrawOutlinedText(draw, pos, text, new Vector4(0.82f, 1f, 0.82f, 1f), new Vector4(0f, 0f, 0f, 0.95f), 1f);
        }
    }

    private void DrawStatusListIcon(uint iconId, float size)
    {
        var lookup = new GameIconLookup(iconId, false, true, null);
        var texture = TextureProvider.GetFromGameIcon(in lookup).GetWrapOrEmpty();
        ImGui.Image(texture.Handle, new Vector2(size, size));
    }
}
