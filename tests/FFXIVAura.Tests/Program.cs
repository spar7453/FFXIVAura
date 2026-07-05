using System.Numerics;
using System.Text.Json;
using FFXIVAura;
using FFXIVAura.Tests;

var tests = new List<(string Name, Action Run)>
{
    ("OverlayLayout centers a single row", OverlayLayoutCentersSingleRow),
    ("OverlayLayout right-aligns a single row", OverlayLayoutRightAlignsSingleRow),
    ("OverlayLayout compacts aura rows from top", OverlayLayoutCompactsAuraRowsFromTop),
    ("OverlayLayout clamps invalid positions", OverlayLayoutClampsInvalidPositions),
    ("OverlayLayout finds a free slot", OverlayLayoutFindsFreeSlot),
    ("OverlayFrameModel exposes display and layout items", OverlayFrameModelExposesDisplayAndLayoutItems),
    ("CooldownMath converts aggregate charge cooldown", CooldownMathConvertsAggregateChargeCooldown),
    ("CooldownMath reports no cooldown at max charges", CooldownMathReportsNoCooldownAtMaxCharges),
    ("ConfigValueNormalizer repairs invalid scalar and positions", ConfigValueNormalizerRepairsInvalidValues),
    ("ConfigMapNormalizer normalizes string list maps", ConfigMapNormalizerNormalizesStringListMaps),
    ("ConfigMapNormalizer normalizes vector maps", ConfigMapNormalizerNormalizesVectorMaps),
    ("PluginConfigNormalizer migrates legacy root config", PluginConfigNormalizerMigratesLegacyRootConfig),
    ("PluginConfigNormalizer repairs window ids and values", PluginConfigNormalizerRepairsWindowIdsAndValues),
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
    ("NativeActionTooltipIdMatcher matches adjusted ids", NativeActionTooltipIdMatcherMatchesAdjustedIds),
    ("NativeActionTooltipIdMatcher handles invalid ids", NativeActionTooltipIdMatcherHandlesInvalidIds),
    ("NativeActionTooltipState controls only active requests", NativeActionTooltipStateControlsOnlyActiveRequests),
    ("NativeActionTooltipState captures sound state once", NativeActionTooltipStateCapturesSoundStateOnce),
    ("NativeActionTooltipController clamps tooltip position", NativeActionTooltipControllerClampsTooltipPosition),
    ("NativeActionTooltipController suppresses sound selectively", NativeActionTooltipControllerSuppressesSoundSelectively),
    ("OverlayTooltipResolver uses latest candidate", OverlayTooltipResolverUsesLatestCandidate),
    ("OverlayControlGeometry splits narrow controls", OverlayControlGeometrySplitsNarrowControls),
    ("OverlayControlGeometry clamps floating windows", OverlayControlGeometryClampsFloatingWindows),
    ("PartyAuraAggregator counts party members once", PartyAuraAggregatorCountsPartyMembersOnce),
    ("PartyAuraAggregator builds own-only aggregates", PartyAuraAggregatorBuildsOwnOnlyAggregates),
};

tests.AddRange(PerformanceFrameStatsTests.Cases);
tests.AddRange(AuraStatusFrameIndexTests.Cases);
tests.AddRange(ActionKeybindIndexTests.Cases);

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

static void OverlayLayoutCentersSingleRow()
{
    var position = OverlayLayout.GetAutoPosition(IconAlignment.Center, 0, 3, new Vector2(200, 100), 40, 5);
    AssertVector(new Vector2(35, 30), position);
}

static void OverlayLayoutRightAlignsSingleRow()
{
    var position = OverlayLayout.GetAutoPosition(IconAlignment.Right, 0, 3, new Vector2(200, 100), 40, 5);
    AssertVector(new Vector2(70, 30), position);
}

static void OverlayLayoutCompactsAuraRowsFromTop()
{
    var position = OverlayLayout.GetCompactPosition(IconAlignment.Center, 0, 3, new Vector2(200, 100), 40, 5);
    AssertVector(new Vector2(35, 0), position);
}

static void OverlayLayoutClampsInvalidPositions()
{
    var position = OverlayLayout.ClampIconPosition(new Vector2(float.NaN, 999), new Vector2(100, 80), 42);
    AssertVector(new Vector2(0, 38), position);
}

static void OverlayLayoutFindsFreeSlot()
{
    var occupied = new List<Vector2> { new(10, 10) };
    var position = OverlayLayout.FindFreeAutoPosition(IconAlignment.Center, 0, 4, occupied, new Vector2(100, 100), 40, 0);
    AssertVector(new Vector2(50, 10), position);
}

