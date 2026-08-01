namespace FFXIVAura;

public sealed partial class Plugin
{
    private void RememberOverlayWindowDiagnostics(IconWindowConfig iconWindow, OverlayFrameModel frame)
    {
        if (!this.performanceProfileRecordingCoordinator.CaptureDiagnosticsThisFrame)
            return;

        var skillReady = 0;
        var skillCooling = 0;
        var skillUnavailable = 0;
        var skillAdjusted = 0;
        foreach (var ability in frame.LayoutAbilities)
        {
            if (!this.TryGetCachedCooldown(ability, out var state))
                continue;

            if (state.IsReady)
                skillReady++;
            if (state.IsCooling)
                skillCooling++;
            if (state.IsUnavailable)
                skillUnavailable++;
            if (state.Adjusted)
                skillAdjusted++;
        }

        var trackedResolveMissing = 0;
        if (!string.IsNullOrWhiteSpace(frame.Job)
            && iconWindow.Role == IconWindowRole.SkillCooldowns
            && AbilityTrackingService.IsManualTracking(iconWindow, frame.Job)
            && iconWindow.TrackedByJob.TryGetValue(frame.Job, out var tracked)
            && tracked.Count > 0)
        {
            trackedResolveMissing = Math.Max(
                0,
                tracked.Distinct(StringComparer.OrdinalIgnoreCase).Count() - frame.LayoutAbilities.Count);
        }

        this.overlayWindowDebugSnapshots[iconWindow.Id] = new OverlayWindowDebugSnapshot(
            iconWindow.Id,
            iconWindow.Role,
            iconWindow.DisplayCondition,
            frame.LayoutAbilities.Count,
            frame.DisplayAbilities.Count,
            Math.Max(0, frame.LayoutAbilities.Count - frame.DisplayAbilities.Count),
            skillReady,
            skillCooling,
            skillUnavailable,
            skillAdjusted,
            trackedResolveMissing,
            frame.LayoutAuras.Count,
            frame.DisplayAuras.Count,
            Math.Max(0, frame.LayoutAuras.Count - frame.DisplayAuras.Count),
            frame.LayoutAuras.Count(aura => aura.Present),
            frame.LayoutAuras.Count(aura => !aura.Present),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            0,
            0,
            0);
    }

    private void RememberPartyCooldownWindowDiagnostics(
        IconWindowConfig iconWindow,
        IReadOnlyList<PartyCooldownMemberRow> rows,
        int candidateItemCount,
        int hiddenByDisplayConditionCount,
        int statuslessCandidateCount)
    {
        if (!this.performanceProfileRecordingCoordinator.CaptureDiagnosticsThisFrame)
            return;

        var displayItemCount = 0;
        var ready = 0;
        var active = 0;
        var cooldown = 0;
        var emptyRows = 0;
        foreach (var row in rows)
        {
            if (row.Items.Count == 0)
            {
                emptyRows++;
                continue;
            }

            displayItemCount += row.Items.Count;
            foreach (var item in row.Items)
            {
                switch (item.State)
                {
                    case PartyCooldownDisplayState.Ready:
                        ready++;
                        break;
                    case PartyCooldownDisplayState.Active:
                        active++;
                        break;
                    case PartyCooldownDisplayState.Cooldown:
                        cooldown++;
                        break;
                }
            }
        }

        this.overlayWindowDebugSnapshots[iconWindow.Id] = new OverlayWindowDebugSnapshot(
            iconWindow.Id,
            iconWindow.Role,
            iconWindow.DisplayCondition,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            candidateItemCount,
            displayItemCount,
            Math.Max(0, hiddenByDisplayConditionCount),
            rows.Count,
            emptyRows,
            ready,
            active,
            cooldown,
            statuslessCandidateCount,
            string.Empty,
            0,
            0,
            0);
    }

    private void RememberPartyCooldownWindowLayoutDiagnostics(
        IconWindowConfig iconWindow,
        PartyCooldownBoardRenderMetrics metrics)
    {
        if (!this.performanceProfileRecordingCoordinator.CaptureDiagnosticsThisFrame)
            return;

        if (!this.overlayWindowDebugSnapshots.TryGetValue(iconWindow.Id, out var snapshot))
            return;

        this.overlayWindowDebugSnapshots[iconWindow.Id] = snapshot with
        {
            PartyLayoutMode = metrics.UseAllianceGrid
                ? metrics.HasOverflow
                    ? "AllianceStack8x3Overflow"
                    : metrics.IsCompact
                        ? "AllianceStack8x3Compact"
                        : "AllianceStack8x3"
                : "Linear",
            PartyIconsPerLine = metrics.IconsPerLine,
            PartyAllianceGroupCount = metrics.AllianceGroupCount,
            PartyAllianceMemberColumnCount = metrics.AllianceMemberColumnCount,
        };
    }

    private bool TryGetCachedCooldown(AbilityDefinition ability, out CooldownState state)
    {
        return this.cooldownFrameService.TryGetCached(ability, out state);
    }

    private void SetBugDiagnosticEvent(string eventName)
    {
        this.lastBugDiagnosticEvent = eventName;
        this.lastBugDiagnosticEventAtUtc = DateTime.UtcNow;
    }
}
