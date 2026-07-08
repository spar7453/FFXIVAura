using System.Text.Json;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownReplacementDataTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("party cooldown replacements match verified ReplaceAction rows", ReplacementsMatchVerifiedReplaceActionRows),
        ("party cooldown replacements use verified trait unlock levels", ReplacementsUseVerifiedTraitUnlockLevels),
        ("party cooldown replacements select verified actions by level", ReplacementsSelectVerifiedActionsByLevel),
    ];

    // Source: ffxiv-datamining-ko csv/ReplaceAction.csv at fac6a4b6029654c858c38bbfeecca56c5dc2403a.
    // These are the level/trait-based action replacements currently used by the party cooldown board.
    private static readonly VerifiedReplacement[] VerifiedReplacements =
    [
        new("pld-sentinel", "PLD", "Defensive", 17, 36920, 564, 92),
        new("war-vengeance", "WAR", "Defensive", 44, 36923, 567, 92),
        new("nin-mug", "NIN", "Synergy", 2248, 36957, 585, 66),
        new("drk-shadow-wall", "DRK", "Defensive", 3636, 36927, 571, 92),
        new("sam-third-eye", "SAM", "Defensive", 7498, 36962, 589, 82),
        new("gnb-nebula", "GNB", "Defensive", 16148, 36935, 574, 92),
        new("sge-physis", "SGE", "Healing", 24288, 24302, 510, 60),
    ];

    // Source: ffxiv-datamining-ko csv/Trait.csv at fac6a4b6029654c858c38bbfeecca56c5dc2403a.
    private static readonly Dictionary<ushort, byte> VerifiedTraitLevels = new()
    {
        [510] = 60,
        [564] = 92,
        [567] = 92,
        [571] = 92,
        [574] = 92,
        [585] = 66,
        [589] = 82,
    };

    private static void ReplacementsMatchVerifiedReplaceActionRows()
    {
        var definitions = LoadPartyCooldownData();
        var groups = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.ReplacementGroup))
            .GroupBy(ReplacementGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var verifiedKeys = VerifiedReplacements
            .Select(ReplacementGroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpectedGroups = groups.Keys
            .Where(key => !verifiedKeys.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        True(unexpectedGroups.Length == 0, $"replacement groups need verified ReplaceAction rows: {string.Join(", ", unexpectedGroups)}");

        foreach (var replacement in VerifiedReplacements)
        {
            var key = ReplacementGroupKey(replacement);
            True(groups.TryGetValue(key, out var group), $"verified replacement group missing from party_cooldowns.json: {key}");

            var actionIds = group!
                .Select(definition => definition.ActionId)
                .OrderBy(actionId => actionId)
                .ToArray();
            var expectedActionIds = new[] { replacement.BaseActionId, replacement.ReplacementActionId }
                .OrderBy(actionId => actionId)
                .ToArray();

            Sequence(expectedActionIds, actionIds);
        }
    }

    private static void ReplacementsUseVerifiedTraitUnlockLevels()
    {
        var abilitiesByActionId = LoadAbilityData()
            .GroupBy(ability => ability.ActionId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var replacement in VerifiedReplacements)
        {
            True(VerifiedTraitLevels.TryGetValue(replacement.TraitId, out var traitLevel), $"verified trait id missing from Trait.csv mapping: {replacement.TraitId}");
            Equal(traitLevel, replacement.TraitLevel);
            True(abilitiesByActionId.TryGetValue(replacement.BaseActionId, out var baseAbility), $"base replacement action missing from abilities.json: {replacement.BaseActionId}");
            True(abilitiesByActionId.TryGetValue(replacement.ReplacementActionId, out var replacementAbility), $"replacement action missing from abilities.json: {replacement.ReplacementActionId}");
            True(baseAbility!.Level < replacementAbility!.Level, $"{replacement.Group} should unlock the replacement after the base action");
            Equal(replacement.TraitLevel, replacementAbility.Level);
        }
    }

    private static void ReplacementsSelectVerifiedActionsByLevel()
    {
        var definitions = LoadPartyCooldownData();
        var abilityLevels = LoadAbilityData()
            .GroupBy(ability => ability.ActionId)
            .ToDictionary(group => group.Key, group => group.First().Level);

        foreach (var definition in definitions)
        {
            if (abilityLevels.TryGetValue(definition.ActionId, out var level))
                definition.Level = level;
        }

        foreach (var replacement in VerifiedReplacements)
        {
            var group = definitions
                .Where(definition => string.Equals(definition.Job, replacement.Job, StringComparison.OrdinalIgnoreCase))
                .Where(definition => string.Equals(definition.Category, replacement.Category, StringComparison.OrdinalIgnoreCase))
                .Where(definition => string.Equals(definition.ReplacementGroup, replacement.Group, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var beforeUnlock = Math.Max(1u, (uint)replacement.TraitLevel - 1);
            var selectedBeforeUnlock = PartyCooldownDefinitionSelector
                .SelectEffectiveForLevel(group, beforeUnlock, DataReplacementKey)
                .Select(definition => definition.ActionId)
                .ToArray();
            var selectedAtUnlock = PartyCooldownDefinitionSelector
                .SelectEffectiveForLevel(group, replacement.TraitLevel, DataReplacementKey)
                .Select(definition => definition.ActionId)
                .ToArray();

            Sequence([replacement.BaseActionId], selectedBeforeUnlock);
            Sequence([replacement.ReplacementActionId], selectedAtUnlock);
        }
    }

    private static List<AbilityDefinition> LoadAbilityData()
    {
        var path = TestFiles.FindRepoFile("Data", "abilities.json");
        var json = File.ReadAllText(path);
        var abilities = JsonSerializer.Deserialize<List<AbilityDefinition>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        True(abilities is { Count: > 0 }, "abilities.json should contain ability definitions");
        return abilities!;
    }

    private static List<PartyCooldownDefinition> LoadPartyCooldownData()
    {
        var path = TestFiles.FindRepoFile("Data", "party_cooldowns.json");
        var json = File.ReadAllText(path);
        var definitions = JsonSerializer.Deserialize<List<PartyCooldownDefinition>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        True(definitions is { Count: > 0 }, "party_cooldowns.json should contain cooldown definitions");
        return definitions!;
    }

    private static string ReplacementGroupKey(PartyCooldownDefinition definition)
        => $"{definition.Job}:{definition.Category}:{definition.ReplacementGroup}";

    private static string ReplacementGroupKey(VerifiedReplacement replacement)
        => $"{replacement.Job}:{replacement.Category}:{replacement.Group}";

    private static string DataReplacementKey(PartyCooldownDefinition definition)
        => string.IsNullOrWhiteSpace(definition.ReplacementGroup)
            ? string.Empty
            : ReplacementGroupKey(definition);

    private sealed record VerifiedReplacement(
        string Group,
        string Job,
        string Category,
        uint BaseActionId,
        uint ReplacementActionId,
        ushort TraitId,
        byte TraitLevel);
}
