using System.Diagnostics;

namespace FFXIVAura;

internal sealed class PerformanceProfiler
{
    private static readonly TimeSpan RecentAverageWindow = TimeSpan.FromSeconds(5);
    private const int RecentSampleLimit = 2400;
    private static readonly string[] Labels =
    [
        "오버레이",
        "프레임 모델",
        "위치 보정",
        "쿨다운",
        "스킬 아이콘",
        "오라 아이콘",
        "오라 스캔",
        "키바인드 재구성",
        "툴팁 렌더링",
        "흑백 처리",
        "\uC124\uC815 \uC800\uC7A5",
        "스킬 후보 구성",
        "오라 인덱스 구성",
        "오라 검색",
    ];

    private readonly SectionStats[] sections;
    private readonly Dictionary<string, NamedStats> windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> currentWindowIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> staleWindowIds = [];

    public PerformanceProfiler()
    {
        this.sections = Enumerable.Range(0, (int)PerformanceProfileSection.Count)
            .Select(_ => new SectionStats())
            .ToArray();
    }

    public bool Enabled { get; private set; }

    public int SectionCount => this.sections.Length;

    public void BeginFrame(bool enabled)
    {
        if (this.Enabled != enabled)
            this.Reset();

        this.Enabled = enabled;
        if (!enabled)
            return;

        this.currentWindowIds.Clear();
        foreach (var section in this.sections)
            section.BeginFrame();

        foreach (var window in this.windows.Values)
            window.Stats.BeginFrame();
    }

    public void FinishFrame()
    {
        this.FinishFrame(DateTime.UtcNow);
    }

    internal void FinishFrame(DateTime nowUtc)
    {
        if (!this.Enabled)
            return;

        foreach (var section in this.sections)
            section.FinishFrame(nowUtc);

        this.staleWindowIds.Clear();
        foreach (var key in this.windows.Keys)
        {
            if (!this.currentWindowIds.Contains(key))
            {
                this.staleWindowIds.Add(key);
                continue;
            }

            this.windows[key].Stats.FinishFrame(nowUtc);
        }

        foreach (var key in this.staleWindowIds)
            this.windows.Remove(key);
    }

    public long BeginSection(PerformanceProfileSection section)
        => this.Enabled && IsValid(section) ? Stopwatch.GetTimestamp() : 0;

    public void EndSection(PerformanceProfileSection section, long startTimestamp)
    {
        if (!this.Enabled || startTimestamp == 0 || !IsValid(section))
            return;

        this.Record(section, Stopwatch.GetElapsedTime(startTimestamp));
    }

    public void Record(PerformanceProfileSection section, TimeSpan elapsed)
    {
        if (!this.Enabled || !IsValid(section))
            return;

        this.sections[(int)section].Record(elapsed);
    }

    public long BeginWindow(string id, string label)
        => this.Enabled && !string.IsNullOrWhiteSpace(id)
            ? this.BeginWindowSample(id, label)
            : 0;

    public void EndWindow(string id, string label, long startTimestamp)
    {
        if (!this.Enabled || startTimestamp == 0 || string.IsNullOrWhiteSpace(id))
            return;

        this.RecordWindow(id, label, Stopwatch.GetElapsedTime(startTimestamp));
    }

    public void RecordWindow(string id, string label, TimeSpan elapsed)
    {
        if (!this.Enabled || string.IsNullOrWhiteSpace(id))
            return;

        var window = this.GetOrCreateWindow(id, label);
        window.Stats.Record(elapsed);
    }

    public PerformanceProfileSectionSnapshot GetSnapshot(PerformanceProfileSection section)
    {
        if (!IsValid(section))
            return default;

        var stats = this.sections[(int)section];
        return new PerformanceProfileSectionSnapshot(
            section,
            Labels[(int)section],
            stats.LastMilliseconds,
            stats.AverageMilliseconds,
            stats.RecentAverageMilliseconds,
            stats.MaxMilliseconds,
            stats.MaxOccurredAtUtc,
            stats.LastCallCount,
            stats.TotalCallCount,
            stats.SampleFrameCount);
    }

    public IEnumerable<PerformanceProfileSectionSnapshot> GetSnapshots()
    {
        for (var index = 0; index < this.sections.Length; index++)
            yield return this.GetSnapshot((PerformanceProfileSection)index);
    }

