using System.Globalization;
using System.Text;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private void AppendPluginDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "plugin", "Plugin", FormatDiagnosticPairs(
            ("enabled", this.config.Enabled),
            ("configVisible", this.configVisible),
            ("configSavePending", this.configSavePending),
            ("configSaveDeferredInCombat", this.configSaveDeferredInCombat),
            ("configSavePendingSec", GetConfigSavePendingSeconds(timestampUtc, this.configSaveQueuedAtUtc)),
            ("configSaveQueue", this.configSaveWorker.PendingCount),
            ("configSaveDropped", this.configSaveWorker.DroppedCount),
            ("configSaveCompleted", this.configSaveWorker.CompletedCount),
            ("configSaveFailed", this.configSaveWorker.FailedCount),
            ("configSaveLastMs", this.configSaveWorker.LastSaveMilliseconds),
            ("configSaveMaxMs", this.configSaveWorker.MaxSaveMilliseconds),
            ("lockOverlay", this.config.LockOverlay),
            ("hideDuringZoneLoad", this.config.HideDuringZoneLoad),
            ("showTooltips", this.config.ShowTooltips),
            ("showPerformanceOverlay", this.config.ShowPerformanceOverlay),
            ("showDetailedProfile", this.config.ShowDetailedPerformanceProfile),
            ("recordProfile", this.config.RecordPerformanceProfile),
            ("recordIntervalSec", this.config.PerformanceProfileRecordIntervalSeconds),
            ("maxFileMb", this.config.PerformanceProfileMaxFileMegabytes),
            ("profileWritePending", this.performanceProfileWriter.PendingCount),
            ("profileWriteDropped", this.performanceProfileWriter.DroppedCount),
            ("profileWriteCompleted", this.performanceProfileWriter.CompletedCount),
            ("profileWriteFailed", this.performanceProfileWriter.FailedCount),
            ("profileWriteLastMs", this.performanceProfileWriter.LastWriteMilliseconds),
            ("profileWriteMaxMs", this.performanceProfileWriter.MaxWriteMilliseconds),
            ("profileWriteLastCompletedLocal", this.performanceProfileWriter.LastCompletedAtUtc == DateTime.MinValue
                ? string.Empty
                : this.performanceProfileWriter.LastCompletedAtUtc.ToLocalTime()),
            ("profileFailureCount", this.performanceProfileFailureCount),
            ("profileLastError", this.performanceProfileLastError),
            ("profileLastErrorLocal", this.performanceProfileLastErrorAtUtc == DateTime.MinValue
                ? string.Empty
                : this.performanceProfileLastErrorAtUtc.ToLocalTime()),
            ("logObserver", this.config.ShowPartyCooldownLogObserver),
            ("partyCooldownLayoutEditMode", this.config.PartyCooldownLayoutEditMode)));
    }

    private void AppendPlayerDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        bool playerLoaded,
        string job)
    {
        var level = playerLoaded ? PlayerState.Level : 0;
        var effectiveLevel = playerLoaded ? this.GetCurrentEffectiveLevel() : 0;
        var betweenAreas = Condition[ConditionFlag.BetweenAreas];
        var betweenAreas51 = Condition[ConditionFlag.BetweenAreas51];
        var loadingSuppressed = this.config.HideDuringZoneLoad
                                && (betweenAreas || betweenAreas51 || this.zoneLoadActive || timestampUtc < this.zoneLoadHiddenUntil);

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
    }

    private void AppendOverlayDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
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
    }

    private void AppendCacheDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownRuntimeStoreDiagnostics partyCooldownRuntimeDiagnostics)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "cache", "Cache", FormatDiagnosticPairs(
            ("cooldownFrame", this.cooldownFrameCache.Count),
            ("visibleAbilityKeys", this.visibleAbilityKeys.Count),
            ("transientSkillLayouts", this.transientSkillPositionsByGroup.Count),
            ("jobCandidates", this.jobCandidatesCache.Count),
            ("gameActionCandidates", this.gameActionCandidatesCache.Count),
            ("actionRows", this.actionRowCache.Count),
            ("actionCategories", this.actionCategoryCache.Count),
            ("actionEquivalenceGroups", this.actionEquivalenceGroupCache.Count),
            ("statusDefinitions", this.statusDefinitionCache.Count),
            ("statusIdentityIndexBuilt", this.statusIdentityIndexState.IsBuilt),
            ("statusIdentityIndexGeneration", this.statusIdentityIndexState.Generation),
            ("statusSearchIndexBuilt", this.allStatusSearchIndexBuilt),
            ("statusSearchEntries", this.allStatusSearchIndex.Count),
            ("statusIdentityGroups", this.statusIdsByGroupIndex.Count),
            ("actionAuraIndexBuilt", this.actionGrantedStatusSearchIndexState.IsBuilt),
            ("actionAuraIndexGeneration", this.actionGrantedStatusSearchIndexState.Generation),
            ("actionAuraEntries", this.actionGrantedStatusSearchIndex.Count),
            ("actionAuraStatusBuckets", this.actionGrantedStatusSearchIndexByStatusId.Count),
            ("actionAuraQueryCache", this.actionGrantedAuraSearchQueryCache.Count),
            ("statusSearchQueryCache", this.allStatusSearchQueryCache.Count),
            ("auraSearchResultCache", this.auraSearchResultCacheByWindow.Count),
            ("auraSearchResultCacheHits", this.auraSearchResultCacheHitCount),
            ("auraSearchResultCacheMisses", this.auraSearchResultCacheMissCount),
            ("partyRowBuffers", partyCooldownRuntimeDiagnostics.RowBufferCount),
            ("partyRowMemberBuffers", partyCooldownRuntimeDiagnostics.RowMemberBufferCount),
            ("statusTooltips", this.statusTooltipTextCache.Count),
            ("statusFallbacks", this.statusSnapshotFallbackCache.Count),
            ("gameObjectOwners", this.gameObjectOwnerFrameCache.Count),
            ("hotbarVisibility", this.hotbarVisibilityCache.Count),
            ("missingActionRows", this.missingActionRows.Count),
            ("keybindDirty", this.keybindCacheDirty),
            ("keybindRefreshAfterLocal", this.keybindCacheRefreshAfter == DateTime.MinValue ? string.Empty : this.keybindCacheRefreshAfter.ToLocalTime())));
    }

    private void AppendAuraDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyAuraRuntimeDiagnostics partyAuraRuntimeDiagnostics)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "aura", "Aura", FormatDiagnosticPairs(
            ("playerCacheValid", this.playerAuraFrameCacheValid),
            ("playerCacheCount", this.playerAuraFrameCache.Count),
            ("targetCacheValid", this.targetAuraFrameCacheValid),
            ("targetCacheCount", this.targetAuraFrameCache.Count),
            ("partyAllCacheValid", this.partyAuraFrameAllCacheValid),
            ("partyAllCacheCount", this.partyAuraFrameAllCache.Count),
            ("partyOwnCacheValid", this.partyAuraFrameOwnCacheValid),
            ("partyOwnCacheCount", this.partyAuraFrameOwnCache.Count),
            ("partyTimerStates", partyAuraRuntimeDiagnostics.TimerStateCount),
            ("partyFallbackBatches", partyAuraRuntimeDiagnostics.FallbackBatchCount),
            ("partyTimerRefreshAccepted", partyAuraRuntimeDiagnostics.RefreshAcceptedCount),
            ("partyExpiredStatusSuppressed", partyAuraRuntimeDiagnostics.ExpiredStatusSuppressedCount),
            ("partyTimerLastDecision", partyAuraRuntimeDiagnostics.LastDecision),
            ("visibleAuraScopes", this.visibleAurasByScope.Count),
            ("visibleAuraIds", this.visibleAurasByScope.Values.Sum(statusIds => statusIds.Count)),
            ("firstSeenScopes", this.auraFirstSeenByScope.Count),
            ("firstSeenStatusIds", this.auraFirstSeenByScope.Values.Sum(statuses => statuses.Count)),
            ("auraSearchRevisionScopes", this.auraSearchStateRevisionByScope.Count),
            ("pendingStatusId", this.pendingStatusId)));
    }

    private void AppendPartyCooldownDiagnosticRows(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownRuntimeStoreDiagnostics partyCooldownRuntimeDiagnostics)
    {
        var lastObservation = this.partyCooldownLogObservations.LastActionable;
        var logSummary = PartyCooldownLogDiagnosticSummary.Create(
            this.partyCooldownLogObservations.Actionable,
            this.partyCooldownLogObservations.CandidateCount);
        this.AppendPartyCooldownDiagnosticRow(
            builder,
            timestampUtc,
            partyCooldownRuntimeDiagnostics,
            logSummary);

        if (lastObservation is not null)
            this.AppendPartyCooldownLastLogDiagnosticRow(builder, timestampUtc, lastObservation);

        var lastCandidateObservation = this.partyCooldownLogObservations.LastCandidate;
        if (lastCandidateObservation is not null)
            this.AppendPartyCooldownLastUnknownLogDiagnosticRow(builder, timestampUtc, lastCandidateObservation);
    }

    private void AppendPartyCooldownDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownRuntimeStoreDiagnostics partyCooldownRuntimeDiagnostics,
        PartyCooldownLogDiagnosticSummary logSummary)
    {
        var partyCooldownRoster = this.partyCooldownFrameSnapshot?.RosterDiagnostics ?? default;
        IReadOnlyList<PartyCooldownMemberSnapshot> diagnosticMembers =
            this.partyCooldownFrameSnapshot?.Members ?? Array.Empty<PartyCooldownMemberSnapshot>();
        var partyListHeader = this.GetPartyListHeader();
        var activeStatusDiagnostics = this.partyCooldownActiveStatusIndex.CreateDiagnostics();
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldown", "Party Cooldown", FormatDiagnosticPairs(
            ("partyListLength", partyListHeader.Length),
            ("partyId", partyListHeader.PartyId),
            ("localAllianceGroup", PartyCooldownAllianceGroups.OwnPartyLabel(partyListHeader.IsAlliance, partyCooldownRoster.LocalAllianceGroupIndex)),
            ("localAllianceGroupIndex", partyCooldownRoster.LocalAllianceGroupIndex),
            ("rawLocalAllianceGroupIndex", partyCooldownRoster.RawLocalAllianceGroupIndex),
            ("hudLocalAllianceGroupIndex", partyCooldownRoster.HudLocalAllianceGroupIndex),
            ("observedHudLocalAllianceGroupIndex", partyCooldownRoster.ObservedHudLocalAllianceGroupIndex),
            ("usedRetainedHudAllianceGroup", partyCooldownRoster.UsedRetainedHudAllianceGroup),
            ("hudAllianceOrderCount", partyCooldownRoster.HudAllianceOrderCount),
            ("crossRealmGroupCount", partyCooldownRoster.CrossRealmGroupCount),
            ("rosterSource", partyCooldownRoster.Source),
            ("rosterReadMode", partyCooldownRoster.ReadMode),
            ("definitions", this.partyCooldownDefinitions.Count),
            ("runtimeStates", partyCooldownRuntimeDiagnostics.RuntimeStateCount),
            ("activeStatuses", activeStatusDiagnostics.StatusCount),
            ("activePartyListSource", activeStatusDiagnostics.PartyListSourceCount),
            ("activePartyListRecipient", activeStatusDiagnostics.PartyListRecipientCount),
            ("activeObjectSource", activeStatusDiagnostics.ObjectSourceCount),
            ("activeObjectRecipient", activeStatusDiagnostics.ObjectRecipientCount),
            ("activeLiveStatuses", activeStatusDiagnostics.LiveStatusCount),
            ("activeFallbackStatuses", activeStatusDiagnostics.FallbackStatusCount),
            ("activeAbsenceConfirmed", activeStatusDiagnostics.AbsenceConfirmed),
            ("liveStatusOwners", activeStatusDiagnostics.LiveOwnerCount),
            ("statusFallbackBatches", this.partyCooldownStatusFallbackBatchCount),
            ("activeTimerRefreshAccepted", this.partyCooldownTimerRefreshAcceptedCount),
            ("activeTimerStaleSuppressed", this.partyCooldownTimerStalePositiveSuppressedCount),
            ("activeTimerLastDecision", this.partyCooldownTimerLastDecision),
            ("statusScansThisFrame", this.performanceStats.PartyStatusScanCount),
            ("statusCacheHitsThisFrame", this.performanceStats.PartyStatusCacheHitCount),
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
            ("rosterOrderHash", PartyCooldownRoster.ComputeMemberIdentityHash(diagnosticMembers).ToString("X16", CultureInfo.InvariantCulture)),
            ("rosterCacheHits", this.partyCooldownRosterService.CacheHitCount),
            ("rosterCacheMisses", this.partyCooldownRosterService.CacheMissCount),
            ("allianceAOrderHash", PartyCooldownRoster.ComputeAllianceGroupIdentityHash(diagnosticMembers, "A").ToString("X16", CultureInfo.InvariantCulture)),
            ("allianceBOrderHash", PartyCooldownRoster.ComputeAllianceGroupIdentityHash(diagnosticMembers, "B").ToString("X16", CultureInfo.InvariantCulture)),
            ("allianceCOrderHash", PartyCooldownRoster.ComputeAllianceGroupIdentityHash(diagnosticMembers, "C").ToString("X16", CultureInfo.InvariantCulture)),
            ("usedFlatFallback", partyCooldownRoster.UsedFlatAllianceFallback),
            ("liveRuntimeKeys", partyCooldownRuntimeDiagnostics.LiveRuntimeKeyCount),
            ("logObservations", this.partyCooldownLogObservations.ActionableCount),
            ("candidateObservations", this.partyCooldownLogObservations.CandidateCount),
            ("candidateMissingTotal", this.partyCooldownCandidateMissingLogCount),
            ("candidateMissingSamples", this.partyCooldownCandidateMissingObservationCount),
            ("logLocalPlayerSkippedTotal", this.partyCooldownLocalPlayerLogSkippedCount),
            ("logLocalOwnedObjectSkippedTotal", this.partyCooldownLocalOwnedObjectLogSkippedCount),
            ("logTracked", logSummary.TrackedCount),
            ("logIgnored", logSummary.IgnoredCount),
            ("logMemberNotFound", logSummary.MemberNotFoundCount),
            ("logOwnerNotFound", logSummary.OwnerNotFoundCount),
            ("logCandidateMissing", logSummary.CandidateMissingCount),
            ("logNotUsableForJob", logSummary.NotUsableForJobCount),
            ("logNotTrackedByWindow", logSummary.NotTrackedByWindowCount),
            ("logAmbiguous", logSummary.AmbiguousCount),
            ("logOtherIgnored", logSummary.OtherIgnoredCount),
            ("logOther", logSummary.OtherResultCount)));
    }

    private void AppendPartyCooldownLastLogDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownLogObservation observation)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldownLastLog", "Party Cooldown Last Log", FormatDiagnosticPairs(
            ("timeLocal", observation.TimestampUtc.ToLocalTime()),
            ("result", observation.Result),
            ("source", observation.SourceName),
            ("member", observation.MemberName),
            ("action", observation.ActionName),
            ("actionId", observation.ActionId),
            ("match", observation.MatchSource),
            ("reason", observation.IgnoredReason),
            ("rosterSource", observation.RosterDiagnostics.Source),
            ("rosterReadMode", observation.RosterDiagnostics.ReadMode),
            ("rosterMembers", observation.RosterDiagnostics.MemberCount),
            ("displayMembers", observation.RosterDiagnostics.DisplayMemberCount),
            ("allianceMembers", observation.RosterDiagnostics.AllianceMemberCount),
            ("allianceA", observation.RosterDiagnostics.AllianceGroupAMemberCount),
            ("allianceB", observation.RosterDiagnostics.AllianceGroupBMemberCount),
            ("allianceC", observation.RosterDiagnostics.AllianceGroupCMemberCount),
            ("allianceEmptySlots", observation.RosterDiagnostics.AllianceEmptySlotCount),
            ("localAllianceGroupIndex", observation.RosterDiagnostics.LocalAllianceGroupIndex),
            ("rawLocalAllianceGroupIndex", observation.RosterDiagnostics.RawLocalAllianceGroupIndex),
            ("hudLocalAllianceGroupIndex", observation.RosterDiagnostics.HudLocalAllianceGroupIndex),
            ("observedHudLocalAllianceGroupIndex", observation.RosterDiagnostics.ObservedHudLocalAllianceGroupIndex),
            ("usedRetainedHudAllianceGroup", observation.RosterDiagnostics.UsedRetainedHudAllianceGroup),
            ("hudAllianceOrderCount", observation.RosterDiagnostics.HudAllianceOrderCount),
            ("crossRealmGroupCount", observation.RosterDiagnostics.CrossRealmGroupCount),
            ("usedFlatFallback", observation.RosterDiagnostics.UsedFlatAllianceFallback),
            ("detail", observation.Detail)));
    }

    private void AppendPartyCooldownLastUnknownLogDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownLogObservation observation)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldownLastUnknownLog", "Party Cooldown Last Unknown Log", FormatDiagnosticPairs(
            ("timeLocal", observation.TimestampUtc.ToLocalTime()),
            ("logMessageId", observation.LogMessageId),
            ("source", observation.SourceName),
            ("detail", observation.Detail)));
    }

    private void AppendGrayscaleDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
        var queueCount = 0;
        var pendingCount = 0;
        var failedCount = 0;
        lock (this.grayscaleIconLock)
        {
            queueCount = this.grayscaleIconQueue.Count;
            pendingCount = this.grayscaleIconPending.Count;
            failedCount = this.grayscaleIconFailed.Count;
        }

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "grayscale", "Grayscale", FormatDiagnosticPairs(
            ("cache", this.grayscaleIconCache.Count),
            ("queue", queueCount),
            ("pending", pendingCount),
            ("failed", failedCount),
            ("processedThisFrame", this.performanceStats.GrayscaleIconProcessCount)));
    }

    private void AppendTooltipDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        TooltipDiagnosticSnapshot tooltipDiagnostics)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "tooltip", "Tooltip", FormatDiagnosticPairs(
            ("showTooltips", this.config.ShowTooltips),
            ("requestedThisFrame", this.overlayTooltipRequestedThisFrame),
            ("rendersThisFrame", this.performanceStats.TooltipRenderCount),
            ("hoverHits", tooltipDiagnostics.HoverHits),
            ("abilityRequests", tooltipDiagnostics.AbilityRequests),
            ("auraRequests", tooltipDiagnostics.AuraRequests),
            ("partyCooldownRequests", tooltipDiagnostics.PartyCooldownRequests),
            ("overlayActionRenders", tooltipDiagnostics.OverlayActionRenders),
            ("disabledSkips", tooltipDiagnostics.DisabledSkips),
            ("zeroActionSkips", tooltipDiagnostics.ZeroActionSkips),
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
            ("lastImGuiHovered", tooltipDiagnostics.HasLastGeometry && tooltipDiagnostics.LastImGuiHovered)));
    }

    private void AppendLastEventDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "lastEvent", "Last Event", FormatDiagnosticPairs(
            ("timeLocal", this.lastBugDiagnosticEventAtUtc == DateTime.MinValue ? string.Empty : this.lastBugDiagnosticEventAtUtc.ToLocalTime()),
            ("event", this.lastBugDiagnosticEvent)));
    }
}
