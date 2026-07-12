using Dalamud.Game.Chat;
using Lumina.Text.ReadOnly;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly TimeSpan PartyCooldownLogDedupeWindow = TimeSpan.FromMilliseconds(500);

    private void OnLogMessage(ILogMessage message)
    {
        try
        {
            this.TrackPartyCooldownFromLogMessage(message);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to process FFXIVAura party cooldown log message.");
        }
    }

    private void TrackPartyCooldownFromLogMessage(ILogMessage message)
    {
        if (!this.TryCreatePartyCooldownLogTrackingContext(message, out var context))
            return;

        var rosterSnapshot = this.GetPartyCooldownLogRosterSnapshot();
        if (!this.TryResolvePartyCooldownLogMember(
                context,
                rosterSnapshot,
                out var member,
                out var memberMatchDetail))
        {
            return;
        }

        if (!this.TryResolveTrackedPartyCooldownDefinition(
                context,
                rosterSnapshot,
                member,
                memberMatchDetail,
                out var effectiveDefinition))
        {
            return;
        }

        this.TrackResolvedPartyCooldownLogUse(
            context,
            rosterSnapshot,
            member,
            memberMatchDetail,
            effectiveDefinition);
    }

    private bool IsCompletedPartyCooldownActionUseLog(ILogMessage message)
    {
        if (!message.GameData.IsValid)
            return false;

        var templateText = message.GameData.Value.Text.ExtractText();
        return PartyCooldownLogMatcher.IsCompletedActionUseTemplate(templateText);
    }

    private bool IsLocalPlayerPartyCooldownLogSource(ILogMessageEntity source)
    {
        if (!source.IsPlayer || ObjectTable.LocalPlayer is not { } localPlayer)
            return false;

        return PartyCooldownLogMatcher.IsSameActor(
            source.Name.ExtractText(),
            source.HomeWorldId,
            localPlayer.Name.ToString(),
            (ushort)PlayerState.HomeWorld.RowId);
    }

    private bool TryFindPartyCooldownMemberByLogSource(
        ILogMessageEntity source,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        ignoredReason = PartyCooldownIgnoredLogReason.OwnerNotFound;
        var sourceName = source.Name.ExtractText();
        if (source.IsPlayer
            && this.TryFindPartyCooldownMemberByLogSourceName(sourceName, source.HomeWorldId, displayMembers, out member, out detail, out ignoredReason))
        {
            return true;
        }

        if (this.TryFindPartyCooldownMemberByOwnedObjectName(sourceName, displayMembers, out member, out detail, out ignoredReason))
        {
            return true;
        }

        if (!source.IsPlayer)
        {
            member = default;
            if (string.IsNullOrWhiteSpace(detail))
                detail = "소환수/객체의 소유 파티원을 찾지 못했습니다.";

            return false;
        }

        return this.TryFindPartyCooldownMemberByLogSourceName(sourceName, source.HomeWorldId, displayMembers, out member, out detail, out ignoredReason);
    }

    private bool TryFindPartyCooldownMemberByLogSourceName(
        string sourceName,
        ushort sourceWorldId,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail,
        out PartyCooldownIgnoredLogReason ignoredReason)
    {
        ignoredReason = PartyCooldownIgnoredLogReason.MemberNotFound;
        PartyCooldownMemberSnapshot? matchedMember = null;
        var matchCount = 0;
        foreach (var candidate in displayMembers)
        {
            if (!PartyCooldownLogMatcher.IsSameActor(sourceName, sourceWorldId, candidate.Name, candidate.WorldId))
                continue;

            matchedMember = candidate;
            matchCount++;
        }

        if (PartyCooldownLogMatcher.IsAmbiguousActorFallback(sourceWorldId, matchCount))
        {
            member = default;
            ignoredReason = PartyCooldownIgnoredLogReason.Ambiguous;
            detail = "월드 정보 없는 동명이인 후보가 여러 명이라 추적하지 않았습니다.";
            return false;
        }

        if (matchCount != 1 || matchedMember is null)
        {
            member = default;
            detail = "파티원 목록에서 시전자와 일치하는 플레이어를 찾지 못했습니다.";
            return false;
        }

        member = matchedMember.Value;
        detail = string.Empty;
        ignoredReason = PartyCooldownIgnoredLogReason.None;
        return true;
    }

    private static string AppendPartyCooldownMemberMatchDetail(string detail, string memberMatchDetail)
    {
        return string.IsNullOrWhiteSpace(memberMatchDetail)
            ? detail
            : $"{detail} ({memberMatchDetail})";
    }

    private string AppendPartyCooldownStatusTrackingDetail(string detail, PartyCooldownDefinition definition)
    {
        if (!this.ShouldObservePartyCooldownLogs())
            return detail;

        var statusIds = this.ResolvePartyCooldownStatusIds(definition);
        return statusIds.Count == 0
            ? $"{detail} / 상태 추적 없음"
            : $"{detail} / 상태 {string.Join(", ", statusIds)}";
    }

    private PartyCooldownObservedAction ExtractObservedPartyCooldownAction(ILogMessage message)
    {
        if (this.partyCooldownLogActionParamIndexByLogMessageId.TryGetValue(message.LogMessageId, out var cachedIndex)
            && this.TryGetTrackedActionIdParameter(message, cachedIndex, out var cachedActionId))
        {
            return new PartyCooldownObservedAction(cachedActionId, string.Empty, "id", cachedIndex);
        }

        for (var index = 0; index < message.ParameterCount; index++)
        {
            if (this.TryGetTrackedActionIdParameter(message, index, out var actionId))
            {
                this.partyCooldownLogActionParamIndexByLogMessageId[message.LogMessageId] = index;
                return new PartyCooldownObservedAction(actionId, string.Empty, "id", index);
            }
        }

        for (var index = 0; index < message.ParameterCount; index++)
        {
            if (!message.TryGetStringParameter(index, out var stringValue))
                continue;

            var actionName = PartyCooldownLogMatcher.NormalizeActionName(ExtractLogParameterText(stringValue));
            if (string.IsNullOrWhiteSpace(actionName))
                continue;

            if (this.partyCooldownDefinitionsByName.ContainsKey(actionName))
                return new PartyCooldownObservedAction(0, actionName, "name", index);
        }

        return default;
    }

    private bool TryGetTrackedActionIdParameter(ILogMessage message, int parameterIndex, out uint actionId)
    {
        actionId = 0;
        if (parameterIndex < 0
            || parameterIndex >= message.ParameterCount
            || !message.TryGetIntParameter(parameterIndex, out var value)
            || value <= 0)
        {
            return false;
        }

        actionId = (uint)value;
        return this.partyCooldownDefinitionsByActionId.ContainsKey(actionId);
    }

    private bool HasPartyCooldownCandidate(PartyCooldownObservedAction observedAction)
        => observedAction.ActionId > 0
           || !string.IsNullOrWhiteSpace(observedAction.ActionName);

    private PartyCooldownDefinition? ResolveObservedPartyCooldownDefinition(PartyCooldownObservedAction observedAction, string job)
    {
        if (observedAction.ActionId > 0
            && this.partyCooldownDefinitionsByActionId.TryGetValue(observedAction.ActionId, out var actionDefinition)
            && this.IsPartyCooldownForJob(actionDefinition, job))
        {
            return actionDefinition;
        }

        var actionName = PartyCooldownLogMatcher.NormalizeActionName(observedAction.ActionName);
        if (actionName.Length > 0 && this.partyCooldownDefinitionsByName.TryGetValue(actionName, out var nameDefinitions))
            return nameDefinitions.FirstOrDefault(definition => this.IsPartyCooldownForJob(definition, job));

        return null;
    }

    private void StartPartyCooldownFromObservedUse(
        PartyCooldownMemberSnapshot member,
        PartyCooldownDefinition definition,
        DateTime nowUtc)
    {
        if (definition.Cooldown <= 0f)
            return;

        var runtimeKey = this.CreatePartyCooldownRuntimeKey(member.Key, definition);
        var runtime = this.partyCooldownRuntimeStore.GetOrCreateState(runtimeKey);

        if (PartyCooldownLogMatcher.IsDuplicateUse(runtime.LastLogTrackedAtUtc, nowUtc, PartyCooldownLogDedupeWindow))
            return;

        runtime.LastLogTrackedAtUtc = nowUtc;
        PartyCooldownChargeTracker.RecordUse(
            runtime,
            nowUtc,
            definition.Cooldown,
            this.GetPartyCooldownMaxCharges(definition, this.GetCurrentEffectiveLevel()),
            PartyCooldownUseObservationSource.CombatLog,
            PartyCooldownLogDedupeWindow,
            PartyCooldownCrossSignalDedupeWindow);
    }

    private static string ExtractLogParameterText(ReadOnlySeString value)
        => value.ExtractText().Trim();

    private string DescribeLogMessageParameters(ILogMessage message)
    {
        if (!this.ShouldObservePartyCooldownLogs())
            return string.Empty;

        var parts = new List<string>(Math.Min(message.ParameterCount, 8));
        for (var index = 0; index < message.ParameterCount && parts.Count < 8; index++)
        {
            if (message.TryGetIntParameter(index, out var intValue))
            {
                parts.Add($"{index}=#{intValue}");
                continue;
            }

            if (message.TryGetStringParameter(index, out var stringValue))
            {
                var text = ExtractLogParameterText(stringValue);
                if (text.Length > 24)
                    text = $"{text[..24]}...";

                parts.Add($"{index}=\"{text}\"");
            }
        }

        return parts.Count == 0 ? "params: -" : $"params: {string.Join(", ", parts)}";
    }

    private void RecordPartyCooldownLogObservation(
        uint logMessageId,
        string sourceName,
        ushort sourceWorldId,
        PartyCooldownMemberSnapshot member,
        PartyCooldownObservedAction observedAction,
        string result,
        PartyCooldownIgnoredLogReason ignoredReason,
        PartyCooldownRosterDiagnostics rosterDiagnostics,
        string detail)
    {
        if (!this.ShouldObservePartyCooldownLogs())
            return;

        var memberName = string.IsNullOrWhiteSpace(member.Name) ? "-" : member.Name;
        var actionName = PartyCooldownLogMatcher.NormalizeActionName(observedAction.ActionName);
        var observation = new PartyCooldownLogObservation(
            DateTime.UtcNow,
            logMessageId,
            sourceName,
            sourceWorldId,
            memberName,
            observedAction.ActionId,
            actionName.Length == 0 ? "-" : actionName,
            string.IsNullOrWhiteSpace(observedAction.MatchSource) ? "-" : observedAction.MatchSource,
            observedAction.ParameterIndex,
            result,
            ignoredReason,
            rosterDiagnostics,
            detail);

        this.partyCooldownLogObservations.Add(observation);
    }

    private bool ShouldObservePartyCooldownLogs()
        => this.config.ShowPartyCooldownLogObserver || this.config.RecordPerformanceProfile;
}
