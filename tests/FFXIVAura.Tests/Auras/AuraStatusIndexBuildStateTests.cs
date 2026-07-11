using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraStatusIndexBuildStateTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraStatusIndexBuildState retries failed builds", RetriesFailedBuilds),
        ("AuraStatusIndexBuildState advances generation on success", AdvancesGenerationOnSuccess),
    ];

    private static void RetriesFailedBuilds()
    {
        var state = new AuraStatusIndexBuildState();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        True(state.ShouldAttempt(now), "a new index should attempt immediately");
        state.MarkFailed(now, TimeSpan.FromSeconds(1));
        True(!state.ShouldAttempt(now.AddMilliseconds(999)), "a failed index should honor retry delay");
        True(state.ShouldAttempt(now.AddSeconds(1)), "a failed index should retry after the delay");
    }

    private static void AdvancesGenerationOnSuccess()
    {
        var state = new AuraStatusIndexBuildState();

        state.MarkSucceeded();

        True(state.IsBuilt, "successful index should stay built");
        Equal(1, state.Generation);
        True(!state.ShouldAttempt(DateTime.MaxValue), "successful index should not rebuild without reset");
    }
}
