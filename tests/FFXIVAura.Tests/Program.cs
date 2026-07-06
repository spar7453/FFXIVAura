using System.Numerics;
using System.Text.Json;
using FFXIVAura;
using FFXIVAura.Tests;

var tests = new List<(string Name, Action Run)>
{
    ("CooldownMath converts aggregate charge cooldown", CooldownMathConvertsAggregateChargeCooldown),
    ("CooldownMath reports no cooldown at max charges", CooldownMathReportsNoCooldownAtMaxCharges),
    ("JobInfo allows caster Addle role action", JobInfoAllowsCasterAddleRoleAction),
    ("JobInfo matches role actions case-insensitively", JobInfoMatchesRoleActionsCaseInsensitively),
    ("JobInfo trims job ids and class ids", JobInfoTrimsJobIdsAndClassIds),
    ("abilities.json has valid core fields", AbilitiesJsonHasValidCoreFields),
    ("abilities.json ids are unique", AbilitiesJsonIdsAreUnique),
    ("abilities.json role actions map to a job", AbilitiesJsonRoleActionsMapToAJob),
    ("IconWindowIdentity creates stable unique ids", IconWindowIdentityCreatesStableUniqueIds),
    ("IconWindowIdentity gets the next available number", IconWindowIdentityGetsNextAvailableNumber),
    ("IconWindowIdentity reuses deleted window numbers", IconWindowIdentityReusesDeletedWindowNumbers),
    ("IconWindowIdentity wraps available window numbers", IconWindowIdentityWrapsAvailableWindowNumbers),
    ("IconWindowIdentity trims window ids", IconWindowIdentityTrimsWindowIds),
    ("IconWindowIdentity remaps aura position groups", IconWindowIdentityRemapsAuraPositionGroups),
    ("IconWindowIdentity merges existing remap targets", IconWindowIdentityMergesExistingRemapTargets),
    ("IconWindowIdentity ignores case-only remaps", IconWindowIdentityIgnoresCaseOnlyRemaps),
    ("IconWindowClone clones string maps safely", IconWindowCloneClonesStringMapsSafely),
    ("IconWindowClone clones vector maps safely", IconWindowCloneClonesVectorMapsSafely),
    ("IconWindowClone clones scoped aura positions", IconWindowCloneClonesScopedAuraPositions),
    ("IconWindowClone skips invalid aura position keys", IconWindowCloneSkipsInvalidAuraPositionKeys),
    ("IconWindowClone ignores invalid aura clone scopes", IconWindowCloneIgnoresInvalidAuraCloneScopes),
    ("OverlayPositionKeys parse aura keys", OverlayPositionKeysParseAuraKeys),
    ("OverlayPositionKeys reject invalid aura keys", OverlayPositionKeysRejectInvalidAuraKeys),
    ("OverlayPositionKeys replace window prefixes", OverlayPositionKeysReplaceWindowPrefixes),
    ("RuntimeScopeKeys match window scoped keys", RuntimeScopeKeysMatchWindowScopedKeys),
    ("RuntimeScopeKeys build stable cache keys", RuntimeScopeKeysBuildStableCacheKeys),
    ("KeybindTextFormatter formats modifiers", KeybindTextFormatterFormatsModifiers),
    ("KeybindTextFormatter maps game glyphs", KeybindTextFormatterMapsGameGlyphs),
    ("KeybindTextFormatter strips unknown glyphs", KeybindTextFormatterStripsUnknownGlyphs),
    ("AuraSearchIndex matches cached search text", AuraSearchIndexMatchesCachedSearchText),
    ("AuraSearchIndex filters internal names", AuraSearchIndexFiltersInternalNames),
};

tests.AddRange(OverlayTests.Cases);
tests.AddRange(ConfigTests.Cases);
tests.AddRange(NativeTooltipTests.Cases);
tests.AddRange(PartyAuraTests.Cases);
tests.AddRange(PerformanceFrameStatsTests.Cases);
tests.AddRange(AuraStatusFrameIndexTests.Cases);
tests.AddRange(ActionKeybindIndexTests.Cases);
tests.AddRange(LoginStabilizationStateTests.Cases);

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} test(s) failed.");
    return failed;
}

Console.WriteLine($"{tests.Count} tests passed.");
return 0;

