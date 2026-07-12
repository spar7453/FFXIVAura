using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace FFXIVAura;

internal static class HotbarKeybindPolicy
{
    private static readonly string[] NormalHotbarAddonNames =
    [
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
    ];

    public static string? GetNormalHotbarAddonName(uint hotbarId)
        => hotbarId < NormalHotbarAddonNames.Length
            ? NormalHotbarAddonNames[hotbarId]
            : null;

    public static bool IsSupportedActionType(RaptureHotbarModule.HotbarSlotType type)
        => type is RaptureHotbarModule.HotbarSlotType.Action
            or RaptureHotbarModule.HotbarSlotType.GeneralAction;

    public static uint ResolveActionId(
        RaptureHotbarModule.HotbarSlotType type,
        uint commandId,
        Func<uint, uint> resolveGeneralActionId)
    {
        if (commandId == 0)
            return 0;

        return type switch
        {
            RaptureHotbarModule.HotbarSlotType.Action => commandId,
            RaptureHotbarModule.HotbarSlotType.GeneralAction => resolveGeneralActionId(commandId),
            _ => 0,
        };
    }
}
