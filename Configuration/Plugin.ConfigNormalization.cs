namespace FFXIVAura;

public sealed partial class Plugin
{
    private bool EnsureIconWindows()
    {
        var changed = PluginConfigNormalizer.Normalize(this.config, GetConfigNormalizationOptions());
        this.PruneIconWindowRuntimeState();
        return changed;
    }

    private static PluginConfigNormalizationOptions GetConfigNormalizationOptions()
        => new(
            DefaultOverlayPosition: new Vector2(DefaultOverlayPositionX, DefaultOverlayPositionY),
            DefaultOverlayWidth: DefaultOverlayWidth,
            DefaultOverlayHeight: DefaultOverlayHeight,
            MinOverlayWidth: MinOverlayWidth,
            MaxOverlayWidth: MaxOverlayWidth,
            MinOverlayHeight: MinOverlayHeight,
            MaxOverlayHeight: MaxOverlayHeight,
            DefaultIconSize: DefaultIconSize,
            MinIconSize: MinIconSize,
            MaxIconSize: MaxIconSize,
            DefaultGap: DefaultGap,
            MinGap: MinGap,
            MaxGap: MaxGap,
            DefaultFontScale: DefaultFontScale,
            MinFontScale: MinFontScale,
            MaxFontScale: MaxFontScale,
            DefaultOrderEditorHeight: DefaultOrderEditorHeight,
            MinOrderEditorHeight: MinOrderEditorHeight,
            MaxOrderEditorHeight: MaxOrderEditorHeight,
            DefaultTrackedEditorTab: TrackedSkillEditorTabs.Items[0].Id,
            TrackedEditorTabs: TrackedSkillEditorTabs.Items.Select(tab => tab.Id).ToArray());

    private IconWindowConfig GetActiveIconWindow()
    {
        if (this.config.IconWindows.Count == 0 && this.EnsureIconWindows())
            this.QueueConfigSave();

        return this.config.IconWindows.FirstOrDefault(window => string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase))
               ?? this.config.IconWindows[0];
    }
}
