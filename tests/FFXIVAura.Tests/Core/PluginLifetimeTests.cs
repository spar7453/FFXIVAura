using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PluginLifetimeTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PluginLifetime cleans registrations in reverse order once", CleansRegistrationsInReverseOrderOnce),
        ("PluginLifetime continues after a cleanup failure", ContinuesAfterCleanupFailure),
        ("PluginLifetime owns disposable resources", OwnsDisposableResources),
    ];

    private static void CleansRegistrationsInReverseOrderOnce()
    {
        var calls = new List<string>();
        var lifetime = new PluginLifetime((_, _) => { });
        lifetime.Register(() => calls.Add("first"), "first");
        lifetime.Register(() => calls.Add("second"), "second");

        lifetime.Dispose();
        lifetime.Dispose();

        Sequence(["second", "first"], calls);
        Equal(0, lifetime.Count);
    }

    private static void ContinuesAfterCleanupFailure()
    {
        var calls = new List<string>();
        var errors = new List<string>();
        var lifetime = new PluginLifetime((_, message) => errors.Add(message));
        lifetime.Register(() => calls.Add("last"), "last");
        lifetime.Register(() => throw new InvalidOperationException("expected"), "failing registration");
        lifetime.Register(() => calls.Add("first"), "first");

        lifetime.Dispose();

        Sequence(["first", "last"], calls);
        Equal(1, errors.Count);
        True(
            errors[0].Contains("failing registration", StringComparison.Ordinal),
            "cleanup diagnostics should identify the failed registration");
    }

    private static void OwnsDisposableResources()
    {
        var lifetime = new PluginLifetime((_, _) => { });
        var resource = lifetime.Own(new StubDisposable(), "stub");

        lifetime.Dispose();

        True(resource.Disposed, "owned resources should be disposed");
    }

    private sealed class StubDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
            => this.Disposed = true;
    }
}
