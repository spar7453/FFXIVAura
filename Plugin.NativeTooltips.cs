using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static void ShowNativeActionTooltip(uint actionId)
    {
        if (actionId == 0)
            return;

        var agent = AgentActionDetail.Instance();
        if (agent is null)
            return;

        agent->HandleActionHover(DetailKind.Action, actionId, flag: 0, isLovmActionDetail: false, a5: 0, a6: 0);
    }
}
