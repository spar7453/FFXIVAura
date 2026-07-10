using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class TooltipDiagnosticsTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("TooltipDiagnostics records interval counters and last geometry", RecordsIntervalCountersAndLastGeometry),
        ("TooltipDiagnostics resets counters but keeps last event", ResetsCountersButKeepsLastEvent),
    ];

    private static void RecordsIntervalCountersAndLastGeometry()
    {
        var diagnostics = new TooltipDiagnostics();
        diagnostics.RecordHover(
            TooltipDiagnosticKind.Ability,
            "win1",
            "jump",
            92,
            drawnIconCount: 12,
            hitboxExpanded: true,
            new Vector2(10, 11),
            new Vector2(1, 2),
            new Vector2(43, 44),
            containsMouse: true,
            imguiHovered: true);
        diagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Ability, "jump", 92);
        diagnostics.RecordNativeActionRequest(92);
        diagnostics.RecordNativeAgentIds(92, 91);
        diagnostics.RecordNativeControl(new NativeActionTooltipControlResult(
            AddonVisible: true,
            AddonSize: new Vector2(120, 64),
            MousePosition: new Vector2(20, 30),
            Position: new Vector2(38, 48)));
        diagnostics.RecordNativeHoverDispatch();
        diagnostics.RecordNativeForcedShow();
        diagnostics.RecordAddonMissingSkip();

        var snapshot = diagnostics.CreateSnapshot();

        Equal(1, snapshot.HoverHits);
        Equal(1, snapshot.AbilityRequests);
        Equal(1, snapshot.NativeActionRequests);
        Equal(1, snapshot.NativeControls);
        Equal(1, snapshot.NativeHoverDispatches);
        Equal(1, snapshot.NativeForcedShows);
        Equal(1, snapshot.AddonMissingSkips);
        Equal("Ability", snapshot.LastKind);
        Equal("jump", snapshot.LastId);
        Equal("win1", snapshot.LastWindowId);
        Equal("jump", snapshot.LastIconId);
        Equal(92u, snapshot.LastActionId);
        Equal(12, snapshot.LastDrawnIconCount);
        True(snapshot.LastHitboxExpanded, "hitbox expansion should be preserved");
        Equal("addonMissing", snapshot.LastSkipReason);
        True(snapshot.HasLastGeometry, "hover geometry should be captured");
        Vector(new Vector2(10, 11), snapshot.LastMouse);
        Vector(new Vector2(1, 2), snapshot.LastRectMin);
        Vector(new Vector2(43, 44), snapshot.LastRectMax);
        True(snapshot.LastContainsMouse, "mouse should be marked inside the rect");
        True(snapshot.LastImGuiHovered, "imgui hover should be preserved");
        Equal(92u, snapshot.LastAgentActionId);
        Equal(91u, snapshot.LastAgentOriginalId);
        True(snapshot.HasLastNativeAddon, "native addon state should be captured");
        True(snapshot.LastAddonVisible, "addon visibility should be preserved");
        Vector(new Vector2(120, 64), snapshot.LastAddonSize);
        Vector(new Vector2(20, 30), snapshot.LastControlMouse);
        Vector(new Vector2(38, 48), snapshot.LastControlPosition);
    }

    private static void ResetsCountersButKeepsLastEvent()
    {
        var diagnostics = new TooltipDiagnostics();
        diagnostics.RecordDisabledSkip(TooltipDiagnosticKind.PartyCooldown, "reprisal", 7535);
        diagnostics.RecordNativeActionRequest(0);
        diagnostics.RecordZeroActionSkip();
        diagnostics.RecordNativeHoverDispatch();
        diagnostics.RecordNativeForcedShow();

        diagnostics.ResetIntervalCounters();
        var snapshot = diagnostics.CreateSnapshot();

        Equal(0, snapshot.PartyCooldownRequests);
        Equal(0, snapshot.NativeActionRequests);
        Equal(0, snapshot.DisabledSkips);
        Equal(0, snapshot.ZeroActionSkips);
        Equal(0, snapshot.NativeHoverDispatches);
        Equal(0, snapshot.NativeForcedShows);
        Equal("PartyCooldown", snapshot.LastKind);
        Equal("reprisal", snapshot.LastId);
        Equal(0u, snapshot.LastActionId);
        Equal("zeroActionId", snapshot.LastSkipReason);
    }
}