static void CooldownMathConvertsAggregateChargeCooldown()
{
    var (total, remaining) = CooldownMath.GetTiming(60, 15, 1, 2, 30);
    AssertNear(30, total);
    AssertNear(15, remaining);
}

static void CooldownMathReportsNoCooldownAtMaxCharges()
{
    var (total, remaining) = CooldownMath.GetTiming(60, 15, 2, 2, 30);
    AssertNear(30, total);
    AssertNear(0, remaining);
}

static void JobInfoAllowsCasterAddleRoleAction()
{
    var addle = new AbilityDefinition { Id = "addle", Job = "ROLE" };
    var typo = new AbilityDefinition { Id = "addling", Job = "ROLE" };

    AssertTrue(JobInfo.CanUseRoleAction("BLM", addle), "casters should include Addle");
    AssertTrue(!JobInfo.CanUseRoleAction("BLM", typo), "unknown role ids should not be accepted");
}

static void JobInfoMatchesRoleActionsCaseInsensitively()
{
    var addle = new AbilityDefinition { Id = " ADDLE ", Job = " role " };
    var rampart = new AbilityDefinition { Id = "RAMPART", Job = "ROLE" };

    AssertTrue(JobInfo.CanUseRoleAction(" pct ", addle), "role matching should trim and ignore case");
    AssertTrue(JobInfo.CanUseRoleAction("pld", rampart), "job matching should ignore case");
    AssertTrue(!JobInfo.CanUseRoleAction("pld", addle), "role groups should stay job-specific");
}

static void JobInfoTrimsJobIdsAndClassIds()
{
    AssertEqual(22u, JobInfo.Id(" drg "));

    var drgIds = JobInfo.ApplicableClassJobIds(" drg ");
    AssertTrue(drgIds.Contains(22u), "job id should be included");
    AssertTrue(drgIds.Contains(4u), "base class id should be included");
}

static void AbilitiesJsonHasValidCoreFields()
{
    var abilities = LoadAbilityData();
    foreach (var ability in abilities)
    {
        AssertTrue(!string.IsNullOrWhiteSpace(ability.Id), "ability id should be set");
        AssertTrue(!string.IsNullOrWhiteSpace(ability.Name), $"{ability.Id} should have a display name");
        AssertTrue(!string.IsNullOrWhiteSpace(ability.Job), $"{ability.Id} should have a job");
        AssertTrue(JobInfo.Id(ability.Job) > 0 || string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase), $"{ability.Id} should use a known job");
        AssertTrue(ability.ActionId > 0, $"{ability.Id} should have an action id");
        AssertTrue(ability.IconId > 0, $"{ability.Id} should have an icon id");
        AssertTrue(ability.Level > 0, $"{ability.Id} should have a level");
        AssertTrue(ability.Charges > 0, $"{ability.Id} should have charges");
        AssertTrue(!float.IsNaN(ability.Cooldown) && !float.IsInfinity(ability.Cooldown) && ability.Cooldown >= 0f, $"{ability.Id} should have a valid cooldown");
    }
}

static void AbilitiesJsonIdsAreUnique()
{
    var duplicateIds = LoadAbilityData()
        .GroupBy(ability => ability.Id.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    AssertTrue(duplicateIds.Count == 0, $"duplicate ability ids: {string.Join(", ", duplicateIds)}");
}

static void AbilitiesJsonRoleActionsMapToAJob()
{
    var jobs = KnownJobCodes();
    var unmappedRoleActions = LoadAbilityData()
        .Where(ability => string.Equals(ability.Job, "ROLE", StringComparison.OrdinalIgnoreCase))
        .Where(ability => jobs.All(job => !JobInfo.CanUseRoleAction(job, ability)))
        .Select(ability => ability.Id)
        .ToList();

    AssertTrue(unmappedRoleActions.Count == 0, $"unmapped role actions: {string.Join(", ", unmappedRoleActions)}");
}

static void IconWindowIdentityCreatesStableUniqueIds()
{
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "win1" };
    var next = 1;
    var id = IconWindowIdentity.CreateUniqueId(used, ref next);

    AssertEqual("win2", id);
    AssertEqual(3, next);
    AssertTrue(used.Contains("win2"), "new id should be reserved");
}