    public IEnumerable<PerformanceProfileWindowSnapshot> GetWindowSnapshots()
    {
        foreach (var (id, window) in this.windows.OrderBy(pair => pair.Value.Label, StringComparer.OrdinalIgnoreCase))
        {
            var stats = window.Stats;
            yield return new PerformanceProfileWindowSnapshot(
                id,
                window.Label,
                stats.LastMilliseconds,
                stats.AverageMilliseconds,
                stats.RecentAverageMilliseconds,
                stats.MaxMilliseconds,
                stats.MaxOccurredAtUtc,
                stats.LastCallCount,
                stats.TotalCallCount,
                stats.SampleFrameCount);
        }
    }

    public void Reset()
    {
        foreach (var section in this.sections)
            section.Reset();

        this.windows.Clear();
        this.currentWindowIds.Clear();
        this.staleWindowIds.Clear();
    }

    private static bool IsValid(PerformanceProfileSection section)
        => section >= 0 && section < PerformanceProfileSection.Count;

    private long BeginWindowSample(string id, string label)
    {
        this.GetOrCreateWindow(id, label);
        return Stopwatch.GetTimestamp();
    }

    private NamedStats GetOrCreateWindow(string id, string label)
    {
        var key = id.Trim();
        this.currentWindowIds.Add(key);
        if (!this.windows.TryGetValue(key, out var window))
        {
            window = new NamedStats();
            this.windows[key] = window;
        }

        window.Label = string.IsNullOrWhiteSpace(label) ? key : label.Trim();
        return window;
    }

    private sealed class NamedStats
    {
        public string Label { get; set; } = string.Empty;
        public SectionStats Stats { get; } = new();
    }

    private sealed class SectionStats
    {
        private double currentMilliseconds;
        private int currentCallCount;
        private readonly Queue<RecentSample> recentSamples = new();
        private double recentMillisecondsTotal;

        public double LastMilliseconds { get; private set; }
        public double AverageMilliseconds { get; private set; }
        public double RecentAverageMilliseconds { get; private set; }
        public double MaxMilliseconds { get; private set; }
        public DateTime MaxOccurredAtUtc { get; private set; } = DateTime.MinValue;
        public int LastCallCount { get; private set; }
        public long TotalCallCount { get; private set; }
        public int SampleFrameCount { get; private set; }

        public void BeginFrame()
        {
            this.currentMilliseconds = 0;
            this.currentCallCount = 0;
        }

        public void Record(TimeSpan elapsed)
        {
            this.currentMilliseconds += Math.Max(0, elapsed.TotalMilliseconds);
            this.currentCallCount++;
        }

        public void FinishFrame(DateTime nowUtc)
        {
            this.LastMilliseconds = this.currentMilliseconds;
            this.LastCallCount = this.currentCallCount;
            if (this.currentCallCount <= 0)
            {
                this.PruneRecentSamples(nowUtc);
                this.UpdateRecentAverage();
                return;
            }

            this.SampleFrameCount++;
            this.TotalCallCount += this.currentCallCount;
            this.AverageMilliseconds = this.SampleFrameCount == 1
                ? this.currentMilliseconds
                : this.AverageMilliseconds + (this.currentMilliseconds - this.AverageMilliseconds) / this.SampleFrameCount;
            if (this.currentMilliseconds >= this.MaxMilliseconds)
            {
                this.MaxMilliseconds = this.currentMilliseconds;
                this.MaxOccurredAtUtc = nowUtc;
            }

            this.recentSamples.Enqueue(new RecentSample(nowUtc, this.currentMilliseconds));
            this.recentMillisecondsTotal += this.currentMilliseconds;
            this.PruneRecentSamples(nowUtc);
            this.UpdateRecentAverage();
        }

        private void UpdateRecentAverage()
        {
            this.RecentAverageMilliseconds = this.recentSamples.Count == 0
                ? 0
                : this.recentMillisecondsTotal / this.recentSamples.Count;
        }

        public void Reset()
        {
            this.currentMilliseconds = 0;
            this.currentCallCount = 0;
            this.LastMilliseconds = 0;
            this.AverageMilliseconds = 0;
            this.RecentAverageMilliseconds = 0;
            this.MaxMilliseconds = 0;
            this.MaxOccurredAtUtc = DateTime.MinValue;
            this.LastCallCount = 0;
            this.TotalCallCount = 0;
            this.SampleFrameCount = 0;
            this.recentSamples.Clear();
            this.recentMillisecondsTotal = 0;
        }

        private void PruneRecentSamples(DateTime now)
        {
            while (this.recentSamples.Count > 0
                   && (now - this.recentSamples.Peek().At > RecentAverageWindow || this.recentSamples.Count > RecentSampleLimit))
            {
                this.recentMillisecondsTotal -= this.recentSamples.Dequeue().Milliseconds;
            }
        }
    }

    private readonly record struct RecentSample(DateTime At, double Milliseconds);
}
