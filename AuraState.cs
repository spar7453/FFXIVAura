namespace FFXIVAura;

public readonly record struct AuraState(
    uint StatusId,
    string Name,
    uint IconId,
    float Remaining,
    ushort Param,
    bool Present,
    bool FromSelf);
