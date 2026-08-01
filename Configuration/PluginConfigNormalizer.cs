namespace FFXIVAura;

internal readonly record struct PluginConfigNormalizationOptions(
    Vector2 DefaultOverlayPosition,
    float DefaultOverlayWidth,
    float DefaultOverlayHeight,
    float MinOverlayWidth,
    float MaxOverlayWidth,
    float MinOverlayHeight,
    float MaxOverlayHeight,
    float DefaultIconSize,
    float MinIconSize,
    float MaxIconSize,
    float DefaultGap,
    float MinGap,
    float MaxGap,
    float DefaultFontScale,
    float MinFontScale,
    float MaxFontScale,
    float DefaultOrderEditorHeight,
    float MinOrderEditorHeight,
    float MaxOrderEditorHeight,
    string DefaultTrackedEditorTab,
    IReadOnlyCollection<string> TrackedEditorTabs);

internal static partial class PluginConfigNormalizer
{
    public static bool Normalize(PluginConfigData config, PluginConfigNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(config);

        var changed = EnsureConfigCollections(config, options);
        changed |= NormalizePerformanceProfileSettings(config);
        changed |= NormalizePartyCooldownLayoutEditMode(config);
        var fallbacks = CreateIconWindowFallbacks(config, options);

        changed |= NormalizeRootIconPositions(config, fallbacks);
        changed |= EnsureInitialIconWindow(config, fallbacks);

        changed |= NormalizeIconWindows(config, options, fallbacks);

        changed |= NormalizeActiveWindow(config);
        changed |= NormalizeWindowCounter(config);

        return changed;
    }

    private static bool NormalizePartyCooldownLayouts(IconWindowConfig window, PluginConfigNormalizationOptions options)
    {
        var changed = false;
        if (IconWindowRoles.IsPartyCooldownRole(window.Role))
        {
            if (window.FourPlayerLayout is null)
            {
                window.FourPlayerLayout = IconWindowLayoutBinding.CreateConfig(window);
                changed = true;
            }

            if (window.AllianceLayout is null)
            {
                window.AllianceLayout = IconWindowLayoutBinding.CreateConfig(window);
                changed = true;
            }
        }

        if (window.FourPlayerLayout is not null)
            changed |= NormalizePartyCooldownLayout(window.FourPlayerLayout, window, options);
        if (window.AllianceLayout is not null)
            changed |= NormalizePartyCooldownLayout(window.AllianceLayout, window, options);

        return changed;
    }

