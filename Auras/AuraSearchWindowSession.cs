namespace FFXIVAura;

internal sealed class AuraSearchWindowSession
{
    public string? WindowId { get; private set; }

    public bool IsVisible { get; private set; }

    public int PendingStatusId { get; set; }

    public void Open(string windowId)
    {
        this.WindowId = windowId;
        this.IsVisible = true;
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            this.IsVisible = true;
            return;
        }

        this.Close();
    }

    public void Close()
    {
        this.IsVisible = false;
        this.WindowId = null;
    }

    public void CloseIfTarget(string windowId)
    {
        if (this.IsTarget(windowId))
            this.Close();
    }

    public void Prune(IReadOnlyCollection<string> validWindowIds)
    {
        if (!string.IsNullOrWhiteSpace(this.WindowId)
            && !validWindowIds.Contains(this.WindowId, StringComparer.OrdinalIgnoreCase))
        {
            this.Close();
        }
    }

    public IconWindowConfig? ResolveWindow(IReadOnlyList<IconWindowConfig> windows)
    {
        if (!this.IsVisible)
            return null;

        var window = string.IsNullOrWhiteSpace(this.WindowId)
            ? windows.FirstOrDefault(candidate =>
                IconWindowRoles.IsStandardAuraRole(candidate.Role))
            : windows.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    this.WindowId,
                    StringComparison.OrdinalIgnoreCase));
        if (window is not null && IconWindowRoles.IsStandardAuraRole(window.Role))
            return window;

        this.Close();
        return null;
    }

    private bool IsTarget(string windowId)
        => !string.IsNullOrWhiteSpace(windowId)
           && string.Equals(
               this.WindowId,
               windowId,
               StringComparison.OrdinalIgnoreCase);
}
