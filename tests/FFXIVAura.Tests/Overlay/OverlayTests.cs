using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class OverlayTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("OverlayLayout centers a single row", OverlayLayoutCentersSingleRow),
        ("OverlayLayout right-aligns a single row", OverlayLayoutRightAlignsSingleRow),
        ("OverlayLayout compacts aura rows from top", OverlayLayoutCompactsAuraRowsFromTop),
        ("OverlayLayout clamps invalid positions", OverlayLayoutClampsInvalidPositions),
        ("OverlayLayout finds a free slot", OverlayLayoutFindsFreeSlot),
        ("OverlayFrameModel exposes display and layout items", OverlayFrameModelExposesDisplayAndLayoutItems),
        ("OverlayTooltipResolver uses latest candidate", OverlayTooltipResolverUsesLatestCandidate),
        ("OverlayControlGeometry splits narrow controls", OverlayControlGeometrySplitsNarrowControls),
        ("OverlayControlGeometry clamps floating windows", OverlayControlGeometryClampsFloatingWindows),
    ];

    private static void OverlayLayoutCentersSingleRow()
    {
        var position = OverlayLayout.GetAutoPosition(IconAlignment.Center, 0, 3, new Vector2(200, 100), 40, 5);
        Vector(new Vector2(35, 30), position);
    }

    private static void OverlayLayoutRightAlignsSingleRow()
    {
        var position = OverlayLayout.GetAutoPosition(IconAlignment.Right, 0, 3, new Vector2(200, 100), 40, 5);
        Vector(new Vector2(70, 30), position);
    }

    private static void OverlayLayoutCompactsAuraRowsFromTop()
    {
        var position = OverlayLayout.GetCompactPosition(IconAlignment.Center, 0, 3, new Vector2(200, 100), 40, 5);
        Vector(new Vector2(35, 0), position);
    }

    private static void OverlayLayoutClampsInvalidPositions()
    {
        var position = OverlayLayout.ClampIconPosition(new Vector2(float.NaN, 999), new Vector2(100, 80), 42);
        Vector(new Vector2(0, 38), position);
    }

    private static void OverlayLayoutFindsFreeSlot()
    {
        var occupied = new List<Vector2> { new(10, 10) };
        var position = OverlayLayout.FindFreeAutoPosition(IconAlignment.Center, 0, 4, occupied, new Vector2(100, 100), 40, 0);
        Vector(new Vector2(50, 10), position);
    }

    private static void OverlayFrameModelExposesDisplayAndLayoutItems()
    {
        var window = new IconWindowConfig { Id = "win1", Width = 300, Height = 120 };
        var displayAbility = new AbilityDefinition { Id = "display", ActionId = 1 };
        var layoutAbility = new AbilityDefinition { Id = "layout", ActionId = 2 };
        var display = new OverlayItemSet([displayAbility], Array.Empty<AuraState>());
        var layout = new OverlayItemSet([displayAbility, layoutAbility], Array.Empty<AuraState>());
        var frame = new OverlayFrameModel(window, "DRG", 100, new Vector2(300, 120), display, layout);

        True(frame.HasDisplayItems, "display items should mark the frame visible");
        Equal(1, frame.DisplayAbilities.Count);
        Equal(2, frame.LayoutAbilities.Count);
        Vector(new Vector2(300, 120), frame.AreaSize);
    }

    private static void OverlayTooltipResolverUsesLatestCandidate()
    {
        var resolver = new OverlayTooltipResolver();
        var ability = new AbilityDefinition { Id = "first", ActionId = 1 };
        var aura = new AuraState(42, "second", 100, 12, 0, 1, 1, true, true);

        resolver.Register(OverlayTooltipCandidate.ForAbility(ability));
        resolver.Register(OverlayTooltipCandidate.ForAura(aura));

        True(resolver.TryConsume(out var candidate), "resolver should consume a candidate");
        Equal(OverlayTooltipCandidateKind.Aura, candidate.Kind);
        Equal(42u, candidate.Aura.StatusId);
        True(!resolver.TryConsume(out _), "resolver should clear after consume");
    }

    private static void OverlayControlGeometrySplitsNarrowControls()
    {
        var layout = OverlayControlGeometry.CreateLayout(
            new Vector2(20, 10),
            new Vector2(100, 80),
            new Vector2(500, 300),
            controlHeight: 20,
            roleWidth: 80,
            conditionWidth: 80,
            nameWidth: 80,
            alignmentWidth: 80);

        True(layout.SplitTopControls, "top controls should split when the overlay is narrow");
        True(layout.TopControlsBelow, "top controls should move below when there is no room above");
        True(layout.SplitBottomControls, "bottom controls should split when the overlay is narrow");
        True(!layout.BottomControlsAbove, "bottom controls should stay below when there is enough room");
        Equal(2, layout.BottomBelowRowOffset);
        Near(94, OverlayControlGeometry.GetTopControlY(new Vector2(20, 10), new Vector2(100, 80), 20, 0, layout));
        Near(142, OverlayControlGeometry.GetBottomControlY(new Vector2(20, 10), new Vector2(100, 80), 20, 0, layout));
    }

    private static void OverlayControlGeometryClampsFloatingWindows()
    {
        var clamped = OverlayControlGeometry.ClampWindowPositionToDisplay(
            new Vector2(490, 290),
            new Vector2(100, 50),
            new Vector2(8, 8),
            new Vector2(500, 300),
            8);
        Vector(new Vector2(392, 242), clamped);

        var fallback = OverlayControlGeometry.ClampWindowPositionToDisplay(
            new Vector2(float.NaN, 1),
            new Vector2(100, 50),
            new Vector2(8, 8),
            Vector2.Zero,
            8);
        Vector(new Vector2(8, 8), fallback);
    }
}
