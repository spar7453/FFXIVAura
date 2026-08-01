namespace FFXIVAura;

internal static partial class PluginConfigNormalizer
{
    private readonly record struct IconWindowNormalizationFallbacks(
        Vector2 Position,
        float Width,
        float Height,
        float IconSize,
        float Gap,
        float FontScale);

    private static bool NormalizePartyCooldownLayoutEditMode(PluginConfigData config)
    {
        if (Enum.IsDefined(typeof(PartyCooldownLayoutEditMode), config.PartyCooldownLayoutEditMode))
            return false;

        config.PartyCooldownLayoutEditMode = PartyCooldownLayoutEditMode.EightPlayer;
        return true;
    }

    private static IconWindowNormalizationFallbacks CreateIconWindowFallbacks(
        PluginConfigData config,
        PluginConfigNormalizationOptions options)
        => new(
            ConfigValueNormalizer.NormalizePosition(
                config.OverlayPosition,
                options.DefaultOverlayPosition,
                options.DefaultOverlayPosition),
            ConfigValueNormalizer.NormalizeDimension(
                config.OverlayWidth,
                options.DefaultOverlayWidth,
                options.MinOverlayWidth,
                options.MaxOverlayWidth),
            ConfigValueNormalizer.NormalizeDimension(
                config.OverlayHeight,
                options.DefaultOverlayHeight,
                options.MinOverlayHeight,
                options.MaxOverlayHeight),
            ConfigValueNormalizer.NormalizeScalar(
                config.IconSize,
                options.DefaultIconSize,
                options.MinIconSize,
                options.MaxIconSize),
            ConfigValueNormalizer.NormalizeScalar(
                config.Gap,
                options.DefaultGap,
                options.MinGap,
                options.MaxGap,
                allowZero: true),
            ConfigValueNormalizer.NormalizeScalar(
                config.FontScale,
                options.DefaultFontScale,
                options.MinFontScale,
                options.MaxFontScale));

    private static bool NormalizeRootIconPositions(
        PluginConfigData config,
        IconWindowNormalizationFallbacks fallbacks)
    {
        config.IconPositionsByJob = ConfigMapNormalizer.NormalizeVector2Map(
            config.IconPositionsByJob,
            new Vector2(fallbacks.Width, fallbacks.Height),
            fallbacks.IconSize,
            out var changed);
        return changed;
    }

    private static bool EnsureInitialIconWindow(
        PluginConfigData config,
        IconWindowNormalizationFallbacks fallbacks)
    {
        if (config.IconWindows.Count != 0)
            return false;

        config.IconWindows.Add(new IconWindowConfig
        {
            Id = "win1",
            Name = "\uCC3D 1",
            Position = fallbacks.Position,
            Width = fallbacks.Width,
            Height = fallbacks.Height,
            IconSize = fallbacks.IconSize,
            Gap = fallbacks.Gap,
            FontScale = fallbacks.FontScale,
            ManualTrackingJobs = IconWindowClone.CloneStringList(config.ManualTrackingJobs),
            TrackedByJob = IconWindowClone.CloneStringListMap(config.TrackedByJob),
            ExcludedByJob = IconWindowClone.CloneStringListMap(config.ExcludedByJob),
            IconPositionsByJob = IconWindowClone.CloneVector2Map(config.IconPositionsByJob),
        });
        return true;
    }

    private static bool NormalizeIconWindows(
        PluginConfigData config,
        PluginConfigNormalizationOptions options,
        IconWindowNormalizationFallbacks fallbacks)
    {
        var changed = false;
        var usedWindowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextWindowNumber = Math.Max(config.WindowCounter, config.IconWindows.Count);
        foreach (var window in config.IconWindows)
        {
            changed |= NormalizeIconWindow(
                window,
                options,
                fallbacks,
                usedWindowIds,
                ref nextWindowNumber);
        }

        return changed;
    }

    private static bool NormalizeIconWindow(
        IconWindowConfig window,
        PluginConfigNormalizationOptions options,
        IconWindowNormalizationFallbacks fallbacks,
        HashSet<string> usedWindowIds,
        ref int nextWindowNumber)
    {
        var changed = EnsureWindowCollections(window);
        changed |= NormalizeIconWindowIdentity(window, usedWindowIds, ref nextWindowNumber);
        changed |= NormalizeIconWindowGeometry(window, options, fallbacks);
        changed |= NormalizeIconWindowDisplaySettings(window);
        changed |= NormalizePartyCooldownLayouts(window, options);
        changed |= NormalizeIconWindowTracking(window);
        return changed;
    }

    private static bool NormalizeIconWindowIdentity(
        IconWindowConfig window,
        HashSet<string> usedWindowIds,
        ref int nextWindowNumber)
    {
        var changed = false;
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

        return changed;
    }

