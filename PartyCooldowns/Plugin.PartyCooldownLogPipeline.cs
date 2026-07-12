using Dalamud.Game.Chat;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private readonly record struct PartyCooldownLogTrackingContext(
        ILogMessage Message,
        ILogMessageEntity Source,
        string SourceName,
        PartyCooldownObservedAction ObservedAction);

    private readonly record struct PartyCooldownLogRosterSnapshot(
        IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers,
        PartyCooldownRosterDiagnostics Diagnostics);

    private bool ShouldProcessPartyCooldownLogMessage(ILogMessage message)
        => this.config.Enabled
           && this.partyCooldownDefinitions.Count > 0
           && this.config.IconWindows.Any(window => IconWindowRoles.IsPartyCooldownRole(window.Role))
           && this.IsCompletedPartyCooldownActionUseLog(message);

    private bool TryCreatePartyCooldownLogTrackingContext(
        ILogMessage message,
        out PartyCooldownLogTrackingContext context)
    {
        context = default;
        if (!this.ShouldProcessPartyCooldownLogMessage(message))
            return false;

        var source = message.SourceEntity;
        if (source is null)
            return false;

        if (this.IsLocalPlayerPartyCooldownLogSource(source))
        {
            this.partyCooldownLocalPlayerLogSkippedCount++;
            return false;
        }

        var observedAction = this.ExtractObservedPartyCooldownAction(message);
        var sourceName = source.Name.ExtractText();
        if (!this.HasPartyCooldownCandidate(observedAction))
        {
            this.RecordMissingPartyCooldownCandidate(message, sourceName, source, observedAction);
            return false;
        }

        context = new PartyCooldownLogTrackingContext(message, source, sourceName, observedAction);
        return true;
    }

    private void RecordMissingPartyCooldownCandidate(
        ILogMessage message,
        string sourceName,
        ILogMessageEntity source,
        PartyCooldownObservedAction observedAction)
    {
        this.partyCooldownCandidateMissingLogCount++;
        if (!this.ShouldObservePartyCooldownLogs())
            return;

        var sampleNowUtc = DateTime.UtcNow;
        if (!this.partyCooldownCandidateObservationThrottle.TryAcquire(
                sampleNowUtc,
                PartyCooldownCandidateMissingSampleInterval))
        {
            return;
        }

        this.partyCooldownCandidateMissingObservationCount++;
        this.RecordPartyCooldownLogObservation(
            message.LogMessageId,
            sourceName,
            source.HomeWorldId,
            default,
            observedAction,
            "무시",
            PartyCooldownIgnoredLogReason.CandidateMissing,
            this.partyCooldownFrameSnapshot?.RosterDiagnostics ?? default,
            $"추적 대상 후보를 찾지 못했습니다. {this.DescribeLogMessageParameters(message)}");
    }

    private PartyCooldownLogRosterSnapshot GetPartyCooldownLogRosterSnapshot()
    {
        var roster = this.partyCooldownRosterService.GetRoster(forceRefresh: true);
        var displayMembers = roster.DisplayMembers;
        return new PartyCooldownLogRosterSnapshot(displayMembers, roster.Diagnostics);
    }

    private bool TryResolvePartyCooldownLogMember(
        PartyCooldownLogTrackingContext context,
        PartyCooldownLogRosterSnapshot rosterSnapshot,
        out PartyCooldownMemberSnapshot member,
        out string memberMatchDetail)
    {
        if (this.TryFindPartyCooldownMemberByLogSource(
                context.Source,
                rosterSnapshot.DisplayMembers,
                out member,
                out memberMatchDetail,
                out var ignoredReason))
        {
            return true;
        }

        if (ignoredReason == PartyCooldownIgnoredLogReason.LocalPlayerExcluded)
        {
            this.partyCooldownLocalOwnedObjectLogSkippedCount++;
            return false;
        }

        this.RecordPartyCooldownLogObservation(
            context,
            default,
            context.ObservedAction,
            "무시",
            ignoredReason,
            rosterSnapshot.Diagnostics,
            memberMatchDetail);
        return false;
    }

    private bool TryResolveTrackedPartyCooldownDefinition(
        PartyCooldownLogTrackingContext context,
        PartyCooldownLogRosterSnapshot rosterSnapshot,
        PartyCooldownMemberSnapshot member,
        string memberMatchDetail,
        out PartyCooldownDefinition effectiveDefinition)
    {
        effectiveDefinition = null!;
        var definition = this.ResolveObservedPartyCooldownDefinition(context.ObservedAction, member.Job);
        if (definition is null)
        {
            this.RecordUnusablePartyCooldownLog(
                context,
                rosterSnapshot.Diagnostics,
                member,
                memberMatchDetail);
            return false;
        }

        var level = this.GetCurrentEffectiveLevel();
        effectiveDefinition = this.ResolveEffectivePartyCooldownDefinition(definition, member.Job, level);
        if (this.IsPartyCooldownTrackedByAnyWindow(effectiveDefinition, member.Job, level))
            return true;

        var detail = AppendPartyCooldownMemberMatchDetail(
            "모든 파티 쿨다운 창에서 제외되었거나 현재 레벨에서 표시되지 않습니다.",
            memberMatchDetail);
        this.RecordPartyCooldownLogObservation(
            context,
            member,
            CreateEffectiveObservedAction(context.ObservedAction, effectiveDefinition),
            "무시",
            PartyCooldownIgnoredLogReason.NotTrackedByWindow,
            rosterSnapshot.Diagnostics,
            detail);
        return false;
    }

    private void RecordUnusablePartyCooldownLog(
        PartyCooldownLogTrackingContext context,
        PartyCooldownRosterDiagnostics rosterDiagnostics,
        PartyCooldownMemberSnapshot member,
        string memberMatchDetail)
    {
        var hasCandidate = this.HasPartyCooldownCandidate(context.ObservedAction);
        var detail = hasCandidate
            ? "추적 대상 스킬이 아니거나 현재 직업에서 사용할 수 없습니다."
            : $"추적 대상 후보를 찾지 못했습니다. {this.DescribeLogMessageParameters(context.Message)}";
        detail = AppendPartyCooldownMemberMatchDetail(detail, memberMatchDetail);
        var ignoredReason = hasCandidate
            ? PartyCooldownIgnoredLogReason.NotUsableForJob
            : PartyCooldownIgnoredLogReason.CandidateMissing;
        this.RecordPartyCooldownLogObservation(
            context,
            member,
            context.ObservedAction,
            "무시",
            ignoredReason,
            rosterDiagnostics,
            detail);
    }

    private void TrackResolvedPartyCooldownLogUse(
        PartyCooldownLogTrackingContext context,
        PartyCooldownLogRosterSnapshot rosterSnapshot,
        PartyCooldownMemberSnapshot member,
        string memberMatchDetail,
        PartyCooldownDefinition effectiveDefinition)
    {
        var nowUtc = DateTime.UtcNow;
        this.StartPartyCooldownFromObservedUse(member, effectiveDefinition, nowUtc);
        var trackedDetail = this.AppendPartyCooldownStatusTrackingDetail(
            $"{effectiveDefinition.Name} 쿨다운 시작",
            effectiveDefinition);
        trackedDetail = AppendPartyCooldownMemberMatchDetail(trackedDetail, memberMatchDetail);
        this.RecordPartyCooldownLogObservation(
            context,
            member,
            CreateEffectiveObservedAction(context.ObservedAction, effectiveDefinition),
            "추적",
            PartyCooldownIgnoredLogReason.None,
            rosterSnapshot.Diagnostics,
            trackedDetail);
    }

    private static PartyCooldownObservedAction CreateEffectiveObservedAction(
        PartyCooldownObservedAction observedAction,
        PartyCooldownDefinition definition)
        => new(
            definition.ActionId,
            definition.Name,
            observedAction.MatchSource,
            observedAction.ParameterIndex);

    private void RecordPartyCooldownLogObservation(
        PartyCooldownLogTrackingContext context,
        PartyCooldownMemberSnapshot member,
        PartyCooldownObservedAction observedAction,
        string result,
        PartyCooldownIgnoredLogReason ignoredReason,
        PartyCooldownRosterDiagnostics rosterDiagnostics,
        string detail)
        => this.RecordPartyCooldownLogObservation(
            context.Message.LogMessageId,
            context.SourceName,
            context.Source.HomeWorldId,
            member,
            observedAction,
            result,
            ignoredReason,
            rosterDiagnostics,
            detail);
}
