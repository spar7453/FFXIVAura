using System.Globalization;
using System.Text;

namespace FFXIVAura;

public sealed partial class Plugin
{
    private void AppendPluginDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
        var configSaveDiagnostics = this.configSaveCoordinator.CreateDiagnostics(timestampUtc);
        var profileRecordingDiagnostics = this.performanceProfileRecordingCoordinator.CreateDiagnostics();
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "plugin", "Plugin", FormatDiagnosticPairs(
            ("enabled", this.config.Enabled),
            ("configVisible", this.configVisible),
            ("configSavePending", configSaveDiagnostics.Pending),
            ("configSaveDeferredInCombat", configSaveDiagnostics.DeferredInCombat),
            ("configSavePendingSec", configSaveDiagnostics.PendingSeconds),
            ("configSaveQueue", configSaveDiagnostics.QueueCount),
            ("configSaveDropped", configSaveDiagnostics.DroppedCount),
            ("configSaveCompleted", configSaveDiagnostics.CompletedCount),
            ("configSaveFailed", configSaveDiagnostics.FailedCount),
            ("configSaveLastMs", configSaveDiagnostics.LastSaveMilliseconds),
            ("configSaveMaxMs", configSaveDiagnostics.MaxSaveMilliseconds),
            ("lockOverlay", this.config.LockOverlay),
            ("hideDuringZoneLoad", this.config.HideDuringZoneLoad),
            ("showTooltips", this.config.ShowTooltips),
            ("showPerformanceOverlay", this.config.ShowPerformanceOverlay),
            ("showDetailedProfile", this.config.ShowDetailedPerformanceProfile),
            ("recordProfile", this.config.RecordPerformanceProfile),
            ("recordIntervalSec", this.config.PerformanceProfileRecordIntervalSeconds),
            ("diagnosticIntervalSec", PerformanceProfileRecordingCoordinator.DiagnosticIntervalSeconds),
            ("maxFileMb", this.config.PerformanceProfileMaxFileMegabytes),
            ("profileWritePending", profileRecordingDiagnostics.PendingCount),
            ("profileWriteDropped", profileRecordingDiagnostics.DroppedCount),
            ("profileWriteCompleted", profileRecordingDiagnostics.CompletedCount),
            ("profileWriteFailed", profileRecordingDiagnostics.FailedCount),
            ("profileWriteLastMs", profileRecordingDiagnostics.LastWriteMilliseconds),
            ("profileWriteMaxMs", profileRecordingDiagnostics.MaxWriteMilliseconds),
            ("profileWriteLastCompletedLocal", profileRecordingDiagnostics.LastCompletedAtUtc == DateTime.MinValue
                ? string.Empty
                : profileRecordingDiagnostics.LastCompletedAtUtc.ToLocalTime()),
            ("profileFailureCount", profileRecordingDiagnostics.RecordingFailureCount),
            ("profileLastError", profileRecordingDiagnostics.LastError),
            ("profileLastErrorLocal", profileRecordingDiagnostics.LastErrorAtUtc == DateTime.MinValue
                ? string.Empty
                : profileRecordingDiagnostics.LastErrorAtUtc.ToLocalTime()),
            ("logObserver", this.config.ShowPartyCooldownLogObserver),
            ("partyCooldownLayoutEditMode", this.config.PartyCooldownLayoutEditMode)));
    }

    private void AppendPlayerDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        in PlayerFrameContext playerContext)
    {
        var loadingSuppressed = this.config.HideDuringZoneLoad
                                && (playerContext.IsBetweenAreas
                                    || this.zoneLoadActive
                                    || timestampUtc < this.zoneLoadHiddenUntil);

        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "player", "Player", FormatDiagnosticPairs(
            ("loggedIn", playerContext.IsLoggedIn),
            ("playerLoaded", playerContext.IsPlayerLoaded),
            ("localPlayer", playerContext.HasLocalPlayer),
            ("job", playerContext.Job),
            ("level", playerContext.Level),
            ("effectiveLevel", playerContext.EffectiveLevel),
            ("levelSynced", playerContext.IsLevelSynced),
            ("inCombat", playerContext.IsInCombat),
            ("betweenAreas", playerContext.BetweenAreas),
            ("betweenAreas51", playerContext.BetweenAreas51),
            ("loadingSuppressed", loadingSuppressed),
            ("target", playerContext.HasTarget),
            ("softTarget", playerContext.HasSoftTarget)));
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
            ("auraSearchVisible", this.auraSearchWindowSession.IsVisible),
            ("auraSearchWindowId", this.auraSearchWindowSession.WindowId ?? string.Empty),
            ("draggedTrackedId", this.trackedSkillEditorSession.DraggedAbilityId ?? string.Empty),
            ("draggedOverlayId", this.overlayDragSession.DragId ?? string.Empty)));
    }

    private void AppendCacheDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownRuntimeStoreDiagnostics partyCooldownRuntimeDiagnostics)
    {
        var statusSnapshotDiagnostics = this.statusSnapshotRuntime.CreateDiagnostics();
        var auraCatalogDiagnostics = this.auraCatalog.CreateDiagnostics();
        var auraSearchDiagnostics = this.auraSearchService.CreateDiagnostics();
        var keybindDiagnostics = this.actionKeybindService.CreateDiagnostics();
        var tooltipContentDiagnostics = this.tooltipContentService.CreateDiagnostics();
        var abilityCatalogDiagnostics = this.abilityCatalog.CreateDiagnostics();
        var partyCooldownCatalogDiagnostics = this.partyCooldownCatalog.CreateDiagnostics();
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "cache", "Cache", FormatDiagnosticPairs(
            ("cooldownFrame", this.cooldownFrameService.CachedCount),
            ("visibleAbilityKeys", this.visibleAbilityKeys.Count),
            ("transientSkillLayouts", this.transientSkillPositionsByGroup.Count),
            ("abilityDefinitions", abilityCatalogDiagnostics.DefinitionCount),
            ("abilityIdLookups", abilityCatalogDiagnostics.DefinitionIdLookupCount),
            ("abilityActionLookups", abilityCatalogDiagnostics.DefinitionActionLookupCount),
            ("jobCandidates", abilityCatalogDiagnostics.JobCandidateCacheCount),
            ("gameActionCandidates", abilityCatalogDiagnostics.GameActionCandidateCacheCount),
            ("abilityIdMatchers", abilityCatalogDiagnostics.IdMatcherCacheCount),
            ("actionRows", this.gameActionRepository.CachedActionCount),
            ("partyCooldownDefinitions", partyCooldownCatalogDiagnostics.DefinitionCount),
            ("partyCooldownActionLookups", partyCooldownCatalogDiagnostics.ActionLookupCount),
            ("partyCooldownNameLookups", partyCooldownCatalogDiagnostics.NameLookupCount),
            ("partyCooldownScopeCache", partyCooldownCatalogDiagnostics.EffectiveScopeCacheCount),
            ("partyCooldownCategoryCache", partyCooldownCatalogDiagnostics.EffectiveCategoryCacheCount),
            ("partyCooldownStatusIds", partyCooldownCatalogDiagnostics.StatusIdCacheCount),
            ("partyCooldownMaxCharges", partyCooldownCatalogDiagnostics.MaxChargeCacheCount),
            ("statusDefinitions", auraCatalogDiagnostics.StatusDefinitionCount),
            ("statusIdentityIndexBuilt", auraCatalogDiagnostics.StatusIdentityIndexBuilt),
            ("statusIdentityIndexGeneration", auraCatalogDiagnostics.StatusIdentityIndexGeneration),
            ("statusSearchIndexBuilt", auraCatalogDiagnostics.AllStatusSearchIndexBuilt),
            ("statusSearchEntries", auraCatalogDiagnostics.AllStatusSearchEntryCount),
            ("statusIdentityGroups", auraCatalogDiagnostics.StatusIdentityGroupCount),
            ("actionAuraIndexBuilt", auraCatalogDiagnostics.ActionGrantedIndexBuilt),
            ("actionAuraIndexGeneration", auraCatalogDiagnostics.ActionGrantedIndexGeneration),
            ("actionAuraEntries", auraCatalogDiagnostics.ActionGrantedEntryCount),
            ("actionAuraStatusBuckets", auraCatalogDiagnostics.ActionGrantedStatusBucketCount),
            ("actionAuraQueryCache", auraCatalogDiagnostics.ActionGrantedQueryCacheCount),
            ("statusSearchQueryCache", auraCatalogDiagnostics.AllStatusSearchQueryCacheCount),
            ("auraSearchResultCache", auraSearchDiagnostics.ResultCacheCount),
            ("auraSearchResultCacheHits", auraSearchDiagnostics.ResultCacheHitCount),
            ("auraSearchResultCacheMisses", auraSearchDiagnostics.ResultCacheMissCount),
            ("partyRowBuffers", partyCooldownRuntimeDiagnostics.RowBufferCount),
            ("partyRowMemberBuffers", partyCooldownRuntimeDiagnostics.RowMemberBufferCount),
            ("statusTooltips", tooltipContentDiagnostics.StatusTextCount),
            ("actionTooltips", tooltipContentDiagnostics.ActionModelCount),
            ("actionTooltipDescriptions", tooltipContentDiagnostics.ActionDescriptionCount),
            ("statusFallbacks", statusSnapshotDiagnostics.FallbackCount),
            ("gameObjectOwners", statusSnapshotDiagnostics.OwnerCacheCount),
            ("hotbarVisibility", keybindDiagnostics.HotbarVisibilityCount),
            ("generalActionIds", keybindDiagnostics.GeneralActionCount),
            ("missingActionRows", this.gameActionRepository.MissingActionCount),
            ("keybindDirty", keybindDiagnostics.IsDirty),
            ("keybindRefreshAfterLocal", keybindDiagnostics.RefreshAfterUtc == DateTime.MinValue
                ? string.Empty
                : keybindDiagnostics.RefreshAfterUtc.ToLocalTime())));
    }

    private void AppendAuraDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        AuraFrameDiagnostics diagnostics)
    {
        var partyAuraRuntimeDiagnostics = diagnostics.PartyRuntime;
        var auraSearchDiagnostics = this.auraSearchService.CreateDiagnostics();
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "aura", "Aura", FormatDiagnosticPairs(
            ("playerCacheValid", diagnostics.PlayerCacheValid),
            ("playerCacheCount", diagnostics.PlayerCacheCount),
            ("targetCacheValid", diagnostics.TargetCacheValid),
            ("targetCacheCount", diagnostics.TargetCacheCount),
            ("partyAllCacheValid", diagnostics.PartyAllCacheValid),
            ("partyAllCacheCount", diagnostics.PartyAllCacheCount),
            ("partyOwnCacheValid", diagnostics.PartyOwnCacheValid),
            ("partyOwnCacheCount", diagnostics.PartyOwnCacheCount),
            ("partyTimerStates", partyAuraRuntimeDiagnostics.TimerStateCount),
            ("partyFallbackBatches", partyAuraRuntimeDiagnostics.FallbackBatchCount),
            ("partyTimerRefreshAccepted", partyAuraRuntimeDiagnostics.RefreshAcceptedCount),
            ("partyExpiredStatusSuppressed", partyAuraRuntimeDiagnostics.ExpiredStatusSuppressedCount),
            ("partyTimerLastDecision", partyAuraRuntimeDiagnostics.LastDecision),
            ("visibleAuraScopes", auraSearchDiagnostics.VisibleScopeCount),
            ("visibleAuraIds", auraSearchDiagnostics.VisibleStatusCount),
            ("firstSeenScopes", auraSearchDiagnostics.FirstSeenScopeCount),
            ("firstSeenStatusIds", auraSearchDiagnostics.FirstSeenStatusCount),
            ("auraSearchRevisionScopes", auraSearchDiagnostics.RevisionScopeCount),
            ("pendingStatusId", this.auraSearchWindowSession.PendingStatusId)));
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
        var signalDiagnostics = this.partyCooldownSignalDiagnostics.CreateSnapshot();
        var levelDiagnostics = PartyCooldownLevelDiagnosticCalculator.Create(
            diagnosticMembers,
            this.playerFrameContext.EffectiveLevel);
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
            ("definitions", this.partyCooldownCatalog.Definitions.Count),
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
            ("statusFallbackBatches", signalDiagnostics.StatusFallbackBatchCount),
            ("activeTimerRefreshAccepted", signalDiagnostics.TimerRefreshAcceptedCount),
            ("activeTimerStaleSuppressed", signalDiagnostics.TimerStalePositiveSuppressedCount),
            ("activeTimerLastDecision", signalDiagnostics.TimerLastDecision),
            ("statusScansThisFrame", this.performanceStats.PartyStatusScanCount),
            ("statusCacheHitsThisFrame", this.performanceStats.PartyStatusCacheHitCount),
            ("frameSnapshot", this.partyCooldownFrameSnapshot is not null),
            ("frameMembers", this.partyCooldownFrameSnapshot?.Members.Count ?? 0),
            ("displayMembers", this.partyCooldownFrameSnapshot?.DisplayMembers.Count ?? 0),
            ("localLevelSynced", this.playerFrameContext.IsLevelSynced),
            ("localEffectiveLevel", this.playerFrameContext.EffectiveLevel),
            ("memberReportedLevelMin", levelDiagnostics.ReportedMinimum),
            ("memberReportedLevelMax", levelDiagnostics.ReportedMaximum),
            ("memberLevelMissing", levelDiagnostics.MissingCount),
            ("memberResolvedLevelMin", levelDiagnostics.ResolvedMinimum),
            ("memberResolvedLevelMax", levelDiagnostics.ResolvedMaximum),
            ("memberReportedAboveLocalEffective", levelDiagnostics.ReportedAboveFallbackCount),
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
            ("candidateMissingTotal", signalDiagnostics.CandidateMissingTotal),
            ("candidateMissingSamples", signalDiagnostics.CandidateMissingSamples),
            ("logLocalPlayerSkippedTotal", signalDiagnostics.LocalPlayerLogSkippedTotal),
            ("logLocalOwnedObjectSkippedTotal", signalDiagnostics.LocalOwnedObjectLogSkippedTotal),
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
        var sourceAlias = this.performanceProfileIdentityAnonymizer.GetAlias(
            observation.SourceName,
            observation.SourceWorldId);
        var memberAlias = this.performanceProfileIdentityAnonymizer.GetAlias(
            observation.MemberName,
            observation.SourceWorldId);
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldownLastLog", "Party Cooldown Last Log", FormatDiagnosticPairs(
            ("timeLocal", observation.TimestampUtc.ToLocalTime()),
            ("result", observation.Result),
            ("source", sourceAlias),
            ("member", memberAlias),
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
            ("detail", PerformanceProfileIdentityAnonymizer.RedactDetail(observation.Detail))));
    }

    private void AppendPartyCooldownLastUnknownLogDiagnosticRow(
        StringBuilder builder,
        DateTime timestampUtc,
        PartyCooldownLogObservation observation)
    {
        var sourceAlias = this.performanceProfileIdentityAnonymizer.GetAlias(
            observation.SourceName,
            observation.SourceWorldId);
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "partyCooldownLastUnknownLog", "Party Cooldown Last Unknown Log", FormatDiagnosticPairs(
            ("timeLocal", observation.TimestampUtc.ToLocalTime()),
            ("logMessageId", observation.LogMessageId),
            ("source", sourceAlias),
            ("detail", PerformanceProfileIdentityAnonymizer.RedactDetail(observation.Detail))));
    }

    private void AppendGrayscaleDiagnosticRow(StringBuilder builder, DateTime timestampUtc)
    {
        var diagnostics = this.iconTextureService.CreateDiagnostics();
        this.AppendPerformanceProfileDiagnosticRow(builder, timestampUtc, "grayscale", "Grayscale", FormatDiagnosticPairs(
            ("cache", diagnostics.GrayscaleCacheCount),
            ("queue", diagnostics.GrayscaleQueueCount),
            ("pending", diagnostics.GrayscalePendingCount),
            ("failed", diagnostics.GrayscaleFailedCount),
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
