using System.Diagnostics;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private long BeginPerformanceFrame()
    {
        var collect = this.ShouldCollectPerformanceProfile();
        this.performanceStats.Begin(collect);
        this.performanceProfiler.BeginFrame(this.config.ShowDetailedPerformanceProfile || this.config.RecordPerformanceProfile);
        return collect ? Stopwatch.GetTimestamp() : 0;
    }

    private void FinishPerformanceFrame(long frameStart)
    {
        this.performanceProfiler.FinishFrame();
        if (!this.performanceStats.Enabled)
            return;

        this.performanceStats.Finish(Stopwatch.GetElapsedTime(frameStart));
        this.RecordPerformanceProfileIfNeeded();
        if (this.ShouldShowPerformanceOverlay())
            this.DrawPerformanceOverlay();
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

        ImGui.SetNextWindowPos(new Vector2(16f, 16f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0.72f);
        if (!ImGui.Begin("FFXIVAura 성능", Flags))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted($"플러그인 처리: {this.performanceStats.FrameMilliseconds:0.00} ms");
        ImGui.TextUnformatted($"평균: {this.performanceStats.AverageFrameMilliseconds:0.00} ms");
        ImGui.TextUnformatted($"최대: {this.performanceStats.MaxFrameMilliseconds:0.00} ms");
        ImGui.Separator();
        ImGui.TextUnformatted($"창: {this.performanceStats.WindowCount}");
        ImGui.TextUnformatted($"스킬 아이콘: {this.performanceStats.SkillIconCount}");
        ImGui.TextUnformatted($"오라 아이콘: {this.performanceStats.AuraIconCount}");
        ImGui.TextUnformatted($"쿨다운 계산: {this.performanceStats.CooldownCalculationCount}");
        ImGui.TextUnformatted($"키바인드 항목: {this.actionKeybindIndex.Count}");
        ImGui.TextUnformatted($"툴팁 제어: {this.performanceStats.NativeTooltipControlCount}");
        ImGui.TextUnformatted($"흑백 처리: {this.performanceStats.GrayscaleIconProcessCount}");
        if (this.config.ShowPartyCooldownLogObserver)
            this.DrawPartyCooldownLogObserver();

        if (this.config.ShowDetailedPerformanceProfile)
            this.DrawDetailedPerformanceProfile();

        ImGui.End();
    }

    private void DrawPartyCooldownLogObserver()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("파티 쿨다운 로그 관측");
        ImGui.SameLine();
        if (ImGui.SmallButton("비우기##party-cooldown-log-observer-clear"))
            this.partyCooldownLogObservations.Clear();

        if (this.partyCooldownLogObservations.Count == 0)
        {
            ImGui.TextDisabled("최근 감지 로그 없음");
            return;
        }

        foreach (var observation in this.partyCooldownLogObservations.Reverse())
        {
            var action = observation.ActionId > 0
                ? $"{observation.ActionName} ({observation.ActionId})"
                : observation.ActionName;
            var match = observation.ParameterIndex >= 0
                ? $"{observation.MatchSource}@{observation.ParameterIndex}"
                : observation.MatchSource;
            ImGui.TextUnformatted(
                $"{FormatPerformanceProfileTime(observation.TimestampUtc)} [{observation.Result}] log:{observation.LogMessageId} {observation.SourceName} -> {observation.MemberName} / {action} / {match}");
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
            this.performanceProfiler.Reset();

        for (var index = 0; index < this.performanceProfiler.SectionCount; index++)
        {
            var snapshot = this.performanceProfiler.GetSnapshot((PerformanceProfileSection)index);
            if (snapshot.SampleFrameCount == 0 && snapshot.LastCallCount == 0)
                continue;

            ImGui.TextUnformatted(
                $"{snapshot.Label}: 현재 {snapshot.LastMilliseconds:0.00} / 평균 {snapshot.AverageMilliseconds:0.00} / 최대 {snapshot.MaxMilliseconds:0.00} ms / 호출 {snapshot.LastCallCount}");
        }

        this.DrawRecentSectionPerformanceProfile();
        this.DrawWindowPerformanceProfile();
    }

    private void DrawRecentSectionPerformanceProfile()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("최근 5초 평균");
        for (var index = 0; index < this.performanceProfiler.SectionCount; index++)
        {
            var snapshot = this.performanceProfiler.GetSnapshot((PerformanceProfileSection)index);
            if (snapshot.SampleFrameCount == 0 && snapshot.LastCallCount == 0)
                continue;

            ImGui.TextUnformatted(
                $"{snapshot.Label}: 평균 {snapshot.RecentAverageMilliseconds:0.00} ms / 최대 시각 {FormatPerformanceProfileTime(snapshot.MaxOccurredAtUtc)}");
        }
    }

    private void DrawWindowPerformanceProfile()
    {
        var drewHeader = false;
        foreach (var snapshot in this.performanceProfiler.GetWindowSnapshots())
        {
            if (!drewHeader)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("창별 프로파일");
                drewHeader = true;
            }

            ImGui.TextUnformatted(
                $"{snapshot.Label}: 현재 {snapshot.LastMilliseconds:0.00} / 최근 5초 {snapshot.RecentAverageMilliseconds:0.00} / 평균 {snapshot.AverageMilliseconds:0.00} / 최대 {snapshot.MaxMilliseconds:0.00} ms / 최대 시각 {FormatPerformanceProfileTime(snapshot.MaxOccurredAtUtc)}");
        }
    }

    private static string FormatPerformanceProfileTime(DateTime utc)
        => utc == DateTime.MinValue ? "-" : utc.ToLocalTime().ToString("HH:mm:ss");
}
