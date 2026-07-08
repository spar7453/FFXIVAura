using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class NativeTooltipTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("NativeActionTooltipIdMatcher matches adjusted ids", NativeActionTooltipIdMatcherMatchesAdjustedIds),
        ("NativeActionTooltipIdMatcher handles invalid ids", NativeActionTooltipIdMatcherHandlesInvalidIds),
        ("NativeActionTooltipState controls only active requests", NativeActionTooltipStateControlsOnlyActiveRequests),
        ("NativeActionTooltipState captures sound state once", NativeActionTooltipStateCapturesSoundStateOnce),
        ("NativeActionTooltipController clamps tooltip position", NativeActionTooltipControllerClampsTooltipPosition),
        ("NativeActionTooltipController suppresses sound selectively", NativeActionTooltipControllerSuppressesSoundSelectively),
    ];

    private static void NativeActionTooltipIdMatcherMatchesAdjustedIds()
    {
        var adjustedIds = new Dictionary<uint, uint>
        {
            [10] = 100,
            [20] = 200,
            [30] = 300,
            [40] = 300,
        };

        uint Adjust(uint id) => adjustedIds.GetValueOrDefault(id);

        True(NativeActionTooltipIdMatcher.Matches(10, 10, Adjust), "direct action id should match");
        True(NativeActionTooltipIdMatcher.Matches(10, 100, Adjust), "adjusted requested id should match tooltip id");
        True(NativeActionTooltipIdMatcher.Matches(200, 20, Adjust), "adjusted tooltip id should match requested id");
        True(NativeActionTooltipIdMatcher.Matches(30, 40, Adjust), "both ids adjusted to same action should match");
        True(NativeActionTooltipIdMatcher.MatchesAny(30, 0, 40, Adjust), "original id should be considered");
    }

    private static void NativeActionTooltipIdMatcherHandlesInvalidIds()
    {
        uint ThrowingAdjust(uint _) => throw new InvalidOperationException("boom");

        True(!NativeActionTooltipIdMatcher.Matches(0, 10, ThrowingAdjust), "zero requested id should not match");
        True(!NativeActionTooltipIdMatcher.Matches(10, 0, ThrowingAdjust), "zero tooltip id should not match");
        True(!NativeActionTooltipIdMatcher.Matches(10, 20, ThrowingAdjust), "adjust resolver failures should not match");
    }

    private static void NativeActionTooltipStateControlsOnlyActiveRequests()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new NativeActionTooltipState();

        True(!state.ShouldControl(now, matchesRequestedAction: true), "inactive state should not control tooltips");

        state.BeginHover(42, now, TimeSpan.FromMilliseconds(500));
        Equal(42u, state.ActionId);
        True(state.Visible, "tooltip should become visible when hover begins");
        True(state.CanControlWithoutActionMatch(now), "visible tooltip should keep lifecycle control active");
        True(state.ShouldControl(now, matchesRequestedAction: false), "hover in progress should control the native tooltip");
        True(state.ShouldHideNativeTooltip(matchesRequestedAction: false), "hover in progress should allow native hide");

        state.EndHover();
        True(state.ShouldControl(now.AddMilliseconds(100), matchesRequestedAction: true), "matching tooltip should stay controlled");
        True(!state.ShouldControl(now.AddMilliseconds(100), matchesRequestedAction: false), "non-matching tooltip should not stay controlled");
        True(!state.ShouldHideNativeTooltip(matchesRequestedAction: false), "non-matching tooltip should not be hidden");

        state.ClearTooltipRequest();
        Equal(0u, state.ActionId);
        True(!state.Visible, "clear should reset visibility");
        True(!state.CanControlWithoutActionMatch(now.AddSeconds(1)), "cleared expired tooltip should skip lifecycle work");
    }

    private static void NativeActionTooltipStateCapturesSoundStateOnce()
    {
        var state = new NativeActionTooltipState();

        state.CaptureSoundState(12, disableShowHideSoundEffects: false);
        state.CaptureSoundState(99, disableShowHideSoundEffects: true);

        True(state.TryGetCapturedSoundState(out var showSoundEffectId, out var disableShowHideSoundEffects), "sound state should be captured");
        Equal((short)12, showSoundEffectId);
        True(!disableShowHideSoundEffects, "first captured sound state should be preserved");

        state.ClearSoundState();
        True(!state.TryGetCapturedSoundState(out _, out _), "cleared sound state should not restore");
    }

    private static void NativeActionTooltipControllerClampsTooltipPosition()
    {
        var position = NativeActionTooltipController.GetPositionAtMouse(
            new Vector2(490, 290),
            new Vector2(500, 300),
            new Vector2(80, 40));

        Vector(new Vector2(420, 260), position);

        var unclamped = NativeActionTooltipController.GetPositionAtMouse(
            new Vector2(10, 20),
            Vector2.Zero,
            new Vector2(80, 40));

        Vector(new Vector2(28, 38), unclamped);
    }

    private static void NativeActionTooltipControllerSuppressesSoundSelectively()
    {
        True(NativeActionTooltipController.ShouldSuppressSound(isShowEvent: true, isVisibleLifecycleEvent: false, addonVisible: false), "show events should suppress before visibility is set");
        True(NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: true, addonVisible: true), "visible update/draw events should suppress");
        True(!NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: true, addonVisible: false), "invisible update/draw events should not capture sound state");
        True(!NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: false, addonVisible: true), "setup events should not capture sound state");
    }
}