static void IconWindowIdentityGetsNextAvailableNumber()
{
    var number = IconWindowIdentity.GetNextAvailableNumber(["win1", "win2", "win4"], 2);
    AssertEqual(3, number);
}

static void IconWindowIdentityReusesDeletedWindowNumbers()
{
    var number = IconWindowIdentity.GetNextAvailableNumber(["win1"], 2);
    AssertEqual(2, number);
}

static void IconWindowIdentityWrapsAvailableWindowNumbers()
{
    var number = IconWindowIdentity.GetNextAvailableNumber(["win1", "win3"], 9999);
    AssertEqual(2, number);
}

static void IconWindowIdentityTrimsWindowIds()
{
    AssertEqual(7, IconWindowIdentity.GetNumber(" win7 "));
    AssertEqual("창 7", IconWindowIdentity.GetDefaultName(" win7 "));

    var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-1"] = new Vector2(1, 2),
        },
    };

    IconWindowIdentity.RemapAuraPositionGroups(positions, " old ", " new ");
    AssertTrue(positions.ContainsKey("new:PartyBuffs"), "trimmed target window id should be used");
}

static void IconWindowIdentityRemapsAuraPositionGroups()
{
    var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-1"] = new Vector2(1, 2),
        },
        ["other:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-2"] = new Vector2(3, 4),
        },
    };

    IconWindowIdentity.RemapAuraPositionGroups(positions, "old", "new");
    AssertTrue(!positions.ContainsKey("old:PartyBuffs"), "old group should be removed");
    AssertTrue(positions.ContainsKey("new:PartyBuffs"), "new group should be created");
    AssertTrue(positions.ContainsKey("other:PartyBuffs"), "unrelated group should stay");
}

static void IconWindowIdentityMergesExistingRemapTargets()
{
    var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-1"] = new Vector2(1, 2),
            ["status-2"] = new Vector2(3, 4),
        },
        ["new:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-2"] = new Vector2(30, 40),
            ["status-3"] = new Vector2(5, 6),
        },
    };

    IconWindowIdentity.RemapAuraPositionGroups(positions, "old", "new");
    AssertTrue(!positions.ContainsKey("old:PartyBuffs"), "old group should be removed");
    AssertVector(new Vector2(1, 2), positions["new:PartyBuffs"]["status-1"]);
    AssertVector(new Vector2(30, 40), positions["new:PartyBuffs"]["status-2"]);
    AssertVector(new Vector2(5, 6), positions["new:PartyBuffs"]["status-3"]);
}

static void IconWindowIdentityIgnoresCaseOnlyRemaps()
{
    var positions = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Win1:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-1"] = new Vector2(1, 2),
        },
    };

    IconWindowIdentity.RemapAuraPositionGroups(positions, "Win1", "win1");
    AssertEqual(1, positions.Count);
    AssertTrue(positions.ContainsKey("Win1:PartyBuffs"), "case-only remap should not remove the existing group");
    AssertVector(new Vector2(1, 2), positions["Win1:PartyBuffs"]["status-1"]);
}

static void IconWindowCloneClonesStringMapsSafely()
{
    var source = new Dictionary<string, List<string>>
    {
        [" DRG "] = [" jump ", "JUMP", "", " dive "],
        [" "] = ["ignored"],
    };

    var clone = IconWindowClone.CloneStringListMap(source);
    source[" DRG "][0] = "changed";

    AssertTrue(clone.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "clone should ignore key case");
    AssertTrue(clone.ContainsKey("DRG"), "trimmed key should exist");
    AssertSequence(["jump", "dive"], clone["DRG"]);
}

static void IconWindowCloneClonesVectorMapsSafely()
{
    var source = new Dictionary<string, Dictionary<string, Vector2>>
    {
        [" DRG "] = new(StringComparer.OrdinalIgnoreCase)
        {
            [" jump "] = new Vector2(1, 2),
            ["bad"] = new Vector2(float.NaN, 0),
        },
    };

    var clone = IconWindowClone.CloneVector2Map(source);
    source[" DRG "][" jump "] = new Vector2(9, 9);

    AssertTrue(clone.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "clone should ignore key case");
    AssertTrue(clone.ContainsKey("DRG"), "trimmed outer key should exist");
    AssertTrue(clone["DRG"].ContainsKey("jump"), "trimmed inner key should exist");
    AssertVector(new Vector2(1, 2), clone["DRG"]["jump"]);
    AssertTrue(!clone["DRG"].ContainsKey("bad"), "invalid vector should be skipped");
}