static void OverlayFrameModelExposesDisplayAndLayoutItems()
{
    var window = new IconWindowConfig { Id = "win1", Width = 300, Height = 120 };
    var displayAbility = new AbilityDefinition { Id = "display", ActionId = 1 };
    var layoutAbility = new AbilityDefinition { Id = "layout", ActionId = 2 };
    var display = new OverlayItemSet([displayAbility], Array.Empty<AuraState>());
    var layout = new OverlayItemSet([displayAbility, layoutAbility], Array.Empty<AuraState>());
    var frame = new OverlayFrameModel(window, "DRG", 100, new Vector2(300, 120), display, layout);

    AssertTrue(frame.HasDisplayItems, "display items should mark the frame visible");
    AssertEqual(1, frame.DisplayAbilities.Count);
    AssertEqual(2, frame.LayoutAbilities.Count);
    AssertVector(new Vector2(300, 120), frame.AreaSize);
}

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

static void ConfigValueNormalizerRepairsInvalidValues()
{
    AssertNear(42, ConfigValueNormalizer.NormalizeScalar(float.NaN, 42, 1, 100));
    AssertNear(760, ConfigValueNormalizer.NormalizeDimension(float.PositiveInfinity, 760, 100, 1000));

    var fallback = new Vector2(2, 3);
    var finalFallback = new Vector2(4, 5);
    AssertVector(fallback, ConfigValueNormalizer.NormalizePosition(new Vector2(50000, 1), fallback, finalFallback));
    AssertVector(finalFallback, ConfigValueNormalizer.NormalizePosition(
        new Vector2(float.NaN, 1),
        new Vector2(float.NaN, 2),
        finalFallback));
}

static void ConfigMapNormalizerNormalizesStringListMaps()
{
    var source = new Dictionary<string, List<string>>
    {
        [" DRG "] = [" a ", "A", "", "b"],
    };

    var result = ConfigMapNormalizer.NormalizeStringListMap(source, out var changed);
    AssertTrue(changed, "map should be changed");
    AssertTrue(result.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "map comparer should ignore case");
    AssertTrue(result.ContainsKey("DRG"), "trimmed key should exist");
    AssertSequence(["a", "b"], result["DRG"]);
}

static void ConfigMapNormalizerNormalizesVectorMaps()
{
    var source = new Dictionary<string, Dictionary<string, Vector2>>
    {
        [" DRG "] = new Dictionary<string, Vector2>
        {
            [" jump "] = new(999, -3),
            ["bad"] = new(float.NaN, 0),
        },
    };

    var result = ConfigMapNormalizer.NormalizeVector2Map(source, new Vector2(100, 80), 40, out var changed);
    AssertTrue(changed, "map should be changed");
    AssertTrue(result.ContainsKey("DRG"), "trimmed outer key should exist");
    AssertTrue(result["DRG"].ContainsKey("jump"), "trimmed inner key should exist");
    AssertVector(new Vector2(60, 0), result["DRG"]["jump"]);
    AssertTrue(!result["DRG"].ContainsKey("bad"), "invalid vector should be removed");
}

static void PluginConfigNormalizerMigratesLegacyRootConfig()
{
    var config = new PluginConfigData
    {
        OverlayPosition = new Vector2(640, 360),
        OverlayWidth = 760,
        OverlayHeight = 170,
        TrackedByJob = new Dictionary<string, List<string>>
        {
            [" DRG "] = [" jump ", "JUMP", "dive"],
        },
        ExcludedByJob = new Dictionary<string, List<string>>
        {
            [" DRG "] = [" hide "],
        },
        IconPositionsByJob = new Dictionary<string, Dictionary<string, Vector2>>
        {
            [" DRG "] = new()
            {
                [" jump "] = new Vector2(999, -3),
                ["bad"] = new Vector2(float.NaN, 0),
            },
        },
    };

    var changed = PluginConfigNormalizer.Normalize(config, TestConfigOptions());

    AssertTrue(changed, "legacy config should create the first window");
    AssertEqual(1, config.IconWindows.Count);
    var window = config.IconWindows[0];
    AssertEqual("win1", window.Id);
    AssertEqual("\uCC3D 1", window.Name);
    AssertEqual("win1", config.ActiveWindowId);
    AssertSequence(["jump", "dive"], window.TrackedByJob["DRG"]);
    AssertSequence(["hide"], window.ExcludedByJob["DRG"]);
    AssertTrue(window.IconPositionsByJob["DRG"].ContainsKey("jump"), "legacy position should be copied");
    AssertTrue(!window.IconPositionsByJob["DRG"].ContainsKey("bad"), "invalid legacy position should be removed");

    AssertTrue(!ReferenceEquals(config.TrackedByJob, window.TrackedByJob), "tracked map should be cloned");
    AssertTrue(!ReferenceEquals(config.ExcludedByJob, window.ExcludedByJob), "excluded map should be cloned");
    AssertTrue(!ReferenceEquals(config.IconPositionsByJob, window.IconPositionsByJob), "position map should be cloned");
    window.TrackedByJob["DRG"].Add("new-window-only");
    AssertTrue(!config.TrackedByJob["DRG"].Contains("new-window-only"), "window edits should not mutate legacy root tracked map");
}

