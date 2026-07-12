using FFXIVAura;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class HotbarKeybindPolicyTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("HotbarKeybindPolicy maps normal hotbar addon names", MapsNormalHotbarAddonNames),
        ("HotbarKeybindPolicy preserves action id namespaces", PreservesActionIdNamespaces),
    ];

    private static void MapsNormalHotbarAddonNames()
    {
        var expected = new[]
        {
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
        };

        for (uint hotbarId = 0; hotbarId < expected.Length; hotbarId++)
            Equal(expected[hotbarId], HotbarKeybindPolicy.GetNormalHotbarAddonName(hotbarId));

        Equal<string?>(null, HotbarKeybindPolicy.GetNormalHotbarAddonName(10));
    }

    private static void PreservesActionIdNamespaces()
    {
        var resolverCalled = false;
        uint ResolveGeneralAction(uint generalActionId)
        {
            resolverCalled = true;
            return generalActionId == 17 ? 117u : 0u;
        }

        Equal(
            17u,
            HotbarKeybindPolicy.ResolveActionId(
                RaptureHotbarModule.HotbarSlotType.Action,
                17,
                ResolveGeneralAction));
        True(!resolverCalled, "normal actions should not invoke the general-action resolver");

        Equal(
            117u,
            HotbarKeybindPolicy.ResolveActionId(
                RaptureHotbarModule.HotbarSlotType.GeneralAction,
                17,
                ResolveGeneralAction));
        True(resolverCalled, "general actions should resolve to their underlying action row");

        resolverCalled = false;
        Equal(
            0u,
            HotbarKeybindPolicy.ResolveActionId(
                RaptureHotbarModule.HotbarSlotType.Macro,
                17,
                ResolveGeneralAction));
        True(!resolverCalled, "macro ids must not be registered as action ids");
    }
}
