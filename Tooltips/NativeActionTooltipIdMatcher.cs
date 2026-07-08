namespace FFXIVAura;

internal static class NativeActionTooltipIdMatcher
{
    public static bool MatchesAny(
        uint requestedActionId,
        uint actionId,
        uint originalId,
        Func<uint, uint> getAdjustedActionId)
    {
        return Matches(requestedActionId, actionId, getAdjustedActionId)
               || Matches(requestedActionId, originalId, getAdjustedActionId);
    }

    public static bool Matches(uint requestedActionId, uint tooltipActionId, Func<uint, uint> getAdjustedActionId)
    {
        if (requestedActionId == 0 || tooltipActionId == 0)
            return false;

        if (requestedActionId == tooltipActionId)
            return true;

        try
        {
            var adjustedRequestedActionId = getAdjustedActionId(requestedActionId);
            var adjustedTooltipActionId = getAdjustedActionId(tooltipActionId);
            return (adjustedRequestedActionId != 0 && adjustedRequestedActionId == tooltipActionId)
                   || (adjustedTooltipActionId != 0 && adjustedTooltipActionId == requestedActionId)
                   || (adjustedRequestedActionId != 0
                       && adjustedTooltipActionId != 0
                       && adjustedRequestedActionId == adjustedTooltipActionId);
        }
        catch
        {
            return false;
        }
    }
}
