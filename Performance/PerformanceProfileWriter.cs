using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace FFXIVAura;

internal sealed class PerformanceProfileWriter : IDisposable
{
    private const int QueueCapacity = 16;
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    private readonly Channel<WriteRequest> requests;
    private readonly Task worker;
    private readonly object metricsLock = new();
    private Exception? lastError;
    private double lastWriteMilliseconds;
    private double maxWriteMilliseconds;
    private int pendingCount;
    private long droppedCount;
    private long completedCount;
    private long failedCount;
    private long lastCompletedAtUtcTicks;
    private bool disposed;

    public PerformanceProfileWriter()
    {
        this.requests = Channel.CreateBounded<WriteRequest>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        this.worker = Task.Run(this.ProcessRequestsAsync);
    }

    public int PendingCount => Volatile.Read(ref this.pendingCount);

    public long DroppedCount => Interlocked.Read(ref this.droppedCount);

    public long CompletedCount => Interlocked.Read(ref this.completedCount);

    public long FailedCount => Interlocked.Read(ref this.failedCount);

    public DateTime LastCompletedAtUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref this.lastCompletedAtUtcTicks);
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
        }
    }

    public double LastWriteMilliseconds
    {
        get
        {
            lock (this.metricsLock)
                return this.lastWriteMilliseconds;
        }
    }

    public double MaxWriteMilliseconds
    {
        get
        {
            lock (this.metricsLock)
                return this.maxWriteMilliseconds;
        }
    }

    public bool TryEnqueueAppend(string path, string header, string content, long maxBytes)
        => this.TryEnqueueAppend(path, header, string.Empty, content, maxBytes);

    public bool TryEnqueueAppend(
        string path,
        string header,
        string prefix,
        string content,
        long maxBytes)
        => this.TryEnqueue(new WriteRequest(
            WriteRequestKind.Append,
            path,
            header,
            prefix,
            content,
            Math.Max(1, maxBytes)));

    public bool TryEnqueueClear(string path)
        => this.TryEnqueue(new WriteRequest(
            WriteRequestKind.Clear,
            path,
            string.Empty,
            string.Empty,
            string.Empty,
            0));

    public bool TryEnqueueEnsureSchema(string path, string header)
        => this.TryEnqueue(new WriteRequest(
            WriteRequestKind.EnsureSchema,
            path,
            header,
            string.Empty,
            string.Empty,
            0));

    public Exception? TakeLastError()
        => Interlocked.Exchange(ref this.lastError, null);

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.requests.Writer.TryComplete();
        this.worker.GetAwaiter().GetResult();
    }

    private bool TryEnqueue(WriteRequest request)
    {
        if (this.disposed)
        {
            Interlocked.Increment(ref this.droppedCount);
            return false;
        }

        Interlocked.Increment(ref this.pendingCount);
        if (this.requests.Writer.TryWrite(request))
            return true;

        Interlocked.Decrement(ref this.pendingCount);
        Interlocked.Increment(ref this.droppedCount);
        return false;
    }

    private async Task ProcessRequestsAsync()
    {
        await foreach (var request in this.requests.Reader.ReadAllAsync())
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                switch (request.Kind)
                {
                    case WriteRequestKind.Append:
                        Append(request);
                        break;
                    case WriteRequestKind.Clear:
                        ClearFiles(request.Path);
                        break;
                    case WriteRequestKind.EnsureSchema:
                        EnsureSchema(request.Path, request.Header);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, null);
                }

                Interlocked.Increment(ref this.completedCount);
                Interlocked.Exchange(ref this.lastCompletedAtUtcTicks, DateTime.UtcNow.Ticks);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref this.failedCount);
                Interlocked.Exchange(ref this.lastError, ex);
            }
            finally
            {
                var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                lock (this.metricsLock)
                {
                    this.lastWriteMilliseconds = milliseconds;
                    this.maxWriteMilliseconds = Math.Max(this.maxWriteMilliseconds, milliseconds);
                }

                Interlocked.Decrement(ref this.pendingCount);
            }
        }
    }

    private static void Append(WriteRequest request)
    {
        var directory = Path.GetDirectoryName(request.Path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        EnsureSchema(request.Path, request.Header);
        RotateIfNeeded(request.Path, request.MaxBytes);
        var fileIsEmpty = !File.Exists(request.Path) || new FileInfo(request.Path).Length == 0;
        using var writer = new StreamWriter(request.Path, append: true, FileEncoding);
        if (fileIsEmpty)
            writer.WriteLine(request.Header);

        writer.Write(request.Prefix);
        writer.Write(request.Content);
    }

    private static void RotateIfNeeded(string path, long maxBytes)
    {
        if (!File.Exists(path))
            return;

        var file = new FileInfo(path);
        if (file.Length >= maxBytes)
            RotateToPrevious(path);
    }

    private static void EnsureSchema(string path, string header)
    {
        if (HasMismatchedHeader(path, header)
            || HasMismatchedHeader(GetPreviousPath(path), header))
        {
            ClearFiles(path);
        }
    }

    private static bool HasMismatchedHeader(string path, string header)
    {
        if (!File.Exists(path))
            return false;

        var file = new FileInfo(path);
        if (file.Length == 0)
            return false;

        using var reader = new StreamReader(path, FileEncoding, detectEncodingFromByteOrderMarks: true);
        return !string.Equals(reader.ReadLine(), header, StringComparison.Ordinal);
    }

    private static void ClearFiles(string path)
    {
        File.Delete(path);
        File.Delete(GetPreviousPath(path));
    }

    private static void RotateToPrevious(string path)
    {
        var previousPath = GetPreviousPath(path);
        File.Delete(previousPath);
        File.Move(path, previousPath);
    }

    private static string GetPreviousPath(string path)
        => Path.Combine(Path.GetDirectoryName(path)!, PerformanceProfileCsv.PreviousFileName);

    private enum WriteRequestKind
    {
        Append,
        Clear,
        EnsureSchema,
    }

    private readonly record struct WriteRequest(
        WriteRequestKind Kind,
        string Path,
        string Header,
        string Prefix,
        string Content,
        long MaxBytes);
}
