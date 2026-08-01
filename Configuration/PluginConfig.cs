using Dalamud.Configuration;

namespace FFXIVAura;

public class PluginConfigData
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool LockOverlay { get; set; }
    public bool HideDuringZoneLoad { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public bool ShowPerformanceOverlay { get; set; }
    public bool ShowDetailedPerformanceProfile { get; set; }
    public bool RecordPerformanceProfile { get; set; }
    public int PerformanceProfileRecordIntervalSeconds { get; set; } = 1;
    public int PerformanceProfileMaxFileMegabytes { get; set; } = 64;
    public bool ShowPartyCooldownLogObserver { get; set; }
    public PartyCooldownLayoutEditMode PartyCooldownLayoutEditMode { get; set; } = PartyCooldownLayoutEditMode.EightPlayer;
    public float IconSize { get; set; } = 42f;
    public float Gap { get; set; } = 5f;
    public float FontScale { get; set; } = 1f;
    public float OverlayWidth { get; set; } = 760f;
    public float OverlayHeight { get; set; } = 170f;
    public System.Numerics.Vector2 OverlayPosition { get; set; } = new(520, 280);
    public List<string> ManualTrackingJobs { get; set; } = new();
    public Dictionary<string, List<string>> TrackedByJob { get; set; } = new();
    public Dictionary<string, List<string>> ExcludedByJob { get; set; } = new();
    public Dictionary<string, Dictionary<string, System.Numerics.Vector2>> IconPositionsByJob { get; set; } = new();
    public string TrackedEditorTab { get; set; } = "WeaponSkill";
    public string TrackedSkillSearch { get; set; } = string.Empty;
    public string ActiveWindowId { get; set; } = "win1";
    public int WindowCounter { get; set; } = 1;
    public List<IconWindowConfig> IconWindows { get; set; } = new();
}

public sealed class PluginConfig : PluginConfigData, IPluginConfiguration;

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
    public IconDisplayCondition SkillDisplayCondition { get; set; } = IconDisplayCondition.Always;
    public IconDisplayCondition AuraDisplayCondition { get; set; } = IconDisplayCondition.Always;
    public IconDisplayCondition PartyCooldownDisplayCondition { get; set; } = IconDisplayCondition.Always;
    public IconAlignment Alignment { get; set; } = IconAlignment.Center;
    public IconWindowLayoutConfig? FourPlayerLayout { get; set; }
    public IconWindowLayoutConfig? AllianceLayout { get; set; }
    public bool HighlightReady { get; set; }
    public bool HighlightAdjusted { get; set; } = true;
    public bool ShowKeybindText { get; set; } = true;
    public bool ShowMissingAuras { get; set; } = true;
    public bool PartyAurasOwnOnly { get; set; }
    public bool ShowPartyAuraCount { get; set; } = true;
    public string AuraSearch { get; set; } = string.Empty;
    public bool AuraSearchActiveOnly { get; set; } = true;
    public bool AuraSearchShowIndividualIds { get; set; }
    public List<uint> TrackedStatusIds { get; set; } = new();
    public List<uint> ExactTrackedStatusIds { get; set; } = new();
    public List<string> ExcludedPartyCooldownIds { get; set; } = new();
    public List<string> ManualTrackingJobs { get; set; } = new();
    public Dictionary<string, List<string>> TrackedByJob { get; set; } = new();
    public Dictionary<string, List<string>> ExcludedByJob { get; set; } = new();
    public Dictionary<string, Dictionary<string, System.Numerics.Vector2>> IconPositionsByJob { get; set; } = new();
    public Dictionary<string, Dictionary<string, System.Numerics.Vector2>> AuraPositionsByRole { get; set; } = new();
}

public sealed class IconWindowLayoutConfig
{
    public System.Numerics.Vector2 Position { get; set; } = new(520, 280);
    public float Width { get; set; } = 760f;
    public float Height { get; set; } = 170f;
    public float IconSize { get; set; } = 42f;
    public float Gap { get; set; } = 5f;
    public float FontScale { get; set; } = 1f;
    public IconAlignment Alignment { get; set; } = IconAlignment.Center;
}

public enum IconWindowRole
{
    SkillCooldowns,
    PlayerBuffs,
    TargetDebuffs,
    PartyBuffs,
    PartyDefensives,
    PartyHealingCooldowns,
    PartySynergies,
}

public enum IconDisplayCondition
{
    Always,
    InCombat,
    OutOfCombat,
    CoolingOnly,
    ReadyOnly,
}

public enum IconAlignment
{
    Left,
    Center,
    Right,
}

public enum PartyCooldownLayoutEditMode
{
    EightPlayer = 0,
    Alliance = 1,
    FourPlayer = 2,
}