    private static bool NormalizeIconWindowGeometry(
        IconWindowConfig window,
        PluginConfigNormalizationOptions options,
        IconWindowNormalizationFallbacks fallbacks)
    {
        var changed = false;
        var position = ConfigValueNormalizer.NormalizePosition(
            window.Position,
            fallbacks.Position,
            options.DefaultOverlayPosition);
        if (!ConfigValueNormalizer.IsFinitePosition(window.Position)
            || Vector2.DistanceSquared(window.Position, position) > 0.25f)
        {
            window.Position = position;
            changed = true;
        }

        var width = ConfigValueNormalizer.NormalizeDimension(
            window.Width,
            fallbacks.Width,
            options.MinOverlayWidth,
            options.MaxOverlayWidth);
        if (!ConfigValueNormalizer.IsFiniteValue(window.Width) || Math.Abs(window.Width - width) > 0.1f)
        {
            window.Width = width;
            changed = true;
        }

        var height = ConfigValueNormalizer.NormalizeDimension(
            window.Height,
            fallbacks.Height,
            options.MinOverlayHeight,
            options.MaxOverlayHeight);
        if (!ConfigValueNormalizer.IsFiniteValue(window.Height) || Math.Abs(window.Height - height) > 0.1f)
        {
            window.Height = height;
            changed = true;
        }

        var iconSize = ConfigValueNormalizer.NormalizeScalar(
            window.IconSize,
            fallbacks.IconSize,
            options.MinIconSize,
            options.MaxIconSize);
        if (!ConfigValueNormalizer.IsFiniteValue(window.IconSize) || Math.Abs(window.IconSize - iconSize) > 0.1f)
        {
            window.IconSize = iconSize;
            changed = true;
        }

        var gap = ConfigValueNormalizer.NormalizeScalar(
            window.Gap,
            fallbacks.Gap,
            options.MinGap,
            options.MaxGap,
            allowZero: true);
        if (!ConfigValueNormalizer.IsFiniteValue(window.Gap) || Math.Abs(window.Gap - gap) > 0.1f)
        {
            window.Gap = gap;
            changed = true;
        }

        var fontScale = ConfigValueNormalizer.NormalizeScalar(
            window.FontScale,
            fallbacks.FontScale,
            options.MinFontScale,
            options.MaxFontScale);
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
        if (!ConfigValueNormalizer.IsFiniteValue(window.OrderEditorHeight)
            || Math.Abs(window.OrderEditorHeight - orderEditorHeight) > 0.1f)
        {
            window.OrderEditorHeight = orderEditorHeight;
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeIconWindowDisplaySettings(IconWindowConfig window)
    {
        var changed = false;
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

        return changed;
    }

    private static bool NormalizeIconWindowTracking(IconWindowConfig window)
    {
        var changed = false;
        if (window.ActiveOrderRow < 0)
        {
            window.ActiveOrderRow = 0;
            changed = true;
        }

        changed |= ConfigMapNormalizer.NormalizeStatusIds(window.TrackedStatusIds);
        changed |= ConfigMapNormalizer.NormalizeStatusIds(window.ExactTrackedStatusIds);
        var trackedStatusIds = window.TrackedStatusIds.ToHashSet();
        if (window.ExactTrackedStatusIds.RemoveAll(statusId => !trackedStatusIds.Contains(statusId)) > 0)
            changed = true;

        window.ExcludedPartyCooldownIds = ConfigMapNormalizer.NormalizeStringList(
            window.ExcludedPartyCooldownIds,
            out var excludedPartyCooldownIdsChanged);
        changed |= excludedPartyCooldownIdsChanged;
        window.ManualTrackingJobs = ConfigMapNormalizer.NormalizeStringList(
            window.ManualTrackingJobs,
            out var manualTrackingJobsChanged);
        changed |= manualTrackingJobsChanged;
        window.TrackedByJob = ConfigMapNormalizer.NormalizeStringListMap(
            window.TrackedByJob,
            out var trackedMapChanged);
        changed |= trackedMapChanged;
        window.ExcludedByJob = ConfigMapNormalizer.NormalizeStringListMap(
            window.ExcludedByJob,
            out var excludedMapChanged);
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
        return changed;
    }

    private static bool NormalizeActiveWindow(PluginConfigData config)
    {
        if (!string.IsNullOrWhiteSpace(config.ActiveWindowId)
            && config.IconWindows.Any(window =>
                string.Equals(window.Id, config.ActiveWindowId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        config.ActiveWindowId = config.IconWindows[0].Id;
        return true;
    }

    private static bool NormalizeWindowCounter(PluginConfigData config)
    {
        var windowCounter = Math.Max(config.WindowCounter, config.IconWindows.Count);
        foreach (var window in config.IconWindows)
            windowCounter = Math.Max(windowCounter, IconWindowIdentity.GetNumber(window.Id));

        if (config.WindowCounter == windowCounter)
            return false;

        config.WindowCounter = windowCounter;
        return true;
    }
}
