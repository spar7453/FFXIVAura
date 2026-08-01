using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class OverlayDragSessionTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("OverlayDragSession applies mouse delta from the original anchor", AppliesMouseDeltaFromOriginalAnchor),
        ("OverlayDragSession resets its anchor for another icon", ResetsAnchorForAnotherIcon),
        ("OverlayDragSession compares scoped ids case-insensitively", ComparesScopedIdsCaseInsensitively),
        ("OverlayDragSession clears drag state on release", ClearsDragStateOnRelease),
    ];

    private static void AppliesMouseDeltaFromOriginalAnchor()
    {
        var session = new OverlayDragSession();

        Vector(
            new Vector2(12f, 24f),
            session.UpdatePosition(
                "window:DRG:jump",
                new Vector2(100f, 200f),
                new Vector2(12f, 24f)));
        Vector(
            new Vector2(27f, 17f),
            session.UpdatePosition(
                "window:DRG:jump",
                new Vector2(115f, 193f),
                new Vector2(500f, 500f)));
    }

    private static void ResetsAnchorForAnotherIcon()
    {
        var session = new OverlayDragSession();
        _ = session.UpdatePosition(
            "window:DRG:jump",
            new Vector2(100f, 100f),
            new Vector2(10f, 10f));

        var switched = session.UpdatePosition(
            "window:status-42",
            new Vector2(300f, 250f),
            new Vector2(40f, 50f));

        Vector(new Vector2(40f, 50f), switched);
        Equal("window:status-42", session.DragId);
        True(session.IsDragging("window:status-42"), "the new icon should own the drag session");
    }

    private static void ComparesScopedIdsCaseInsensitively()
    {
        var session = new OverlayDragSession();
        _ = session.UpdatePosition(
            "Window:DRG:Jump",
            new Vector2(10f, 10f),
            new Vector2(20f, 30f));

        var moved = session.UpdatePosition(
            "window:drg:jump",
            new Vector2(14f, 16f),
            new Vector2(999f, 999f));

        True(session.IsDragging("WINDOW:DRG:JUMP"), "drag identity should be case-insensitive");
        Vector(new Vector2(24f, 36f), moved);
    }

    private static void ClearsDragStateOnRelease()
    {
        var session = new OverlayDragSession();
        _ = session.UpdatePosition(
            "window:DRG:jump",
            new Vector2(10f, 10f),
            new Vector2(20f, 30f));

        session.EndDrag();

        True(session.DragId is null, "ending a drag should clear its identity");
        True(!session.IsDragging("window:DRG:jump"), "the previous icon should no longer be active");
        Vector(
            new Vector2(7f, 8f),
            session.UpdatePosition(
                "window:DRG:jump",
                new Vector2(50f, 60f),
                new Vector2(7f, 8f)));
    }
}