static void IconWindowCloneClonesScopedAuraPositions()
{
    var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            [" status-1 "] = new Vector2(1, 2),
            ["not-status"] = new Vector2(7, 8),
            ["bad"] = new Vector2(float.NaN, 0),
        },
        ["other:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-2"] = new Vector2(3, 4),
        },
    };

    var clone = IconWindowClone.CloneAuraPositionsForWindow(source, "old", "new");

    AssertEqual(1, clone.Count);
    AssertTrue(clone.ContainsKey("new:PartyBuffs"), "target group should be created");
    AssertTrue(!clone.ContainsKey("other:PartyBuffs"), "unrelated groups should not be cloned");
    AssertVector(new Vector2(1, 2), clone["new:PartyBuffs"]["status-1"]);
    AssertTrue(!clone["new:PartyBuffs"].ContainsKey("not-status"), "invalid aura keys should not be cloned");
    AssertTrue(!clone["new:PartyBuffs"].ContainsKey("bad"), "invalid positions should not be cloned");
}

static void IconWindowCloneSkipsInvalidAuraPositionKeys()
{
    var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-0"] = new Vector2(1, 2),
            ["status-a"] = new Vector2(3, 4),
            ["status-2"] = new Vector2(5, 6),
        },
    };

    var clone = IconWindowClone.CloneAuraPositionsForWindow(source, "old", "new");
    AssertEqual(1, clone["new:PartyBuffs"].Count);
    AssertVector(new Vector2(5, 6), clone["new:PartyBuffs"]["status-2"]);
}

static void IconWindowCloneIgnoresInvalidAuraCloneScopes()
{
    var source = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
    {
        ["old:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["status-1"] = new Vector2(1, 2),
        },
    };

    AssertEqual(0, IconWindowClone.CloneAuraPositionsForWindow(source, "", "new").Count);
    AssertEqual(0, IconWindowClone.CloneAuraPositionsForWindow(source, "old", " OLD ").Count);
}

static void OverlayPositionKeysParseAuraKeys()
{
    AssertEqual("status-42", OverlayPositionKeys.Aura(42));
    AssertTrue(OverlayPositionKeys.TryParseAura("STATUS-42", out var statusId), "aura key should parse case-insensitively");
    AssertEqual(42u, statusId);
    AssertTrue(!OverlayPositionKeys.TryParseAura("skill-42", out _), "non-aura key should not parse");
}

static void OverlayPositionKeysRejectInvalidAuraKeys()
{
    AssertTrue(!OverlayPositionKeys.TryParseAura("status-0", out _), "zero status id should not parse");
    AssertTrue(!OverlayPositionKeys.TryParseAura("status-", out _), "empty status id should not parse");
    AssertTrue(!OverlayPositionKeys.TryParseAura("status-a", out _), "non-numeric status id should not parse");
}

