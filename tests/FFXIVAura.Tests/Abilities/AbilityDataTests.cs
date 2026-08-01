using System.Text.Json;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class AbilityDataTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("abilities.json has valid core fields", AbilitiesJsonHasValidCoreFields),
        ("abilities.json ids are unique", AbilitiesJsonIdsAreUnique),
        ("abilities.json role actions map to a job", AbilitiesJsonRoleActionsMapToAJob),
        ("abilities.json replacement groups are well formed", AbilitiesJsonReplacementGroupsAreWellFormed),
        ("abilities.json replacement groups select effective actions", AbilitiesJsonReplacementGroupsSelectEffectiveActions),
        ("party_cooldowns.json has valid core fields", PartyCooldownsJsonHasValidCoreFields),
        ("party_cooldowns.json ids are unique", PartyCooldownsJsonIdsAreUnique),
        ("party_cooldowns.json keeps replacement metadata centralized", PartyCooldownsJsonKeepsReplacementMetadataCentralized),
        ("party_cooldowns.json durationed definitions have status ids", PartyCooldownsJsonDurationedDefinitionsHaveStatusIds),
        ("party_cooldowns.json durationless definitions are documented", PartyCooldownsJsonDurationlessDefinitionsAreDocumented),
        ("party_cooldowns.json references ability data", PartyCooldownsJsonReferencesAbilityData),
    ];

    private static void AbilitiesJsonHasValidCoreFields()
    {
        var abilities = LoadAbilityData();
        foreach (var ability in abilities)
        {
            True(!string.IsNullOrWhiteSpace(ability.Id), "ability id should be set");
            True(!string.IsNullOrWhiteSpace(ability.Name), $"{ability.Id} should have a display name");
            True(!string.IsNullOrWhiteSpace(ability.Job), $"{ability.Id} should have a job");
            True(JobInfo.Id(ability.Job) > 0 || string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase), $"{ability.Id} should use a known job");
            True(ability.ActionId > 0, $"{ability.Id} should have an action id");
            True(ability.IconId > 0, $"{ability.Id} should have an icon id");
            True(ability.Level > 0, $"{ability.Id} should have a level");
            True(ability.Charges > 0, $"{ability.Id} should have charges");
            True(!float.IsNaN(ability.Cooldown) && !float.IsInfinity(ability.Cooldown) && ability.Cooldown >= 0f, $"{ability.Id} should have a valid cooldown");
        }
    }

    private static void AbilitiesJsonIdsAreUnique()
    {
        var duplicateIds = LoadAbilityData()
            .GroupBy(ability => ability.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        True(duplicateIds.Count == 0, $"duplicate ability ids: {string.Join(", ", duplicateIds)}");
    }

    private static void AbilitiesJsonRoleActionsMapToAJob()
    {
        var jobs = KnownJobCodes();
        var unmappedRoleActions = LoadAbilityData()
            .Where(ability => string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
            .Where(ability => jobs.All(job => !JobInfo.CanUseRoleAction(job, ability)))
            .Select(ability => ability.Id)
            .ToList();

        True(unmappedRoleActions.Count == 0, $"unmapped role actions: {string.Join(", ", unmappedRoleActions)}");
    }

    private static void AbilitiesJsonReplacementGroupsAreWellFormed()
    {
        var groups = LoadAbilityData()
            .Where(ability => !string.IsNullOrWhiteSpace(ability.ReplacementGroup))
            .GroupBy(AbilityReplacementKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var singletons = groups
            .Where(group => group.Count() < 2)
            .Select(group => group.Key)
            .ToArray();
        var duplicateLevels = groups
            .Where(group => group.GroupBy(ability => ability.Level).Any(levelGroup => levelGroup.Count() > 1))
            .Select(group => group.Key)
            .ToArray();

        True(groups.Length > 0, "abilities.json should contain verified replacement groups");
        True(singletons.Length == 0, $"replacement groups should contain at least two actions: {string.Join(", ", singletons)}");
        True(duplicateLevels.Length == 0, $"replacement groups should use distinct unlock levels: {string.Join(", ", duplicateLevels)}");
    }

    private static void AbilitiesJsonReplacementGroupsSelectEffectiveActions()
    {
        var physisGroup = LoadAbilityData()
            .Where(ability => string.Equals(ability.Job, "SGE", StringComparison.OrdinalIgnoreCase))
            .Where(ability => string.Equals(ability.ReplacementGroup, "sge-physis", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var lowLevel = SelectEffectiveReplacement(physisGroup, 50);
        var highLevel = SelectEffectiveReplacement(physisGroup, 100);

        Equal("sge-physis", lowLevel?.Id);
        Equal("sge-physis-ii", highLevel?.Id);
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

    private static void PartyCooldownsJsonHasValidCoreFields()
    {
        var definitions = LoadPartyCooldownData();
        foreach (var definition in definitions)
        {
            True(!string.IsNullOrWhiteSpace(definition.Id), "party cooldown id should be set");
            Equal(definition.Id.Trim(), definition.Id);
            True(Enum.TryParse<PartyCooldownCategory>(definition.Category, ignoreCase: true, out _), $"{definition.Id} should use a known category");
            Equal(definition.Category.Trim(), definition.Category);
            True(!string.IsNullOrWhiteSpace(definition.Job), $"{definition.Id} should have a job");
            Equal(definition.Job.Trim(), definition.Job);
            True(JobInfo.Id(definition.Job) > 0 || string.Equals(definition.Job, "ROLE", StringComparison.OrdinalIgnoreCase), $"{definition.Id} should use a known job");
            True(definition.ActionId > 0, $"{definition.Id} should have an action id");
            True(!float.IsNaN(definition.Duration) && !float.IsInfinity(definition.Duration) && definition.Duration >= 0f, $"{definition.Id} should have a valid duration");
            Equal(definition.ReplacementGroup.Trim(), definition.ReplacementGroup);
            True(definition.StatusIds.All(statusId => statusId > 0), $"{definition.Id} should not include invalid status ids");
            Equal(definition.StatusIds.Distinct().Count(), definition.StatusIds.Length);
        }
    }

    private static void PartyCooldownsJsonReferencesAbilityData()
    {
        var abilityActionIds = LoadAbilityData()
            .Select(ability => ability.ActionId)
            .ToHashSet();
        var missingActionIds = LoadPartyCooldownData()
            .Where(definition => !abilityActionIds.Contains(definition.ActionId))
            .Select(definition => $"{definition.Id}:{definition.ActionId}")
            .ToList();

        True(missingActionIds.Count == 0, $"party cooldown actions missing from abilities.json: {string.Join(", ", missingActionIds)}");
    }

    private static void PartyCooldownsJsonIdsAreUnique()
    {
        var duplicateIds = LoadPartyCooldownData()
            .GroupBy(definition => definition.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        True(duplicateIds.Count == 0, $"duplicate party cooldown ids: {string.Join(", ", duplicateIds)}");
    }

    private static void PartyCooldownsJsonKeepsReplacementMetadataCentralized()
        => True(
            LoadPartyCooldownData().All(definition => string.IsNullOrWhiteSpace(definition.ReplacementGroup)),
            "built-in replacement metadata should live only in abilities.json");

    private static void PartyCooldownsJsonDurationlessDefinitionsAreDocumented()
    {
        var expected = new[]
        {
            "dnc-curing-waltz",
            "rescue",
            "sch-deployment-tactics",
            "sch-fey-blessing",
            "second-wind",
            "sge-rhizomata",
            "war-equilibrium",
            "whm-benediction",
            "whm-tetragrammaton",
        };
        var actual = LoadPartyCooldownData()
            .Where(definition => definition.Duration <= 0f)
            .Select(definition => definition.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Sequence(expected, actual);
    }

    private static void PartyCooldownsJsonDurationedDefinitionsHaveStatusIds()
    {
        var missingStatusIds = LoadPartyCooldownData()
            .Where(definition => definition.Duration > 0f)
            .Where(definition => definition.StatusIds.Length == 0)
            .Select(definition => definition.Id)
            .ToArray();

        True(missingStatusIds.Length == 0, $"durationed party cooldowns should define status ids: {string.Join(", ", missingStatusIds)}");
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

    private static string[] KnownJobCodes()
        => ["PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE", "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT"];

    private static string AbilityReplacementKey(AbilityDefinition ability)
        => $"{ability.Job}:{ability.ReplacementGroup}";

    private static AbilityDefinition? SelectEffectiveReplacement(
        IEnumerable<AbilityDefinition> abilities,
        uint level)
        => abilities
            .Where(ability => ability.Level <= level)
            .OrderByDescending(ability => ability.Level)
            .ThenByDescending(ability => ability.ActionId)
            .FirstOrDefault();
}
