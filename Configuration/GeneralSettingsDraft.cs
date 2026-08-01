namespace FFXIVAura;

internal struct GeneralSettingsDraft
{
    public bool Enabled;
    public bool LockOverlay;
    public bool HideDuringZoneLoad;
    public bool ShowTooltips;
    public bool ShowPerformanceOverlay;
    public bool ShowDetailedPerformanceProfile;
    public bool RecordPerformanceProfile;
    public int PerformanceProfileRecordIntervalSeconds;
    public int PerformanceProfileMaxFileMegabytes;
    public bool ShowPartyCooldownLogObserver;

    public static GeneralSettingsDraft Create(PluginConfigData config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new GeneralSettingsDraft
        {
            Enabled = config.Enabled,
            LockOverlay = config.LockOverlay,
            HideDuringZoneLoad = config.HideDuringZoneLoad,
            ShowTooltips = config.ShowTooltips,
            ShowPerformanceOverlay = config.ShowPerformanceOverlay,
            ShowDetailedPerformanceProfile = config.ShowDetailedPerformanceProfile,
            RecordPerformanceProfile = config.RecordPerformanceProfile,
            PerformanceProfileRecordIntervalSeconds = config.PerformanceProfileRecordIntervalSeconds,
            PerformanceProfileMaxFileMegabytes = config.PerformanceProfileMaxFileMegabytes,
            ShowPartyCooldownLogObserver = config.ShowPartyCooldownLogObserver,
        };
    }

    public bool ApplyTo(PluginConfigData config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var recordIntervalSeconds = PerformanceProfileConfigPolicy.ClampRecordInterval(
            this.PerformanceProfileRecordIntervalSeconds);
        var maxFileMegabytes = PerformanceProfileConfigPolicy.ClampMaxFileMegabytes(
            this.PerformanceProfileMaxFileMegabytes);
        var changed = config.Enabled != this.Enabled
                      || config.LockOverlay != this.LockOverlay
                      || config.HideDuringZoneLoad != this.HideDuringZoneLoad
                      || config.ShowTooltips != this.ShowTooltips
                      || config.ShowPerformanceOverlay != this.ShowPerformanceOverlay
                      || config.ShowDetailedPerformanceProfile != this.ShowDetailedPerformanceProfile
                      || config.RecordPerformanceProfile != this.RecordPerformanceProfile
                      || config.PerformanceProfileRecordIntervalSeconds != recordIntervalSeconds
                      || config.PerformanceProfileMaxFileMegabytes != maxFileMegabytes
                      || config.ShowPartyCooldownLogObserver != this.ShowPartyCooldownLogObserver;

        config.Enabled = this.Enabled;
        config.LockOverlay = this.LockOverlay;
        config.HideDuringZoneLoad = this.HideDuringZoneLoad;
        config.ShowTooltips = this.ShowTooltips;
        config.ShowPerformanceOverlay = this.ShowPerformanceOverlay;
        config.ShowDetailedPerformanceProfile = this.ShowDetailedPerformanceProfile;
        config.RecordPerformanceProfile = this.RecordPerformanceProfile;
        config.PerformanceProfileRecordIntervalSeconds = recordIntervalSeconds;
        config.PerformanceProfileMaxFileMegabytes = maxFileMegabytes;
        config.ShowPartyCooldownLogObserver = this.ShowPartyCooldownLogObserver;
        return changed;
    }
}
