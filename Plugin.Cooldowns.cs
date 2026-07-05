namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private CooldownState GetCooldown(AbilityDefinition ability)
    {
        var key = RuntimeScopeKeys.CooldownFrame(ability.Id, ability.ActionId);
        if (this.cooldownFrameCache.TryGetValue(key, out var cached))
            return cached;

        var state = this.ComputeCooldown(ability);
        this.cooldownFrameCache[key] = state;
        return state;
    }

    private CooldownState ComputeCooldown(AbilityDefinition ability)
    {
        var baseActionId = ability.ActionId;
        var actionId = baseActionId;
        var manager = ActionManager.Instance();
        var adjusted = manager->GetAdjustedActionId(actionId);
        if (adjusted != 0)
            actionId = adjusted;

        var total = 0f;
        var elapsed = 0f;
        var group = manager->GetRecastGroup((int)ActionType.Action, actionId);
        if (group >= 0)
        {
            var detail = manager->GetRecastGroupDetail(group);
            if (detail is not null && detail->IsActive)
            {
                total = detail->Total;
                elapsed = detail->Elapsed;
            }
        }

        if (total <= 0f)
        {
            var totalMs = ActionManager.GetAdjustedRecastTime(ActionType.Action, actionId, true);
            total = Math.Max(ability.Cooldown, totalMs / 1000f);
        }

        var currentLevel = Math.Max(1u, this.GetCurrentEffectiveLevel());
        var adjustedMaxCharges = (uint)ActionManager.GetMaxCharges(actionId, currentLevel);
        var maxCharges = adjustedMaxCharges > 0 ? adjustedMaxCharges : Math.Max((uint)ability.Charges, 1u);
        var charges = Math.Min(manager->GetCurrentCharges(actionId), maxCharges);
        var (displayTotal, remaining) = CooldownMath.GetTiming(total, elapsed, charges, maxCharges, ability.Cooldown);
        var highlighted = manager->IsActionHighlighted(ActionType.Action, actionId);
        var iconId = this.GetActionIconId(actionId);
        var conditionUnavailable = this.IsGlobalActionUnavailable()
                                   || (!highlighted
                                    && (this.IsActionResourceUnavailable(manager, actionId)
                                        || this.IsActionStatusUnavailable(manager, actionId)));

        return new CooldownState(actionId, iconId, displayTotal, Math.Clamp(remaining, 0f, displayTotal), charges, maxCharges, highlighted, actionId != baseActionId, conditionUnavailable);
    }

    private bool IsGlobalActionUnavailable()
    {
        return Condition[ConditionFlag.Mounted];
    }

    private bool IsActionResourceUnavailable(ActionManager* manager, uint actionId)
    {
        try
        {
            return manager->CheckActionResources(ActionType.Action, actionId, null) != 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action resources for {actionId}.");
            return false;
        }
    }

    private bool IsActionStatusUnavailable(ActionManager* manager, uint actionId)
    {
        try
        {
            if (this.IsTargetOnlyUnavailable(manager, actionId))
                return true;

            var targetId = TargetManager.Target?.EntityId ?? 0xE0000000;
            return manager->GetActionStatus(ActionType.Action, actionId, targetId, false, true, null) != 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action status for {actionId}.");
            return false;
        }
    }

    private bool IsTargetOnlyUnavailable(ActionManager* manager, uint actionId)
    {
        var action = this.GetActionRow(actionId);
        if (action is null)
            return false;

        var requiresTarget = action.Value.CanTargetHostile
                             && !action.Value.CanTargetSelf
                             && !action.Value.CanTargetParty
                             && !action.Value.CanTargetAlly;
        if (!requiresTarget)
            return false;

        if (TargetManager.Target is null)
            return true;

        return !manager->IsActionTargetInRange(ActionType.Action, actionId);
    }

    private uint GetActionIconId(uint actionId)
    {
        try
        {
            var row = this.GetActionRow(actionId);
            if (row is not null)
                return row.Value.Icon;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action icon for {actionId}.");
        }

        return 0;
    }

    private GameAction? GetActionRow(uint actionId)
    {
        var sheet = DataManager.GetExcelSheet<GameAction>();
        if (sheet is null)
            return null;

        try
        {
            var row = sheet.GetRow(actionId);
            return row.RowId == 0 ? null : row;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Failed to read action row for {actionId}.");
            return null;
        }
    }

}
