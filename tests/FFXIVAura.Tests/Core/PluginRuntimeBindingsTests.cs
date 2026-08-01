using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PluginRuntimeBindingsTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PluginRuntimeBindings subscribes once and unsubscribes in reverse", SubscribesAndUnsubscribesInReverse),
        ("PluginRuntimeBindings rolls back earlier subscriptions on failure", RollsBackEarlierSubscriptionsOnFailure),
    ];

    private static void SubscribesAndUnsubscribesInReverse()
    {
        var events = new List<string>();
        var bindings = new PluginRuntimeBindings(
        [
            CreateBinding("first", events),
            CreateBinding("second", events),
        ],
        static (_, _) => { });

        Equal(2, bindings.Count);
        Sequence(["subscribe:first", "subscribe:second"], events);

        bindings.Dispose();
        bindings.Dispose();

        Sequence(
        [
            "subscribe:first",
            "subscribe:second",
            "unsubscribe:second",
            "unsubscribe:first",
        ],
        events);
    }

    private static void RollsBackEarlierSubscriptionsOnFailure()
    {
        var events = new List<string>();
        var failed = false;
        try
        {
            _ = new PluginRuntimeBindings(
            [
                CreateBinding("first", events),
                new PluginRuntimeBinding(
                    () => throw new InvalidOperationException("subscription failed"),
                    () => events.Add("unsubscribe:failed"),
                    "failed"),
            ],
            static (_, _) => { });
        }
        catch (InvalidOperationException)
        {
            failed = true;
        }

        True(failed, "a subscription failure should escape initialization");
        Sequence(
        [
            "subscribe:first",
            "unsubscribe:failed",
            "unsubscribe:first",
        ],
        events);
    }

    private static PluginRuntimeBinding CreateBinding(string name, List<string> events)
        => new(
            () => events.Add($"subscribe:{name}"),
            () => events.Add($"unsubscribe:{name}"),
            name);
}
