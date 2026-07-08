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
            "jump",
            92,
            new Vector2(10, 11),
            new Vector2(1, 2),
            new Vector2(43, 44),
            containsMouse: true,
            imguiHovered: true);
        diagnostics.RecordTooltipRequest(TooltipDiagnosticKind.Ability, "jump", 92);
        diagnostics.RecordNativeActionRequest(92);
        diagnostics.RecordNativeControl();
        diagnostics.RecordAddonMissingSkip();

        var snapshot = diagnostics.CreateSnapshot();

        Equal(1, snapshot.HoverHits);
        Equal(1, snapshot.AbilityRequests);
        Equal(1, snapshot.NativeActionRequests);
        Equal(1, snapshot.NativeControls);
        Equal(1, snapshot.AddonMissingSkips);
        Equal("Ability", snapshot.LastKind);
        Equal("jump", snapshot.LastId);
        Equal(92u, snapshot.LastActionId);
        Equal("addonMissing", snapshot.LastSkipReason);
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
        diagnostics.RecordNativeActionRequest(0);
        diagnostics.RecordZeroActionSkip();

        diagnostics.ResetIntervalCounters();
        var snapshot = diagnostics.CreateSnapshot();

        Equal(0, snapshot.PartyCooldownRequests);
        Equal(0, snapshot.NativeActionRequests);
        Equal(0, snapshot.DisabledSkips);
        Equal(0, snapshot.ZeroActionSkips);
        Equal("PartyCooldown", snapshot.LastKind);
        Equal("reprisal", snapshot.LastId);
        Equal(0u, snapshot.LastActionId);
        Equal("zeroActionId", snapshot.LastSkipReason);
    }
}