static void PluginConfigNormalizerRepairsWindowIdsAndValues()
{
    var config = new PluginConfigData
    {
        ActiveWindowId = "missing",
        TrackedEditorTab = "bad",
        WindowCounter = 1,
        IconWindows =
        [
            new IconWindowConfig
            {
                Id = " win2 ",
                Name = string.Empty,
                Position = new Vector2(float.NaN, 1),
                Width = float.NaN,
                Height = -1,
                IconSize = 999,
                Gap = -1,
                FontScale = float.PositiveInfinity,
                OrderEditorHeight = -5,
                ActiveOrderRow = -1,
                Role = (IconWindowRole)999,
                DisplayCondition = (IconDisplayCondition)999,
                Alignment = (IconAlignment)999,
                TrackedStatusIds = [0, 5, 5],
                TrackedByJob = new Dictionary<string, List<string>>
                {
                    [" DRG "] = [" jump ", "JUMP"],
                },
                AuraPositionsByRole = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["win2:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["status-5"] = new Vector2(1, 2),
                    },
                },
            },
            new IconWindowConfig
            {
                Id = "win2",
                Name = "Custom",
                Role = IconWindowRole.PartyBuffs,
                AuraPositionsByRole = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["win2:PartyBuffs"] = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["status-7"] = new Vector2(3, 4),
                    },
                },
            },
        ],
    };

    var changed = PluginConfigNormalizer.Normalize(config, TestConfigOptions());

    AssertTrue(changed, "broken window config should be repaired");
    AssertEqual("WeaponSkill", config.TrackedEditorTab);
    AssertEqual("win2", config.ActiveWindowId);
    AssertEqual(3, config.WindowCounter);

    var repaired = config.IconWindows[0];
    AssertEqual("win2", repaired.Id);
    AssertEqual("\uCC3D 2", repaired.Name);
    AssertVector(new Vector2(520, 280), repaired.Position);
    AssertNear(760, repaired.Width);
    AssertNear(170, repaired.Height);
    AssertNear(72, repaired.IconSize);
    AssertNear(5, repaired.Gap);
    AssertNear(1, repaired.FontScale);
    AssertNear(180, repaired.OrderEditorHeight);
    AssertEqual(0, repaired.ActiveOrderRow);
    AssertEqual(IconWindowRole.SkillCooldowns, repaired.Role);
    AssertEqual(IconDisplayCondition.Always, repaired.DisplayCondition);
    AssertEqual(IconAlignment.Center, repaired.Alignment);
    AssertSequence([5u], repaired.TrackedStatusIds);
    AssertSequence(["jump"], repaired.TrackedByJob["DRG"]);

    var duplicate = config.IconWindows[1];
    AssertEqual("win3", duplicate.Id);
    AssertTrue(duplicate.AuraPositionsByRole.ContainsKey("win3:PartyBuffs"), "duplicate window aura positions should be remapped");
    AssertTrue(!duplicate.AuraPositionsByRole.ContainsKey("win2:PartyBuffs"), "old duplicate aura position key should be removed");
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

static void NativeActionTooltipIdMatcherMatchesAdjustedIds()
{
    var adjustedIds = new Dictionary<uint, uint>
    {
        [10] = 100,
        [20] = 200,
        [30] = 300,
        [40] = 300,
    };

    uint Adjust(uint id) => adjustedIds.GetValueOrDefault(id);

    AssertTrue(NativeActionTooltipIdMatcher.Matches(10, 10, Adjust), "direct action id should match");
    AssertTrue(NativeActionTooltipIdMatcher.Matches(10, 100, Adjust), "adjusted requested id should match tooltip id");
    AssertTrue(NativeActionTooltipIdMatcher.Matches(200, 20, Adjust), "adjusted tooltip id should match requested id");
    AssertTrue(NativeActionTooltipIdMatcher.Matches(30, 40, Adjust), "both ids adjusted to same action should match");
    AssertTrue(NativeActionTooltipIdMatcher.MatchesAny(30, 0, 40, Adjust), "original id should be considered");
}

