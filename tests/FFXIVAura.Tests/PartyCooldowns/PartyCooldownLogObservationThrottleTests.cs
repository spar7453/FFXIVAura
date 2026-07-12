using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLogObservationThrottleTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLogObservationThrottle limits samples by interval", LimitsSamplesByInterval),
        ("PartyCooldownLogObservationThrottle resets immediately", ResetsImmediately),
    ];

    private static void LimitsSamplesByInterval()
    {
        var throttle = new PartyCooldownLogObservationThrottle();
        var nowUtc = new DateTime(2026, 7, 12, 3, 0, 0, DateTimeKind.Utc);
        var interval = TimeSpan.FromSeconds(5);

        True(throttle.TryAcquire(nowUtc, interval), "the first sample should be accepted");
        True(!throttle.TryAcquire(nowUtc.AddSeconds(4), interval), "a sample inside the interval should be rejected");
        True(throttle.TryAcquire(nowUtc.AddSeconds(5), interval), "a sample on the interval boundary should be accepted");
        True(!throttle.TryAcquire(nowUtc.AddSeconds(1), interval), "a backward timestamp should not bypass the throttle");
    }

    private static void ResetsImmediately()
    {
        var throttle = new PartyCooldownLogObservationThrottle();
        var nowUtc = new DateTime(2026, 7, 12, 3, 0, 0, DateTimeKind.Utc);
        True(throttle.TryAcquire(nowUtc, TimeSpan.FromMinutes(1)), "the first sample should be accepted");

        throttle.Reset();

        True(throttle.TryAcquire(nowUtc, TimeSpan.FromMinutes(1)), "reset should allow an immediate sample");
    }
}
