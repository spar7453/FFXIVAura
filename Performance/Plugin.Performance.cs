using System.Diagnostics;

namespace FFXIVAura;

public sealed partial class Plugin
{
    private static readonly TimeSpan PerformanceOverlayRefreshInterval = TimeSpan.FromMilliseconds(250);
    private readonly List<string> performanceOverlaySummaryLines = [];
    private readonly List<string> performanceOverlaySectionLines = [];
    private readonly List<string> performanceOverlayRecentSectionLines = [];
    private readonly List<string> performanceOverlayWindowLines = [];
    private string performanceOverlayFailureLine = string.Empty;
    private DateTime performanceOverlayNextRefreshAtUtc = DateTime.MinValue;

    private readonly record struct PerformanceFrameStart(
        long Timestamp,
        long AllocatedBytes,
        int Gen0CollectionCount);

    private PerformanceFrameStart BeginPerformanceFrame()
    {
        var collect = this.ShouldCollectPerformanceProfile();
        this.PreparePerformanceProfileFrame();
        var frameStart = collect
            ? new PerformanceFrameStart(
                Stopwatch.GetTimestamp(),
                GC.GetAllocatedBytesForCurrentThread(),
                GC.CollectionCount(0))
            : default;
        this.performanceStats.Begin(collect);
        this.performanceProfiler.BeginFrame(this.config.ShowDetailedPerformanceProfile || this.config.RecordPerformanceProfile);
        return frameStart;
    }

    private void FinishPerformanceFrame(PerformanceFrameStart frameStart)
    {
        if (!this.performanceStats.Enabled)
        {
            this.performanceProfiler.FinishFrame();
            return;
        }

        if (this.ShouldShowPerformanceOverlay())
        {
            var overlayProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.PerformanceOverlay);
            this.DrawPerformanceOverlay();
            this.performanceProfiler.EndSection(PerformanceProfileSection.PerformanceOverlay, overlayProfileStart);
        }

        this.performanceStats.Finish(
            Stopwatch.GetElapsedTime(frameStart.Timestamp),
            GC.GetAllocatedBytesForCurrentThread() - frameStart.AllocatedBytes,
            GC.CollectionCount(0) - frameStart.Gen0CollectionCount);
        this.performanceProfiler.FinishFrame();

