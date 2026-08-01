namespace FFXIVAura;

internal sealed class VisibleAbilityLayoutState
{
    private readonly string[] abilityIds;

    private VisibleAbilityLayoutState(
        uint level,
        IconAlignment alignment,
        Vector2 areaSize,
        float iconSize,
        float gap,
        bool autoAlignSuppressed,
        bool hasSavedPositions,
        string[] abilityIds)
    {
        this.Level = level;
        this.Alignment = alignment;
        this.AreaSize = areaSize;
        this.IconSize = iconSize;
        this.Gap = gap;
        this.AutoAlignSuppressed = autoAlignSuppressed;
        this.HasSavedPositions = hasSavedPositions;
        this.abilityIds = abilityIds;
    }

    public uint Level { get; }

    public IconAlignment Alignment { get; }

    public Vector2 AreaSize { get; }

    public float IconSize { get; }

    public float Gap { get; }

    public bool AutoAlignSuppressed { get; }

    public bool HasSavedPositions { get; }

    public IReadOnlyList<string> AbilityIds => this.abilityIds;

    public static VisibleAbilityLayoutState Create(
        uint level,
        IReadOnlyList<AbilityDefinition> visible,
        IconAlignment alignment,
        Vector2 areaSize,
        float iconSize,
        float gap,
        bool autoAlignSuppressed,
        bool hasSavedPositions)
    {
        var ids = new string[visible.Count];
        for (var index = 0; index < visible.Count; index++)
            ids[index] = visible[index].Id;

        return new VisibleAbilityLayoutState(
            level,
            alignment,
            areaSize,
            iconSize,
            gap,
            autoAlignSuppressed,
            hasSavedPositions,
            ids);
    }

    public bool Matches(
        uint level,
        IReadOnlyList<AbilityDefinition> visible,
        IconAlignment alignment,
        Vector2 areaSize,
        float iconSize,
        float gap,
        bool autoAlignSuppressed,
        bool hasSavedPositions)
    {
        if (this.Level != level
            || this.Alignment != alignment
            || this.AreaSize != areaSize
            || this.IconSize != iconSize
            || this.Gap != gap
            || this.AutoAlignSuppressed != autoAlignSuppressed
            || this.HasSavedPositions != hasSavedPositions
            || this.abilityIds.Length != visible.Count)
        {
            return false;
        }

        for (var index = 0; index < visible.Count; index++)
        {
            if (!string.Equals(this.abilityIds[index], visible[index].Id, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
