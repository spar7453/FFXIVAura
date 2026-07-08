using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PluginDataFilesTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PluginDataFiles falls back to embedded party cooldown data", FallsBackToEmbeddedPartyCooldownData),
        ("PluginDataFiles falls back to embedded ability data", FallsBackToEmbeddedAbilityData),
    ];

    private static void FallsBackToEmbeddedPartyCooldownData()
    {
        var json = PluginDataFiles.ReadText(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            Path.Combine("Data", "party_cooldowns.json"),
            PluginDataFiles.PartyCooldownsResourceName);

        True(json.Contains("\"category\"", StringComparison.OrdinalIgnoreCase), "embedded party cooldown data should be readable");
        True(json.Contains("\"Defensive\"", StringComparison.OrdinalIgnoreCase), "embedded party cooldown data should include defensive entries");
    }

    private static void FallsBackToEmbeddedAbilityData()
    {
        var json = PluginDataFiles.ReadText(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            Path.Combine("Data", "abilities.json"),
            PluginDataFiles.AbilitiesResourceName);

        True(json.Contains("\"actionId\"", StringComparison.OrdinalIgnoreCase), "embedded ability data should be readable");
        True(json.Contains("\"cooldown\"", StringComparison.OrdinalIgnoreCase), "embedded ability data should include cooldown fields");
    }
}
