namespace FFXIVAura;

internal sealed class PartyCooldownLogObservationBuffer
{
    private readonly int actionableLimit;
    private readonly int candidateLimit;
    private readonly Queue<PartyCooldownLogObservation> actionable = new();
    private readonly Queue<PartyCooldownLogObservation> candidates = new();

    public PartyCooldownLogObservationBuffer(int actionableLimit, int candidateLimit)
    {
        this.actionableLimit = Math.Max(1, actionableLimit);
        this.candidateLimit = Math.Max(1, candidateLimit);
    }

    public int ActionableCount => this.actionable.Count;

    public int CandidateCount => this.candidates.Count;

    public IEnumerable<PartyCooldownLogObservation> Actionable => this.actionable;

    public PartyCooldownLogObservation? LastActionable { get; private set; }

    public PartyCooldownLogObservation? LastCandidate { get; private set; }

    public void Add(PartyCooldownLogObservation observation)
    {
        var target = observation.IgnoredReason == PartyCooldownIgnoredLogReason.CandidateMissing
            ? this.candidates
            : this.actionable;
        var limit = ReferenceEquals(target, this.candidates) ? this.candidateLimit : this.actionableLimit;
        target.Enqueue(observation);
        while (target.Count > limit)
            target.Dequeue();

        if (ReferenceEquals(target, this.candidates))
            this.LastCandidate = observation;
        else
            this.LastActionable = observation;
    }

    public IEnumerable<PartyCooldownLogObservation> EnumerateActionableNewestFirst()
        => this.actionable.Reverse();

    public void Clear()
    {
        this.actionable.Clear();
        this.candidates.Clear();
        this.LastActionable = null;
        this.LastCandidate = null;
    }
}
