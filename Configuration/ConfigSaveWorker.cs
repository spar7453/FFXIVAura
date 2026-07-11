using System.Diagnostics;
using System.Threading.Channels;

namespace FFXIVAura;

internal sealed class ConfigSaveWorker : IDisposable
{
    private const int QueueCapacity = 16;
    private const int SaveAttemptCount = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly Action<PluginConfig> save;
    private readonly Channel<PluginConfig> snapshots;
    private readonly Task worker;
    private readonly object metricsLock = new();
    private Exception? lastError;
    private double lastSaveMilliseconds;
    private double maxSaveMilliseconds;
    private int pendingCount;
    private long droppedCount;
    private long completedCount;
    private long failedCount;
    private bool disposed;

    public ConfigSaveWorker(Action<PluginConfig> save)
    {
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.snapshots = Channel.CreateBounded<PluginConfig>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        this.worker = Task.Run(this.ProcessSnapshotsAsync);
    }

    public int PendingCount => Volatile.Read(ref this.pendingCount);

    public long DroppedCount => Interlocked.Read(ref this.droppedCount);

    public long CompletedCount => Interlocked.Read(ref this.completedCount);

    public long FailedCount => Interlocked.Read(ref this.failedCount);

    public double LastSaveMilliseconds
    {
        get
        {
            lock (this.metricsLock)
                return this.lastSaveMilliseconds;
        }
    }

    public double MaxSaveMilliseconds
    {
        get
        {
            lock (this.metricsLock)
                return this.maxSaveMilliseconds;
        }
    }

    public bool TryEnqueue(PluginConfig snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (this.disposed || !this.snapshots.Writer.TryWrite(snapshot))
        {
            Interlocked.Increment(ref this.droppedCount);
            return false;
        }

        Interlocked.Increment(ref this.pendingCount);
        return true;
    }

    public Exception? TakeLastError()
        => Interlocked.Exchange(ref this.lastError, null);

    public void Dispose()
        => this.CompleteAndSaveLatest(null);

    public bool CompleteAndSaveLatest(PluginConfig? latestSnapshot)
    {
        if (this.disposed)
            return latestSnapshot is null;

        this.disposed = true;
        this.snapshots.Writer.TryComplete();
        this.worker.GetAwaiter().GetResult();
        return latestSnapshot is null || this.SaveSynchronously(latestSnapshot);
    }

    private async Task ProcessSnapshotsAsync()
    {
        await foreach (var snapshot in this.snapshots.Reader.ReadAllAsync())
        {
            var started = Stopwatch.GetTimestamp();
            Exception? finalError = null;
            var saved = false;
            for (var attempt = 0; attempt < SaveAttemptCount; attempt++)
            {
                try
                {
                    this.save(snapshot);
                    saved = true;
                    break;
                }
                catch (Exception ex)
                {
                    finalError = ex;
                    if (attempt + 1 < SaveAttemptCount)
                        await Task.Delay(RetryDelay).ConfigureAwait(false);
                }
            }

            try
            {
                if (saved)
                {
                    Interlocked.Increment(ref this.completedCount);
                }
                else
                {
                    Interlocked.Increment(ref this.failedCount);
                    Interlocked.Exchange(ref this.lastError, finalError);
                }
            }
            finally
            {
                var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                lock (this.metricsLock)
                {
                    this.lastSaveMilliseconds = milliseconds;
                    this.maxSaveMilliseconds = Math.Max(this.maxSaveMilliseconds, milliseconds);
                }

                Interlocked.Decrement(ref this.pendingCount);
            }
        }
    }

    private bool SaveSynchronously(PluginConfig snapshot)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? finalError = null;
        var saved = false;
        for (var attempt = 0; attempt < SaveAttemptCount; attempt++)
        {
            try
            {
                this.save(snapshot);
                saved = true;
                break;
            }
            catch (Exception ex)
            {
                finalError = ex;
                if (attempt + 1 < SaveAttemptCount)
                    Thread.Sleep(RetryDelay);
            }
        }

        if (saved)
        {
            Interlocked.Increment(ref this.completedCount);
        }
        else
        {
            Interlocked.Increment(ref this.failedCount);
            Interlocked.Exchange(ref this.lastError, finalError);
        }

        var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        lock (this.metricsLock)
        {
            this.lastSaveMilliseconds = milliseconds;
            this.maxSaveMilliseconds = Math.Max(this.maxSaveMilliseconds, milliseconds);
        }

        return saved;
    }
}
