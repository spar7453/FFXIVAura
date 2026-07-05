namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private IEnumerable<AuraState> GetDisplayAuras(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            yield break;

        foreach (var statusId in iconWindow.TrackedStatusIds.Distinct())
        {
            var aura = this.GetAuraState(iconWindow, statusId);
            if (!this.ShouldDisplayAura(aura, iconWindow))
                continue;

            if (!aura.Present && !ShouldShowMissingAura(iconWindow))
                continue;

            yield return aura;
        }
    }

    private static bool ShouldShowMissingAura(IconWindowConfig iconWindow)
        => iconWindow.ShowMissingAuras || iconWindow.DisplayCondition == IconDisplayCondition.ReadyOnly;

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
        (float Remaining, ushort Param)? best = null;
        var count = 0;
        var ownCount = 0;
        var anyFromSelf = false;
        for (var i = 0; i < PartyList.Length; i++)
        {
            var member = PartyList[i];
            if (member is null)
                continue;

            (float Remaining, ushort Param)? memberBest = null;
            var memberFromSelf = false;
            foreach (var status in member.Statuses)
            {
                if (status.StatusId != statusId)
                    continue;

                var fromSelf = this.IsStatusFromSelf(status.SourceId);
                if (ownOnly && !fromSelf)
                    continue;

                memberFromSelf |= fromSelf;
                var remaining = Math.Max(0f, status.RemainingTime);
                if (memberBest is null || remaining > memberBest.Value.Remaining)
                    memberBest = (remaining, status.Param);
            }

            if (memberBest is null)
                continue;

            count++;
            if (memberFromSelf)
                ownCount++;

            anyFromSelf |= memberFromSelf;
            if (best is null || memberBest.Value.Remaining > best.Value.Remaining)
                best = memberBest;
        }

        return best is null
            ? null
            : (best.Value.Remaining, best.Value.Param, count, ownCount, anyFromSelf);
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindStatus(IBattleChara chara, uint statusId, bool preferOwnStatus)
    {
        (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? best = null;
        (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? bestOwn = null;
        var count = 0;
        var ownCount = 0;
        foreach (var status in chara.StatusList)
        {
            if (status.StatusId != statusId)
                continue;

            var fromSelf = this.IsStatusFromSelf(status.SourceId);
            count++;
            if (fromSelf)
                ownCount++;

            var candidate = (
                Remaining: Math.Max(0f, status.RemainingTime),
                Param: status.Param,
                Count: 1,
                OwnCount: fromSelf ? 1 : 0,
                FromSelf: fromSelf);
            if (best is null || candidate.Remaining > best.Value.Remaining)
                best = candidate;

            if (fromSelf && (bestOwn is null || candidate.Remaining > bestOwn.Value.Remaining))
                bestOwn = candidate;
        }

        var selected = preferOwnStatus ? bestOwn ?? best : best;
        if (selected is null)
            return null;

        var value = selected.Value;
        return (value.Remaining, value.Param, count, ownCount, value.FromSelf);
    }

    private bool IsStatusFromSelf(uint sourceId)
    {
        return ObjectTable.LocalPlayer is not null && sourceId == ObjectTable.LocalPlayer.EntityId;
    }

    private (string Name, uint IconId) GetStatusDefinition(uint statusId)
    {
        if (this.statusDefinitionCache.TryGetValue(statusId, out var cached))
            return cached;

        var definition = ($"Status {statusId}", 0u);
        var shouldCache = false;
        try
        {
            var sheet = DataManager.GetExcelSheet<GameStatus>();
            if (sheet is not null)
            {
                var row = sheet.GetRow(statusId);
                var name = row.Name.ExtractText();
                definition = (string.IsNullOrWhiteSpace(name) ? $"Status {statusId}" : name, row.Icon);
                shouldCache = true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read status {statusId}.");
        }

        if (shouldCache)
            this.statusDefinitionCache[statusId] = definition;

        return definition;
    }
}