static void OverlayPositionKeysReplaceWindowPrefixes()
{
    AssertEqual("new:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("old:PartyBuffs", "old", "new"));
    AssertEqual("new:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("old:PartyBuffs", " old ", " new "));
    AssertEqual("other:PartyBuffs", OverlayPositionKeys.ReplaceWindowPrefix("other:PartyBuffs", "old", "new"));
}

static void RuntimeScopeKeysMatchWindowScopedKeys()
{
    AssertTrue(RuntimeScopeKeys.BelongsToWindow("win1", "win1"), "exact key should belong to the window");
    AssertTrue(RuntimeScopeKeys.BelongsToWindow("win1:DRG", "win1"), "scoped key should belong to the window");
    AssertTrue(!RuntimeScopeKeys.BelongsToWindow("win10:DRG", "win1"), "similarly-prefixed windows should not match");
    AssertTrue(!RuntimeScopeKeys.BelongsToWindow("win1:DRG", ""), "empty window id should not match");
}

static void RuntimeScopeKeysBuildStableCacheKeys()
{
    AssertEqual("win1:PartyBuffs:True", RuntimeScopeKeys.AuraSeen("win1", IconWindowRole.PartyBuffs, true));
    AssertEqual("jump:92", RuntimeScopeKeys.CooldownFrame("jump", 92));
    AssertEqual("win1:DRG:jump", RuntimeScopeKeys.AbilityDrag("win1", "DRG", "jump"));
    AssertEqual("win1:status-42", RuntimeScopeKeys.AuraDrag("win1", 42));
}

static void KeybindTextFormatterFormatsModifiers()
{
    var text = KeybindTextFormatter.Format("[Shift+1]", out var unknown);
    AssertEqual("s1", text);
    AssertTrue(!unknown, "known modifier text should not be marked unknown");

    text = KeybindTextFormatter.Format("Ctrl-Alt+5", out unknown);
    AssertEqual("ca5", text);
    AssertTrue(!unknown, "known modifier text should not be marked unknown");

    text = KeybindTextFormatter.Format("Shift+q", out unknown);
    AssertEqual("sq", text);
    AssertTrue(!unknown, "letter keys should be preserved");
}

static void KeybindTextFormatterMapsGameGlyphs()
{
    var text = KeybindTextFormatter.Format("\u00a7\u00a2\u00aa\u00ba", out var unknown);
    AssertEqual("scan", text);
    AssertTrue(!unknown, "known game glyphs should not be marked unknown");
}

static void KeybindTextFormatterStripsUnknownGlyphs()
{
    var text = KeybindTextFormatter.Format("Shift+?", out var unknown);
    AssertEqual("s", text);
    AssertTrue(unknown, "unknown glyph should be reported");
}

static void AuraSearchIndexMatchesCachedSearchText()
{
    var entries = new[]
    {
        new AuraSearchIndexEntry(10, "피의 갈증", 100, "피의 갈증", "10"),
        new AuraSearchIndexEntry(20, "원초의 혈기", 200, "Raw Intuition", "7531"),
    };

    var byActionName = AuraSearchIndex.Search(entries, "intuition").ToList();
    AssertEqual(1, byActionName.Count);
    AssertEqual(20u, byActionName[0].StatusId);

    var byId = AuraSearchIndex.Search(entries, "53").ToList();
    AssertEqual(1, byId.Count);
    AssertEqual(20u, byId[0].StatusId);
}

static void AuraSearchIndexFiltersInternalNames()
{
    AssertTrue(AuraSearchIndex.IsSearchableStatusName("피의 갈증"), "normal Korean status names should be searchable");
    AssertTrue(!AuraSearchIndex.IsSearchableStatusName("rsv_test"), "reserved status names should be hidden");
    AssertTrue(!AuraSearchIndex.IsSearchableStatusName("_hidden"), "internal status names should be hidden");
    AssertTrue(!AuraSearchIndex.IsSearchableStatusName("テスト"), "Japanese-only status names should be hidden");
}

static List<AbilityDefinition> LoadAbilityData()
{
    var path = FindRepoFile("Data", "abilities.json");
    var json = File.ReadAllText(path);
    var abilities = JsonSerializer.Deserialize<List<AbilityDefinition>>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    });

    AssertTrue(abilities is { Count: > 0 }, "abilities.json should contain ability definitions");
    return abilities!;
}

static string FindRepoFile(params string[] pathParts)
{
    var relativePath = Path.Combine(pathParts);
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        var path = Path.Combine(directory.FullName, relativePath);
        if (File.Exists(path))
            return path;
    }

    throw new FileNotFoundException($"Could not find {relativePath} from {Directory.GetCurrentDirectory()}.");
}

static string[] KnownJobCodes()
    => ["PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE", "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT"];

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"expected {expected}, got {actual}");
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
{
    AssertEqual(expected.Count, actual.Count);
    for (var index = 0; index < expected.Count; index++)
        AssertEqual(expected[index], actual[index]);
}

static void AssertVector(Vector2 expected, Vector2 actual)
{
    AssertNear(expected.X, actual.X);
    AssertNear(expected.Y, actual.Y);
}

static void AssertNear(float expected, float actual, float tolerance = 0.001f)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new InvalidOperationException($"expected {expected}, got {actual}");
}
