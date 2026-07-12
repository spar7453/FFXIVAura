namespace FFXIVAura;

internal enum StatusSnapshotOrigin
{
    None,
    Live,
    Fallback,
}

internal readonly record struct StatusSnapshot(
    uint StatusId,
    uint SourceId,
    ushort Param,
    float RemainingTime);
