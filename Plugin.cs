namespace FFXIVAura;

public sealed unsafe partial class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = ["/fa"];

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IPlayerState PlayerState { get; set; } = null!;
    [PluginService] private static ICondition Condition { get; set; } = null!;
    [PluginService] private static ITextureProvider TextureProvider { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;
    [PluginService] private static ITargetManager TargetManager { get; set; } = null!;
    [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
    [PluginService] private static IPartyList PartyList { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private static readonly (string Id, string Label)[] TrackedEditorTabs =
    [
        ("WeaponSkill", "무기"),
        ("Spell", "마법"),
        ("Ability", "능력"),
        ("Role", "역할"),
    ];
    private readonly List<AbilityDefinition> abilities = [];
    private readonly Dictionary<uint, (uint RowId, string Name)> actionCategoryCache = new();
    private readonly Dictionary<IconWindowRole, Dictionary<uint, DateTime>> auraFirstSeenByRole = new();
    private readonly Dictionary<string, List<AbilityDefinition>> gameActionCandidatesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CooldownState> cooldownFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(uint BaseActionId, uint DisplayActionId), string> keybindTextCache = new();
    private readonly Dictionary<IconWindowRole, HashSet<uint>> visibleAurasByRole = new();
    private readonly Dictionary<uint, IDalamudTextureWrap> grayscaleIconCache = new();
    private readonly Dictionary<string, string> visibleAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<uint> grayscaleIconPending = new();
    private readonly object grayscaleIconLock = new();
    private string? draggedTrackedId;
    private string? draggedOverlayId;
    private bool auraSearchWindowVisible;
    private bool configSavePending;
    private bool configWasVisible;
    private bool keybindCacheDirty = true;
    private int pendingStatusId;
    private Vector2 draggedOverlayMouseStart;
    private Vector2 draggedOverlayPositionStart;
    private DateTime configSaveAfter = DateTime.MinValue;
    private PluginConfig config;
    private bool configVisible;
    private bool zoneLoadActive;
    private DateTime zoneLoadHiddenUntil = DateTime.MinValue;
    private readonly IFontHandle cooldownFont;
    private readonly IFontHandle chargeFont;
    private readonly IFontHandle keybindFont;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        this.EnsureIconWindows();
        if (this.config.Version < 2)
        {
            this.config.Version = 2;
            this.config.OverlayWidth = 760f;
            this.config.OverlayHeight = 170f;
            this.SaveConfigNow();
        }
        if (this.config.Version < 3)
        {
            this.config.Version = 3;
            this.EnsureIconWindows();
            this.SaveConfigNow();
        }

        this.cooldownFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 20f)
        {
            Bold = true,
        });
        this.chargeFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 18f)
        {
            Bold = true,
        });
        this.keybindFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 12f)
        {
            Bold = true,
        });
        this.LoadAbilities();

        foreach (var commandName in CommandNames)
        {
            CommandManager.AddHandler(commandName, new CommandInfo(this.OnCommand)
            {
                HelpMessage = "FFXIVAura 설정을 엽니다.",
            });
        }

        PluginInterface.UiBuilder.Draw += this.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenConfig;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfig;
        Condition.ConditionChange += this.OnConditionChange;
        ClientState.ZoneInit += this.OnZoneInit;
    }

    public void Dispose()
    {
        this.FlushConfigSave(force: true);
        PluginInterface.UiBuilder.Draw -= this.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenConfig;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        Condition.ConditionChange -= this.OnConditionChange;
        ClientState.ZoneInit -= this.OnZoneInit;
        foreach (var commandName in CommandNames)
            CommandManager.RemoveHandler(commandName);
        lock (this.grayscaleIconLock)
        {
            foreach (var texture in this.grayscaleIconCache.Values)
                texture.Dispose();

            this.grayscaleIconCache.Clear();
            this.grayscaleIconPending.Clear();
        }

        this.cooldownFont.Dispose();
        this.chargeFont.Dispose();
        this.keybindFont.Dispose();
    }

    private void LoadAbilities()
    {
        try
        {
            var path = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "Data", "abilities.json");
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<List<AbilityDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (loaded is not null)
                this.abilities.AddRange(loaded.Where(a => a.ActionId > 0 && a.IconId > 0));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load FFXIVAura ability data.");
        }
    }

    private void OnCommand(string command, string args)
    {
        this.configVisible = !this.configVisible;
        if (this.configVisible)
            this.InvalidateKeybindCache();
    }

    private void OpenConfig()
    {
        this.configVisible = true;
        this.InvalidateKeybindCache();
    }

    private void OnZoneInit(ZoneInitEventArgs args)
    {
        this.zoneLoadHiddenUntil = DateTime.UtcNow.AddMilliseconds(900);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag is not ConditionFlag.BetweenAreas and not ConditionFlag.BetweenAreas51)
            return;

        if (value)
        {
            this.zoneLoadActive = true;
            this.zoneLoadHiddenUntil = DateTime.UtcNow.AddSeconds(20);
            return;
        }

        this.zoneLoadActive = Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51];
        this.zoneLoadHiddenUntil = DateTime.UtcNow.AddMilliseconds(220);
    }

    private void Draw()
    {
        this.BeginFrameCache();
        if (this.configVisible && !this.configWasVisible)
            this.InvalidateKeybindCache();

        this.configWasVisible = this.configVisible;

        if (this.configVisible)
            this.DrawConfig();

        if (!this.config.Enabled || !ClientState.IsLoggedIn || !PlayerState.IsLoaded)
        {
            this.FlushConfigSave(force: false);
            return;
        }

        if (this.config.HideDuringZoneLoad && this.IsLoading())
        {
            this.FlushConfigSave(force: false);
            return;
        }

        this.DrawOverlay();
        this.FlushConfigSave(force: false);
    }

    private void BeginFrameCache()
    {
        this.cooldownFrameCache.Clear();
    }

    private void QueueConfigSave()
    {
        this.configSavePending = true;
        this.configSaveAfter = DateTime.UtcNow.AddMilliseconds(400);
    }

    private void SaveConfigNow()
    {
        PluginInterface.SavePluginConfig(this.config);
        this.configSavePending = false;
        this.configSaveAfter = DateTime.MinValue;
    }

    private void FlushConfigSave(bool force)
    {
        if (!this.configSavePending)
            return;

        if (!force && DateTime.UtcNow < this.configSaveAfter)
            return;

        this.SaveConfigNow();
    }

    private void InvalidateKeybindCache()
    {
        this.keybindCacheDirty = true;
    }

    private bool IsLoading()
    {
        if (Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51])
        {
            this.zoneLoadActive = true;
            this.zoneLoadHiddenUntil = DateTime.UtcNow.AddSeconds(20);
            return true;
        }

        if (this.zoneLoadActive)
        {
            this.zoneLoadActive = false;
            this.zoneLoadHiddenUntil = DateTime.UtcNow.AddMilliseconds(220);
            return true;
        }

        return DateTime.UtcNow < this.zoneLoadHiddenUntil;
    }

    private void EnsureIconWindows()
    {
        if (this.config.IconWindows.Count == 0)
        {
            this.config.IconWindows.Add(new IconWindowConfig
            {
                Id = "win1",
                Name = "창 1",
                Position = this.config.OverlayPosition,
                Width = this.config.OverlayWidth <= 0 ? 760f : this.config.OverlayWidth,
                Height = this.config.OverlayHeight <= 0 ? 170f : this.config.OverlayHeight,
                IconSize = this.config.IconSize,
                Gap = this.config.Gap,
                FontScale = this.config.FontScale,
                TrackedByJob = this.config.TrackedByJob,
                IconPositionsByJob = this.config.IconPositionsByJob,
            });
        }

        foreach (var window in this.config.IconWindows)
        {
            if (IsBrokenWindowName(window.Name))
                window.Name = GetDefaultWindowName(window);
            if (window.IconSize <= 0)
                window.IconSize = this.config.IconSize;
            if (window.Gap < 0)
                window.Gap = this.config.Gap;
            if (window.FontScale <= 0)
                window.FontScale = this.config.FontScale;
            if (window.OrderEditorHeight <= 0)
                window.OrderEditorHeight = 180f;
        }

        if (string.IsNullOrWhiteSpace(this.config.ActiveWindowId)
            || this.config.IconWindows.All(window => !string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase)))
        {
            this.config.ActiveWindowId = this.config.IconWindows[0].Id;
        }

        this.config.WindowCounter = Math.Max(this.config.WindowCounter, this.config.IconWindows.Count);
    }

    private static bool IsBrokenWindowName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var trimmed = name.Trim();
        if (trimmed.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            return true;

        return trimmed.Contains('?') && trimmed.Any(ch => ch > 127);
    }

    private static string GetDefaultWindowName(IconWindowConfig window)
    {
        if (window.Id.StartsWith("win", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(window.Id[3..], out var number)
            && number > 0)
            return $"창 {number}";

        return "창";
    }

    private IconWindowConfig GetActiveIconWindow()
    {
        this.EnsureIconWindows();
        return this.config.IconWindows.FirstOrDefault(window => string.Equals(window.Id, this.config.ActiveWindowId, StringComparison.OrdinalIgnoreCase))
               ?? this.config.IconWindows[0];
    }
}
