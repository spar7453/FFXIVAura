using FFXIVClientStructs.FFXIV.Client.Game;

namespace FFXIVAura;

internal interface IGameActionRuntime
{
    CooldownState ReadCooldown(AbilityDefinition ability, in PlayerFrameContext playerContext);

    uint GetAdjustedActionId(uint actionId);

    uint GetMaxCharges(uint actionId, uint effectiveLevel);
}

internal sealed unsafe class DalamudGameActionRuntime : IGameActionRuntime
{
    private const uint InvalidTargetId = 0xE0000000;

    private readonly IGameActionRepository actionRepository;
    private readonly IPluginLog log;

    public DalamudGameActionRuntime(
        IGameActionRepository actionRepository,
        IPluginLog log)
    {
        this.actionRepository = actionRepository;
        this.log = log;
    }

    public CooldownState ReadCooldown(AbilityDefinition ability, in PlayerFrameContext playerContext)
    {
        var baseActionId = ability.ActionId;
        var actionId = baseActionId;
        var manager = ActionManager.Instance();
        if (manager is null)
            return this.CreateReadyFallbackCooldown(ability, baseActionId);

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

        var currentLevel = Math.Max(1u, playerContext.EffectiveLevel);
        var adjustedMaxCharges = this.GetMaxCharges(actionId, currentLevel);
        var maxCharges = adjustedMaxCharges > 0 ? adjustedMaxCharges : Math.Max((uint)ability.Charges, 1u);
        var charges = Math.Min(manager->GetCurrentCharges(actionId), maxCharges);
        var (displayTotal, remaining) = CooldownMath.GetTiming(
            total,
            elapsed,
            charges,
            maxCharges,
            ability.Cooldown);
        var highlighted = manager->IsActionHighlighted(ActionType.Action, actionId);
        var iconId = this.GetActionIconId(actionId);
        var conditionUnavailable = playerContext.IsMounted
                                   || (!highlighted
                                       && (this.IsActionResourceUnavailable(manager, actionId)
                                           || this.IsActionStatusUnavailable(manager, actionId, playerContext)));

        return new CooldownState(
            actionId,
            iconId,
            displayTotal,
            Math.Clamp(remaining, 0f, displayTotal),
            charges,
            maxCharges,
            highlighted,
            actionId != baseActionId,
            conditionUnavailable);
    }

    public uint GetAdjustedActionId(uint actionId)
    {
        var manager = ActionManager.Instance();
        return manager is null ? actionId : manager->GetAdjustedActionId(actionId);
    }

    // ActionManager can be briefly unavailable during early login / logout / zone
    // transitions. Return a safe "ready, no cooldown" state instead of dereferencing
    // a null pointer on the UI thread.
    private CooldownState CreateReadyFallbackCooldown(AbilityDefinition ability, uint baseActionId)
    {
        var fallbackCharges = Math.Max((uint)ability.Charges, 1u);
        return new CooldownState(
            baseActionId,
            this.GetActionIconId(baseActionId),
            0f,
            0f,
            fallbackCharges,
            fallbackCharges,
            false,
            false,
            false);
    }

    public uint GetMaxCharges(uint actionId, uint effectiveLevel)
        => (uint)ActionManager.GetMaxCharges(actionId, effectiveLevel);

    private bool IsActionResourceUnavailable(ActionManager* manager, uint actionId)
    {
        try
        {
            return manager->CheckActionResources(ActionType.Action, actionId, null) != 0;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read action resources for {actionId}.");
            return false;
        }
    }

    private bool IsActionStatusUnavailable(
        ActionManager* manager,
        uint actionId,
        in PlayerFrameContext playerContext)
    {
        try
        {
            if (this.IsTargetOnlyUnavailable(manager, actionId, playerContext.HasEffectiveTarget))
                return true;

            var targetId = playerContext.HasEffectiveTarget
                ? playerContext.EffectiveTargetEntityId
                : InvalidTargetId;
            return manager->GetActionStatus(ActionType.Action, actionId, targetId, false, true, null) != 0;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read action status for {actionId}.");
            return false;
        }
    }

    private bool IsTargetOnlyUnavailable(ActionManager* manager, uint actionId, bool hasTarget)
    {
        var action = this.actionRepository.GetAction(actionId);
        if (action is null)
            return false;

        var requiresTarget = action.Value.CanTargetHostile
                             && !action.Value.CanTargetSelf
                             && !action.Value.CanTargetParty
                             && !action.Value.CanTargetAlly;
        if (!requiresTarget)
            return false;

        return !hasTarget || !manager->IsActionTargetInRange(ActionType.Action, actionId);
    }

    private uint GetActionIconId(uint actionId)
    {
        try
        {
            return this.actionRepository.GetAction(actionId)?.Icon ?? 0;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, $"Failed to read action icon for {actionId}.");
            return 0;
        }
    }
}
