using System.Diagnostics;

namespace FFXIVAura;

public sealed unsafe partial class Plugin
{
    private long BeginPerformanceFrame()
    {
        var enabled = this.config.ShowPerformanceOverlay;
        this.performanceStats.Begin(enabled);
        return enabled ? Stopwatch.GetTimestamp() : 0;
    }

    private void FinishPerformanceFrame(long frameStart)
    {
        if (!this.performanceStats.Enabled)
            return;

        this.performanceStats.Finish(Stopwatch.GetElapsedTime(frameStart));
        this.DrawPerformanceOverlay();
    }

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
        ImGui.End();
    }
}
