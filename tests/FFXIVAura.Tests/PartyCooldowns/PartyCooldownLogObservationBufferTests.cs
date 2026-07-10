using FFXIVAura;
using static FFXIVAura.Tests.TestAssert;

namespace FFXIVAura.Tests;

internal static class PartyCooldownLogObservationBufferTests
{
    public static IReadOnlyList<(string Name, Action Run)> Cases { get; } =
    [
        ("PartyCooldownLogObservationBuffer separates candidate noise", SeparatesCandidateNoise),
        ("PartyCooldownLogObservationBuffer enforces independent limits", EnforcesIndependentLimits),
    ];

    private static void SeparatesCandidateNoise()
    {
        var buffer = new PartyCooldownLogObservationBuffer(4, 2);
        buffer.Add(Observation(1, PartyCooldownIgnoredLogReason.None));
        buffer.Add(Observation(2, PartyCooldownIgnoredLogReason.CandidateMissing));

        Equal(1, buffer.ActionableCount);
        Equal(1, buffer.CandidateCount);
        Equal(1u, buffer.LastActionable?.LogMessageId ?? 0);
        Equal(2u, buffer.LastCandidate?.LogMessageId ?? 0);
    }

    private static void EnforcesIndependentLimits()
    {
        var buffer = new PartyCooldownLogObservationBuffer(2, 1);
        buffer.Add(Observation(1, PartyCooldownIgnoredLogReason.None));
        buffer.Add(Observation(2, PartyCooldownIgnoredLogReason.MemberNotFound));
        buffer.Add(Observation(3, PartyCooldownIgnoredLogReason.None));
        buffer.Add(Observation(4, PartyCooldownIgnoredLogReason.CandidateMissing));
        buffer.Add(Observation(5, PartyCooldownIgnoredLogReason.CandidateMissing));

        Sequence([3u, 2u], buffer.EnumerateActionableNewestFirst().Select(item => item.LogMessageId).ToList());
        Equal(1, buffer.CandidateCount);
        Equal(5u, buffer.LastCandidate?.LogMessageId ?? 0);

        buffer.Clear();
        Equal(0, buffer.ActionableCount);
        Equal(0, buffer.CandidateCount);
    }

    private static PartyCooldownLogObservation Observation(uint logMessageId, PartyCooldownIgnoredLogReason reason)
        => new(
            DateTime.UtcNow,
            logMessageId,
            "source",
            0,
            "member",
            0,
            "action",
            "name",
            0,
            reason == PartyCooldownIgnoredLogReason.None ? "tracked" : "ignored",
            reason,
            default,
            string.Empty);
}
