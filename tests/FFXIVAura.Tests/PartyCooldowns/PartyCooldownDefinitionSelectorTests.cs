using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownDefinitionSelectorTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownDefinitionSelector uses highest available replacement", UsesHighestAvailableReplacement),
        ("PartyCooldownDefinitionSelector keeps lower replacement before unlock", KeepsLowerReplacementBeforeUnlock),
        ("PartyCooldownDefinitionSelector uses explicit replacement groups", UsesExplicitReplacementGroups),
        ("PartyCooldownDefinitionSelector uses canonical highest replacement", UsesCanonicalHighestReplacement),
        ("PartyCooldownDefinitionSelector keeps independent definitions", KeepsIndependentDefinitions),
    ];

    private static void UsesHighestAvailableReplacement()
    {
        var equivalenceKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentinel"] = "tank-30",
            ["guardian"] = "tank-30",
            ["hallowed-ground"] = "hallowed-ground",
        };
        var definitions = new[]
        {
            Definition("sentinel", 17, 38),
            Definition("guardian", 36920, 92),
            Definition("hallowed-ground", 30, 50),
        };

        var selected = PartyCooldownDefinitionSelector
            .SelectEffectiveForLevel(definitions, 100, definition => DefinitionKey(definition, equivalenceKeys))
            .Select(definition => definition.Id)
            .ToArray();

        Sequence(["guardian", "hallowed-ground"], selected);
    }

    private static void KeepsLowerReplacementBeforeUnlock()
    {
        var equivalenceKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentinel"] = "tank-30",
            ["guardian"] = "tank-30",
        };
        var definitions = new[]
        {
            Definition("sentinel", 17, 38),
            Definition("guardian", 36920, 92),
        };

        var selected = PartyCooldownDefinitionSelector
            .SelectEffectiveForLevel(definitions, 80, definition => DefinitionKey(definition, equivalenceKeys))
            .Select(definition => definition.Id)
            .ToArray();

        Sequence(["sentinel"], selected);
    }

    private static void UsesExplicitReplacementGroups()
    {
        var definitions = new[]
        {
            Definition("sge-physis", 24288, 20, category: "Healing", job: "SGE", replacementGroup: "sge-physis"),
            Definition("sge-physis-ii", 24302, 60, category: "Healing", job: "SGE", replacementGroup: "sge-physis"),
        };

        var lowLevelSelected = PartyCooldownDefinitionSelector
            .SelectEffectiveForLevel(definitions, 50, ExplicitReplacementKey)
            .Select(definition => definition.Id)
            .ToArray();
        var highLevelSelected = PartyCooldownDefinitionSelector
            .SelectEffectiveForLevel(definitions, 100, ExplicitReplacementKey)
            .Select(definition => definition.Id)
            .ToArray();

        Sequence(["sge-physis"], lowLevelSelected);
        Sequence(["sge-physis-ii"], highLevelSelected);
    }

    private static void UsesCanonicalHighestReplacement()
    {
        var equivalenceKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentinel"] = "tank-30",
            ["guardian"] = "tank-30",
            ["hallowed-ground"] = "hallowed-ground",
        };
        var definitions = new[]
        {
            Definition("sentinel", 17, 38),
            Definition("guardian", 36920, 92),
            Definition("hallowed-ground", 30, 50),
        };

        var selected = PartyCooldownDefinitionSelector
            .SelectCanonicalReplacements(definitions, definition => DefinitionKey(definition, equivalenceKeys))
            .Select(definition => definition.Id)
            .ToArray();

        Sequence(["guardian", "hallowed-ground"], selected);
    }

    private static void KeepsIndependentDefinitions()
    {
        var definitions = new[]
        {
            Definition("second-wind", 7541, 8),
            Definition("bloodbath", 7542, 12),
        };

        var selected = PartyCooldownDefinitionSelector
            .SelectEffectiveForLevel(definitions, 100, _ => string.Empty)
            .Select(definition => definition.Id)
            .ToArray();

        Sequence(["second-wind", "bloodbath"], selected);
    }

    private static PartyCooldownDefinition Definition(
        string id,
        uint actionId,
        byte level,
        string category = "Defensive",
        string job = "PLD",
        string replacementGroup = "")
        => new()
        {
            Id = id,
            Category = category,
            Job = job,
            ActionId = actionId,
            Level = level,
            Name = id,
            IconId = actionId,
            Cooldown = 120f,
            ReplacementGroup = replacementGroup,
        };

    private static string DefinitionKey(PartyCooldownDefinition definition, Dictionary<string, string> equivalenceKeys)
        => equivalenceKeys.GetValueOrDefault(definition.Id, string.Empty);

    private static string ExplicitReplacementKey(PartyCooldownDefinition definition)
        => string.IsNullOrWhiteSpace(definition.ReplacementGroup)
            ? string.Empty
            : $"{definition.Job}:{definition.Category}:{definition.ReplacementGroup}";
}
