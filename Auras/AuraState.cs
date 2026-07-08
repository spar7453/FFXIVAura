namespace FFXIVAura;

public readonly record struct AuraState(
    uint StatusId,
    string Name,
    uint IconId,
    float Remaining,
    ushort Param,
    int Count,
    int OwnCount,
    bool Present,
    bool FromSelf);
