using System.Numerics;
using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class ConfigTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("ConfigValueNormalizer repairs invalid scalar and positions", ConfigValueNormalizerRepairsInvalidValues),
        ("ConfigMapNormalizer normalizes string lists", ConfigMapNormalizerNormalizesStringLists),
        ("ConfigMapNormalizer normalizes string list maps", ConfigMapNormalizerNormalizesStringListMaps),
        ("ConfigMapNormalizer normalizes vector maps", ConfigMapNormalizerNormalizesVectorMaps),
        ("PluginConfigNormalizer migrates legacy root config", PluginConfigNormalizerMigratesLegacyRootConfig),
        ("PluginConfigNormalizer creates separate party-size layouts for party boards", PluginConfigNormalizerCreatesPartySizeLayoutsForPartyBoards),
        ("PluginConfigNormalizer repairs window ids and values", PluginConfigNormalizerRepairsWindowIdsAndValues),
        ("PluginConfigNormalizer is idempotent after repairing null collections", PluginConfigNormalizerIsIdempotentAfterRepair),
        ("PluginConfigNormalizer repairs performance profile settings", PluginConfigNormalizerRepairsPerformanceProfileSettings),
        ("PluginConfigMigrator upgrades legacy versions", PluginConfigMigratorUpgradesLegacyVersions),
        ("PluginConfigMigrator separates legacy manual tracking mode", PluginConfigMigratorSeparatesLegacyManualTrackingMode),
        ("PluginConfigMigrator preserves explicit empty manual tracking", PluginConfigMigratorPreservesExplicitEmptyManualTracking),
        ("PluginConfigMigrator is idempotent at the current version", PluginConfigMigratorIsIdempotentAtCurrentVersion),
        ("PluginConfigMigrator preserves future versions", PluginConfigMigratorPreservesFutureVersions),
    ];

    private static void ConfigValueNormalizerRepairsInvalidValues()
    {
        Near(42, ConfigValueNormalizer.NormalizeScalar(float.NaN, 42, 1, 100));
        Near(760, ConfigValueNormalizer.NormalizeDimension(float.PositiveInfinity, 760, 100, 1000));

        var fallback = new Vector2(2, 3);
        var finalFallback = new Vector2(4, 5);
        Vector(fallback, ConfigValueNormalizer.NormalizePosition(new Vector2(50000, 1), fallback, finalFallback));
        Vector(finalFallback, ConfigValueNormalizer.NormalizePosition(
            new Vector2(float.NaN, 1),
            new Vector2(float.NaN, 2),
            finalFallback));
    }

    private static void ConfigMapNormalizerNormalizesStringListMaps()
    {
        var source = new Dictionary<string, List<string>>
        {
            [" DRG "] = [" a ", "A", "", "b"],
            [" EMPTY "] = [],
        };

        var result = ConfigMapNormalizer.NormalizeStringListMap(source, out var changed);
        True(changed, "map should be changed");
        True(result.Comparer.Equals(StringComparer.OrdinalIgnoreCase), "map comparer should ignore case");
        True(result.ContainsKey("DRG"), "trimmed key should exist");
        Sequence(["a", "b"], result["DRG"]);
        True(!result.ContainsKey("EMPTY"), "ordinary maps should discard empty entries");
    }

    private static void ConfigMapNormalizerNormalizesStringLists()
    {
        var source = new List<string> { " rampart ", "Rampart", "", "reprisal" };
        var result = ConfigMapNormalizer.NormalizeStringList(source, out var changed);

        True(changed, "list should be changed");
        Sequence(["rampart", "reprisal"], result);
    }

    private static void ConfigMapNormalizerNormalizesVectorMaps()
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
        True(changed, "map should be changed");
        True(result.ContainsKey("DRG"), "trimmed outer key should exist");
        True(result["DRG"].ContainsKey("jump"), "trimmed inner key should exist");
        Vector(new Vector2(60, 0), result["DRG"]["jump"]);
        True(!result["DRG"].ContainsKey("bad"), "invalid vector should be removed");
    }

    private static void PluginConfigNormalizerMigratesLegacyRootConfig()
    {
        var config = new PluginConfigData
        {
            OverlayPosition = new Vector2(640, 360),
            OverlayWidth = 760,
            OverlayHeight = 170,
            ManualTrackingJobs = [" DRG "],
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

        var changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "legacy config should create the first window");
        Equal(1, config.IconWindows.Count);
        var window = config.IconWindows[0];
        Equal("win1", window.Id);
        Equal("\uCC3D 1", window.Name);
        Equal("win1", config.ActiveWindowId);
        Sequence(["DRG"], window.ManualTrackingJobs);
        Sequence(["jump", "dive"], window.TrackedByJob["DRG"]);
        Sequence(["hide"], window.ExcludedByJob["DRG"]);
        True(window.IconPositionsByJob["DRG"].ContainsKey("jump"), "legacy position should be copied");
        True(!window.IconPositionsByJob["DRG"].ContainsKey("bad"), "invalid legacy position should be removed");

        True(!ReferenceEquals(config.TrackedByJob, window.TrackedByJob), "tracked map should be cloned");
        True(!ReferenceEquals(config.ExcludedByJob, window.ExcludedByJob), "excluded map should be cloned");
        True(!ReferenceEquals(config.IconPositionsByJob, window.IconPositionsByJob), "position map should be cloned");
        window.TrackedByJob["DRG"].Add("new-window-only");
        True(!config.TrackedByJob["DRG"].Contains("new-window-only"), "window edits should not mutate legacy root tracked map");
    }

    private static void PluginConfigNormalizerRepairsWindowIdsAndValues()
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
                    SkillDisplayCondition = (IconDisplayCondition)998,
                    AuraDisplayCondition = (IconDisplayCondition)997,
                    Alignment = (IconAlignment)999,
                    TrackedStatusIds = [0, 5, 5],
                    ExactTrackedStatusIds = [0, 5, 5, 7],
                    ExcludedPartyCooldownIds = [" rampart ", "Rampart", "", "reprisal"],
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
                    DisplayCondition = IconDisplayCondition.CoolingOnly,
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

        var changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "broken window config should be repaired");
        Equal("WeaponSkill", config.TrackedEditorTab);
        Equal("win2", config.ActiveWindowId);
        Equal(3, config.WindowCounter);

        var repaired = config.IconWindows[0];
        Equal("win2", repaired.Id);
        Equal("\uCC3D 2", repaired.Name);
        Vector(new Vector2(520, 280), repaired.Position);
        Near(760, repaired.Width);
        Near(170, repaired.Height);
        Near(72, repaired.IconSize);
        Near(5, repaired.Gap);
        Near(1, repaired.FontScale);
        Near(180, repaired.OrderEditorHeight);
        Equal(0, repaired.ActiveOrderRow);
        Equal(IconWindowRole.SkillCooldowns, repaired.Role);
        Equal(IconDisplayCondition.Always, repaired.DisplayCondition);
        Equal(IconDisplayCondition.Always, repaired.SkillDisplayCondition);
        Equal(IconDisplayCondition.Always, repaired.AuraDisplayCondition);
        Equal(IconAlignment.Center, repaired.Alignment);
        Sequence([5u], repaired.TrackedStatusIds);
        Sequence([5u], repaired.ExactTrackedStatusIds);
        Sequence(["rampart", "reprisal"], repaired.ExcludedPartyCooldownIds);
        Sequence(["jump"], repaired.TrackedByJob["DRG"]);

        var duplicate = config.IconWindows[1];
        Equal("win3", duplicate.Id);
        Equal(IconDisplayCondition.CoolingOnly, duplicate.AuraDisplayCondition);
        True(duplicate.AuraPositionsByRole.ContainsKey("win3:PartyBuffs"), "duplicate window aura positions should be remapped");
        True(!duplicate.AuraPositionsByRole.ContainsKey("win2:PartyBuffs"), "old duplicate aura position key should be removed");
    }

    private static void PluginConfigNormalizerCreatesPartySizeLayoutsForPartyBoards()
    {
        var config = new PluginConfigData
        {
            ActiveWindowId = "win1",
            WindowCounter = 2,
            IconWindows =
            [
                new IconWindowConfig
                {
                    Id = "win1",
                    Name = "party",
                    Role = IconWindowRole.PartyDefensives,
                    Position = new Vector2(25, 35),
                    Width = 360,
                    Height = 280,
                    IconSize = 44,
                    Gap = 6,
                    FontScale = 1.1f,
                    Alignment = IconAlignment.Right,
                },
                new IconWindowConfig
                {
                    Id = "win2",
                    Name = "skill",
                    Role = IconWindowRole.SkillCooldowns,
                },
            ],
        };

        var changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "legacy party board should receive four-player and alliance layouts");
        var regular = config.IconWindows[0];
        var fourPlayer = regular.FourPlayerLayout!;
        var alliance = regular.AllianceLayout!;
        Vector(regular.Position, fourPlayer.Position);
        Near(regular.Width, fourPlayer.Width);
        Near(regular.Height, fourPlayer.Height);
        Near(regular.IconSize, fourPlayer.IconSize);
        Near(regular.Gap, fourPlayer.Gap);
        Near(regular.FontScale, fourPlayer.FontScale);
        Equal(regular.Alignment, fourPlayer.Alignment);
        Vector(regular.Position, alliance.Position);
        Near(regular.Width, alliance.Width);
        Near(regular.Height, alliance.Height);
        Near(regular.IconSize, alliance.IconSize);
        Near(regular.Gap, alliance.Gap);
        Near(regular.FontScale, alliance.FontScale);
        Equal(regular.Alignment, alliance.Alignment);
        True(config.IconWindows[1].FourPlayerLayout is null, "non-party windows should not add unused four-player settings");
        True(config.IconWindows[1].AllianceLayout is null, "non-party windows should not add unused alliance settings");

        fourPlayer.Position = new Vector2(float.NaN, 1);
        fourPlayer.Width = float.PositiveInfinity;
        fourPlayer.Height = -1;
        fourPlayer.IconSize = 999;
        fourPlayer.Gap = -1;
        fourPlayer.FontScale = float.NaN;
        fourPlayer.Alignment = (IconAlignment)999;
        alliance.Position = new Vector2(float.NaN, 1);
        alliance.Width = float.PositiveInfinity;
        alliance.Height = -1;
        alliance.IconSize = 999;
        alliance.Gap = -1;
        alliance.FontScale = float.NaN;
        alliance.Alignment = (IconAlignment)999;
        config.PartyCooldownLayoutEditMode = (PartyCooldownLayoutEditMode)999;
        changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "invalid party-size layout values should be repaired");
        Vector(regular.Position, fourPlayer.Position);
        Near(regular.Width, fourPlayer.Width);
        Near(regular.Height, fourPlayer.Height);
        Near(72, fourPlayer.IconSize);
        Near(regular.Gap, fourPlayer.Gap);
        Near(regular.FontScale, fourPlayer.FontScale);
        Equal(regular.Alignment, fourPlayer.Alignment);
        Vector(regular.Position, alliance.Position);
        Near(regular.Width, alliance.Width);
        Near(regular.Height, alliance.Height);
        Near(72, alliance.IconSize);
        Near(regular.Gap, alliance.Gap);
        Near(regular.FontScale, alliance.FontScale);
        Equal(regular.Alignment, alliance.Alignment);
        Equal(PartyCooldownLayoutEditMode.EightPlayer, config.PartyCooldownLayoutEditMode);
    }

    private static void PluginConfigNormalizerIsIdempotentAfterRepair()
    {
        var config = new PluginConfigData
        {
            TrackedByJob = null!,
            ExcludedByJob = null!,
            IconPositionsByJob = null!,
            TrackedEditorTab = "invalid",
            TrackedSkillSearch = null!,
            ActiveWindowId = null!,
            IconWindows =
            [
                null!,
                new IconWindowConfig
                {
                    Id = " win2 ",
                    Name = null!,
                    AuraSearch = null!,
                    TrackedStatusIds = null!,
                    ExactTrackedStatusIds = null!,
                    ExcludedPartyCooldownIds = null!,
                    TrackedByJob = null!,
                    ExcludedByJob = null!,
                    IconPositionsByJob = null!,
                    AuraPositionsByRole = null!,
                },
            ],
        };

        True(
            PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions()),
            "the first pass should repair invalid collections");
        True(
            !PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions()),
            "a repaired config should not change on a second pass");

        Equal(1, config.IconWindows.Count);
        Equal("win2", config.ActiveWindowId);
        Equal(2, config.WindowCounter);
        Equal("\uCC3D 2", config.IconWindows[0].Name);
    }

    private static void PluginConfigNormalizerRepairsPerformanceProfileSettings()
    {
        var config = new PluginConfigData
        {
            PerformanceProfileRecordIntervalSeconds = 0,
            PerformanceProfileMaxFileMegabytes = -10,
        };

        var changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "invalid profile settings should be repaired");
        Equal(1, config.PerformanceProfileRecordIntervalSeconds);
        Equal(64, config.PerformanceProfileMaxFileMegabytes);

        config.PerformanceProfileRecordIntervalSeconds = 999;
        config.PerformanceProfileMaxFileMegabytes = 9999;
        changed = PluginConfigNormalizer.Normalize(config, TestData.ConfigOptions());

        True(changed, "profile settings should be clamped");
        Equal(60, config.PerformanceProfileRecordIntervalSeconds);
        Equal(1024, config.PerformanceProfileMaxFileMegabytes);
    }

    private static void PluginConfigMigratorUpgradesLegacyVersions()
    {
        var config = new PluginConfigData
        {
            Version = 1,
            OverlayWidth = 320,
            OverlayHeight = 90,
        };

        var changed = PluginConfigMigrator.Migrate(config, TestData.ConfigOptions());

        True(changed, "legacy config should be migrated");
        Equal(PluginConfigMigrator.CurrentVersion, config.Version);
        Near(760, config.OverlayWidth);
        Near(170, config.OverlayHeight);
        Equal(1, config.IconWindows.Count);
        Near(760, config.IconWindows[0].Width);
        Near(170, config.IconWindows[0].Height);
    }

    private static void PluginConfigMigratorSeparatesLegacyManualTrackingMode()
    {
        var config = new PluginConfigData
        {
            Version = 6,
            TrackedByJob = new Dictionary<string, List<string>>
            {
                [" DRG "] = [" jump "],
                [" PLD "] = [],
            },
            IconWindows =
            [
                new IconWindowConfig
                {
                    Id = "win1",
                    TrackedByJob = new Dictionary<string, List<string>>
                    {
                        [" WHM "] = [" benison "],
                        [" WAR "] = ["", " "],
                    },
                },
            ],
        };

        True(
            PluginConfigMigrator.Migrate(config, TestData.ConfigOptions()),
            "legacy tracking mode should be migrated");

        Sequence(["DRG"], config.ManualTrackingJobs);
        True(!config.TrackedByJob.ContainsKey("PLD"), "legacy empty root entries should return to automatic mode");
        Sequence(["WHM"], config.IconWindows[0].ManualTrackingJobs);
        True(!config.IconWindows[0].TrackedByJob.ContainsKey("WAR"), "legacy empty window entries should return to automatic mode");
    }

    private static void PluginConfigMigratorPreservesExplicitEmptyManualTracking()
    {
        var config = new PluginConfigData
        {
            Version = PluginConfigMigrator.CurrentVersion,
            IconWindows =
            [
                new IconWindowConfig
                {
                    Id = "win1",
                    ManualTrackingJobs = [" PLD ", "pld"],
                    TrackedByJob = new Dictionary<string, List<string>>
                    {
                        ["PLD"] = [],
                    },
                },
            ],
        };

        True(
            PluginConfigMigrator.Migrate(config, TestData.ConfigOptions()),
            "current explicit tracking state should be normalized");

        Sequence(["PLD"], config.IconWindows[0].ManualTrackingJobs);
        True(!config.IconWindows[0].TrackedByJob.ContainsKey("PLD"), "empty tracked data should not encode mode");
        True(
            AbilityTrackingService.IsManualTracking(config.IconWindows[0], "pld"),
            "manual mode should survive without an empty map entry");
    }

    private static void PluginConfigMigratorIsIdempotentAtCurrentVersion()
    {
        var config = new PluginConfigData();
        PluginConfigMigrator.Migrate(config, TestData.ConfigOptions());

        True(
            !PluginConfigMigrator.Migrate(config, TestData.ConfigOptions()),
            "a current normalized config should not change on a second migration");
        Equal(PluginConfigMigrator.CurrentVersion, config.Version);
    }

    private static void PluginConfigMigratorPreservesFutureVersions()
    {
        var config = new PluginConfigData();
        PluginConfigMigrator.Migrate(config, TestData.ConfigOptions());
        config.Version = PluginConfigMigrator.CurrentVersion + 1;

        True(
            !PluginConfigMigrator.Migrate(config, TestData.ConfigOptions()),
            "a normalized future config should not be rewritten");
        Equal(PluginConfigMigrator.CurrentVersion + 1, config.Version);
    }
}