static void NativeActionTooltipIdMatcherHandlesInvalidIds()
{
    uint ThrowingAdjust(uint _) => throw new InvalidOperationException("boom");

    AssertTrue(!NativeActionTooltipIdMatcher.Matches(0, 10, ThrowingAdjust), "zero requested id should not match");
    AssertTrue(!NativeActionTooltipIdMatcher.Matches(10, 0, ThrowingAdjust), "zero tooltip id should not match");
    AssertTrue(!NativeActionTooltipIdMatcher.Matches(10, 20, ThrowingAdjust), "adjust resolver failures should not match");
}

static void NativeActionTooltipStateControlsOnlyActiveRequests()
{
    var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var state = new NativeActionTooltipState();

    AssertTrue(!state.ShouldControl(now, matchesRequestedAction: true), "inactive state should not control tooltips");

    state.BeginHover(42, now, TimeSpan.FromMilliseconds(500));
    AssertEqual(42u, state.ActionId);
    AssertTrue(state.Visible, "tooltip should become visible when hover begins");
    AssertTrue(state.CanControlWithoutActionMatch(now), "visible tooltip should keep lifecycle control active");
    AssertTrue(state.ShouldControl(now, matchesRequestedAction: false), "hover in progress should control the native tooltip");
    AssertTrue(state.ShouldHideNativeTooltip(matchesRequestedAction: false), "hover in progress should allow native hide");

    state.EndHover();
    AssertTrue(state.ShouldControl(now.AddMilliseconds(100), matchesRequestedAction: true), "matching tooltip should stay controlled");
    AssertTrue(!state.ShouldControl(now.AddMilliseconds(100), matchesRequestedAction: false), "non-matching tooltip should not stay controlled");
    AssertTrue(!state.ShouldHideNativeTooltip(matchesRequestedAction: false), "non-matching tooltip should not be hidden");

    state.ClearTooltipRequest();
    AssertEqual(0u, state.ActionId);
    AssertTrue(!state.Visible, "clear should reset visibility");
    AssertTrue(!state.CanControlWithoutActionMatch(now.AddSeconds(1)), "cleared expired tooltip should skip lifecycle work");
}

static void NativeActionTooltipStateCapturesSoundStateOnce()
{
    var state = new NativeActionTooltipState();

    state.CaptureSoundState(12, disableShowHideSoundEffects: false);
    state.CaptureSoundState(99, disableShowHideSoundEffects: true);

    AssertTrue(state.TryGetCapturedSoundState(out var showSoundEffectId, out var disableShowHideSoundEffects), "sound state should be captured");
    AssertEqual((short)12, showSoundEffectId);
    AssertTrue(!disableShowHideSoundEffects, "first captured sound state should be preserved");

    state.ClearSoundState();
    AssertTrue(!state.TryGetCapturedSoundState(out _, out _), "cleared sound state should not restore");
}

static void NativeActionTooltipControllerClampsTooltipPosition()
{
    var position = NativeActionTooltipController.GetPositionAtMouse(
        new Vector2(490, 290),
        new Vector2(500, 300),
        new Vector2(80, 40));

    AssertVector(new Vector2(420, 260), position);

    var unclamped = NativeActionTooltipController.GetPositionAtMouse(
        new Vector2(10, 20),
        Vector2.Zero,
        new Vector2(80, 40));

    AssertVector(new Vector2(28, 38), unclamped);
}

static void NativeActionTooltipControllerSuppressesSoundSelectively()
{
    AssertTrue(NativeActionTooltipController.ShouldSuppressSound(isShowEvent: true, isVisibleLifecycleEvent: false, addonVisible: false), "show events should suppress before visibility is set");
    AssertTrue(NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: true, addonVisible: true), "visible update/draw events should suppress");
    AssertTrue(!NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: true, addonVisible: false), "invisible update/draw events should not capture sound state");
    AssertTrue(!NativeActionTooltipController.ShouldSuppressSound(isShowEvent: false, isVisibleLifecycleEvent: false, addonVisible: true), "setup events should not capture sound state");
}

