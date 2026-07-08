using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Lumina.Excel;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class StatusSnapshotReaderTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("StatusSnapshotReader reads valid statuses", ReadsValidStatuses),
        ("StatusSnapshotReader clears output on status read failure", ClearsOutputOnStatusReadFailure),
        ("StatusSnapshotReader clears output on enumeration failure", ClearsOutputOnEnumerationFailure),
    ];

    private static void ReadsValidStatuses()
    {
        var output = new List<StatusSnapshot>();
        var ok = StatusSnapshotReader.ReadTo(
            [
                new FakeStatus(0, 1, 2, 3),
                new FakeStatus(123, 456, 7, 8.5f),
            ],
            output,
            out var error);

        True(ok, "valid status reads should succeed");
        Equal(null, error);
        Equal(1, output.Count);
        Equal(123u, output[0].StatusId);
        Equal(456u, output[0].SourceId);
        Equal((ushort)7, output[0].Param);
        Near(8.5f, output[0].RemainingTime);
    }

    private static void ClearsOutputOnStatusReadFailure()
    {
        var output = new List<StatusSnapshot> { new(1, 2, 3, 4) };
        var ok = StatusSnapshotReader.ReadTo([new FakeStatus(1, 2, 3, 4, throwOnRead: true)], output, out var error);

        True(!ok, "throwing status reads should fail");
        True(error is InvalidOperationException, "read exception should be returned");
        Equal(0, output.Count);
    }

    private static void ClearsOutputOnEnumerationFailure()
    {
        var output = new List<StatusSnapshot> { new(1, 2, 3, 4) };
        var ok = StatusSnapshotReader.ReadTo(new ThrowingStatusEnumerable(), output, out var error);

        True(!ok, "throwing enumerators should fail");
        True(error is NullReferenceException, "enumeration exception should be returned");
        Equal(0, output.Count);
    }

    private sealed class FakeStatus(uint statusId, uint sourceId, ushort param, float remainingTime, bool throwOnRead = false) : IStatus
    {
        public nint Address => 1;

        public uint StatusId => throwOnRead ? throw new InvalidOperationException("status read failed") : statusId;

        public RowRef<Lumina.Excel.Sheets.Status> GameData => default;

        public ushort Param => param;

        public float RemainingTime => remainingTime;

        public uint SourceId => sourceId;

        public IGameObject? SourceObject => null;

        public bool Equals(IStatus? other)
            => other is not null
               && StatusId == other.StatusId
               && SourceId == other.SourceId
               && Param == other.Param
               && Math.Abs(RemainingTime - other.RemainingTime) <= 0.001f;
    }

    private sealed class ThrowingStatusEnumerable : IEnumerable<IStatus>
    {
        public IEnumerator<IStatus> GetEnumerator()
            => throw new NullReferenceException("status list disappeared");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => this.GetEnumerator();
    }
}
