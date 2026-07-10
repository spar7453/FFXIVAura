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
        ("Overlay action tooltip formatting handles instant and timed actions", FormatsOverlayActionTooltipValues),
        ("Overlay tooltip positioning stays inside the display", PositionsOverlayTooltipInsideDisplay),
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
        diagnostics.RecordOverlayActionRender(92);

        var snapshot = diagnostics.CreateSnapshot();

        Equal(1, snapshot.HoverHits);
        Equal(1, snapshot.AbilityRequests);
        Equal(1, snapshot.OverlayActionRenders);
        Equal("Ability", snapshot.LastKind);
        Equal("jump", snapshot.LastId);
        Equal("win1", snapshot.LastWindowId);
        Equal("jump", snapshot.LastIconId);
        Equal(92u, snapshot.LastActionId);
        Equal(12, snapshot.LastDrawnIconCount);
        True(snapshot.LastHitboxExpanded, "hitbox expansion should be preserved");
        Equal(string.Empty, snapshot.LastSkipReason);
        True(snapshot.HasLastGeometry, "hover geometry should be captured");
        Vector(new Vector2(10, 11), snapshot.LastMouse);
        Vector(new Vector2(1, 2), snapshot.LastRectMin);
        Vector(new Vector2(43, 44), snapshot.LastRectMax);
        True(snapshot.LastContainsMouse, "mouse should be marked inside the rect");
        True(snapshot.LastImGuiHovered, "imgui hover should be preserved");
    }

    private static void ResetsCountersButKeepsLastEvent()
    {
        var diagnostics = new TooltipDiagnostics();
        diagnostics.RecordDisabledSkip(TooltipDiagnosticKind.PartyCooldown, "reprisal", 7535);
        diagnostics.RecordOverlayActionRender(0);
        diagnostics.RecordZeroActionSkip();

        diagnostics.ResetIntervalCounters();
        var snapshot = diagnostics.CreateSnapshot();

        Equal(0, snapshot.PartyCooldownRequests);
        Equal(0, snapshot.OverlayActionRenders);
        Equal(0, snapshot.DisabledSkips);
        Equal(0, snapshot.ZeroActionSkips);
        Equal("PartyCooldown", snapshot.LastKind);
        Equal("reprisal", snapshot.LastId);
        Equal(0u, snapshot.LastActionId);
        Equal("zeroActionId", snapshot.LastSkipReason);
    }

    private static void FormatsOverlayActionTooltipValues()
    {
        Equal("즉시 발동", OverlayActionTooltipFormatting.FormatCastTime(0));
        Equal("2.50초", OverlayActionTooltipFormatting.FormatCastTime(25));
        Equal("-", OverlayActionTooltipFormatting.FormatRecastTime(0));
        Equal("90.00초", OverlayActionTooltipFormatting.FormatRecastTime(900));
        Equal("0m", OverlayActionTooltipFormatting.FormatDistance(-1));
        Equal("25m", OverlayActionTooltipFormatting.FormatDistance(25));
        True(OverlayActionTooltipFormatting.ShouldShowMaxCharges(3, "즉시 이동합니다."), "missing charge text should use the structured fallback");
        True(!OverlayActionTooltipFormatting.ShouldShowMaxCharges(3, "최대 누적수: 3"), "evaluated descriptions should not duplicate charge text");
        True(!OverlayActionTooltipFormatting.ShouldShowMaxCharges(1, string.Empty), "single-charge actions should not show a charge row");
    }

    private static void PositionsOverlayTooltipInsideDisplay()
    {
        var position = OverlayTooltipPositioning.GetPositionAtMouse(
            new Vector2(95, 95),
            new Vector2(100, 100),
            new Vector2(20, 20));
        Vector(new Vector2(57, 57), position);

        var oversized = OverlayTooltipPositioning.GetPositionAtMouse(
            new Vector2(95, 95),
            new Vector2(100, 100),
            new Vector2(200, 200));
        Vector(Vector2.Zero, oversized);
    }
}
