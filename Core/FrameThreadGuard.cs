using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FFXIVAura;

/// <summary>
/// Guard for the codebase's implicit threading invariant: the per-frame caches (aura indexes,
/// roster snapshot, status caches) are plain non-concurrent collections and are only correct
/// because every access happens on the UI/Draw thread.
///
/// The UI thread is captured on every <see cref="CaptureUiThread"/> call (from <c>Plugin.Draw</c>).
/// <see cref="AssertUiThread"/> verifies later cache-touching entry points run on that same
/// thread; it is <see cref="ConditionalAttribute"/>("DEBUG") and compiled out of Release builds.
/// <see cref="DetectCrossThreadUse"/> is the Release-safe complement for the few Dalamud event
/// entry points: it never throws, costs one thread-id compare, and returns true at most once per
/// session so the caller can log a diagnostic if Dalamud ever moves an event off the UI thread.
/// </summary>
internal static class FrameThreadGuard
{
    private const int Uncaptured = -1;

    private static int uiThreadId = Uncaptured;
    private static int crossThreadUseReported;

    public static void CaptureUiThread()
        => Volatile.Write(ref uiThreadId, Environment.CurrentManagedThreadId);

    [Conditional("DEBUG")]
    public static void AssertUiThread([CallerMemberName] string caller = "")
    {
        var captured = Volatile.Read(ref uiThreadId);
        if (captured == Uncaptured)
            return;

        Debug.Assert(
            Environment.CurrentManagedThreadId == captured,
            $"{caller} must run on the UI/Draw thread (captured {captured}, " +
            $"actual {Environment.CurrentManagedThreadId}). Per-frame caches are not thread-safe.");
    }

    /// <summary>
    /// Returns true (at most once per session) when the current call runs off the captured
    /// UI/Draw thread, so the caller can surface a diagnostic in Release builds.
    /// </summary>
    public static bool DetectCrossThreadUse()
    {
        var captured = Volatile.Read(ref uiThreadId);
        if (captured == Uncaptured || Environment.CurrentManagedThreadId == captured)
            return false;

        return Interlocked.Exchange(ref crossThreadUseReported, 1) == 0;
    }
}
