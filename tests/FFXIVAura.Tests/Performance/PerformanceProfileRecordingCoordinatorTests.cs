using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PerformanceProfileRecordingCoordinatorTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("Performance profile coordinator schedules records and diagnostics", SchedulesRecordsAndDiagnostics),
        ("Performance profile coordinator owns failure state", OwnsFailureState),
    ];

    private static void SchedulesRecordsAndDiagnostics()
    {
        using var coordinator = CreateCoordinator();
        var nowUtc = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);

        var first = coordinator.Prepare(true, 5, nowUtc);
        True(first.ShouldRecord, "the first enabled frame should be recorded");
        True(first.IncludeDiagnostics, "the first enabled frame should include diagnostics");
        True(
            !coordinator.Prepare(true, 5, nowUtc.AddSeconds(1)).ShouldRecord,
            "frames inside the interval should not be recorded");

        var second = coordinator.Prepare(true, 5, nowUtc.AddSeconds(5));
        True(second.ShouldRecord, "the next interval should be recorded");
        True(!second.IncludeDiagnostics, "diagnostics should use their own interval");

        var third = coordinator.Prepare(true, 5, nowUtc.AddSeconds(10));
        True(third.ShouldRecord, "the following interval should be recorded");
        True(third.IncludeDiagnostics, "diagnostics should run after ten seconds");

        True(
            !coordinator.Prepare(false, 5, nowUtc.AddSeconds(11)).ShouldRecord,
            "disabled recording should not schedule a write");
        var afterReset = coordinator.Prepare(true, 5, nowUtc.AddSeconds(12));
        True(afterReset.ShouldRecord, "reenabling should reset the record schedule");
        True(afterReset.IncludeDiagnostics, "reenabling should reset the diagnostic schedule");

        coordinator.Clear();
        True(!coordinator.ShouldRecordCurrentFrame, "clearing should cancel the current frame write");
        True(!coordinator.CaptureDiagnosticsThisFrame, "clearing should cancel current diagnostics");
    }

    private static void OwnsFailureState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ffxivaura-tests-{Guid.NewGuid():N}");
        var bugs = new List<string>();
        var errors = new List<string>();
        try
        {
            using var coordinator = new PerformanceProfileRecordingCoordinator(
                directory,
                bugs.Add,
                (_, message) => errors.Add(message));
            var nowUtc = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);

            coordinator.NoteFailure("queue", null, nowUtc);
            coordinator.NoteFailure("write", new IOException("test"), nowUtc);
            coordinator.NoteFailure("write", new IOException("test"), nowUtc.AddSeconds(1));

            var diagnostics = coordinator.CreateDiagnostics();
            Equal(3L, diagnostics.RecordingFailureCount);
            Equal("write:IOException", diagnostics.LastError);
            Equal(3, bugs.Count);
            Equal(1, errors.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static PerformanceProfileRecordingCoordinator CreateCoordinator()
        => new(
            Path.Combine(Path.GetTempPath(), $"ffxivaura-tests-{Guid.NewGuid():N}"),
            _ => { },
            (_, _) => { });
}
