using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PlayerFrameContextTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PlayerFrameContext requires login and loaded player", RequiresLoginAndLoadedPlayer),
        ("PlayerFrameContext combines both area transition flags", CombinesAreaTransitionFlags),
        ("PlayerFrameContext prefers the soft target", PrefersSoftTarget),
    ];

    private static void RequiresLoginAndLoadedPlayer()
    {
        True(Create(isLoggedIn: true, isPlayerLoaded: true).IsReady, "logged-in loaded player should be ready");
        True(!Create(isLoggedIn: false, isPlayerLoaded: true).IsReady, "logged-out player should not be ready");
        True(!Create(isLoggedIn: true, isPlayerLoaded: false).IsReady, "unloaded player should not be ready");
    }

    private static void CombinesAreaTransitionFlags()
    {
        True(Create(betweenAreas: true).IsBetweenAreas, "primary transition flag should mark loading");
        True(Create(betweenAreas51: true).IsBetweenAreas, "secondary transition flag should mark loading");
        True(!Create().IsBetweenAreas, "clear transition flags should not mark loading");
    }

    private static void PrefersSoftTarget()
    {
        var hardTarget = Create(hasTarget: true, targetEntityId: 10);
        True(hardTarget.HasEffectiveTarget, "hard target should be an effective target");
        Equal(10u, hardTarget.EffectiveTargetEntityId);

        var softTarget = Create(
            hasTarget: true,
            targetEntityId: 10,
            hasSoftTarget: true,
            softTargetEntityId: 20);
        True(softTarget.HasEffectiveTarget, "soft target should be an effective target");
        Equal(20u, softTarget.EffectiveTargetEntityId);

        var invalidSoftTarget = Create(
            hasTarget: true,
            targetEntityId: 10,
            hasSoftTarget: true,
            softTargetEntityId: 0);
        Equal(10u, invalidSoftTarget.EffectiveTargetEntityId);
    }

    private static PlayerFrameContext Create(
        bool isLoggedIn = true,
        bool isPlayerLoaded = true,
        bool betweenAreas = false,
        bool betweenAreas51 = false,
        bool hasTarget = false,
        uint targetEntityId = 0,
        bool hasSoftTarget = false,
        uint softTargetEntityId = 0)
        => new(
            IsLoggedIn: isLoggedIn,
            IsPlayerLoaded: isPlayerLoaded,
            Job: isPlayerLoaded ? "VPR" : string.Empty,
            Level: isPlayerLoaded ? 100u : 0,
            EffectiveLevel: isPlayerLoaded ? 100u : 0,
            IsLevelSynced: false,
            IsInCombat: false,
            BetweenAreas: betweenAreas,
            BetweenAreas51: betweenAreas51,
            IsMounted: false,
            HasLocalPlayer: isPlayerLoaded,
            LocalPlayerEntityId: isPlayerLoaded ? 1u : 0,
            HasTarget: hasTarget,
            TargetEntityId: targetEntityId,
            HasSoftTarget: hasSoftTarget,
            SoftTargetEntityId: softTargetEntityId);
}
