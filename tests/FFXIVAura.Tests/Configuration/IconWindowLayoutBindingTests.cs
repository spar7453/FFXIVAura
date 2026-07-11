using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class IconWindowLayoutBindingTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("IconWindowLayoutBinding keeps regular and alliance layouts independent", KeepsLayoutsIndependent),
        ("IconWindowLayoutBinding initializes alliance layout from regular values", InitializesAllianceFromRegularValues),
        ("IconWindowLayoutBinding initializes four-player layout from regular values", InitializesFourPlayerFromRegularValues),
    ];

    private static void KeepsLayoutsIndependent()
    {
        var window = new IconWindowConfig
        {
            Position = new Vector2(10, 20),
            Width = 300,
            Height = 200,
            IconSize = 40,
            Gap = 5,
            FontScale = 1,
            Alignment = IconAlignment.Left,
            AllianceLayout = new IconWindowLayoutConfig
            {
                Position = new Vector2(100, 200),
                Width = 450,
                Height = 800,
                IconSize = 32,
                Gap = 2,
                FontScale = 0.9f,
                Alignment = IconAlignment.Right,
            },
        };

        var regular = IconWindowLayoutBinding.Regular(window);
        var alliance = IconWindowLayoutBinding.PartyCooldown(window, PartyCooldownLayoutEditMode.Alliance, out var created);
        True(!created, "existing alliance layout should be reused");

        alliance.Position = new Vector2(150, 250);
        alliance.IconSize = 36;
        alliance.Alignment = IconAlignment.Center;

        Vector(new Vector2(10, 20), regular.Position);
        Near(40, regular.IconSize);
        Equal(IconAlignment.Left, regular.Alignment);
        Vector(new Vector2(150, 250), window.AllianceLayout!.Position);
        Near(36, window.AllianceLayout.IconSize);
        Equal(IconAlignment.Center, window.AllianceLayout.Alignment);
    }

    private static void InitializesAllianceFromRegularValues()
    {
        var window = new IconWindowConfig
        {
            Position = new Vector2(25, 35),
            Width = 320,
            Height = 240,
            IconSize = 44,
            Gap = 6,
            FontScale = 1.1f,
            Alignment = IconAlignment.Right,
        };

        var alliance = IconWindowLayoutBinding.PartyCooldown(window, PartyCooldownLayoutEditMode.Alliance, out var created);

        True(created, "missing alliance layout should be initialized");
        True(alliance.IsOverride, "alliance binding should target the override layout");
        Vector(window.Position, alliance.Position);
        Near(window.Width, alliance.Width);
        Near(window.Height, alliance.Height);
        Near(window.IconSize, alliance.IconSize);
        Near(window.Gap, alliance.Gap);
        Near(window.FontScale, alliance.FontScale);
        Equal(window.Alignment, alliance.Alignment);
    }

    private static void InitializesFourPlayerFromRegularValues()
    {
        var window = new IconWindowConfig
        {
            Position = new Vector2(45, 55),
            Width = 340,
            Height = 260,
            IconSize = 46,
            Gap = 7,
            FontScale = 1.2f,
            Alignment = IconAlignment.Center,
        };

        var fourPlayer = IconWindowLayoutBinding.PartyCooldown(
            window,
            PartyCooldownLayoutEditMode.FourPlayer,
            out var created);

        True(created, "missing four-player layout should be initialized");
        True(fourPlayer.IsOverride, "four-player binding should target the override layout");
        Vector(window.Position, fourPlayer.Position);
        Near(window.Width, fourPlayer.Width);
        Near(window.Height, fourPlayer.Height);
        Near(window.IconSize, fourPlayer.IconSize);
        Near(window.Gap, fourPlayer.Gap);
        Near(window.FontScale, fourPlayer.FontScale);
        Equal(window.Alignment, fourPlayer.Alignment);
    }
}
