namespace FFXIVAura;

public sealed unsafe partial class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = ["/fa"];
    private static readonly AddonEvent[] ActionDetailTooltipEvents =
    [
        AddonEvent.PreSetup,
        AddonEvent.PostSetup,
        AddonEvent.PreOpen,
        AddonEvent.PostOpen,
        AddonEvent.PreShow,
        AddonEvent.PostShow,
        AddonEvent.PreRequestedUpdate,
        AddonEvent.PostRequestedUpdate,
        AddonEvent.PreRefresh,
        AddonEvent.PostRefresh,
        AddonEvent.PreUpdate,
        AddonEvent.PostUpdate,
        AddonEvent.PreDraw,
        AddonEvent.PostDraw,
    ];

    private const float DefaultOverlayPositionX = 520f;
    private const float DefaultOverlayPositionY = 280f;
    private const float DefaultOverlayWidth = 760f;
    private const float DefaultOverlayHeight = 170f;
    private const float MinOverlayWidth = 120f;
    private const float MaxOverlayWidth = 1200f;
    private const float MinOverlayHeight = 40f;
    private const float MaxOverlayHeight = 400f;
    private const float DefaultIconSize = 42f;
    private const float MinIconSize = 24f;
    private const float MaxIconSize = 72f;
    private const float DefaultGap = 5f;
    private const float MinGap = 0f;
    private const float MaxGap = 16f;
    private const float DefaultFontScale = 1f;
    private const float MinFontScale = 0.75f;
    private const float MaxFontScale = 1.5f;
    private const float DefaultOrderEditorHeight = 180f;
    private const float MinOrderEditorHeight = 90f;
    private const float MaxOrderEditorHeight = 520f;
    private const float OverlayWindowMargin = 4f;
    private static readonly TimeSpan LoginSkillAutoAlignSuppressionDuration = TimeSpan.FromSeconds(3);

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
    [PluginService] private static IAddonLifecycle AddonLifecycle { get; set; } = null!;
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
    private readonly Dictionary<uint, byte> actionEquivalenceGroupCache = new();
    private readonly Dictionary<uint, GameAction> actionRowCache = new();
    private readonly Dictionary<uint, (string Name, uint IconId)> statusDefinitionCache = new();
    private readonly Dictionary<uint, string> statusTooltipTextCache = new();
    private readonly Dictionary<string, Dictionary<uint, DateTime>> auraFirstSeenByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<AbilityDefinition>> gameActionCandidatesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AuraSearchIndexEntry> actionGrantedStatusSearchIndex = [];
    private readonly List<AuraSearchIndexEntry> allStatusSearchIndex = [];
    private readonly Dictionary<string, CooldownState> cooldownFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, CharacterAuraAggregate> playerAuraFrameCache = new();
    private readonly Dictionary<uint, CharacterAuraAggregate> targetAuraFrameCache = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraFrameAllCache = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraFrameOwnCache = new();
    private readonly ActionKeybindIndex actionKeybindIndex = new();
    private readonly Dictionary<uint, bool> hotbarVisibilityCache = new();
    private readonly Dictionary<string, HashSet<uint>> visibleAurasByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, IDalamudTextureWrap> grayscaleIconCache = new();
    private readonly Dictionary<string, string> visibleAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<uint> grayscaleIconQueue = new();
    private readonly HashSet<uint> grayscaleIconPending = new();
    private readonly HashSet<uint> grayscaleIconFailed = new();
    private readonly HashSet<uint> missingActionRows = new();
    private readonly object grayscaleIconLock = new();
    private string? draggedTrackedId;
    private string? draggedOverlayId;
    private string? auraSearchWindowId;
    private bool auraSearchWindowVisible;
    private bool configSavePending;
    private bool configWasVisible;
    private bool actionGrantedStatusSearchIndexBuilt;
    private bool allStatusSearchIndexBuilt;
    private bool keybindCacheDirty = true;
    private bool playerAuraFrameCacheValid;
    private bool targetAuraFrameCacheValid;
    private bool partyAuraFrameAllCacheValid;
    private bool partyAuraFrameOwnCacheValid;
    private bool overlayTooltipRequestedThisFrame;
    private int pendingStatusId;
    private Vector2 draggedOverlayMouseStart;
    private Vector2 draggedOverlayPositionStart;
    private DateTime configSaveAfter = DateTime.MinValue;
    private DateTime keybindCacheRefreshAfter = DateTime.MinValue;
    private PluginConfig config;
    private bool configVisible;
    private bool zoneLoadActive;
    private DateTime zoneLoadHiddenUntil = DateTime.MinValue;
    private readonly NativeActionTooltipController nativeActionTooltipController = new();
    private readonly OverlayTooltipResolver overlayTooltipResolver = new();
    private readonly PerformanceFrameStats performanceStats = new();
    private readonly LoginStabilizationState loginStabilizationState = new(LoginSkillAutoAlignSuppressionDuration);
    private readonly IFontHandle cooldownFont;
    private readonly IFontHandle chargeFont;
    private readonly IFontHandle auraCountFont;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        var configChanged = this.EnsureIconWindows();
        if (this.config.Version < 2)
        {
            this.config.Version = 2;
            this.config.OverlayWidth = 760f;
            this.config.OverlayHeight = 170f;
            configChanged = true;
        }

        if (this.config.Version < 3)
        {
            this.config.Version = 3;
            configChanged |= this.EnsureIconWindows();
        }

        if (configChanged)
            this.SaveConfigNow();

        this.cooldownFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 20f)
        {
            Bold = true,
        });
        this.chargeFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 18f)
        {
            Bold = true,
        });
        this.auraCountFont = PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Meidinger, 12f)
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
        foreach (var eventType in ActionDetailTooltipEvents)
            AddonLifecycle.RegisterListener(eventType, "ActionDetail", this.OnActionDetailTooltipLifecycle);
    }

    public void Dispose()
    {
        this.FlushConfigSave(force: true);
        this.HideNativeActionTooltip();
        foreach (var eventType in ActionDetailTooltipEvents)
            AddonLifecycle.UnregisterListener(eventType, "ActionDetail", this.OnActionDetailTooltipLifecycle);
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
            this.grayscaleIconQueue.Clear();
            this.grayscaleIconPending.Clear();
            this.grayscaleIconFailed.Clear();
        }

        this.cooldownFont.Dispose();
        this.chargeFont.Dispose();
        this.auraCountFont.Dispose();
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
                this.abilities.AddRange(loaded.Where(a => a.ActionId > 0 && a.IconId > 0).Select(this.NormalizeAbilityDefinition));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load FFXIVAura ability data.");
        }
    }

    private AbilityDefinition NormalizeAbilityDefinition(AbilityDefinition ability)
    {
        if (ability.ActionCategoryId == 0)
            ability.ActionCategoryId = this.GetActionCategory(ability.ActionId).RowId;

        return ability;
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
        var performanceFrameStart = this.BeginPerformanceFrame();
        this.BeginFrameCache();
        try
        {
            var wasConfigVisible = this.configWasVisible;
            if (this.configVisible && !wasConfigVisible)
                this.InvalidateKeybindCache();

            if (!this.configVisible && wasConfigVisible)
                this.CloseAuraSearchWindow();

            this.configWasVisible = this.configVisible;

            if (this.configVisible)
                this.DrawConfig();

            var loggedInAndLoaded = ClientState.IsLoggedIn && PlayerState.IsLoaded;
            var loginStarted = this.loginStabilizationState.Update(loggedInAndLoaded, DateTime.UtcNow);
            if (!loggedInAndLoaded || loginStarted)
                this.visibleAbilityKeys.Clear();

            if (!this.config.Enabled || !loggedInAndLoaded)
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
            this.ShowDeferredOverlayTooltip();
            this.FlushConfigSave(force: false);
        }
        finally
        {
            this.FinishOverlayTooltipFrame();
            this.FinishPerformanceFrame(performanceFrameStart);
        }
    }

    private void BeginFrameCache()
    {
        this.overlayTooltipRequestedThisFrame = false;
        this.overlayTooltipResolver.Clear();
        this.cooldownFrameCache.Clear();
        this.playerAuraFrameCacheValid = false;
        this.targetAuraFrameCacheValid = false;
        this.partyAuraFrameAllCacheValid = false;
        this.partyAuraFrameOwnCacheValid = false;
        this.performanceStats.CountGrayscaleIcons(this.ProcessGrayscaleIconQueue());
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
        this.keybindCacheRefreshAfter = DateTime.MinValue;
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

}