    private static bool NormalizePartyCooldownLayout(
        IconWindowLayoutConfig layout,
        IconWindowConfig window,
        PluginConfigNormalizationOptions options)
    {
        var changed = false;
        var position = ConfigValueNormalizer.NormalizePosition(layout.Position, window.Position, options.DefaultOverlayPosition);
        if (!ConfigValueNormalizer.IsFinitePosition(layout.Position)
            || Vector2.DistanceSquared(layout.Position, position) > 0.25f)
        {
            layout.Position = position;
            changed = true;
        }

        var width = ConfigValueNormalizer.NormalizeDimension(layout.Width, window.Width, options.MinOverlayWidth, options.MaxOverlayWidth);
        if (!ConfigValueNormalizer.IsFiniteValue(layout.Width) || Math.Abs(layout.Width - width) > 0.1f)
        {
            layout.Width = width;
            changed = true;
        }

        var height = ConfigValueNormalizer.NormalizeDimension(layout.Height, window.Height, options.MinOverlayHeight, options.MaxOverlayHeight);
        if (!ConfigValueNormalizer.IsFiniteValue(layout.Height) || Math.Abs(layout.Height - height) > 0.1f)
        {
            layout.Height = height;
            changed = true;
        }

        var iconSize = ConfigValueNormalizer.NormalizeScalar(layout.IconSize, window.IconSize, options.MinIconSize, options.MaxIconSize);
        if (!ConfigValueNormalizer.IsFiniteValue(layout.IconSize) || Math.Abs(layout.IconSize - iconSize) > 0.1f)
        {
            layout.IconSize = iconSize;
            changed = true;
        }

        var gap = ConfigValueNormalizer.NormalizeScalar(layout.Gap, window.Gap, options.MinGap, options.MaxGap, allowZero: true);
        if (!ConfigValueNormalizer.IsFiniteValue(layout.Gap) || Math.Abs(layout.Gap - gap) > 0.1f)
        {
            layout.Gap = gap;
            changed = true;
        }

        var fontScale = ConfigValueNormalizer.NormalizeScalar(layout.FontScale, window.FontScale, options.MinFontScale, options.MaxFontScale);
        if (!ConfigValueNormalizer.IsFiniteValue(layout.FontScale) || Math.Abs(layout.FontScale - fontScale) > 0.01f)
        {
            layout.FontScale = fontScale;
            changed = true;
        }

        if (!Enum.IsDefined(typeof(IconAlignment), layout.Alignment))
        {
            layout.Alignment = window.Alignment;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizePerformanceProfileSettings(PluginConfigData config)
    {
        var changed = false;
        var interval = PerformanceProfileConfigPolicy.NormalizeRecordInterval(
            config.PerformanceProfileRecordIntervalSeconds);
        if (config.PerformanceProfileRecordIntervalSeconds != interval)
        {
            config.PerformanceProfileRecordIntervalSeconds = interval;
            changed = true;
        }

        var maxFileMegabytes = PerformanceProfileConfigPolicy.NormalizeMaxFileMegabytes(
            config.PerformanceProfileMaxFileMegabytes);
        if (config.PerformanceProfileMaxFileMegabytes != maxFileMegabytes)
        {
            config.PerformanceProfileMaxFileMegabytes = maxFileMegabytes;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureConfigCollections(PluginConfigData config, PluginConfigNormalizationOptions options)
    {
        var changed = false;
        if (config.ManualTrackingJobs is null)
        {
            config.ManualTrackingJobs = [];
            changed = true;
        }

        if (config.TrackedByJob is null)
        {
            config.TrackedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (config.ExcludedByJob is null)
        {
            config.ExcludedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (config.IconPositionsByJob is null)
        {
            config.IconPositionsByJob = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (config.IconWindows is null)
        {
            config.IconWindows = [];
            changed = true;
        }
        else
        {
            var compactedWindows = config.IconWindows.Where(window => window is not null).ToList();
            if (compactedWindows.Count != config.IconWindows.Count)
            {
                config.IconWindows = compactedWindows;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(config.TrackedEditorTab)
            || options.TrackedEditorTabs.All(tab => !string.Equals(tab, config.TrackedEditorTab, StringComparison.OrdinalIgnoreCase)))
        {
            config.TrackedEditorTab = options.DefaultTrackedEditorTab;
            changed = true;
        }

        if (config.TrackedSkillSearch is null)
        {
            config.TrackedSkillSearch = string.Empty;
            changed = true;
        }

        if (config.ActiveWindowId is null)
        {
            config.ActiveWindowId = string.Empty;
            changed = true;
        }

        config.ManualTrackingJobs = ConfigMapNormalizer.NormalizeStringList(
            config.ManualTrackingJobs,
            out var manualTrackingJobsChanged);
        changed |= manualTrackingJobsChanged;
        config.TrackedByJob = ConfigMapNormalizer.NormalizeStringListMap(
            config.TrackedByJob,
            out var trackedMapChanged);
        changed |= trackedMapChanged;
        config.ExcludedByJob = ConfigMapNormalizer.NormalizeStringListMap(config.ExcludedByJob, out var excludedMapChanged);
        changed |= excludedMapChanged;
        return changed;
    }

    private static bool EnsureWindowCollections(IconWindowConfig window)
    {
        var changed = false;
        if (window.Name is null)
        {
            window.Name = string.Empty;
            changed = true;
        }

        if (window.AuraSearch is null)
        {
            window.AuraSearch = string.Empty;
            changed = true;
        }

        if (window.TrackedStatusIds is null)
        {
            window.TrackedStatusIds = [];
            changed = true;
        }

        if (window.ExactTrackedStatusIds is null)
        {
            window.ExactTrackedStatusIds = [];
            changed = true;
        }

        if (window.ExcludedPartyCooldownIds is null)
        {
            window.ExcludedPartyCooldownIds = [];
            changed = true;
        }

        if (window.ManualTrackingJobs is null)
        {
            window.ManualTrackingJobs = [];
            changed = true;
        }

        if (window.TrackedByJob is null)
        {
            window.TrackedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (window.ExcludedByJob is null)
        {
            window.ExcludedByJob = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (window.IconPositionsByJob is null)
        {
            window.IconPositionsByJob = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        if (window.AuraPositionsByRole is null)
        {
            window.AuraPositionsByRole = new Dictionary<string, Dictionary<string, Vector2>>(StringComparer.OrdinalIgnoreCase);
            changed = true;
        }

        return changed;
    }
}
