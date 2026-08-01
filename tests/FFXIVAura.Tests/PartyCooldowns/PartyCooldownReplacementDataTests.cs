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
        ("party cooldown data has stable unique identities", DataHasStableUniqueIdentities),
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
        var abilities = LoadAbilityData();
        var groups = abilities
            .Where(ability => !string.IsNullOrWhiteSpace(ability.ReplacementGroup))
            .GroupBy(AbilityReplacementGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var verifiedKeys = VerifiedReplacements
            .Select(AbilityReplacementGroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpectedGroups = groups.Keys
            .Where(key => !verifiedKeys.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        True(unexpectedGroups.Length == 0, $"replacement groups need verified ReplaceAction rows: {string.Join(", ", unexpectedGroups)}");

        foreach (var replacement in VerifiedReplacements)
        {
            var key = AbilityReplacementGroupKey(replacement);
            True(groups.TryGetValue(key, out var group), $"verified replacement group missing from abilities.json: {key}");

            var actionIds = group!
                .Select(ability => ability.ActionId)
                .OrderBy(actionId => actionId)
                .ToArray();
            var expectedActionIds = new[] { replacement.BaseActionId, replacement.ReplacementActionId }
                .OrderBy(actionId => actionId)
                .ToArray();

            Sequence(expectedActionIds, actionIds);
        }

        var partyDefinitions = LoadPartyCooldownData();
        foreach (var replacement in VerifiedReplacements)
        {
            var partyActionIds = partyDefinitions
                .Where(definition => string.Equals(definition.Job, replacement.Job, StringComparison.OrdinalIgnoreCase))
                .Where(definition => string.Equals(definition.Category, replacement.Category, StringComparison.OrdinalIgnoreCase))
                .Select(definition => definition.ActionId)
                .ToHashSet();

            True(partyActionIds.Contains(replacement.BaseActionId), $"party cooldown base action missing: {replacement.BaseActionId}");
            True(partyActionIds.Contains(replacement.ReplacementActionId), $"party cooldown replacement action missing: {replacement.ReplacementActionId}");
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
            Equal(replacement.Group, baseAbility.ReplacementGroup);
            Equal(replacement.Group, replacementAbility.ReplacementGroup);
        }
    }

    private static void ReplacementsSelectVerifiedActionsByLevel()
    {
        var definitions = LoadPartyCooldownData();
        var abilities = LoadAbilityData()
            .GroupBy(ability => ability.ActionId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var definition in definitions)
        {
            if (!abilities.TryGetValue(definition.ActionId, out var ability))
                continue;

            definition.Level = ability.Level;
            definition.ReplacementGroup = ability.ReplacementGroup;
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

    private static void DataHasStableUniqueIdentities()
    {
        var definitions = LoadPartyCooldownData();
        var duplicateIds = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Id))
            .GroupBy(definition => definition.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var duplicateActionIds = definitions
            .Where(definition => definition.ActionId > 0)
            .GroupBy(definition => definition.ActionId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();

        True(definitions.All(definition => !string.IsNullOrWhiteSpace(definition.Id)), "party cooldown ids must not be empty");
        True(definitions.All(definition => definition.ActionId > 0), "party cooldown action ids must be positive");
        True(definitions.All(definition => !string.IsNullOrWhiteSpace(definition.Job)), "party cooldown jobs must not be empty");
        True(
            definitions.All(definition => Enum.TryParse<PartyCooldownCategory>(definition.Category, true, out _)),
            "party cooldown categories must be recognized");
        True(duplicateIds.Length == 0, $"duplicate party cooldown ids: {string.Join(", ", duplicateIds)}");
        True(duplicateActionIds.Length == 0, $"duplicate party cooldown action ids: {string.Join(", ", duplicateActionIds)}");
        True(
            definitions.All(definition => definition.StatusIds.All(statusId => statusId > 0)),
            "party cooldown status ids must be positive");
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

    private static string AbilityReplacementGroupKey(AbilityDefinition ability)
        => $"{ability.Job}:{ability.ReplacementGroup}";

    private static string AbilityReplacementGroupKey(VerifiedReplacement replacement)
        => $"{replacement.Job}:{replacement.Group}";

    private static string DataReplacementKey(PartyCooldownDefinition definition)
        => string.IsNullOrWhiteSpace(definition.ReplacementGroup)
            ? string.Empty
            : $"{definition.Job}:{definition.Category}:{definition.ReplacementGroup}";

    private sealed record VerifiedReplacement(
        string Group,
        string Job,
        string Category,
        uint BaseActionId,
        uint ReplacementActionId,
        ushort TraitId,
        byte TraitLevel);
}
