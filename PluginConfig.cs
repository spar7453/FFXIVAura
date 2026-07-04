using Dalamud.Configuration;

namespace FFXIVAura;

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool LockOverlay { get; set; }
    public bool HideDuringZoneLoad { get; set; } = true;
    public bool ShowRoleActions { get; set; } = true;
    public int MaxVisibleIcons { get; set; } = 12;
    public float IconSize { get; set; } = 42f;
    public float Gap { get; set; } = 5f;
    public float FontScale { get; set; } = 1f;
    public float OverlayWidth { get; set; } = 760f;
    public float OverlayHeight { get; set; } = 170f;
    public System.Numerics.Vector2 OverlayPosition { get; set; } = new(520, 280);
    public Dictionary<string, List<string>> TrackedByJob { get; set; } = new();
    public Dictionary<string, Dictionary<string, System.Numerics.Vector2>> IconPositionsByJob { get; set; } = new();
    public string TrackedEditorTab { get; set; } = "WeaponSkill";
    public string TrackedSkillSearch { get; set; } = string.Empty;
    public string ActiveWindowId { get; set; } = "win1";
    public int WindowCounter { get; set; } = 1;
    public List<IconWindowConfig> IconWindows { get; set; } = new();
}

public sealed class IconWindowConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public System.Numerics.Vector2 Position { get; set; } = new(520, 280);
    public float Width { get; set; } = 760f;
    public float Height { get; set; } = 170f;
    public float IconSize { get; set; } = 42f;
    public float Gap { get; set; } = 5f;
    public float FontScale { get; set; } = 1f;
    public float OrderEditorHeight { get; set; } = 180f;
    public int ActiveOrderRow { get; set; }
    public IconWindowRole Role { get; set; } = IconWindowRole.SkillCooldowns;
    public IconDisplayCondition DisplayCondition { get; set; } = IconDisplayCondition.Always;
    public bool DesaturateUnavailable { get; set; } = true;
    public bool DimUnavailable { get; set; }
    public bool HighlightReady { get; set; }
    public bool HighlightAdjusted { get; set; } = true;
    public bool ShowKeybindText { get; set; } = true;
    public bool ShowMissingAuras { get; set; } = true;
    public string AuraSearch { get; set; } = string.Empty;
    public bool AuraSearchActiveOnly { get; set; } = true;
    public List<uint> TrackedStatusIds { get; set; } = new();
    public Dictionary<string, List<string>> TrackedByJob { get; set; } = new();
    public Dictionary<string, Dictionary<string, System.Numerics.Vector2>> IconPositionsByJob { get; set; } = new();
}

public enum IconWindowRole
{
    SkillCooldowns,
    PlayerBuffs,
    TargetDebuffs,
    PartyBuffs,
}

public enum IconDisplayCondition
{
    Always,
    InCombat,
    OutOfCombat,
    CoolingOnly,
    ReadyOnly,
}
