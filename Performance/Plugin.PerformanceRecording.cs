using System.Globalization;
using System.Text;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly Encoding PerformanceProfileFileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private void RecordPerformanceProfileIfNeeded()
    {
        if (!this.config.RecordPerformanceProfile)
        {
            this.performanceProfileNextRecordAtUtc = DateTime.MinValue;
            this.tooltipDiagnostics.ResetIntervalCounters();
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var intervalSeconds = Math.Clamp(
            this.config.PerformanceProfileRecordIntervalSeconds,
            MinPerformanceProfileRecordIntervalSeconds,
            MaxPerformanceProfileRecordIntervalSeconds);
        if (this.performanceProfileNextRecordAtUtc != DateTime.MinValue
            && nowUtc < this.performanceProfileNextRecordAtUtc)
        {
            return;
        }

        this.performanceProfileNextRecordAtUtc = nowUtc.AddSeconds(intervalSeconds);

        try
        {
            this.AppendPerformanceProfileRows(nowUtc);
        }
        catch (Exception ex)
        {
            this.LogPerformanceProfileRecordingError(ex);
        }
    }

    private void ClearPerformanceProfileFiles()
    {
        try
        {
            var directory = GetPerformanceProfileDirectory();
            File.Delete(Path.Combine(directory, PerformanceProfileCsv.FileName));
            File.Delete(Path.Combine(directory, PerformanceProfileCsv.PreviousFileName));
            this.performanceProfileNextRecordAtUtc = DateTime.MinValue;
        }
        catch (Exception ex)
        {
            this.LogPerformanceProfileRecordingError(ex);
        }
    }

    private string GetPerformanceProfileFilePath()
        => Path.Combine(GetPerformanceProfileDirectory(), PerformanceProfileCsv.FileName);

    private static string GetPerformanceProfileDirectory()
    {
        var directory = PluginInterface.ConfigDirectory.FullName;
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void AppendPerformanceProfileRows(DateTime timestampUtc)
    {
        var tooltipDiagnosticSnapshot = this.tooltipDiagnostics.CreateSnapshot();
        var path = this.GetPerformanceProfileFilePath();
        this.RotatePerformanceProfileFileIfNeeded(path);
        this.RotatePerformanceProfileFileIfHeaderChanged(path);

        var fileExists = File.Exists(path);
        var fileIsEmpty = !fileExists || new FileInfo(path).Length == 0;
        var builder = new StringBuilder(4096);
        if (fileIsEmpty)
            builder.AppendLine(PerformanceProfileCsv.Header);

        this.AppendPerformanceProfileFrameRow(builder, timestampUtc);
        foreach (var snapshot in this.performanceProfiler.GetSnapshots())
            this.AppendPerformanceProfileSectionRow(builder, timestampUtc, snapshot);

        foreach (var snapshot in this.performanceProfiler.GetWindowSnapshots())
            this.AppendPerformanceProfileWindowRow(builder, timestampUtc, snapshot);

        this.AppendPerformanceProfileDiagnosticRows(builder, timestampUtc, tooltipDiagnosticSnapshot);

        File.AppendAllText(path, builder.ToString(), PerformanceProfileFileEncoding);
        this.tooltipDiagnostics.ResetIntervalCounters();
    }

    private void RotatePerformanceProfileFileIfNeeded(string path)
    {
        if (!File.Exists(path))
            return;

        var maxBytes = (long)Math.Clamp(
            this.config.PerformanceProfileMaxFileMegabytes,
            MinPerformanceProfileMaxFileMegabytes,
            MaxPerformanceProfileMaxFileMegabytes) * 1024L * 1024L;
        if (new FileInfo(path).Length < maxBytes)
            return;

        RotatePerformanceProfileFileToPrevious(path);
    }

    private void RotatePerformanceProfileFileIfHeaderChanged(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            return;

        using var reader = new StreamReader(path, PerformanceProfileFileEncoding, detectEncodingFromByteOrderMarks: true);
        var header = reader.ReadLine();
        if (string.Equals(header, PerformanceProfileCsv.Header, StringComparison.Ordinal))
            return;

        RotatePerformanceProfileFileToPrevious(path);
    }

    private static void RotatePerformanceProfileFileToPrevious(string path)
    {
        var previousPath = Path.Combine(Path.GetDirectoryName(path)!, PerformanceProfileCsv.PreviousFileName);
        if (File.Exists(previousPath))
            File.Delete(previousPath);

        File.Move(path, previousPath);
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
            this.actionKeybindIndex.Count,
            this.performanceStats.NativeTooltipControlCount,
            this.performanceStats.GrayscaleIconProcessCount));
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
            this.actionKeybindIndex.Count,
            this.performanceStats.NativeTooltipControlCount,
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
            this.actionKeybindIndex.Count,
            this.performanceStats.NativeTooltipControlCount,
            this.performanceStats.GrayscaleIconProcessCount));
    }

    private void AppendPerformanceProfileDiagnosticRows(
        StringBuilder builder,
        DateTime timestampUtc,
        TooltipDiagnosticSnapshot tooltipDiagnostics)
    {
        var playerLoaded = PlayerState.IsLoaded;
        var job = playerLoaded ? JobInfo.Code(PlayerState.ClassJob.RowId) : string.Empty;
        var level = playerLoaded ? PlayerState.Level : 0;
        var effectiveLevel = playerLoaded ? this.GetCurrentEffectiveLevel() : 0;
        var betweenAreas = Condition[ConditionFlag.BetweenAreas];
        var betweenAreas51 = Condition[ConditionFlag.BetweenAreas51];
        var loadingSuppressed = this.config.HideDuringZoneLoad
                                && (betweenAreas || betweenAreas51 || this.zoneLoadActive || timestampUtc < this.zoneLoadHiddenUntil);

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "plugin", "Plugin", FormatDiagnosticPairs(
            ("enabled", this.config.Enabled),
            ("configVisible", this.configVisible),
            ("configSavePending", this.configSavePending),
            ("lockOverlay", this.config.LockOverlay),
            ("hideDuringZoneLoad", this.config.HideDuringZoneLoad),
            ("showTooltips", this.config.ShowTooltips),
            ("showPerformanceOverlay", this.config.ShowPerformanceOverlay),
            ("showDetailedProfile", this.config.ShowDetailedPerformanceProfile),
            ("recordProfile", this.config.RecordPerformanceProfile),
            ("recordIntervalSec", this.config.PerformanceProfileRecordIntervalSeconds),
            ("maxFileMb", this.config.PerformanceProfileMaxFileMegabytes),
            ("logObserver", this.config.ShowPartyCooldownLogObserver)));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "player", "Player", FormatDiagnosticPairs(
            ("loggedIn", ClientState.IsLoggedIn),
            ("playerLoaded", playerLoaded),
            ("localPlayer", ObjectTable.LocalPlayer is not null),
            ("job", job),
            ("level", level),
            ("effectiveLevel", effectiveLevel),
            ("inCombat", this.IsInCombat()),
            ("betweenAreas", betweenAreas),
            ("betweenAreas51", betweenAreas51),
            ("loadingSuppressed", loadingSuppressed),
            ("target", TargetManager.Target is not null),
            ("softTarget", TargetManager.SoftTarget is not null)));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "overlay", "Overlay", FormatDiagnosticPairs(
            ("configuredWindows", this.config.IconWindows.Count),
            ("activeWindowId", this.config.ActiveWindowId),
            ("drawnWindows", this.performanceStats.WindowCount),
            ("drawnSkillIcons", this.performanceStats.SkillIconCount),
            ("drawnAuraIcons", this.performanceStats.AuraIconCount),
            ("skillWindows", this.config.IconWindows.Count(window => window.Role == IconWindowRole.SkillCooldowns)),
            ("standardAuraWindows", this.config.IconWindows.Count(window => IconWindowRoles.IsStandardAuraRole(window.Role))),
            ("partyCooldownWindows", this.config.IconWindows.Count(window => IconWindowRoles.IsPartyCooldownRole(window.Role))),
            ("auraSearchVisible", this.auraSearchWindowVisible),
            ("auraSearchWindowId", this.auraSearchWindowId ?? string.Empty),
            ("draggedTrackedId", this.draggedTrackedId ?? string.Empty),
            ("draggedOverlayId", this.draggedOverlayId ?? string.Empty)));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "cache", "Cache", FormatDiagnosticPairs(
            ("cooldownFrame", this.cooldownFrameCache.Count),
            ("visibleAbilityKeys", this.visibleAbilityKeys.Count),
            ("jobCandidates", this.jobCandidatesCache.Count),
            ("gameActionCandidates", this.gameActionCandidatesCache.Count),
            ("actionRows", this.actionRowCache.Count),
            ("actionCategories", this.actionCategoryCache.Count),
            ("actionEquivalenceGroups", this.actionEquivalenceGroupCache.Count),
            ("statusDefinitions", this.statusDefinitionCache.Count),
            ("statusTooltips", this.statusTooltipTextCache.Count),
            ("hotbarVisibility", this.hotbarVisibilityCache.Count),
            ("missingActionRows", this.missingActionRows.Count),
            ("keybindDirty", this.keybindCacheDirty),
            ("keybindRefreshAfterLocal", this.keybindCacheRefreshAfter == DateTime.MinValue ? string.Empty : this.keybindCacheRefreshAfter.ToLocalTime())));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "aura", "Aura", FormatDiagnosticPairs(
            ("playerCacheValid", this.playerAuraFrameCacheValid),
            ("playerCacheCount", this.playerAuraFrameCache.Count),
            ("targetCacheValid", this.targetAuraFrameCacheValid),
            ("targetCacheCount", this.targetAuraFrameCache.Count),
            ("partyAllCacheValid", this.partyAuraFrameAllCacheValid),
            ("partyAllCacheCount", this.partyAuraFrameAllCache.Count),
            ("partyOwnCacheValid", this.partyAuraFrameOwnCacheValid),
            ("partyOwnCacheCount", this.partyAuraFrameOwnCache.Count),
            ("visibleAuraScopes", this.visibleAurasByScope.Count),
            ("visibleAuraIds", this.visibleAurasByScope.Values.Sum(statusIds => statusIds.Count)),
            ("firstSeenScopes", this.auraFirstSeenByScope.Count),
            ("firstSeenStatusIds", this.auraFirstSeenByScope.Values.Sum(statuses => statuses.Count)),
            ("pendingStatusId", this.pendingStatusId)));

        var partyLogTracked = 0;
        var partyLogIgnored = 0;
        var partyLogOther = 0;
        PartyCooldownLogObservation? lastObservation = null;
        foreach (var observation in this.partyCooldownLogObservations)
        {
            lastObservation = observation;
            if (string.Equals(observation.Result, "추적", StringComparison.Ordinal))
                partyLogTracked++;
            else if (string.Equals(observation.Result, "무시", StringComparison.Ordinal))
                partyLogIgnored++;
            else
                partyLogOther++;
        }

        var partyCooldownRoster = this.partyCooldownFrameSnapshot?.RosterDiagnostics ?? default;
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldown", "Party Cooldown", FormatDiagnosticPairs(
            ("partyListLength", PartyList.Length),
            ("partyId", PartyList.PartyId),
            ("localAllianceGroup", PartyCooldownAllianceGroups.OwnPartyLabel(PartyList.IsAlliance, (int)PartyList.PartyId)),
            ("rosterSource", partyCooldownRoster.Source),
            ("rosterReadMode", partyCooldownRoster.ReadMode),
            ("definitions", this.partyCooldownDefinitions.Count),
            ("runtimeStates", this.partyCooldownRuntimeStates.Count),
            ("activeStatuses", this.partyCooldownActiveStatusFrameCache.Count),
            ("frameSnapshot", this.partyCooldownFrameSnapshot is not null),
            ("frameMembers", this.partyCooldownFrameSnapshot?.Members.Count ?? 0),
            ("displayMembers", this.partyCooldownFrameSnapshot?.DisplayMembers.Count ?? 0),
            ("excludedLocalPlayer", partyCooldownRoster.ExcludedLocalPlayer),
            ("alliancePartyCount", partyCooldownRoster.AlliancePartyCount),
            ("allianceMembers", partyCooldownRoster.AllianceMemberCount),
            ("hasAllianceSource", partyCooldownRoster.HasAllianceSource),
            ("allianceA", partyCooldownRoster.AllianceGroupAMemberCount),
            ("allianceB", partyCooldownRoster.AllianceGroupBMemberCount),
            ("allianceC", partyCooldownRoster.AllianceGroupCMemberCount),
            ("allianceEmptySlots", partyCooldownRoster.AllianceEmptySlotCount),
            ("usedFlatFallback", partyCooldownRoster.UsedFlatAllianceFallback),
            ("liveRuntimeKeys", this.partyCooldownLiveRuntimeKeysFrameCache?.Count ?? 0),
            ("logObservations", this.partyCooldownLogObservations.Count),
            ("logTracked", partyLogTracked),
            ("logIgnored", partyLogIgnored),
            ("logOther", partyLogOther)));

        if (lastObservation is not null)
        {
            this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldownLastLog", "Party Cooldown Last Log", FormatDiagnosticPairs(
                ("timeLocal", lastObservation.TimestampUtc.ToLocalTime()),
                ("result", lastObservation.Result),
                ("source", lastObservation.SourceName),
                ("member", lastObservation.MemberName),
                ("action", lastObservation.ActionName),
                ("actionId", lastObservation.ActionId),
                ("match", lastObservation.MatchSource),
                ("detail", lastObservation.Detail)));
        }

        var grayscaleQueueCount = 0;
        var grayscalePendingCount = 0;
        var grayscaleFailedCount = 0;
        lock (this.grayscaleIconLock)
        {
            grayscaleQueueCount = this.grayscaleIconQueue.Count;
            grayscalePendingCount = this.grayscaleIconPending.Count;
            grayscaleFailedCount = this.grayscaleIconFailed.Count;
        }

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "grayscale", "Grayscale", FormatDiagnosticPairs(
            ("cache", this.grayscaleIconCache.Count),
            ("queue", grayscaleQueueCount),
            ("pending", grayscalePendingCount),
            ("failed", grayscaleFailedCount),
            ("processedThisFrame", this.performanceStats.GrayscaleIconProcessCount)));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "tooltip", "Tooltip", FormatDiagnosticPairs(
            ("showTooltips", this.config.ShowTooltips),
            ("requestedThisFrame", this.overlayTooltipRequestedThisFrame),
            ("graceFramesRemaining", this.overlayTooltipGraceFramesRemaining),
            ("nativeControlsThisFrame", this.performanceStats.NativeTooltipControlCount),
            ("hoverHits", tooltipDiagnostics.HoverHits),
            ("abilityRequests", tooltipDiagnostics.AbilityRequests),
            ("auraRequests", tooltipDiagnostics.AuraRequests),
            ("partyCooldownRequests", tooltipDiagnostics.PartyCooldownRequests),
            ("nativeActionRequests", tooltipDiagnostics.NativeActionRequests),
            ("disabledSkips", tooltipDiagnostics.DisabledSkips),
            ("zeroActionSkips", tooltipDiagnostics.ZeroActionSkips),
            ("agentMissingSkips", tooltipDiagnostics.AgentMissingSkips),
            ("addonMissingSkips", tooltipDiagnostics.AddonMissingSkips),
            ("nativeControls", tooltipDiagnostics.NativeControls),
            ("lastKind", tooltipDiagnostics.LastKind),
            ("lastId", tooltipDiagnostics.LastId),
            ("lastWindowId", tooltipDiagnostics.LastWindowId),
            ("lastIconId", tooltipDiagnostics.LastIconId),
            ("lastActionId", tooltipDiagnostics.LastActionId),
            ("lastDrawnIconCount", tooltipDiagnostics.LastDrawnIconCount),
            ("lastHitboxExpanded", tooltipDiagnostics.LastHitboxExpanded),
            ("lastSkip", tooltipDiagnostics.LastSkipReason),
            ("lastTimeLocal", tooltipDiagnostics.LastEventUtc == DateTime.MinValue ? string.Empty : tooltipDiagnostics.LastEventUtc.ToLocalTime()),
            ("lastMouse", tooltipDiagnostics.HasLastGeometry ? FormatDiagnosticVector(tooltipDiagnostics.LastMouse) : string.Empty),
            ("lastRect", tooltipDiagnostics.HasLastGeometry ? FormatDiagnosticRect(tooltipDiagnostics.LastRectMin, tooltipDiagnostics.LastRectMax) : string.Empty),
            ("lastContainsMouse", tooltipDiagnostics.HasLastGeometry && tooltipDiagnostics.LastContainsMouse),
            ("lastImGuiHovered", tooltipDiagnostics.HasLastGeometry && tooltipDiagnostics.LastImGuiHovered),
            ("agentActionId", tooltipDiagnostics.LastAgentActionId),
            ("agentOriginalId", tooltipDiagnostics.LastAgentOriginalId),
            ("addonCaptured", tooltipDiagnostics.HasLastNativeAddon),
            ("addonVisible", tooltipDiagnostics.HasLastNativeAddon && tooltipDiagnostics.LastAddonVisible),
            ("addonSize", tooltipDiagnostics.HasLastNativeAddon ? FormatDiagnosticVector(tooltipDiagnostics.LastAddonSize) : string.Empty),
            ("lastControlMouse", tooltipDiagnostics.HasLastNativeAddon ? FormatDiagnosticVector(tooltipDiagnostics.LastControlMouse) : string.Empty),
            ("lastControlPosition", tooltipDiagnostics.HasLastNativeAddon ? FormatDiagnosticVector(tooltipDiagnostics.LastControlPosition) : string.Empty)));

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "lastEvent", "Last Event", FormatDiagnosticPairs(
            ("timeLocal", this.lastBugDiagnosticEventAtUtc == DateTime.MinValue ? string.Empty : this.lastBugDiagnosticEventAtUtc.ToLocalTime()),
            ("event", this.lastBugDiagnosticEvent)));

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

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, $"window:{window.Id}", GetIconWindowDisplayName(window), FormatDiagnosticPairs(
            ("active", string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase)),
            ("role", window.Role),
            ("displayCondition", window.DisplayCondition),
            ("alignment", window.Alignment),
            ("position", $"{FormatDiagnosticNumber(window.Position.X)}:{FormatDiagnosticNumber(window.Position.Y)}"),
            ("size", $"{FormatDiagnosticNumber(window.Width)}x{FormatDiagnosticNumber(window.Height)}"),
            ("iconSize", window.IconSize),
            ("gap", window.Gap),
            ("fontScale", window.FontScale),
            ("highlightReady", window.HighlightReady),
            ("highlightAdjusted", window.HighlightAdjusted),
            ("showKeybindText", window.ShowKeybindText),
            ("showMissingAuras", window.ShowMissingAuras),
            ("partyAurasOwnOnly", window.PartyAurasOwnOnly),
            ("showPartyAuraCount", window.ShowPartyAuraCount),
            ("trackedCurrentJob", trackedCurrentJob),
            ("excludedCurrentJob", excludedCurrentJob),
            ("trackedJobs", window.TrackedByJob.Count),
            ("excludedJobs", window.ExcludedByJob.Count),
            ("trackedStatusIds", window.TrackedStatusIds.Count),
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
            ("partyStatuslessCandidates", debug.PartyStatuslessCandidateCount)));
    }

    private void AppendPerformanceProfileDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        string id,
        string label,
        string detail)
    {
        PerformanceProfileCsv.AppendRow(builder, new PerformanceProfileCsvRow(
            timestampUtc,
            "diagnostic",
            id,
            label,
            detail,
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
            this.actionKeybindIndex.Count,
            this.performanceStats.NativeTooltipControlCount,
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

    private void LogPerformanceProfileRecordingError(Exception ex)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc < this.performanceProfileNextErrorLogAtUtc)
            return;

        this.performanceProfileNextErrorLogAtUtc = nowUtc.AddSeconds(30);
        Log.Debug(ex, "Failed to record FFXIVAura performance profile.");
    }
}
