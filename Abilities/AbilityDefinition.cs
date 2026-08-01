namespace FFXIVAura;

public sealed class AbilityDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint ActionId { get; set; }
    public uint[] ActionIds { get; set; } = [];
    public uint ActionCategoryId { get; set; }
    public string Job { get; set; } = string.Empty;
    public string ReplacementGroup { get; set; } = string.Empty;
    public byte Level { get; set; } = 1;
    public float Cooldown { get; set; }
    public byte Charges { get; set; } = 1;
    public uint IconId { get; set; }
}
