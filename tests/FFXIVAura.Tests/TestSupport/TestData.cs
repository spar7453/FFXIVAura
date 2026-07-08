using FFXIVAura;

namespace FFXIVAura.Tests;

internal static class TestData
{
    public static PluginConfigNormalizationOptions ConfigOptions()
        => new(
            DefaultOverlayPosition: new System.Numerics.Vector2(520, 280),
            DefaultOverlayWidth: 760,
            DefaultOverlayHeight: 170,
            MinOverlayWidth: 120,
            MaxOverlayWidth: 1200,
            MinOverlayHeight: 40,
            MaxOverlayHeight: 400,
            DefaultIconSize: 42,
            MinIconSize: 24,
            MaxIconSize: 72,
            DefaultGap: 5,
            MinGap: 0,
            MaxGap: 16,
            DefaultFontScale: 1,
            MinFontScale: 0.75f,
            MaxFontScale: 1.5f,
            DefaultOrderEditorHeight: 180,
            MinOrderEditorHeight: 90,
            MaxOrderEditorHeight: 520,
            DefaultTrackedEditorTab: "WeaponSkill",
            TrackedEditorTabs: ["WeaponSkill", "Spell", "Ability", "Role"]);
}
