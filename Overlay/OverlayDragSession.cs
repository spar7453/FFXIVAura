namespace FFXIVAura;

internal sealed class OverlayDragSession
{
    private Vector2 mouseStart;
    private Vector2 positionStart;

    public string? DragId { get; private set; }

    public Vector2 UpdatePosition(
        string dragId,
        Vector2 mousePosition,
        Vector2 itemPosition)
    {
        if (!this.IsDragging(dragId))
        {
            this.DragId = dragId;
            this.mouseStart = mousePosition;
            this.positionStart = itemPosition;
        }

        return this.positionStart + mousePosition - this.mouseStart;
    }

    public bool IsDragging(string dragId)
        => string.Equals(
            this.DragId,
            dragId,
            StringComparison.OrdinalIgnoreCase);

    public void EndDrag()
    {
        this.DragId = null;
        this.mouseStart = Vector2.Zero;
        this.positionStart = Vector2.Zero;
    }
}
