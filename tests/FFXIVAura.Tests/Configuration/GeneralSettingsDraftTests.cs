using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class GeneralSettingsDraftTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("GeneralSettingsDraft copies current configuration", CopiesCurrentConfiguration),
        ("GeneralSettingsDraft applies and clamps edited values", AppliesAndClampsEditedValues),
        ("GeneralSettingsDraft detects unchanged configuration", DetectsUnchangedConfiguration),
    ];

    private static void CopiesCurrentConfiguration()
    {
        var config = Config();

        var draft = GeneralSettingsDraft.Create(config);

        Equal(config.Enabled, draft.Enabled);
        Equal(config.LockOverlay, draft.LockOverlay);
        Equal(config.HideDuringZoneLoad, draft.HideDuringZoneLoad);
        Equal(config.ShowTooltips, draft.ShowTooltips);
        Equal(config.ShowPerformanceOverlay, draft.ShowPerformanceOverlay);
        Equal(config.ShowDetailedPerformanceProfile, draft.ShowDetailedPerformanceProfile);
        Equal(config.RecordPerformanceProfile, draft.RecordPerformanceProfile);
        Equal(
            config.PerformanceProfileRecordIntervalSeconds,
            draft.PerformanceProfileRecordIntervalSeconds);
        Equal(
            config.PerformanceProfileMaxFileMegabytes,
            draft.PerformanceProfileMaxFileMegabytes);
        Equal(config.ShowPartyCooldownLogObserver, draft.ShowPartyCooldownLogObserver);
    }

    private static void AppliesAndClampsEditedValues()
    {
        var config = Config();
        var draft = GeneralSettingsDraft.Create(config);
        draft.Enabled = false;
        draft.LockOverlay = true;
        draft.HideDuringZoneLoad = false;
        draft.ShowTooltips = false;
        draft.ShowPerformanceOverlay = true;
        draft.ShowDetailedPerformanceProfile = true;
        draft.RecordPerformanceProfile = true;
        draft.PerformanceProfileRecordIntervalSeconds = 999;
        draft.PerformanceProfileMaxFileMegabytes = -10;
        draft.ShowPartyCooldownLogObserver = true;

        var changed = draft.ApplyTo(config);

        True(changed, "edited settings should report a configuration change");
        True(!config.Enabled, "enabled should be applied");
        True(config.LockOverlay, "overlay lock should be applied");
        True(!config.HideDuringZoneLoad, "zone-load visibility should be applied");
        True(!config.ShowTooltips, "tooltip visibility should be applied");
        True(config.ShowPerformanceOverlay, "performance overlay should be applied");
        True(config.ShowDetailedPerformanceProfile, "detailed profiling should be applied");
        True(config.RecordPerformanceProfile, "profile recording should be applied");
        Equal(PerformanceProfileConfigPolicy.MaxRecordIntervalSeconds, config.PerformanceProfileRecordIntervalSeconds);
        Equal(PerformanceProfileConfigPolicy.MinFileMegabytes, config.PerformanceProfileMaxFileMegabytes);
        True(config.ShowPartyCooldownLogObserver, "log observation should be applied");
    }

    private static void DetectsUnchangedConfiguration()
    {
        var config = Config();
        var draft = GeneralSettingsDraft.Create(config);

        True(!draft.ApplyTo(config), "an untouched draft should not report a change");
    }

    private static PluginConfigData Config()
        => new()
        {
            Enabled = true,
            LockOverlay = false,
            HideDuringZoneLoad = true,
            ShowTooltips = true,
            ShowPerformanceOverlay = false,
            ShowDetailedPerformanceProfile = false,
            RecordPerformanceProfile = false,
            PerformanceProfileRecordIntervalSeconds = 5,
            PerformanceProfileMaxFileMegabytes = 128,
            ShowPartyCooldownLogObserver = false,
        };
}
