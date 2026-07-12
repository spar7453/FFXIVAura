namespace FFXIVAura;

internal readonly record struct PartyCooldownCrossRealmHeader(
    int LocalGroupIndex,
    int RawLocalGroupIndex,
    int HudLocalGroupIndex,
    int ObservedHudGroupIndex,
    bool UsedRetainedHudGroup,
    int GroupCount)
{
    public static PartyCooldownCrossRealmHeader Empty { get; } = new(-1, -1, -1, -1, false, 0);
}

internal readonly record struct PartyCooldownCrossRealmSnapshot(
    int GroupCount,
    int RawLocalGroupIndex,
    int MemberLocalGroupIndex);

internal static class PartyCooldownCrossRealmHeaderResolver
{
    private const int AllianceGroupCount = 3;

    public static PartyCooldownCrossRealmHeader Resolve(
        PartyCooldownAllianceGroupResolution hudResolution,
        PartyCooldownCrossRealmSnapshot? snapshot)
    {
        var hudGroupIndex = NormalizeGroupIndex(hudResolution.GroupIndex);
        if (snapshot is not { } source)
            return CreateHudFallback(hudResolution, hudGroupIndex, groupCount: 0);

        var groupCount = Math.Clamp(source.GroupCount, 0, AllianceGroupCount);
        if (groupCount < 2)
            return CreateHudFallback(hudResolution, hudGroupIndex, groupCount);

        var rawLocalGroupIndex = NormalizeGroupIndex(source.RawLocalGroupIndex);
        var memberLocalGroupIndex = NormalizeGroupIndex(source.MemberLocalGroupIndex);
        return new PartyCooldownCrossRealmHeader(
            PartyCooldownAllianceGroups.ResolveLocalGroupIndex(
                hudGroupIndex,
                memberLocalGroupIndex,
                rawLocalGroupIndex),
            rawLocalGroupIndex,
            hudGroupIndex,
            hudResolution.ObservedGroupIndex,
            hudResolution.UsedRetainedValue,
            groupCount);
    }

    private static PartyCooldownCrossRealmHeader CreateHudFallback(
        PartyCooldownAllianceGroupResolution hudResolution,
        int hudGroupIndex,
        int groupCount)
        => new(
            hudGroupIndex,
            -1,
            hudGroupIndex,
            hudResolution.ObservedGroupIndex,
            hudResolution.UsedRetainedValue,
            groupCount);

    private static int NormalizeGroupIndex(int groupIndex)
        => groupIndex is >= 0 and < AllianceGroupCount ? groupIndex : -1;
}
