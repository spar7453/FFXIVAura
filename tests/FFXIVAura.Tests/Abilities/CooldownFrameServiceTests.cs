using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class CooldownFrameServiceTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("CooldownFrameService caches one runtime read per frame", CachesOneRuntimeReadPerFrame),
        ("CooldownFrameService clears cached states between frames", ClearsCachedStatesBetweenFrames),
    ];

    private static void CachesOneRuntimeReadPerFrame()
    {
        var expected = new CooldownState(101, 202, 30, 12, 1, 2, false, true, false);
        var runtime = new StubGameActionRuntime(expected);
        var service = new CooldownFrameService(runtime);
        var ability = new AbilityDefinition { Id = "VPR-action", ActionId = 100 };
        var playerContext = CreatePlayerContext(effectiveLevel: 90);

        True(!service.TryGetCached(ability, out _), "new frame cache should start empty");
        Equal(expected, service.EvaluateAndCache(ability, playerContext));
        True(service.TryGetCached(
            new AbilityDefinition { Id = "vpr-ACTION", ActionId = 100 },
            out var cached), "equivalent ability key should reuse the cached state");

        Equal(expected, cached);
        Equal(1, service.CachedCount);
        Equal(1, runtime.CooldownReadCount);
        Equal(90u, runtime.LastEffectiveLevel);
    }

    private static void ClearsCachedStatesBetweenFrames()
    {
        var runtime = new StubGameActionRuntime(default);
        var service = new CooldownFrameService(runtime);
        var ability = new AbilityDefinition { Id = "action", ActionId = 100 };
        var playerContext = CreatePlayerContext(effectiveLevel: 100);

        service.EvaluateAndCache(ability, playerContext);
        service.BeginFrame();

        Equal(0, service.CachedCount);
        True(!service.TryGetCached(ability, out _), "begin frame should discard the previous frame state");
        service.EvaluateAndCache(ability, playerContext);
        Equal(2, runtime.CooldownReadCount);
    }

    private static PlayerFrameContext CreatePlayerContext(uint effectiveLevel)
        => new(
            IsLoggedIn: true,
            IsPlayerLoaded: true,
            Job: "VPR",
            Level: 100,
            EffectiveLevel: effectiveLevel,
            IsLevelSynced: effectiveLevel < 100,
            IsInCombat: false,
            BetweenAreas: false,
            BetweenAreas51: false,
            IsMounted: false,
            HasLocalPlayer: true,
            LocalPlayerEntityId: 1,
            HasTarget: false,
            TargetEntityId: 0,
            HasSoftTarget: false,
            SoftTargetEntityId: 0);

    private sealed class StubGameActionRuntime(CooldownState state) : IGameActionRuntime
    {
        public int CooldownReadCount { get; private set; }

        public uint LastEffectiveLevel { get; private set; }

        public CooldownState ReadCooldown(AbilityDefinition ability, in PlayerFrameContext playerContext)
        {
            this.CooldownReadCount++;
            this.LastEffectiveLevel = playerContext.EffectiveLevel;
            return state;
        }

        public uint GetAdjustedActionId(uint actionId)
            => actionId;

        public uint GetMaxCharges(uint actionId, uint effectiveLevel)
            => 1;
    }
}
