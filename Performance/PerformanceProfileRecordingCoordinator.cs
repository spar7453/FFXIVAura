namespace FFXIVAura;

internal readonly record struct PerformanceProfileFrameDecision(
    bool ShouldRecord,
    bool IncludeDiagnostics);

internal readonly record struct PerformanceProfileRecordingDiagnostics(
    int PendingCount,
    long DroppedCount,
    long CompletedCount,
    long FailedCount,
    double LastWriteMilliseconds,
    double MaxWriteMilliseconds,
    DateTime LastCompletedAtUtc,
    long RecordingFailureCount,
    string LastError,
    DateTime LastErrorAtUtc);

internal sealed class PerformanceProfileRecordingCoordinator : IDisposable
{
    public const int MinRecordIntervalSeconds = 1;
    public const int MaxRecordIntervalSeconds = 60;
    public const int DiagnosticIntervalSeconds = 10;

    private static readonly TimeSpan ErrorLogInterval = TimeSpan.FromSeconds(30);
    private readonly PerformanceProfileWriter writer;
    private readonly Action<string> setBugDiagnosticEvent;
    private readonly Action<Exception, string> logError;
    private DateTime nextRecordAtUtc = DateTime.MinValue;
    private DateTime nextDiagnosticAtUtc = DateTime.MinValue;
    private DateTime nextErrorLogAtUtc = DateTime.MinValue;
    private DateTime lastErrorAtUtc = DateTime.MinValue;
    private long failureCount;
    private string lastError = string.Empty;

    public PerformanceProfileRecordingCoordinator(
        string directory,
        Action<string> setBugDiagnosticEvent,
        Action<Exception, string> logError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.FilePath = Path.Combine(directory, PerformanceProfileCsv.FileName);
        this.setBugDiagnosticEvent = setBugDiagnosticEvent
                                     ?? throw new ArgumentNullException(nameof(setBugDiagnosticEvent));
        this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
        this.writer = new PerformanceProfileWriter();
        try
        {
            if (!this.writer.TryEnqueueEnsureSchema(this.FilePath, PerformanceProfileCsv.Header))
                this.NoteFailure("schemaQueueFull", null, DateTime.UtcNow);
        }
        catch
        {
            try
            {
                this.writer.Dispose();
            }
            catch
            {
                // Preserve the initialization exception.
            }

            throw;
        }
    }

    public string FilePath { get; }

    public bool ShouldRecordCurrentFrame { get; private set; }

    public bool CaptureDiagnosticsThisFrame { get; private set; }

    public PerformanceProfileFrameDecision Prepare(
        bool enabled,
        int requestedIntervalSeconds,
        DateTime nowUtc)
    {
        this.ShouldRecordCurrentFrame = false;
        this.CaptureDiagnosticsThisFrame = false;
        if (this.writer.TakeLastError() is { } writerError)
            this.NoteFailure("writer", writerError, nowUtc);

        if (!enabled)
        {
            this.ResetSchedule();
            return default;
        }

        if (this.nextRecordAtUtc != DateTime.MinValue && nowUtc < this.nextRecordAtUtc)
            return default;

        var intervalSeconds = Math.Clamp(
            requestedIntervalSeconds,
            MinRecordIntervalSeconds,
            MaxRecordIntervalSeconds);
        this.nextRecordAtUtc = nowUtc.AddSeconds(intervalSeconds);

        var includeDiagnostics = this.nextDiagnosticAtUtc == DateTime.MinValue
                                 || nowUtc >= this.nextDiagnosticAtUtc;
        if (includeDiagnostics)
            this.nextDiagnosticAtUtc = nowUtc.AddSeconds(DiagnosticIntervalSeconds);

        this.ShouldRecordCurrentFrame = true;
        this.CaptureDiagnosticsThisFrame = includeDiagnostics;
        return new PerformanceProfileFrameDecision(true, includeDiagnostics);
    }

    public bool TryAppend(string prefix, string content, long maxBytes, DateTime nowUtc)
    {
        if (this.writer.TryEnqueueAppend(
                this.FilePath,
                PerformanceProfileCsv.Header,
                prefix,
                content,
                maxBytes))
        {
            return true;
        }

        this.setBugDiagnosticEvent("performanceProfileWriteQueueFull");
        this.NoteFailure("queueFull", null, nowUtc);
        return false;
    }

    public void Clear()
    {
        if (!this.writer.TryEnqueueClear(this.FilePath))
            this.setBugDiagnosticEvent("performanceProfileClearQueueFull");

        this.ResetSchedule();
    }

    public void NoteFailure(string stage, Exception? exception, DateTime nowUtc)
    {
        this.failureCount++;
        this.lastErrorAtUtc = nowUtc;
        this.lastError = exception is null ? stage : $"{stage}:{exception.GetType().Name}";
        this.setBugDiagnosticEvent($"performanceProfileFailed:{this.lastError}");

        if (exception is null || nowUtc < this.nextErrorLogAtUtc)
            return;

        this.nextErrorLogAtUtc = nowUtc + ErrorLogInterval;
        this.logError(exception, $"Failed to record FFXIVAura performance profile ({stage}).");
    }

    public PerformanceProfileRecordingDiagnostics CreateDiagnostics()
        => new(
            this.writer.PendingCount,
            this.writer.DroppedCount,
            this.writer.CompletedCount,
            this.writer.FailedCount,
            this.writer.LastWriteMilliseconds,
            this.writer.MaxWriteMilliseconds,
            this.writer.LastCompletedAtUtc,
            this.failureCount,
            this.lastError,
            this.lastErrorAtUtc);

    public void Dispose()
        => this.writer.Dispose();

    private void ResetSchedule()
    {
        this.nextRecordAtUtc = DateTime.MinValue;
        this.nextDiagnosticAtUtc = DateTime.MinValue;
        this.ShouldRecordCurrentFrame = false;
        this.CaptureDiagnosticsThisFrame = false;
    }
}
