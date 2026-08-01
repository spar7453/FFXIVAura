using System.Text.Json;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PluginDataFilesTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PluginDataFiles falls back to embedded party cooldown data", FallsBackToEmbeddedPartyCooldownData),
        ("PluginDataFiles falls back to embedded ability data", FallsBackToEmbeddedAbilityData),
        ("PluginDataFiles replaces malformed external JSON with embedded data", ReplacesMalformedExternalJson),
        ("PluginDataFiles replaces stale external ability data with embedded data", ReplacesStaleExternalAbilityData),
        ("PluginDataFiles replaces legacy party replacement metadata", ReplacesLegacyPartyReplacementMetadata),
        ("PluginDataFiles fails when external and embedded data are invalid", FailsWhenEverySourceIsInvalid),
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

    private static void ReplacesMalformedExternalJson()
    {
        var root = CreateExternalDataFile("abilities.json", "{ malformed");
        try
        {
            var result = ReadAbilities(root, PluginDataFiles.AbilitiesResourceName);

            Equal(PluginDataSource.EmbeddedFallback, result.Source);
            True(result.ExternalFailure is JsonException, "the rejected external parse error should remain observable");
            True(result.Value.Count > 0, "embedded ability data should replace malformed external JSON");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void ReplacesStaleExternalAbilityData()
    {
        const string staleJson =
            """
            [
              {
                "id": "stale-action",
                "name": "Stale action",
                "actionId": 1,
                "actionIds": [1],
                "job": "PLD",
                "level": 1,
                "cooldown": 2.5,
                "charges": 1,
                "iconId": 1
              }
            ]
            """;
        var root = CreateExternalDataFile("abilities.json", staleJson);
        try
        {
            var result = ReadAbilities(root, PluginDataFiles.AbilitiesResourceName);

            Equal(PluginDataSource.EmbeddedFallback, result.Source);
            True(result.ExternalFailure is InvalidDataException, "the rejected external schema error should remain observable");
            True(
                result.Value.Any(definition => !string.IsNullOrWhiteSpace(definition.ReplacementGroup)),
                "the embedded schema should restore centralized replacement metadata");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void FailsWhenEverySourceIsInvalid()
    {
        var root = CreateExternalDataFile("abilities.json", "{ malformed");
        var failed = false;
        try
        {
            _ = ReadAbilities(root, "FFXIVAura.Data.missing.json");
        }
        catch (AggregateException)
        {
            failed = true;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        True(failed, "invalid external data must not hide a missing embedded fallback");
    }

    private static void ReplacesLegacyPartyReplacementMetadata()
    {
        const string legacyJson =
            """
            [
              {
                "id": "legacy-sentinel",
                "category": "Defensive",
                "job": "PLD",
                "actionId": 17,
                "statusIds": [74],
                "replacementGroup": "legacy-group",
                "duration": 15
              }
            ]
            """;
        var root = CreateExternalDataFile("party_cooldowns.json", legacyJson);
        try
        {
            var result = PluginDataFiles.ReadJson<List<PartyCooldownDefinition>>(
                root,
                Path.Combine("Data", "party_cooldowns.json"),
                PluginDataFiles.PartyCooldownsResourceName,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                PartyCooldownDataValidator.IsValid);

            Equal(PluginDataSource.EmbeddedFallback, result.Source);
            True(
                result.Value.All(definition => string.IsNullOrWhiteSpace(definition.ReplacementGroup)),
                "replacement metadata should come only from the ability catalog");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PluginDataReadResult<List<AbilityDefinition>> ReadAbilities(
        string root,
        string resourceName)
        => PluginDataFiles.ReadJson<List<AbilityDefinition>>(
            root,
            Path.Combine("Data", "abilities.json"),
            resourceName,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            AbilityDataValidator.IsValid);

    private static string CreateExternalDataFile(string fileName, string contents)
    {
        var root = Path.Combine(Path.GetTempPath(), $"FFXIVAura-tests-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(root, "Data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, fileName), contents);
        return root;
    }
}
