namespace FFXIVAura;

internal readonly record struct StatusSnapshot(
    uint StatusId,
    uint SourceId,
    ushort Param,
    float RemainingTime);
