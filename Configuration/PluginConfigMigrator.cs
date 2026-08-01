namespace FFXIVAura;

internal static class PluginConfigMigrator
{
    public const int CurrentVersion = 7;

    public static bool Migrate(
        PluginConfigData config,
        PluginConfigNormalizationOptions normalizationOptions,
        Action<string>? onNewerVersionDetected = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var changed = false;
        if (config.Version > CurrentVersion)
        {
            // A newer build wrote this config. We load it as-is (future versions are preserved),
            // but settings introduced by that newer build may be lost the next time this build saves.
            onNewerVersionDetected?.Invoke(
                $"Configuration version {config.Version} is newer than the supported version {CurrentVersion}. "
                + "Loading as-is; settings added by a newer build may be lost when this build saves.");
        }

        if (config.Version < 7)
            changed |= MigrateLegacyManualTrackingModes(config);

        if (config.Version < 2)
        {
            config.OverlayWidth = normalizationOptions.DefaultOverlayWidth;
            config.OverlayHeight = normalizationOptions.DefaultOverlayHeight;
            changed = true;
        }

        if (config.Version < CurrentVersion)
        {
            config.Version = CurrentVersion;
            changed = true;
        }

        changed |= PluginConfigNormalizer.Normalize(config, normalizationOptions);

        return changed;
    }

    private static bool MigrateLegacyManualTrackingModes(PluginConfigData config)
    {
        var changed = SetLegacyManualTrackingJobs(
            config.TrackedByJob,
            config.ManualTrackingJobs,
            jobs => config.ManualTrackingJobs = jobs);

        if (config.IconWindows is null)
            return changed;

        foreach (var window in config.IconWindows)
        {
            if (window is null)
                continue;

            changed |= SetLegacyManualTrackingJobs(
                window.TrackedByJob,
                window.ManualTrackingJobs,
                jobs => window.ManualTrackingJobs = jobs);
        }

        return changed;
    }

    private static bool SetLegacyManualTrackingJobs(
        Dictionary<string, List<string>>? trackedByJob,
        List<string>? existing,
        Action<List<string>> setValue)
    {
        var migrated = new List<string>();
        if (trackedByJob is not null)
        {
            foreach (var (rawJob, tracked) in trackedByJob)
            {
                var job = rawJob?.Trim();
                if (string.IsNullOrWhiteSpace(job)
                    || tracked is null
                    || !tracked.Any(id => !string.IsNullOrWhiteSpace(id))
                    || migrated.Contains(job, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                migrated.Add(job);
            }
        }

        if (existing is not null
            && existing.SequenceEqual(migrated, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        setValue(migrated);
        return true;
    }
}
