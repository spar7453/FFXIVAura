using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace FFXIVAura;

public sealed partial class Plugin
{
    private void PreparePerformanceProfileFrame()
    {
        _ = this.performanceProfileRecordingCoordinator.Prepare(
            this.config.RecordPerformanceProfile,
            this.config.PerformanceProfileRecordIntervalSeconds,
            DateTime.UtcNow);
        if (!this.config.RecordPerformanceProfile)
            this.tooltipDiagnostics.ResetIntervalCounters();
    }

    private void RecordPerformanceProfileIfNeeded(bool includeDiagnostics)
    {
        var timestampUtc = DateTime.UtcNow;
        var recordingStart = Stopwatch.GetTimestamp();
        var allocatedBytesStart = GC.GetAllocatedBytesForCurrentThread();
        var gen0CollectionCountStart = GC.CollectionCount(0);
        string body;
        try
        {
            body = this.BuildPerformanceProfileBody(timestampUtc, includeDiagnostics);
        }
        catch (Exception ex)
        {
            this.CompletePerformanceProfileMeasurement(
                timestampUtc,
                recordingStart,
                allocatedBytesStart,
                gen0CollectionCountStart);
            this.performanceProfileRecordingCoordinator.NoteFailure("batch", ex, DateTime.UtcNow);
            return;
        }

        this.CompletePerformanceProfileMeasurement(
            timestampUtc,
            recordingStart,
            allocatedBytesStart,
            gen0CollectionCountStart);

        try
        {
            this.EnqueuePerformanceProfileRows(timestampUtc, includeDiagnostics, body);
        }
        catch (Exception ex)
        {
            this.performanceProfileRecordingCoordinator.NoteFailure("enqueue", ex, DateTime.UtcNow);
        }
    }

    private void ClearPerformanceProfileFiles()
        => this.performanceProfileRecordingCoordinator.Clear();

    private string GetPerformanceProfileFilePath()
        => this.performanceProfileRecordingCoordinator.FilePath;

    private string BuildPerformanceProfileBody(DateTime timestampUtc, bool includeDiagnostics)
    {
        var tooltipDiagnosticSnapshot = includeDiagnostics
            ? this.tooltipDiagnostics.CreateSnapshot()
            : default;
        var builder = new StringBuilder(4096);
        foreach (var snapshot in this.performanceProfiler.GetSnapshots())
        {
            if (snapshot.Section == PerformanceProfileSection.ProfileRecording)
                continue;

            this.AppendPerformanceProfileSectionRow(builder, timestampUtc, snapshot);
        }

        foreach (var snapshot in this.performanceProfiler.GetWindowSnapshots())
            this.AppendPerformanceProfileWindowRow(builder, timestampUtc, snapshot);

        if (includeDiagnostics)
        {
            try
            {
                this.AppendPerformanceProfileDiagnosticRows(builder, timestampUtc, tooltipDiagnosticSnapshot);
            }
            catch (Exception ex)
            {
                this.performanceProfileRecordingCoordinator.NoteFailure("diagnostic", ex, DateTime.UtcNow);
                this.AppendPerformanceProfileDiagnosticRow(
                    builder,
                    timestampUtc,
                    "profileDiagnosticError",
                    "Profile Diagnostic Error",
                    FormatDiagnosticPairs(("error", ex.GetType().Name)));
            }
        }

        return builder.ToString();
    }

    private void CompletePerformanceProfileMeasurement(
        DateTime timestampUtc,
        long recordingStart,
        long allocatedBytesStart,
        int gen0CollectionCountStart)
    {
        var elapsed = Stopwatch.GetElapsedTime(recordingStart);
        this.performanceStats.IncludePostFrameWork(
            elapsed,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBytesStart,
            GC.CollectionCount(0) - gen0CollectionCountStart);
        this.performanceProfiler.RecordCompletedOperation(
            PerformanceProfileSection.ProfileRecording,
            elapsed,
            timestampUtc);
    }

    private void EnqueuePerformanceProfileRows(
        DateTime timestampUtc,
        bool includeDiagnostics,
        string body)
    {
        var prefixBuilder = new StringBuilder(1024);
        this.AppendPerformanceProfileFrameRow(prefixBuilder, timestampUtc);
        this.AppendPerformanceProfileSectionRow(
            prefixBuilder,
            timestampUtc,
            this.performanceProfiler.GetSnapshot(PerformanceProfileSection.ProfileRecording));

        var maxBytes = (long)PerformanceProfileConfigPolicy.ClampMaxFileMegabytes(
            this.config.PerformanceProfileMaxFileMegabytes) * 1024L * 1024L;
        if (this.performanceProfileRecordingCoordinator.TryAppend(
                prefixBuilder.ToString(),
                body,
                maxBytes,
                timestampUtc))
        {
            if (includeDiagnostics)
                this.tooltipDiagnostics.ResetIntervalCounters();
        }
    }

