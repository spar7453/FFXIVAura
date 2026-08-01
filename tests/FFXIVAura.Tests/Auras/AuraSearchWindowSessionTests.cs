using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AuraSearchWindowSessionTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("AuraSearchWindowSession resolves its target case-insensitively", ResolvesTargetCaseInsensitively),
        ("AuraSearchWindowSession falls back to the first aura window", FallsBackToFirstAuraWindow),
        ("AuraSearchWindowSession closes when its target is invalid", ClosesForInvalidTarget),
        ("AuraSearchWindowSession closes only for its removed target", ClosesOnlyForRemovedTarget),
        ("AuraSearchWindowSession prunes missing targets", PrunesMissingTargets),
        ("AuraSearchWindowSession preserves pending IDs when closed", PreservesPendingIdWhenClosed),
    ];

    private static void ResolvesTargetCaseInsensitively()
    {
        var target = Window("player-buffs", IconWindowRole.PlayerBuffs);
        var session = new AuraSearchWindowSession();
        session.Open("PLAYER-BUFFS");

        var resolved = session.ResolveWindow(
            [
                Window("skills", IconWindowRole.SkillCooldowns),
                target,
            ]);

        Equal(target, resolved);
        True(session.IsVisible, "a valid target should keep the search window open");
        Equal("PLAYER-BUFFS", session.WindowId);
    }

    private static void FallsBackToFirstAuraWindow()
    {
        var target = Window("party-buffs", IconWindowRole.PartyBuffs);
        var session = new AuraSearchWindowSession();
        session.Open(string.Empty);

        var resolved = session.ResolveWindow(
            [
                Window("skills", IconWindowRole.SkillCooldowns),
                target,
                Window("target-debuffs", IconWindowRole.TargetDebuffs),
            ]);

        Equal(target, resolved);
    }

    private static void ClosesForInvalidTarget()
    {
        var session = new AuraSearchWindowSession();
        session.Open("skills");

        var resolved = session.ResolveWindow(
            [Window("skills", IconWindowRole.SkillCooldowns)]);

        Equal<IconWindowConfig?>(null, resolved);
        True(!session.IsVisible, "a non-aura target should close the search window");
        Equal<string?>(null, session.WindowId);
    }

    private static void ClosesOnlyForRemovedTarget()
    {
        var session = new AuraSearchWindowSession();
        session.Open("player-buffs");

        session.CloseIfTarget("party-buffs");
        True(session.IsVisible, "removing another window should not close the search window");

        session.CloseIfTarget("PLAYER-BUFFS");
        True(!session.IsVisible, "removing the target should close the search window");
        Equal<string?>(null, session.WindowId);
    }

    private static void PrunesMissingTargets()
    {
        var session = new AuraSearchWindowSession();
        session.Open("target-debuffs");

        session.Prune(["player-buffs", "TARGET-DEBUFFS"]);
        True(session.IsVisible, "a retained target should keep the search window open");

        session.Prune(["player-buffs"]);
        True(!session.IsVisible, "a missing target should close the search window");
    }

    private static void PreservesPendingIdWhenClosed()
    {
        var session = new AuraSearchWindowSession
        {
            PendingStatusId = 82,
        };
        session.Open("player-buffs");

        session.SetVisible(false);

        True(!session.IsVisible, "closing through the UI should hide the search window");
        Equal<string?>(null, session.WindowId);
        Equal(82, session.PendingStatusId);
    }

    private static IconWindowConfig Window(string id, IconWindowRole role)
        => new()
        {
            Id = id,
            Role = role,
        };
}
