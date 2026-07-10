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
        => this.TryEnqueue(new WriteRequest(WriteRequestKind.Append, path, header, content, Math.Max(1, maxBytes)));

    public bool TryEnqueueClear(string path)
        => this.TryEnqueue(new WriteRequest(WriteRequestKind.Clear, path, string.Empty, string.Empty, 0));

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
        if (this.disposed || !this.requests.Writer.TryWrite(request))
        {
            Interlocked.Increment(ref this.droppedCount);
            return false;
        }

        Interlocked.Increment(ref this.pendingCount);
        return true;
    }

    private async Task ProcessRequestsAsync()
    {
        await foreach (var request in this.requests.Reader.ReadAllAsync())
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                if (request.Kind == WriteRequestKind.Clear)
                    ClearFiles(request.Path);
                else
                    Append(request);

                Interlocked.Increment(ref this.completedCount);
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

        RotateIfNeeded(request.Path, request.Header, request.MaxBytes);
        var fileIsEmpty = !File.Exists(request.Path) || new FileInfo(request.Path).Length == 0;
        using var writer = new StreamWriter(request.Path, append: true, FileEncoding);
        if (fileIsEmpty)
            writer.WriteLine(request.Header);

        writer.Write(request.Content);
    }

    private static void RotateIfNeeded(string path, string header, long maxBytes)
    {
        if (!File.Exists(path))
            return;

        var file = new FileInfo(path);
        if (file.Length >= maxBytes)
        {
            RotateToPrevious(path);
            return;
        }

        if (file.Length == 0)
            return;

        using var reader = new StreamReader(path, FileEncoding, detectEncodingFromByteOrderMarks: true);
        if (!string.Equals(reader.ReadLine(), header, StringComparison.Ordinal))
            RotateToPrevious(path);
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
    }

    private readonly record struct WriteRequest(
        WriteRequestKind Kind,
        string Path,
        string Header,
        string Content,
        long MaxBytes);
}