        if (this.performanceProfileRecordingCoordinator.ShouldRecordCurrentFrame)
        {
            this.RecordPerformanceProfileIfNeeded(
                this.performanceProfileRecordingCoordinator.CaptureDiagnosticsThisFrame);
        }
    }

    private bool ShouldCollectPerformanceProfile()
        => this.ShouldShowPerformanceOverlay()
           || this.config.RecordPerformanceProfile;

    private bool ShouldShowPerformanceOverlay()
        => this.config.ShowPerformanceOverlay
           || this.config.ShowDetailedPerformanceProfile
           || this.config.ShowPartyCooldownLogObserver;

    private void DrawPerformanceOverlay()
    {
        const ImGuiWindowFlags Flags = ImGuiWindowFlags.AlwaysAutoResize
                                       | ImGuiWindowFlags.NoSavedSettings
                                       | ImGuiWindowFlags.NoFocusOnAppearing
                                       | ImGuiWindowFlags.NoNav;

        this.RefreshPerformanceOverlayDisplayIfNeeded();
        ImGui.SetNextWindowPos(new Vector2(16f, 16f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0.72f);
        try
        {
            if (ImGui.Begin("FFXIVAura 성능", Flags))
                this.DrawPerformanceOverlayContent();
        }
        finally
        {
            ImGui.End();
        }
    }

    private void DrawPerformanceOverlayContent()
    {
        for (var index = 0; index < this.performanceOverlaySummaryLines.Count; index++)
        {
            if (index == 5)
                ImGui.Separator();

            ImGui.TextUnformatted(this.performanceOverlaySummaryLines[index]);
        }

        if (!string.IsNullOrEmpty(this.performanceOverlayFailureLine))
            ImGui.TextDisabled(this.performanceOverlayFailureLine);
        if (this.config.ShowPartyCooldownLogObserver)
            this.DrawPartyCooldownLogObserver();

        if (this.config.ShowDetailedPerformanceProfile)
            this.DrawDetailedPerformanceProfile();
    }

    private void DrawPartyCooldownLogObserver()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("파티 쿨다운 로그 관측");
        ImGui.SameLine();
        if (ImGui.SmallButton("비우기##party-cooldown-log-observer-clear"))
        {
            this.partyCooldownLogObservations.Clear();
            this.partyCooldownSignalDiagnostics.ResetCandidateMissing();
            this.partyCooldownCandidateObservationThrottle.Reset();
        }

        var signalDiagnostics = this.partyCooldownSignalDiagnostics.CreateSnapshot();
        ImGui.TextDisabled($"후보 없음 {signalDiagnostics.CandidateMissingTotal} / 표본 {signalDiagnostics.CandidateMissingSamples}");

        if (this.partyCooldownLogObservations.LastCandidate is { } candidateSample)
            ImGui.TextDisabled($"후보 없음 최근 표본: log {candidateSample.LogMessageId} / {candidateSample.SourceName}");

        if (this.partyCooldownLogObservations.ActionableCount == 0)
        {
            ImGui.TextDisabled("최근 감지 로그 없음");
            return;
        }

        foreach (var observation in this.partyCooldownLogObservations.EnumerateActionableNewestFirst())
        {
            var action = observation.ActionId > 0
                ? $"{observation.ActionName} ({observation.ActionId})"
                : observation.ActionName;
            var match = observation.ParameterIndex >= 0
                ? $"{observation.MatchSource}@{observation.ParameterIndex}"
                : observation.MatchSource;
            var reason = observation.IgnoredReason == PartyCooldownIgnoredLogReason.None
                ? string.Empty
                : $" / {observation.IgnoredReason}";
            ImGui.TextUnformatted(
                $"{FormatPerformanceProfileTime(observation.TimestampUtc)} [{observation.Result}] log:{observation.LogMessageId} {observation.SourceName} -> {observation.MemberName} / {action} / {match}{reason}");
            ImGui.TextDisabled(
                $"roster {observation.RosterDiagnostics.Source}/{observation.RosterDiagnostics.ReadMode} members {observation.RosterDiagnostics.MemberCount}/{observation.RosterDiagnostics.DisplayMemberCount} alliance {observation.RosterDiagnostics.AllianceGroupAMemberCount}/{observation.RosterDiagnostics.AllianceGroupBMemberCount}/{observation.RosterDiagnostics.AllianceGroupCMemberCount} empty {observation.RosterDiagnostics.AllianceEmptySlotCount}");
            if (!string.IsNullOrWhiteSpace(observation.Detail))
                ImGui.TextDisabled(observation.Detail);
        }
    }

    private void DrawDetailedPerformanceProfile()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("상세 프로파일");
        ImGui.SameLine();
        if (ImGui.SmallButton("초기화##profile-reset"))
        {
            this.performanceProfiler.Reset();
            this.performanceOverlayNextRefreshAtUtc = DateTime.MinValue;
            this.RefreshPerformanceOverlayDisplayIfNeeded();
        }

        for (var index = 0; index < this.performanceOverlaySectionLines.Count; index++)
            ImGui.TextUnformatted(this.performanceOverlaySectionLines[index]);

        this.DrawRecentSectionPerformanceProfile();
        this.DrawWindowPerformanceProfile();
    }

    private void DrawRecentSectionPerformanceProfile()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("최근 5초 평균");
        for (var index = 0; index < this.performanceOverlayRecentSectionLines.Count; index++)
            ImGui.TextUnformatted(this.performanceOverlayRecentSectionLines[index]);
    }

    private void DrawWindowPerformanceProfile()
    {
        if (this.performanceOverlayWindowLines.Count == 0)
            return;

        ImGui.Separator();
        ImGui.TextUnformatted("창별 프로파일");
        for (var index = 0; index < this.performanceOverlayWindowLines.Count; index++)
            ImGui.TextUnformatted(this.performanceOverlayWindowLines[index]);
    }

    private void RefreshPerformanceOverlayDisplayIfNeeded()
    {
        var nowUtc = DateTime.UtcNow;
        if (this.performanceOverlayNextRefreshAtUtc != DateTime.MinValue
            && nowUtc < this.performanceOverlayNextRefreshAtUtc)
        {
            return;
        }

        this.performanceOverlayNextRefreshAtUtc = nowUtc + PerformanceOverlayRefreshInterval;
        this.performanceOverlaySummaryLines.Clear();
        this.performanceOverlaySummaryLines.Add($"플러그인 처리: {this.performanceStats.FrameMilliseconds:0.00} ms");
        this.performanceOverlaySummaryLines.Add($"평균: {this.performanceStats.AverageFrameMilliseconds:0.00} ms");
        this.performanceOverlaySummaryLines.Add($"최대: {this.performanceStats.MaxFrameMilliseconds:0.00} ms");
        this.performanceOverlaySummaryLines.Add($"프레임 할당: {this.performanceStats.FrameAllocatedBytes / 1024.0:0.0} KiB (평균 {this.performanceStats.AverageFrameAllocatedBytes / 1024.0:0.0} KiB)");
        this.performanceOverlaySummaryLines.Add($"Gen0 수집: {this.performanceStats.Gen0CollectionCount}");
        this.performanceOverlaySummaryLines.Add($"창: {this.performanceStats.WindowCount}");
        this.performanceOverlaySummaryLines.Add($"스킬 아이콘: {this.performanceStats.SkillIconCount}");
        this.performanceOverlaySummaryLines.Add($"오라 아이콘: {this.performanceStats.AuraIconCount}");
        this.performanceOverlaySummaryLines.Add($"쿨다운 계산: {this.performanceStats.CooldownCalculationCount}");
        this.performanceOverlaySummaryLines.Add($"키바인드 항목: {this.actionKeybindService.Count}");
        this.performanceOverlaySummaryLines.Add($"툴팁 렌더링: {this.performanceStats.TooltipRenderCount}");
        this.performanceOverlaySummaryLines.Add($"흑백 처리: {this.performanceStats.GrayscaleIconProcessCount}");
        var profileRecordingDiagnostics = this.performanceProfileRecordingCoordinator.CreateDiagnostics();
        this.performanceOverlaySummaryLines.Add($"프로파일 기록: {profileRecordingDiagnostics.LastWriteMilliseconds:0.00} ms / 대기 {profileRecordingDiagnostics.PendingCount} / 실패 {profileRecordingDiagnostics.FailedCount}");
        var configSaveDiagnostics = this.configSaveCoordinator.CreateDiagnostics(nowUtc);
        this.performanceOverlaySummaryLines.Add($"설정 저장: {configSaveDiagnostics.LastSaveMilliseconds:0.00} ms / 대기 {configSaveDiagnostics.QueueCount} / 성공 {configSaveDiagnostics.CompletedCount} / 실패 {configSaveDiagnostics.FailedCount}");
        this.performanceOverlayFailureLine = profileRecordingDiagnostics.RecordingFailureCount > 0
            ? $"프로파일 최근 오류: {profileRecordingDiagnostics.LastError} / 누적 {profileRecordingDiagnostics.RecordingFailureCount}"
            : string.Empty;

        this.performanceOverlaySectionLines.Clear();
        this.performanceOverlayRecentSectionLines.Clear();
        for (var index = 0; index < this.performanceProfiler.SectionCount; index++)
        {
            var snapshot = this.performanceProfiler.GetSnapshot((PerformanceProfileSection)index);
            if (snapshot.SampleFrameCount == 0 && snapshot.LastCallCount == 0)
                continue;

            this.performanceOverlaySectionLines.Add(
                $"{snapshot.Label}: 현재 {snapshot.LastMilliseconds:0.00} / 평균 {snapshot.AverageMilliseconds:0.00} / 최대 {snapshot.MaxMilliseconds:0.00} ms / 호출 {snapshot.LastCallCount}");
            this.performanceOverlayRecentSectionLines.Add(
                $"{snapshot.Label}: 평균 {snapshot.RecentAverageMilliseconds:0.00} ms / 최대 시각 {FormatPerformanceProfileTime(snapshot.MaxOccurredAtUtc)}");
        }

        this.performanceOverlayWindowLines.Clear();
        foreach (var snapshot in this.performanceProfiler.GetWindowSnapshots())
        {
            this.performanceOverlayWindowLines.Add(
                $"{snapshot.Label}: 현재 {snapshot.LastMilliseconds:0.00} / 최근 5초 {snapshot.RecentAverageMilliseconds:0.00} / 평균 {snapshot.AverageMilliseconds:0.00} / 최대 {snapshot.MaxMilliseconds:0.00} ms / 최대 시각 {FormatPerformanceProfileTime(snapshot.MaxOccurredAtUtc)}");
        }
    }

    private static string FormatPerformanceProfileTime(DateTime utc)
        => utc == DateTime.MinValue ? "-" : utc.ToLocalTime().ToString("HH:mm:ss");
}
