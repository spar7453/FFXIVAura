namespace FFXIVAura;

public sealed partial class Plugin
{
    private IEnumerable<AuraState> GetDisplayAuras(IconWindowConfig iconWindow)
    {
        if (iconWindow.TrackedStatusIds.Count == 0)
            yield break;

        foreach (var group in this.auraSearchService.GetTrackedGroups(iconWindow))
        {
            var aura = this.GetAuraState(iconWindow, group);
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
            IconDisplayCondition.InCombat => this.playerFrameContext.IsInCombat,
            IconDisplayCondition.OutOfCombat => !this.playerFrameContext.IsInCombat,
            IconDisplayCondition.CoolingOnly => aura.Present,
            IconDisplayCondition.ReadyOnly => !aura.Present,
            _ => true,
        };
    }

    private AuraState GetAuraState(IconWindowConfig iconWindow, AuraStatusGroup group)
    {
        var builder = new AuraStatusGroupStateBuilder(group);
        foreach (var statusId in group.MemberStatusIds)
        {
            var active = this.FindAuraStatus(iconWindow, statusId);
            if (active is null)
                continue;

            var definition = this.auraCatalog.GetDefinition(statusId);
            builder.Add(new AuraStatusGroupActiveState(
                statusId,
                definition.IconId,
                active.Value.Remaining,
                active.Value.Param,
                active.Value.Count,
                active.Value.OwnCount,
                active.Value.FromSelf));
        }

        var state = builder.Build();
        if (iconWindow.Role != IconWindowRole.PartyBuffs
            || group.IsExact
            || !group.Key.IsValid
            || !this.auraFrameService.TryGetPartyGroup(
                group.Key,
                iconWindow.PartyAurasOwnOnly,
                out var groupCount))
        {
            return state;
        }

        return state with
        {
            Count = groupCount.Count,
            OwnCount = groupCount.OwnCount,
        };
    }

    private (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf)? FindAuraStatus(
        IconWindowConfig iconWindow,
        uint statusId)
        => iconWindow.Role switch
        {
            IconWindowRole.TargetDebuffs => this.auraFrameService.FindTarget(statusId),
            IconWindowRole.PartyBuffs => this.auraFrameService.FindParty(statusId, iconWindow.PartyAurasOwnOnly),
            _ => this.auraFrameService.FindPlayer(statusId),
        };

}
