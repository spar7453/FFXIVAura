namespace FFXIVAura;

internal readonly record struct ConfigSaveCoordinatorOptions(
    TimeSpan DebounceDelay,
    TimeSpan RetryDelay,
    TimeSpan MaxCombatDeferDuration,
    TimeSpan ErrorLogInterval)
{
    public static ConfigSaveCoordinatorOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(30));
}

internal readonly record struct ConfigSaveDiagnostics(
    bool Pending,
    bool DeferredInCombat,
    double PendingSeconds,
    int QueueCount,
    long DroppedCount,
    long CompletedCount,
    long FailedCount,
    double LastSaveMilliseconds,
    double MaxSaveMilliseconds);

internal sealed class ConfigSaveCoordinator : IDisposable
{
    private readonly ConfigSaveWorker worker;
    private readonly PerformanceProfiler performanceProfiler;
    private readonly Action<Exception, string> logError;
    private readonly Action<string> noteDiagnostic;
    private readonly ConfigSaveCoordinatorOptions options;
    private bool pending;
    private bool deferredInCombat;
    private bool completed;
    private DateTime saveAfterUtc = DateTime.MinValue;
    private DateTime queuedAtUtc = DateTime.MinValue;
    private DateTime nextErrorLogAtUtc = DateTime.MinValue;

    public ConfigSaveCoordinator(
        Action<PluginConfig> save,
        PerformanceProfiler performanceProfiler,
        Action<Exception, string> logError,
        Action<string> noteDiagnostic,
        ConfigSaveCoordinatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        this.performanceProfiler = performanceProfiler
                                   ?? throw new ArgumentNullException(nameof(performanceProfiler));
        this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
        this.noteDiagnostic = noteDiagnostic ?? throw new ArgumentNullException(nameof(noteDiagnostic));
        this.options = options ?? ConfigSaveCoordinatorOptions.Default;
        this.worker = new ConfigSaveWorker(save);
    }

    public void Queue(DateTime nowUtc)
    {
        if (this.completed)
            return;

        if (!this.pending)
            this.queuedAtUtc = nowUtc;

        this.pending = true;
        this.saveAfterUtc = nowUtc.Add(this.options.DebounceDelay);
    }

    public void ObserveWorkerError(DateTime nowUtc)
    {
        if (this.completed || this.worker.TakeLastError() is not { } exception)
            return;

        if (nowUtc >= this.nextErrorLogAtUtc)
        {
            this.nextErrorLogAtUtc = nowUtc.Add(this.options.ErrorLogInterval);
            this.logError(exception, "Failed to save FFXIVAura configuration; retrying the latest snapshot.");
        }

        this.MarkPending(nowUtc, this.options.RetryDelay);
        this.noteDiagnostic("configSaveFailed");
    }

    public bool SaveNow(PluginConfig config, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (this.completed)
            return false;

        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.ConfigSave);
        var accepted = false;
        try
        {
            accepted = this.worker.TryEnqueue(PluginConfigClone.CreateSnapshot(config));
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.ConfigSave, profileStart);
        }

        if (!accepted)
        {
            // The bounded save queue is full, so this snapshot was dropped. Re-mark pending so the
            // latest state is retried (latest-wins), and surface the drop as a visible diagnostic.
            this.MarkPending(nowUtc, this.options.RetryDelay);
            this.noteDiagnostic("configSaveDropped");
            return false;
        }

        this.noteDiagnostic("configSaveQueued");
        this.ClearPending();
        return true;
    }

    public bool Flush(
        PluginConfig config,
        in PlayerFrameContext playerContext,
        DateTime nowUtc,
        bool force)
    {
        if (!this.pending)
            return false;

        if (!force && nowUtc < this.saveAfterUtc)
            return false;

        if (!force && this.ShouldDeferInCombat(playerContext, nowUtc))
        {
            this.deferredInCombat = true;
            this.saveAfterUtc = nowUtc.Add(this.options.RetryDelay);
            return false;
        }

        return this.SaveNow(config, nowUtc);
    }

    public bool Complete(PluginConfig config, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (this.completed)
            return true;

        _ = this.Flush(config, default, nowUtc, force: true);
        var alreadyQueuedGeneration = this.pending
            ? 0
            : this.worker.LastAcceptedGeneration;
        var finalSnapshot = this.pending || alreadyQueuedGeneration > 0
            ? PluginConfigClone.CreateSnapshot(config)
            : null;
        var saved = this.worker.CompleteAndSaveLatest(finalSnapshot, alreadyQueuedGeneration);
        if (finalSnapshot is not null && saved)
            this.ClearPending();

        if (this.worker.TakeLastError() is { } exception)
        {
            this.logError(
                exception,
                "Failed to save the final FFXIVAura configuration snapshot during unload.");
        }

        this.completed = true;
        return saved;
    }

    public ConfigSaveDiagnostics CreateDiagnostics(DateTime nowUtc)
        => new(
            this.pending,
            this.deferredInCombat,
            this.queuedAtUtc == DateTime.MinValue
                ? 0
                : Math.Max(0, (nowUtc - this.queuedAtUtc).TotalSeconds),
            this.worker.PendingCount,
            this.worker.DroppedCount,
            this.worker.CompletedCount,
            this.worker.FailedCount,
            this.worker.LastSaveMilliseconds,
            this.worker.MaxSaveMilliseconds);

    public void Dispose()
    {
        if (this.completed)
            return;

        this.worker.Dispose();
        this.completed = true;
    }

    private bool ShouldDeferInCombat(in PlayerFrameContext playerContext, DateTime nowUtc)
    {
        if (!playerContext.IsReady || !playerContext.IsInCombat)
            return false;

        var queuedAtUtc = this.queuedAtUtc == DateTime.MinValue
            ? nowUtc
            : this.queuedAtUtc;
        return nowUtc - queuedAtUtc < this.options.MaxCombatDeferDuration;
    }

    private void MarkPending(DateTime nowUtc, TimeSpan delay)
    {
        if (!this.pending)
            this.queuedAtUtc = nowUtc;

        this.pending = true;
        this.saveAfterUtc = nowUtc.Add(delay);
    }

    private void ClearPending()
    {
        this.pending = false;
        this.deferredInCombat = false;
        this.saveAfterUtc = DateTime.MinValue;
        this.queuedAtUtc = DateTime.MinValue;
    }
}
