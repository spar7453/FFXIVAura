namespace FFXIVAura;

internal readonly record struct CharacterAuraStatusSample(
    uint StatusId,
    float Remaining,
    ushort Param,
    bool FromSelf);

internal readonly record struct CharacterAuraAggregate(
    float Remaining,
    ushort Param,
    int Count,
    int OwnCount,
    bool FromSelf,
    float OwnRemaining,
    ushort OwnParam,
    bool HasOwn)
{
    public (float Remaining, ushort Param, int Count, int OwnCount, bool FromSelf) Select(bool preferOwnStatus)
    {
        if (preferOwnStatus && this.HasOwn)
            return (this.OwnRemaining, this.OwnParam, this.Count, this.OwnCount, true);

        return (this.Remaining, this.Param, this.Count, this.OwnCount, this.FromSelf);
    }
}

internal static class AuraStatusFrameIndex
{
    public static void AddStatus(
        Dictionary<uint, CharacterAuraAggregate> index,
        CharacterAuraStatusSample sample)
    {
        if (sample.StatusId == 0)
            return;

        var remaining = Math.Max(0f, sample.Remaining);
        if (!index.TryGetValue(sample.StatusId, out var existing))
        {
            index[sample.StatusId] = new CharacterAuraAggregate(
                remaining,
                sample.Param,
                1,
                sample.FromSelf ? 1 : 0,
                sample.FromSelf,
                sample.FromSelf ? remaining : 0f,
                sample.FromSelf ? sample.Param : default,
                sample.FromSelf);
            return;
        }

        var useBest = remaining > existing.Remaining;
        var useOwnBest = sample.FromSelf && (!existing.HasOwn || remaining > existing.OwnRemaining);
        index[sample.StatusId] = new CharacterAuraAggregate(
            useBest ? remaining : existing.Remaining,
            useBest ? sample.Param : existing.Param,
            existing.Count + 1,
            existing.OwnCount + (sample.FromSelf ? 1 : 0),
            useBest ? sample.FromSelf : existing.FromSelf,
            useOwnBest ? remaining : existing.OwnRemaining,
            useOwnBest ? sample.Param : existing.OwnParam,
            existing.HasOwn || sample.FromSelf);
    }
}
