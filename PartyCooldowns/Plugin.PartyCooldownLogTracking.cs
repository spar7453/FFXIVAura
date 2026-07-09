using Dalamud.Game.Chat;
using Lumina.Text.ReadOnly;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private static readonly TimeSpan PartyCooldownLogDedupeWindow = TimeSpan.FromSeconds(1);

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
        if (!this.config.Enabled || this.partyCooldownDefinitions.Count == 0)
            return;

        if (!this.config.IconWindows.Any(window => IconWindowRoles.IsPartyCooldownRole(window.Role)))
            return;

        if (!this.IsCompletedPartyCooldownActionUseLog(message))
            return;

        var source = message.SourceEntity;
        if (source is null)
            return;

        var observedAction = this.ExtractObservedPartyCooldownAction(message);
        if (!this.HasPartyCooldownCandidate(observedAction) && !this.ShouldObservePartyCooldownLogs())
            return;

        var sourceName = source.Name.ExtractText();
        var rosterSnapshot = this.GetPartyCooldownLogRosterSnapshot();
        if (!this.TryFindPartyCooldownMemberByLogSource(source, rosterSnapshot.DisplayMembers, out var member, out var memberMatchDetail))
        {
            this.RecordPartyCooldownLogObservation(
                message.LogMessageId,
                sourceName,
                source.HomeWorldId,
                default,
                observedAction,
                "무시",
                rosterSnapshot.Diagnostics,
                memberMatchDetail);
            return;
        }

        var definition = this.ResolveObservedPartyCooldownDefinition(observedAction, member.Job);
        if (definition is null)
        {
            var detail = this.HasPartyCooldownCandidate(observedAction)
                ? "추적 대상 스킬이 아니거나 현재 직업에서 사용할 수 없습니다."
                : $"추적 대상 후보를 찾지 못했습니다. {this.DescribeLogMessageParameters(message)}";
            detail = AppendPartyCooldownMemberMatchDetail(detail, memberMatchDetail);
            this.RecordPartyCooldownLogObservation(
                message.LogMessageId,
                sourceName,
                source.HomeWorldId,
                member,
                observedAction,
                "무시",
                rosterSnapshot.Diagnostics,
                detail);
            return;
        }

        var level = this.GetCurrentEffectiveLevel();
        var effectiveDefinition = this.ResolveEffectivePartyCooldownDefinition(definition, member.Job, level);
        if (!this.IsPartyCooldownTrackedByAnyWindow(effectiveDefinition, member.Job, level))
        {
            var detail = AppendPartyCooldownMemberMatchDetail(
                "모든 파티 쿨다운 창에서 제외되었거나 현재 레벨에서 표시되지 않습니다.",
                memberMatchDetail);
            this.RecordPartyCooldownLogObservation(
                message.LogMessageId,
                sourceName,
                source.HomeWorldId,
                member,
                new PartyCooldownObservedAction(
                    effectiveDefinition.ActionId,
                    effectiveDefinition.Name,
                    observedAction.MatchSource,
                    observedAction.ParameterIndex),
                "무시",
                rosterSnapshot.Diagnostics,
                detail);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        this.StartPartyCooldownFromObservedUse(member, effectiveDefinition, nowUtc);
        var trackedDetail = this.AppendPartyCooldownStatusTrackingDetail(
            $"{effectiveDefinition.Name} 쿨다운 시작",
            effectiveDefinition);
        trackedDetail = AppendPartyCooldownMemberMatchDetail(trackedDetail, memberMatchDetail);
        this.RecordPartyCooldownLogObservation(
            message.LogMessageId,
            sourceName,
            source.HomeWorldId,
            member,
            new PartyCooldownObservedAction(
                effectiveDefinition.ActionId,
                effectiveDefinition.Name,
                observedAction.MatchSource,
                observedAction.ParameterIndex),
            "추적",
            rosterSnapshot.Diagnostics,
            trackedDetail);
    }

    private (IReadOnlyList<PartyCooldownMemberSnapshot> DisplayMembers, PartyCooldownRosterDiagnostics Diagnostics) GetPartyCooldownLogRosterSnapshot()
    {
        var roster = this.GetPartyCooldownRoster();
        var displayMembers = this.GetPartyCooldownDisplayMembers(roster.Members);
        var diagnostics = PartyCooldownRoster.CreateDiagnostics(
            roster.Source,
            roster.ReadMode,
            roster.PartyListLength,
            roster.Members,
            displayMembers,
            ObjectTable.LocalPlayer?.EntityId ?? 0,
            roster.AlliancePartyCount,
            roster.AllianceMemberCount,
            roster.HasAllianceSource,
            roster.UsedFlatAllianceFallback);
        return (displayMembers, diagnostics);
    }

    private bool IsCompletedPartyCooldownActionUseLog(ILogMessage message)
    {
        if (!message.GameData.IsValid)
            return false;

        var templateText = message.GameData.Value.Text.ExtractText();
        return PartyCooldownLogMatcher.IsCompletedActionUseTemplate(templateText);
    }

    private bool TryFindPartyCooldownMemberByLogSource(
        ILogMessageEntity source,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail)
    {
        var sourceName = source.Name.ExtractText();
        if (source.IsPlayer
            && this.TryFindPartyCooldownMemberByLogSourceName(sourceName, source.HomeWorldId, displayMembers, out member, out detail))
        {
            return true;
        }

        if (this.TryFindPartyCooldownMemberByOwnedObjectName(sourceName, displayMembers, out member, out detail))
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

        return this.TryFindPartyCooldownMemberByLogSourceName(sourceName, source.HomeWorldId, displayMembers, out member, out detail);
    }

    private bool TryFindPartyCooldownMemberByLogSourceName(
        string sourceName,
        ushort sourceWorldId,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail)
    {
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
        return true;
    }

    private bool TryFindPartyCooldownMemberByOwnedObjectName(
        string sourceName,
        IReadOnlyList<PartyCooldownMemberSnapshot> displayMembers,
        out PartyCooldownMemberSnapshot member,
        out string detail)
    {
        var normalizedSourceName = PartyCooldownLogMatcher.NormalizeActorName(sourceName);
        if (normalizedSourceName.Length == 0)
        {
            member = default;
            detail = "소환수/객체 이름이 비어 있습니다.";
            return false;
        }

        var matchedMembers = new Dictionary<string, PartyCooldownMemberSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var gameObject in ObjectTable)
        {
            if (gameObject is null || !IsValidPartyCooldownEntityId(gameObject.OwnerId))
                continue;

            var objectName = PartyCooldownLogMatcher.NormalizeActorName(gameObject.Name.ToString());
            if (!string.Equals(normalizedSourceName, objectName, StringComparison.Ordinal))
                continue;

            if (TryFindPartyCooldownMemberByEntityId(displayMembers, gameObject.OwnerId, out var ownerMember))
                matchedMembers.TryAdd(ownerMember.Key, ownerMember);
        }

        if (matchedMembers.Count == 1)
        {
            member = matchedMembers.Values.First();
            detail = "소환수/객체 소유자 매칭";
            return true;
        }

        if (matchedMembers.Count > 1)
        {
            member = default;
            detail = "같은 이름의 소환수/객체 소유 파티원이 여러 명이라 추적하지 않았습니다.";
            return false;
        }

        member = default;
        detail = string.Empty;
        return false;
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

        var runtimeKey = this.PartyCooldownRuntimeKey(member.Key, definition);
        if (!this.partyCooldownRuntimeStates.TryGetValue(runtimeKey, out var runtime))
        {
            runtime = new PartyCooldownRuntimeState();
            this.partyCooldownRuntimeStates[runtimeKey] = runtime;
        }

        if (PartyCooldownLogMatcher.IsDuplicateUse(runtime.LastLogTrackedAtUtc, nowUtc, PartyCooldownLogDedupeWindow))
            return;

        runtime.LastLogTrackedAtUtc = nowUtc;
        var cooldownEndsAtUtc = nowUtc.AddSeconds(definition.Cooldown);
        if (cooldownEndsAtUtc > runtime.CooldownEndsAtUtc)
            runtime.CooldownEndsAtUtc = cooldownEndsAtUtc;
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
        PartyCooldownRosterDiagnostics rosterDiagnostics,
        string detail)
    {
        if (!this.ShouldObservePartyCooldownLogs())
            return;

        var memberName = string.IsNullOrWhiteSpace(member.Name) ? "-" : member.Name;
        var actionName = PartyCooldownLogMatcher.NormalizeActionName(observedAction.ActionName);
        this.partyCooldownLogObservations.Enqueue(new PartyCooldownLogObservation(
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
            rosterDiagnostics,
            detail));

        while (this.partyCooldownLogObservations.Count > PartyCooldownLogObservationLimit)
            this.partyCooldownLogObservations.Dequeue();
    }

    private bool ShouldObservePartyCooldownLogs()
        => this.config.ShowPartyCooldownLogObserver || this.config.RecordPerformanceProfile;
}
