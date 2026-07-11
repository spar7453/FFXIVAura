namespace FFXIVAura;

internal readonly struct IconWindowLayoutBinding
{
    private readonly IconWindowConfig window;
    private readonly IconWindowLayoutConfig? overrideLayout;

    private IconWindowLayoutBinding(IconWindowConfig window, IconWindowLayoutConfig? overrideLayout)
    {
        this.window = window;
        this.overrideLayout = overrideLayout;
    }

    public Vector2 Position
    {
        get => this.overrideLayout?.Position ?? this.window.Position;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.Position = value;
            else
                this.window.Position = value;
        }
    }

    public float Width
    {
        get => this.overrideLayout?.Width ?? this.window.Width;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.Width = value;
            else
                this.window.Width = value;
        }
    }

    public float Height
    {
        get => this.overrideLayout?.Height ?? this.window.Height;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.Height = value;
            else
                this.window.Height = value;
        }
    }

    public float IconSize
    {
        get => this.overrideLayout?.IconSize ?? this.window.IconSize;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.IconSize = value;
            else
                this.window.IconSize = value;
        }
    }

    public float Gap
    {
        get => this.overrideLayout?.Gap ?? this.window.Gap;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.Gap = value;
            else
                this.window.Gap = value;
        }
    }

    public float FontScale
    {
        get => this.overrideLayout?.FontScale ?? this.window.FontScale;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.FontScale = value;
            else
                this.window.FontScale = value;
        }
    }

    public IconAlignment Alignment
    {
        get => this.overrideLayout?.Alignment ?? this.window.Alignment;
        set
        {
            if (this.overrideLayout is not null)
                this.overrideLayout.Alignment = value;
            else
                this.window.Alignment = value;
        }
    }

    public bool IsOverride => this.overrideLayout is not null;

    public static IconWindowLayoutBinding Regular(IconWindowConfig window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new IconWindowLayoutBinding(window, null);
    }

    public static IconWindowLayoutBinding PartyCooldown(IconWindowConfig window, bool useAllianceLayout, out bool created)
    {
        ArgumentNullException.ThrowIfNull(window);
        created = false;
        if (!useAllianceLayout)
            return Regular(window);

        if (window.AllianceLayout is null)
        {
            window.AllianceLayout = CreateConfig(window);
            created = true;
        }

        return new IconWindowLayoutBinding(window, window.AllianceLayout);
    }

    public static IconWindowLayoutConfig CreateConfig(IconWindowConfig window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new IconWindowLayoutConfig
        {
            Position = window.Position,
            Width = window.Width,
            Height = window.Height,
            IconSize = window.IconSize,
            Gap = window.Gap,
            FontScale = window.FontScale,
            Alignment = window.Alignment,
        };
    }

    public static IconWindowLayoutConfig? CloneConfig(IconWindowLayoutConfig? source)
        => source is null
            ? null
            : new IconWindowLayoutConfig
            {
                Position = source.Position,
                Width = source.Width,
                Height = source.Height,
                IconSize = source.IconSize,
                Gap = source.Gap,
                FontScale = source.FontScale,
                Alignment = source.Alignment,
            };
}
