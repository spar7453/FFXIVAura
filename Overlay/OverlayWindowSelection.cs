namespace FFXIVAura;

internal static class OverlayWindowSelection
{
    public static bool ShouldSelect(
        bool overlayLocked,
        bool windowHovered,
        bool leftClicked,
        string currentWindowId,
        string candidateWindowId)
        => !overlayLocked
           && windowHovered
           && leftClicked
           && !string.IsNullOrWhiteSpace(candidateWindowId)
           && !string.Equals(currentWindowId, candidateWindowId, StringComparison.OrdinalIgnoreCase);
}
