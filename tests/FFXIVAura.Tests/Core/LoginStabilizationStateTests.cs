using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class LoginStabilizationStateTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("LoginStabilizationState suppresses auto-align after login", SuppressesAutoAlignAfterLogin),
        ("LoginStabilizationState starts suppression on relogin", StartsSuppressionOnRelogin),
    ];

    private static void SuppressesAutoAlignAfterLogin()
    {
        var now = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var state = new LoginStabilizationState(TimeSpan.FromSeconds(3));

        True(state.Update(loggedInAndLoaded: true, now), "first loaded frame should start stabilization");
        True(state.ShouldSuppressSkillAutoAlign(now.AddSeconds(2)), "auto-align should be suppressed during stabilization");
        True(!state.ShouldSuppressSkillAutoAlign(now.AddSeconds(3)), "auto-align should resume after stabilization");
        True(!state.Update(loggedInAndLoaded: true, now.AddSeconds(4)), "continued login should not restart stabilization");
    }

    private static void StartsSuppressionOnRelogin()
    {
        var now = new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc);
        var state = new LoginStabilizationState(TimeSpan.FromSeconds(3));

        True(state.Update(loggedInAndLoaded: true, now), "initial login should start stabilization");
        True(!state.Update(loggedInAndLoaded: false, now.AddSeconds(4)), "logout should not start stabilization");
        True(state.Update(loggedInAndLoaded: true, now.AddSeconds(5)), "relogin should restart stabilization");
        True(state.ShouldSuppressSkillAutoAlign(now.AddSeconds(7)), "relogin should suppress auto-align again");
    }
}
