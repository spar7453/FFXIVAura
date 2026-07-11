using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceProfileWriterTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PerformanceProfileWriter appends header and rows in order", AppendsHeaderAndRowsInOrder),
        ("PerformanceProfileWriter rotates oversized files", RotatesOversizedFiles),
        ("PerformanceProfileWriter rotates files with an old header", RotatesMismatchedHeader),
        ("PerformanceProfileWriter reports write failures", ReportsWriteFailures),
    ];

    private static void AppendsHeaderAndRowsInOrder()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, PerformanceProfileCsv.FileName);
            using (var writer = new PerformanceProfileWriter())
            {
                True(writer.TryEnqueueAppend(path, "header", "first\n", 1024), "first row should be queued");
                True(writer.TryEnqueueAppend(path, "header", "second\n", 1024), "second row should be queued");
            }

            Sequence(["header", "first", "second"], File.ReadAllLines(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void RotatesOversizedFiles()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, PerformanceProfileCsv.FileName);
            File.WriteAllText(path, "header\nold-data\n");
            using (var writer = new PerformanceProfileWriter())
                True(writer.TryEnqueueAppend(path, "header", "new-data\n", 1), "rotation row should be queued");

            var previousPath = Path.Combine(directory, PerformanceProfileCsv.PreviousFileName);
            True(File.Exists(previousPath), "oversized profile should be rotated");
            Sequence(["header", "new-data"], File.ReadAllLines(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ReportsWriteFailures()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var writer = new PerformanceProfileWriter();
            True(writer.TryEnqueueAppend(directory, "header", "row\n", 1024), "invalid write should be queued");
            writer.Dispose();

            Equal(0L, writer.CompletedCount);
            Equal(1L, writer.FailedCount);
            True(writer.TakeLastError() is not null, "write exception should be observable");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void RotatesMismatchedHeader()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, PerformanceProfileCsv.FileName);
            File.WriteAllText(path, "old-header\nold-data\n");
            using (var writer = new PerformanceProfileWriter())
                True(writer.TryEnqueueAppend(path, "new-header", "new-data\n", 1024), "new schema row should be queued");

            var previousPath = Path.Combine(directory, PerformanceProfileCsv.PreviousFileName);
            True(File.Exists(previousPath), "old schema profile should be rotated");
            Sequence(["old-header", "old-data"], File.ReadAllLines(previousPath));
            Sequence(["new-header", "new-data"], File.ReadAllLines(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"FFXIVAura.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