    private void AppendPerformanceProfileFrameRow(StringBuilder builder, DateTime timestampUtc)
    {
        PerformanceProfileCsv.AppendRow(builder, new PerformanceProfileCsvRow(
            timestampUtc,
            "frame",
            "plugin",
            "Plugin Frame",
            string.Empty,
            this.performanceStats.FrameMilliseconds,
            0,
            this.performanceStats.AverageFrameMilliseconds,
            this.performanceStats.MaxFrameMilliseconds,
            this.performanceStats.MaxFrameOccurredAtUtc,
            0,
            0,
            this.performanceStats.FrameSampleCount,
            this.performanceStats.WindowCount,
            this.performanceStats.SkillIconCount,
            this.performanceStats.AuraIconCount,
            this.performanceStats.CooldownCalculationCount,
            this.actionKeybindService.Count,
            this.performanceStats.TooltipRenderCount,
            this.performanceStats.GrayscaleIconProcessCount,
            this.performanceStats.FrameAllocatedBytes,
            this.performanceStats.AverageFrameAllocatedBytes,
            this.performanceStats.MaxFrameAllocatedBytes,
            this.performanceStats.Gen0CollectionCount));
    }

    private void AppendPerformanceProfileSectionRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PerformanceProfileSectionSnapshot snapshot)
    {
        PerformanceProfileCsv.AppendRow(builder, new PerformanceProfileCsvRow(
            timestampUtc,
            "section",
            snapshot.Section.ToString(),
            snapshot.Label,
            string.Empty,
            snapshot.LastMilliseconds,
            snapshot.RecentAverageMilliseconds,
            snapshot.AverageMilliseconds,
            snapshot.MaxMilliseconds,
            snapshot.MaxOccurredAtUtc,
            snapshot.LastCallCount,
            snapshot.TotalCallCount,
            snapshot.SampleFrameCount,
            this.performanceStats.WindowCount,
            this.performanceStats.SkillIconCount,
            this.performanceStats.AuraIconCount,
            this.performanceStats.CooldownCalculationCount,
            this.actionKeybindService.Count,
            this.performanceStats.TooltipRenderCount,
            this.performanceStats.GrayscaleIconProcessCount));
    }

    private void AppendPerformanceProfileWindowRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PerformanceProfileWindowSnapshot snapshot)
    {
        PerformanceProfileCsv.AppendRow(builder, new PerformanceProfileCsvRow(
            timestampUtc,
            "window",
            snapshot.Id,
            snapshot.Label,
            string.Empty,
            snapshot.LastMilliseconds,
            snapshot.RecentAverageMilliseconds,
            snapshot.AverageMilliseconds,
            snapshot.MaxMilliseconds,
            snapshot.MaxOccurredAtUtc,
            snapshot.LastCallCount,
            snapshot.TotalCallCount,
            snapshot.SampleFrameCount,
            this.performanceStats.WindowCount,
            this.performanceStats.SkillIconCount,
            this.performanceStats.AuraIconCount,
            this.performanceStats.CooldownCalculationCount,
            this.actionKeybindService.Count,
            this.performanceStats.TooltipRenderCount,
            this.performanceStats.GrayscaleIconProcessCount));
    }

    private void AppendPerformanceProfileDiagnosticRows(
        StringBuilder builder,
        DateTime timestampUtc,
        TooltipDiagnosticSnapshot tooltipDiagnostics)
    {
        var playerContext = this.playerFrameContext;
        var job = playerContext.IsPlayerLoaded ? playerContext.Job : string.Empty;
        var partyCooldownRuntimeDiagnostics = this.partyCooldownRuntimeStore.CreateDiagnostics();
        var auraFrameDiagnostics = this.auraFrameService.CreateDiagnostics();

        this.AppendPluginDiagnosticRow(builder, timestampUtc);
        this.AppendPlayerDiagnosticRow(builder, timestampUtc, playerContext);
        this.AppendOverlayDiagnosticRow(builder, timestampUtc);
        this.AppendCacheDiagnosticRow(builder, timestampUtc, partyCooldownRuntimeDiagnostics);
        this.AppendAuraDiagnosticRow(builder, timestampUtc, auraFrameDiagnostics);

        this.AppendPartyCooldownDiagnosticRows(builder, timestampUtc, partyCooldownRuntimeDiagnostics);

        this.AppendGrayscaleDiagnosticRow(builder, timestampUtc);
        this.AppendTooltipDiagnosticRow(builder, timestampUtc, tooltipDiagnostics);
        this.AppendLastEventDiagnosticRow(builder, timestampUtc);

        foreach (var window in this.config.IconWindows)
            this.AppendPerformanceProfileWindowDiagnosticRow(builder, timestampUtc, window, job);
    }

    private void AppendPerformanceProfileWindowDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        IconWindowConfig window,
        string job)
    {
        var trackedCurrentJob = !string.IsNullOrWhiteSpace(job) && window.TrackedByJob.TryGetValue(job, out var tracked)
            ? tracked.Count
            : 0;
        var excludedCurrentJob = !string.IsNullOrWhiteSpace(job) && window.ExcludedByJob.TryGetValue(job, out var excluded)
            ? excluded.Count
            : 0;
        this.overlayWindowDebugSnapshots.TryGetValue(window.Id, out var debug);

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, $"window:{window.Id}", IconWindowPresentation.GetDisplayName(window), FormatDiagnosticPairs(
            ("active", string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase)),
            ("role", window.Role),
            ("displayCondition", window.DisplayCondition),
            ("alignment", window.Alignment),
            ("position", $"{FormatDiagnosticNumber(window.Position.X)}:{FormatDiagnosticNumber(window.Position.Y)}"),
            ("size", $"{FormatDiagnosticNumber(window.Width)}x{FormatDiagnosticNumber(window.Height)}"),
            ("iconSize", window.IconSize),
            ("gap", window.Gap),
            ("fontScale", window.FontScale),
            ("fourPlayerLayoutConfigured", window.FourPlayerLayout is not null),
            ("fourPlayerAlignment", window.FourPlayerLayout?.Alignment.ToString() ?? string.Empty),
            ("fourPlayerPosition", window.FourPlayerLayout is null
                ? string.Empty
                : $"{FormatDiagnosticNumber(window.FourPlayerLayout.Position.X)}:{FormatDiagnosticNumber(window.FourPlayerLayout.Position.Y)}"),
            ("fourPlayerSize", window.FourPlayerLayout is null
                ? string.Empty
                : $"{FormatDiagnosticNumber(window.FourPlayerLayout.Width)}x{FormatDiagnosticNumber(window.FourPlayerLayout.Height)}"),
            ("fourPlayerIconSize", window.FourPlayerLayout?.IconSize ?? 0f),
            ("fourPlayerGap", window.FourPlayerLayout?.Gap ?? 0f),
            ("fourPlayerFontScale", window.FourPlayerLayout?.FontScale ?? 0f),
            ("allianceLayoutConfigured", window.AllianceLayout is not null),
            ("allianceAlignment", window.AllianceLayout?.Alignment.ToString() ?? string.Empty),
            ("alliancePosition", window.AllianceLayout is null
                ? string.Empty
                : $"{FormatDiagnosticNumber(window.AllianceLayout.Position.X)}:{FormatDiagnosticNumber(window.AllianceLayout.Position.Y)}"),
            ("allianceSize", window.AllianceLayout is null
                ? string.Empty
                : $"{FormatDiagnosticNumber(window.AllianceLayout.Width)}x{FormatDiagnosticNumber(window.AllianceLayout.Height)}"),
            ("allianceIconSize", window.AllianceLayout?.IconSize ?? 0f),
            ("allianceGap", window.AllianceLayout?.Gap ?? 0f),
            ("allianceFontScale", window.AllianceLayout?.FontScale ?? 0f),
            ("highlightReady", window.HighlightReady),
            ("highlightAdjusted", window.HighlightAdjusted),
            ("showKeybindText", window.ShowKeybindText),
            ("showMissingAuras", window.ShowMissingAuras),
            ("partyAurasOwnOnly", window.PartyAurasOwnOnly),
            ("showPartyAuraCount", window.ShowPartyAuraCount),
            ("auraSearchShowIndividualIds", window.AuraSearchShowIndividualIds),
            ("trackedCurrentJob", trackedCurrentJob),
            ("excludedCurrentJob", excludedCurrentJob),
            ("manualTrackingCurrentJob", !string.IsNullOrWhiteSpace(job)
                                         && AbilityTrackingService.IsManualTracking(window, job)),
            ("manualTrackingJobs", window.ManualTrackingJobs.Count),
            ("trackedJobs", window.TrackedByJob.Count),
            ("excludedJobs", window.ExcludedByJob.Count),
            ("trackedStatusIds", window.TrackedStatusIds.Count),
            ("exactTrackedStatusIds", window.ExactTrackedStatusIds.Count),
            ("partyCooldownExclusions", window.ExcludedPartyCooldownIds.Count),
            ("iconPositionScopes", window.IconPositionsByJob.Count),
            ("auraPositionScopes", window.AuraPositionsByRole.Count),
            ("debugCaptured", !string.IsNullOrWhiteSpace(debug.WindowId)),
            ("skillLayout", debug.SkillLayoutCount),
            ("skillDisplay", debug.SkillDisplayCount),
            ("skillHiddenByDisplayCondition", debug.SkillHiddenByDisplayConditionCount),
            ("skillReady", debug.SkillReadyCount),
            ("skillCooling", debug.SkillCoolingCount),
            ("skillUnavailable", debug.SkillUnavailableCount),
            ("skillAdjusted", debug.SkillAdjustedCount),
            ("skillTrackedResolveMissing", debug.SkillTrackedResolveMissingCount),
            ("auraLayout", debug.AuraLayoutCount),
            ("auraDisplay", debug.AuraDisplayCount),
            ("auraHiddenByDisplayCondition", debug.AuraHiddenByDisplayConditionCount),
            ("auraPresent", debug.AuraPresentCount),
            ("auraMissing", debug.AuraMissingCount),
            ("partyCandidateItems", debug.PartyCandidateItemCount),
            ("partyDisplayItems", debug.PartyDisplayItemCount),
            ("partyHiddenByDisplayCondition", debug.PartyHiddenByDisplayConditionCount),
            ("partyRows", debug.PartyRowCount),
            ("partyEmptyRows", debug.PartyEmptyRowCount),
            ("partyReady", debug.PartyReadyCount),
            ("partyActive", debug.PartyActiveCount),
            ("partyCooldown", debug.PartyCooldownCount),
            ("partyStatuslessCandidates", debug.PartyStatuslessCandidateCount),
            ("partyLayoutMode", debug.PartyLayoutMode),
            ("partyIconsPerLine", debug.PartyIconsPerLine),
            ("partyAllianceGroups", debug.PartyAllianceGroupCount),
            ("partyAllianceMemberColumns", debug.PartyAllianceMemberColumnCount)));
    }

    private void AppendPerformanceProfileDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        string id,
        string label,
        string detail)
    {
        PerformanceProfileCsv.AppendRow(builder, PerformanceProfileCsv.CreateDiagnosticRow(
            timestampUtc,
            id,
            label,
            detail,
            this.performanceStats.WindowCount,
            this.performanceStats.SkillIconCount,
            this.performanceStats.AuraIconCount,
            this.performanceStats.CooldownCalculationCount,
            this.actionKeybindService.Count,
            this.performanceStats.TooltipRenderCount,
            this.performanceStats.GrayscaleIconProcessCount));
    }

    private static string FormatDiagnosticPairs(params (string Key, object? Value)[] pairs)
        => string.Join(";", pairs.Select(pair => $"{pair.Key}={FormatDiagnosticValue(pair.Value)}"));

    private static string FormatDiagnosticValue(object? value)
    {
        if (value is null)
            return string.Empty;

        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            float floatValue => FormatDiagnosticNumber(floatValue),
            double doubleValue => FormatDiagnosticNumber(doubleValue),
            decimal decimalValue => decimalValue.ToString("0.###", CultureInfo.InvariantCulture),
            DateTime dateTimeValue => dateTimeValue == DateTime.MinValue
                ? string.Empty
                : dateTimeValue.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => SanitizeDiagnosticValue(formattable.ToString(null, CultureInfo.InvariantCulture)),
            _ => SanitizeDiagnosticValue(value.ToString() ?? string.Empty),
        };
    }

    private static string FormatDiagnosticNumber(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatDiagnosticNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatDiagnosticVector(Vector2 value)
        => $"{FormatDiagnosticNumber(value.X)}:{FormatDiagnosticNumber(value.Y)}";

    private static string FormatDiagnosticRect(Vector2 min, Vector2 max)
        => $"{FormatDiagnosticVector(min)}-{FormatDiagnosticVector(max)}";

    private static string SanitizeDiagnosticValue(string value)
        => value.Replace(';', ',')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

}
