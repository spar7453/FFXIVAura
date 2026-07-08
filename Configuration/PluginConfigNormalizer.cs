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

internal static class PluginConfigNormalizer
{
    private const int MinPerformanceProfileRecordIntervalSeconds = 1;
    private const int MaxPerformanceProfileRecordIntervalSeconds = 60;
    private const int DefaultPerformanceProfileRecordIntervalSeconds = 1;
    private const int MinPerformanceProfileMaxFileMegabytes = 1;
    private const int MaxPerformanceProfileMaxFileMegabytes = 1024;
    private const int DefaultPerformanceProfileMaxFileMegabytes = 64;

    public static bool Normalize(PluginConfigData config, PluginConfigNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(config);

        var changed = EnsureConfigCollections(config, options);
        changed |= NormalizePerformanceProfileSettings(config);
        var fallbackPosition = ConfigValueNormalizer.NormalizePosition(
            config.OverlayPosition,
            options.DefaultOverlayPosition,
            options.DefaultOverlayPosition);
        var fallbackIconSize = ConfigValueNormalizer.NormalizeScalar(
            config.IconSize,
            options.DefaultIconSize,
            options.MinIconSize,
            options.MaxIconSize);
        var fallbackGap = ConfigValueNormalizer.NormalizeScalar(
            config.Gap,
            options.DefaultGap,
            options.MinGap,
            options.MaxGap,
            allowZero: true);
        var fallbackFontScale = ConfigValueNormalizer.NormalizeScalar(
            config.FontScale,
            options.DefaultFontScale,
            options.MinFontScale,
            options.MaxFontScale);
        var fallbackWidth = ConfigValueNormalizer.NormalizeDimension(
            config.OverlayWidth,
            options.DefaultOverlayWidth,
            options.MinOverlayWidth,
            options.MaxOverlayWidth);
        var fallbackHeight = ConfigValueNormalizer.NormalizeDimension(
            config.OverlayHeight,
            options.DefaultOverlayHeight,
            options.MinOverlayHeight,
            options.MaxOverlayHeight);

        config.IconPositionsByJob = ConfigMapNormalizer.NormalizeVector2Map(
            config.IconPositionsByJob,
            new Vector2(fallbackWidth, fallbackHeight),
            fallbackIconSize,
            out var rootPositionsChanged);
        changed |= rootPositionsChanged;

        if (config.IconWindows.Count == 0)
        {
            config.IconWindows.Add(new IconWindowConfig
            {
                Id = "win1",
                Name = "\uCC3D 1",
                Position = fallbackPosition,
                Width = fallbackWidth,
                Height = fallbackHeight,
                IconSize = fallbackIconSize,
                Gap = fallbackGap,
                FontScale = fallbackFontScale,
                TrackedByJob = IconWindowClone.CloneStringListMap(config.TrackedByJob),
                ExcludedByJob = IconWindowClone.CloneStringListMap(config.ExcludedByJob),
                IconPositionsByJob = IconWindowClone.CloneVector2Map(config.IconPositionsByJob),
            });
            changed = true;
        }

        var usedWindowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextWindowNumber = Math.Max(config.WindowCounter, config.IconWindows.Count);
        foreach (var window in config.IconWindows)
        {
            changed |= EnsureWindowCollections(window);

            var previousId = window.Id;
            var normalizedId = window.Id?.Trim() ?? string.Empty;
            if (!string.Equals(window.Id, normalizedId, StringComparison.Ordinal))
            {
                window.Id = normalizedId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(window.Id) || !usedWindowIds.Add(window.Id))
            {
                window.Id = IconWindowIdentity.CreateUniqueId(usedWindowIds, ref nextWindowNumber);
                changed = true;
            }
            else
            {
                nextWindowNumber = Math.Max(nextWindowNumber, IconWindowIdentity.GetNumber(window.Id));
            }

            if (!string.IsNullOrWhiteSpace(previousId)
                && !string.Equals(previousId, window.Id, StringComparison.Ordinal))
            {
                IconWindowIdentity.RemapAuraPositionGroups(window.AuraPositionsByRole, previousId, window.Id);
            }

            if (!string.IsNullOrWhiteSpace(normalizedId)
                && !string.Equals(normalizedId, previousId, StringComparison.Ordinal)
                && !string.Equals(normalizedId, window.Id, StringComparison.Ordinal))
            {
                IconWindowIdentity.RemapAuraPositionGroups(window.AuraPositionsByRole, normalizedId, window.Id);
            }

            if (IconWindowIdentity.IsBrokenName(window.Name))
            {
                window.Name = IconWindowIdentity.GetDefaultName(window.Id);
                changed = true;
            }

            var position = ConfigValueNormalizer.NormalizePosition(window.Position, fallbackPosition, options.DefaultOverlayPosition);
            if (!ConfigValueNormalizer.IsFinitePosition(window.Position) || Vector2.DistanceSquared(window.Position, position) > 0.25f)
            {
                window.Position = position;
                changed = true;
            }

            var width = ConfigValueNormalizer.NormalizeDimension(window.Width, fallbackWidth, options.MinOverlayWidth, options.MaxOverlayWidth);
            if (float.IsNaN(window.Width) || float.IsInfinity(window.Width) || Math.Abs(window.Width - width) > 0.1f)
            {
                window.Width = width;
                changed = true;
            }

            var height = ConfigValueNormalizer.NormalizeDimension(window.Height, fallbackHeight, options.MinOverlayHeight, options.MaxOverlayHeight);
            if (float.IsNaN(window.Height) || float.IsInfinity(window.Height) || Math.Abs(window.Height - height) > 0.1f)
            {
                window.Height = height;
                changed = true;
            }

            var iconSize = ConfigValueNormalizer.NormalizeScalar(window.IconSize, fallbackIconSize, options.MinIconSize, options.MaxIconSize);
            if (!ConfigValueNormalizer.IsFiniteValue(window.IconSize) || Math.Abs(window.IconSize - iconSize) > 0.1f)
            {
                window.IconSize = iconSize;
                changed = true;
            }

            var gap = ConfigValueNormalizer.NormalizeScalar(window.Gap, fallbackGap, options.MinGap, options.MaxGap, allowZero: true);
            if (!ConfigValueNormalizer.IsFiniteValue(window.Gap) || Math.Abs(window.Gap - gap) > 0.1f)
            {
                window.Gap = gap;
                changed = true;
            }

            var fontScale = ConfigValueNormalizer.NormalizeScalar(window.FontScale, fallbackFontScale, options.MinFontScale, options.MaxFontScale);
            if (!ConfigValueNormalizer.IsFiniteValue(window.FontScale) || Math.Abs(window.FontScale - fontScale) > 0.01f)
            {
                window.FontScale = fontScale;
                changed = true;
            }

            var orderEditorHeight = ConfigValueNormalizer.NormalizeScalar(
                window.OrderEditorHeight,
                options.DefaultOrderEditorHeight,
                options.MinOrderEditorHeight,
                options.MaxOrderEditorHeight);
            if (!ConfigValueNormalizer.IsFiniteValue(window.OrderEditorHeight) || Math.Abs(window.OrderEditorHeight - orderEditorHeight) > 0.1f)
            {
                window.OrderEditorHeight = orderEditorHeight;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconWindowRole), window.Role))
            {
                window.Role = IconWindowRole.SkillCooldowns;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconDisplayCondition), window.DisplayCondition))
            {
                window.DisplayCondition = IconDisplayCondition.Always;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconDisplayCondition), window.SkillDisplayCondition))
            {
                window.SkillDisplayCondition = IconDisplayCondition.Always;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconDisplayCondition), window.AuraDisplayCondition))
            {
                window.AuraDisplayCondition = IconDisplayCondition.Always;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconDisplayCondition), window.PartyCooldownDisplayCondition))
            {
                window.PartyCooldownDisplayCondition = IconDisplayCondition.Always;
                changed = true;
            }

            if (window.Role == IconWindowRole.SkillCooldowns
                && window.SkillDisplayCondition == IconDisplayCondition.Always
                && window.DisplayCondition != IconDisplayCondition.Always)
            {
                window.SkillDisplayCondition = window.DisplayCondition;
                changed = true;
            }

            if (IconWindowRoles.IsStandardAuraRole(window.Role)
                && window.AuraDisplayCondition == IconDisplayCondition.Always
                && window.DisplayCondition != IconDisplayCondition.Always)
            {
                window.AuraDisplayCondition = window.DisplayCondition;
                changed = true;
            }

            if (IconWindowRoles.IsPartyCooldownRole(window.Role)
                && window.PartyCooldownDisplayCondition == IconDisplayCondition.Always
                && window.DisplayCondition != IconDisplayCondition.Always)
            {
                window.PartyCooldownDisplayCondition = window.DisplayCondition;
                changed = true;
            }

            if (!Enum.IsDefined(typeof(IconAlignment), window.Alignment))
            {
                window.Alignment = IconAlignment.Center;
                changed = true;
            }

            if (window.ActiveOrderRow < 0)
            {
                window.ActiveOrderRow = 0;
                changed = true;
            }

            changed |= ConfigMapNormalizer.NormalizeStatusIds(window.TrackedStatusIds);
            window.ExcludedPartyCooldownIds = ConfigMapNormalizer.NormalizeStringList(
                window.ExcludedPartyCooldownIds,
                out var excludedPartyCooldownIdsChanged);
            changed |= excludedPartyCooldownIdsChanged;
            window.TrackedByJob = ConfigMapNormalizer.NormalizeStringListMap(window.TrackedByJob, out var trackedMapChanged);
            changed |= trackedMapChanged;
            window.ExcludedByJob = ConfigMapNormalizer.NormalizeStringListMap(window.ExcludedByJob, out var excludedMapChanged);
            changed |= excludedMapChanged;
            window.IconPositionsByJob = ConfigMapNormalizer.NormalizeVector2Map(
                window.IconPositionsByJob,
                new Vector2(window.Width, window.Height),
                window.IconSize,
                out var iconPositionsChanged);
            changed |= iconPositionsChanged;
            window.AuraPositionsByRole = ConfigMapNormalizer.NormalizeVector2Map(
                window.AuraPositionsByRole,
                new Vector2(window.Width, window.Height),
                window.IconSize,
                out var auraPositionsChanged);
            changed |= auraPositionsChanged;
        }

        if (string.IsNullOrWhiteSpace(config.ActiveWindowId)
            || config.IconWindows.All(window => !string.Equals(window.Id, config.ActiveWindowId, StringComparison.OrdinalIgnoreCase)))
        {
            config.ActiveWindowId = config.IconWindows[0].Id;
            changed = true;
        }

        var windowCounter = Math.Max(config.WindowCounter, config.IconWindows.Count);
        foreach (var window in config.IconWindows)
            windowCounter = Math.Max(windowCounter, IconWindowIdentity.GetNumber(window.Id));

        if (config.WindowCounter != windowCounter)
        {
            config.WindowCounter = windowCounter;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizePerformanceProfileSettings(PluginConfigData config)
    {
        var changed = false;
        var interval = config.PerformanceProfileRecordIntervalSeconds <= 0
            ? DefaultPerformanceProfileRecordIntervalSeconds
            : Math.Clamp(
                config.PerformanceProfileRecordIntervalSeconds,
                MinPerformanceProfileRecordIntervalSeconds,
                MaxPerformanceProfileRecordIntervalSeconds);
        if (config.PerformanceProfileRecordIntervalSeconds != interval)
        {
            config.PerformanceProfileRecordIntervalSeconds = interval;
            changed = true;
        }

        var maxFileMegabytes = config.PerformanceProfileMaxFileMegabytes <= 0
            ? DefaultPerformanceProfileMaxFileMegabytes
            : Math.Clamp(
                config.PerformanceProfileMaxFileMegabytes,
                MinPerformanceProfileMaxFileMegabytes,
                MaxPerformanceProfileMaxFileMegabytes);
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

        config.TrackedByJob = ConfigMapNormalizer.NormalizeStringListMap(config.TrackedByJob, out var trackedMapChanged);
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

        if (window.ExcludedPartyCooldownIds is null)
        {
            window.ExcludedPartyCooldownIds = [];
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
