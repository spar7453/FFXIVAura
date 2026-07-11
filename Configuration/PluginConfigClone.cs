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
            IconWindows = source.IconWindows.Select(CloneWindow).ToList(),
        };
    }

    private static IconWindowConfig CloneWindow(IconWindowConfig source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Position = source.Position,
            Width = source.Width,
            Height = source.Height,
            IconSize = source.IconSize,
            Gap = source.Gap,
            FontScale = source.FontScale,
            OrderEditorHeight = source.OrderEditorHeight,
            ActiveOrderRow = source.ActiveOrderRow,
            Role = source.Role,
            DisplayCondition = source.DisplayCondition,
            SkillDisplayCondition = source.SkillDisplayCondition,
            AuraDisplayCondition = source.AuraDisplayCondition,
            PartyCooldownDisplayCondition = source.PartyCooldownDisplayCondition,
            Alignment = source.Alignment,
            AllianceLayout = IconWindowLayoutBinding.CloneConfig(source.AllianceLayout),
            HighlightReady = source.HighlightReady,
            HighlightAdjusted = source.HighlightAdjusted,
            ShowKeybindText = source.ShowKeybindText,
            ShowMissingAuras = source.ShowMissingAuras,
            PartyAurasOwnOnly = source.PartyAurasOwnOnly,
            ShowPartyAuraCount = source.ShowPartyAuraCount,
            AuraSearch = source.AuraSearch,
            AuraSearchActiveOnly = source.AuraSearchActiveOnly,
            AuraSearchShowIndividualIds = source.AuraSearchShowIndividualIds,
            TrackedStatusIds = source.TrackedStatusIds.ToList(),
            ExactTrackedStatusIds = source.ExactTrackedStatusIds.ToList(),
            ExcludedPartyCooldownIds = source.ExcludedPartyCooldownIds.ToList(),
            TrackedByJob = IconWindowClone.CloneStringListMap(source.TrackedByJob),
            ExcludedByJob = IconWindowClone.CloneStringListMap(source.ExcludedByJob),
            IconPositionsByJob = IconWindowClone.CloneVector2Map(source.IconPositionsByJob),
            AuraPositionsByRole = IconWindowClone.CloneVector2Map(source.AuraPositionsByRole),
        };
}
