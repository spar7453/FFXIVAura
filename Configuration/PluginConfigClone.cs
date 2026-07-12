namespace FFXIVAura;

internal static class PluginConfigClone
{
    public static PluginConfig CreateSnapshot(PluginConfigData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PluginConfig
        {
            Version = source.Version,
            Enabled = source.Enabled,
            LockOverlay = source.LockOverlay,
            HideDuringZoneLoad = source.HideDuringZoneLoad,
            ShowTooltips = source.ShowTooltips,
            ShowPerformanceOverlay = source.ShowPerformanceOverlay,
            ShowDetailedPerformanceProfile = source.ShowDetailedPerformanceProfile,
            RecordPerformanceProfile = source.RecordPerformanceProfile,
            PerformanceProfileRecordIntervalSeconds = source.PerformanceProfileRecordIntervalSeconds,
            PerformanceProfileMaxFileMegabytes = source.PerformanceProfileMaxFileMegabytes,
            ShowPartyCooldownLogObserver = source.ShowPartyCooldownLogObserver,
            PartyCooldownLayoutEditMode = source.PartyCooldownLayoutEditMode,
            IconSize = source.IconSize,
            Gap = source.Gap,
            FontScale = source.FontScale,
            OverlayWidth = source.OverlayWidth,
            OverlayHeight = source.OverlayHeight,
            OverlayPosition = source.OverlayPosition,
            TrackedByJob = IconWindowClone.CloneStringListMap(source.TrackedByJob),
            ExcludedByJob = IconWindowClone.CloneStringListMap(source.ExcludedByJob),
            IconPositionsByJob = IconWindowClone.CloneVector2Map(source.IconPositionsByJob),
            TrackedEditorTab = source.TrackedEditorTab,
            TrackedSkillSearch = source.TrackedSkillSearch,
            ActiveWindowId = source.ActiveWindowId,
            WindowCounter = source.WindowCounter,
            IconWindows = source.IconWindows.Select(IconWindowClone.CloneSnapshot).ToList(),
        };
    }
}