static void OverlayTooltipResolverUsesLatestCandidate()
{
    var resolver = new OverlayTooltipResolver();
    var ability = new AbilityDefinition { Id = "first", ActionId = 1 };
    var aura = new AuraState(42, "second", 100, 12, 0, 1, 1, true, true);

    resolver.Register(OverlayTooltipCandidate.ForAbility(ability));
    resolver.Register(OverlayTooltipCandidate.ForAura(aura));

    AssertTrue(resolver.TryConsume(out var candidate), "resolver should consume a candidate");
    AssertEqual(OverlayTooltipCandidateKind.Aura, candidate.Kind);
    AssertEqual(42u, candidate.Aura.StatusId);
    AssertTrue(!resolver.TryConsume(out _), "resolver should clear after consume");
}

static void OverlayControlGeometrySplitsNarrowControls()
{
    var layout = OverlayControlGeometry.CreateLayout(
        new Vector2(20, 10),
        new Vector2(100, 80),
        new Vector2(500, 300),
        controlHeight: 20,
        roleWidth: 80,
        conditionWidth: 80,
        nameWidth: 80,
        alignmentWidth: 80);

    AssertTrue(layout.SplitTopControls, "top controls should split when the overlay is narrow");
    AssertTrue(layout.TopControlsBelow, "top controls should move below when there is no room above");
    AssertTrue(layout.SplitBottomControls, "bottom controls should split when the overlay is narrow");
    AssertTrue(!layout.BottomControlsAbove, "bottom controls should stay below when there is enough room");
    AssertEqual(2, layout.BottomBelowRowOffset);
    AssertNear(94, OverlayControlGeometry.GetTopControlY(new Vector2(20, 10), new Vector2(100, 80), 20, 0, layout));
    AssertNear(142, OverlayControlGeometry.GetBottomControlY(new Vector2(20, 10), new Vector2(100, 80), 20, 0, layout));
}

static void OverlayControlGeometryClampsFloatingWindows()
{
    var clamped = OverlayControlGeometry.ClampWindowPositionToDisplay(
        new Vector2(490, 290),
        new Vector2(100, 50),
        new Vector2(8, 8),
        new Vector2(500, 300),
        8);
    AssertVector(new Vector2(392, 242), clamped);

    var fallback = OverlayControlGeometry.ClampWindowPositionToDisplay(
        new Vector2(float.NaN, 1),
        new Vector2(100, 50),
        new Vector2(8, 8),
        Vector2.Zero,
        8);
    AssertVector(new Vector2(8, 8), fallback);
}

static void PartyAuraAggregatorCountsPartyMembersOnce()
{
    var member = new Dictionary<uint, PartyMemberAuraState>();
    var aggregate = new Dictionary<uint, PartyAuraAggregate>();

    PartyAuraAggregator.AddMemberStatus(member, new PartyAuraStatusSample(42, 10, 100, false), ownOnly: false);
    PartyAuraAggregator.AddMemberStatus(member, new PartyAuraStatusSample(42, 5, 200, true), ownOnly: false);
    PartyAuraAggregator.MergeMemberAuras(member, aggregate);

    AssertEqual(1, aggregate[42].Count);
    AssertEqual(1, aggregate[42].OwnCount);
    AssertNear(10, aggregate[42].Remaining);
    AssertEqual((ushort)100, aggregate[42].Param);
    AssertTrue(aggregate[42].FromSelf, "member should still count as own when a shorter own status exists");
}

static void PartyAuraAggregatorBuildsOwnOnlyAggregates()
{
    var memberOne = new Dictionary<uint, PartyMemberAuraState>();
    var memberTwo = new Dictionary<uint, PartyMemberAuraState>();
    var aggregate = new Dictionary<uint, PartyAuraAggregate>();

    PartyAuraAggregator.AddMemberStatus(memberOne, new PartyAuraStatusSample(42, 30, 100, false), ownOnly: true);
    PartyAuraAggregator.AddMemberStatus(memberOne, new PartyAuraStatusSample(42, 20, 200, true), ownOnly: true);
    PartyAuraAggregator.AddMemberStatus(memberTwo, new PartyAuraStatusSample(42, 40, 300, true), ownOnly: true);
    PartyAuraAggregator.MergeMemberAuras(memberOne, aggregate);
    PartyAuraAggregator.MergeMemberAuras(memberTwo, aggregate);

    AssertEqual(2, aggregate[42].Count);
    AssertEqual(2, aggregate[42].OwnCount);
    AssertNear(40, aggregate[42].Remaining);
    AssertEqual((ushort)300, aggregate[42].Param);
    AssertTrue(aggregate[42].FromSelf, "own-only aggregate should be marked from self");
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

static PluginConfigNormalizationOptions TestConfigOptions()
    => new(
        DefaultOverlayPosition: new Vector2(520, 280),
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
