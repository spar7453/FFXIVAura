namespace FFXIVAura;

public sealed unsafe partial class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = ["/fa"];

    private const float DefaultOverlayPositionX = 520f;
    private const float DefaultOverlayPositionY = 280f;
    private const float DefaultOverlayWidth = 760f;
    private const float DefaultOverlayHeight = 170f;
    private const float MinOverlayWidth = 120f;
    private const float MaxOverlayWidth = 1200f;
    private const float MinOverlayHeight = 40f;
    private const float MaxOverlayHeight = 900f;
    private const int AbilityCandidateCacheLimit = 512;
    private const int AuraSearchQueryCacheLimit = 16;
    private const int AuraSearchResultCacheLimit = 4;
    private const int MinPerformanceProfileRecordIntervalSeconds = 1;
    private const int MaxPerformanceProfileRecordIntervalSeconds = 60;
    private const int MinPerformanceProfileMaxFileMegabytes = 1;
    private const int MaxPerformanceProfileMaxFileMegabytes = 1024;
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
    private static readonly TimeSpan ConfigSaveDebounceDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ConfigSaveCombatRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ConfigSaveMaxCombatDeferDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PartyCooldownCandidateMissingSampleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PartyCooldownStatusCacheDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PartyCooldownCrossSignalDedupeWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PartyCooldownStatusMissingGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PartyCooldownAllianceLayoutRetention = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StatusSnapshotFailureRetention = TimeSpan.FromMilliseconds(500);

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
    [PluginService] private static IChatGui ChatGui { get; set; } = null!;
    [PluginService] private static ISeStringEvaluator SeStringEvaluator { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private static readonly (string Id, string Label)[] TrackedEditorTabs =
    [
        ("WeaponSkill", "무기"),
        ("Spell", "마법"),
        ("Ability", "능력"),
        ("Role", "역할"),
    ];
    private readonly List<AbilityDefinition> abilities = [];
    private readonly List<PartyCooldownDefinition> partyCooldownDefinitions = [];
    private readonly Dictionary<uint, (uint RowId, string Name)> actionCategoryCache = new();
    private readonly Dictionary<uint, byte> actionEquivalenceGroupCache = new();
    private readonly Dictionary<uint, GameAction> actionRowCache = new();
    private readonly Dictionary<uint, OverlayActionTooltipModel> actionTooltipModelCache = new();
    private readonly Dictionary<uint, AuraStatusDefinition> statusDefinitionCache = new();
    private readonly Dictionary<uint, string> statusTooltipTextCache = new();
    private readonly Dictionary<string, Dictionary<uint, DateTime>> auraFirstSeenByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> auraSearchStateRevisionByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AuraSearchResultCacheEntry> auraSearchResultCacheByWindow = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<uint> auraSearchCurrentStatusIdBuffer = [];
    private readonly HashSet<uint> auraSearchCurrentStatusIdSetBuffer = [];
    private readonly List<uint> auraSeenStatusIdBuffer = [];
    private readonly Dictionary<string, List<AbilityDefinition>> gameActionCandidatesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AuraSearchIndexEntry> actionGrantedStatusSearchIndex = [];
    private readonly List<AuraSearchIndexEntry> allStatusSearchIndex = [];
    private readonly Dictionary<uint, List<AuraSearchIndexEntry>> actionGrantedStatusSearchIndexByStatusId = new();
    private readonly Dictionary<string, IReadOnlyList<AuraSearchIndexEntry>> actionGrantedAuraSearchQueryCache = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AuraSearchResult>> allStatusSearchQueryCache = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly List<uint> statusIdentityStatusIds = [];
    private readonly Dictionary<AuraStatusGroupKey, List<uint>> statusIdsByGroupIndex = new();
    private readonly Dictionary<string, AuraStatusGroupCacheEntry> trackedAuraGroupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly AuraStatusIndexBuildState statusIdentityIndexState = new();
    private readonly AuraStatusIndexBuildState actionGrantedStatusSearchIndexState = new();
    private readonly Dictionary<string, CooldownState> cooldownFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AbilityDefinition>> jobCandidatesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PartyCooldownRuntimeKey, PartyCooldownRuntimeState> partyCooldownRuntimeStates =
        new(PartyCooldownRuntimeKeyComparer.Instance);
    private readonly Dictionary<ulong, PartyCooldownActiveStatus> partyCooldownActiveStatusFrameCache = new();
    private readonly Dictionary<uint, uint[]> partyCooldownStatusIdsByActionId = new();
    private readonly Dictionary<uint, PartyCooldownDefinition> partyCooldownDefinitionsByActionId = new();
    private readonly Dictionary<ulong, uint> partyCooldownMaxChargesByActionAndLevel = new();
    private readonly Dictionary<string, List<PartyCooldownDefinition>> partyCooldownDefinitionsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<PartyCooldownCategory, List<PartyCooldownDefinition>> partyCooldownDefinitionsByCategory = new();
    private readonly Dictionary<string, IReadOnlyList<PartyCooldownDefinition>> partyCooldownEffectiveDefinitionsByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<PartyCooldownCategory, IReadOnlyList<PartyCooldownDefinition>> partyCooldownPresetDefinitionsByCategory = new();
    private readonly Dictionary<string, IReadOnlyList<PartyCooldownDefinition>> partyCooldownEffectiveDefinitionsByCategoryAndLevel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> partyCooldownCanonicalDefinitionIdById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, int> partyCooldownLogActionParamIndexByLogMessageId = new();
    private readonly PartyCooldownLogObservationBuffer partyCooldownLogObservations = new(64, 8);
    private readonly PartyCooldownLayoutModeTracker partyCooldownLayoutModeTracker = new(PartyCooldownAllianceLayoutRetention);
    private readonly List<PartyCooldownRuntimeKey> partyCooldownRuntimePruneBuffer = [];
    private readonly Dictionary<string, PartyCooldownWindowRowBuffer> partyCooldownRowBuffersByWindow = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<uint> partyCooldownMemberEntityIdsBuffer = [];
    private readonly HashSet<uint> partyCooldownLiveStatusOwnerIdsBuffer = [];
    private readonly HashSet<uint> partyCooldownOwnedObjectOwnerIdsBuffer = [];
    private readonly HashSet<uint> partyCooldownOwnedObjectPartyEntityIdsBuffer = [];
    private readonly Dictionary<uint, CharacterAuraAggregate> playerAuraFrameCache = new();
    private readonly Dictionary<uint, CharacterAuraAggregate> targetAuraFrameCache = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraFrameAllCache = new();
    private readonly Dictionary<uint, PartyAuraAggregate> partyAuraFrameOwnCache = new();
    private readonly Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> partyAuraGroupFrameAllCache = new();
    private readonly Dictionary<AuraStatusGroupKey, PartyAuraGroupAggregate> partyAuraGroupFrameOwnCache = new();
    private readonly Dictionary<uint, PartyMemberAuraState> partyMemberAuraFrameBuffer = new();
    private readonly Dictionary<uint, PartyMemberAuraState> partyMemberAuraOwnFrameBuffer = new();
    private readonly Dictionary<AuraStatusGroupKey, bool> partyMemberAuraGroupFrameBuffer = new();
    private readonly Dictionary<AuraStatusGroupKey, bool> partyMemberAuraGroupOwnFrameBuffer = new();
    private readonly Dictionary<PartyAuraTimerKey, PartyAuraTimerState> partyAuraTimerStates = new();
    private readonly HashSet<PartyAuraTimerKey> partyAuraTimerLiveKeys = [];
    private readonly HashSet<uint> partyAuraTimerMemberOwnerIds = [];
    private readonly HashSet<uint> partyAuraTimerLiveOwnerIds = [];
    private readonly List<PartyAuraTimerKey> partyAuraTimerPruneBuffer = [];
    private readonly List<StatusSnapshot> statusSnapshotBuffer = [];
    private readonly StatusSnapshotFallbackCache statusSnapshotFallbackCache = new();
    private readonly Dictionary<uint, uint> gameObjectOwnerFrameCache = new();
    private readonly ActionKeybindIndex actionKeybindIndex = new();
    private readonly Dictionary<uint, bool> hotbarVisibilityCache = new();
    private readonly Dictionary<string, HashSet<uint>> visibleAurasByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, IDalamudTextureWrap> grayscaleIconCache = new();
    private readonly Dictionary<string, string> visibleAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, Vector2>> transientSkillPositionsByGroup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OverlayWindowDebugSnapshot> overlayWindowDebugSnapshots = new(StringComparer.OrdinalIgnoreCase);
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
    private bool allStatusSearchIndexBuilt;
    private bool keybindCacheDirty = true;
    private bool playerAuraFrameCacheValid;
    private bool targetAuraFrameCacheValid;
    private bool partyAuraFrameAllCacheValid;
    private bool partyAuraFrameOwnCacheValid;
    private PartyCooldownFrameSnapshot? partyCooldownFrameSnapshot;
    private readonly HashSet<PartyCooldownRuntimeKey> partyCooldownLiveRuntimeKeysFrameCache =
        new(PartyCooldownRuntimeKeyComparer.Instance);
    private bool partyCooldownLiveRuntimeKeysFrameCacheValid;
    private bool overlayTooltipRequestedThisFrame;
    private int pendingStatusId;
    private Vector2 draggedOverlayMouseStart;
    private Vector2 draggedOverlayPositionStart;
    private DateTime configSaveAfter = DateTime.MinValue;
    private DateTime configSaveQueuedAtUtc = DateTime.MinValue;
    private DateTime configSaveNextErrorLogAtUtc = DateTime.MinValue;
    private bool configSaveDeferredInCombat;
    private DateTime keybindCacheRefreshAfter = DateTime.MinValue;
    private DateTime performanceProfileNextRecordAtUtc = DateTime.MinValue;
    private DateTime performanceProfileNextErrorLogAtUtc = DateTime.MinValue;
    private DateTime performanceProfileLastErrorAtUtc = DateTime.MinValue;
    private DateTime lastBugDiagnosticEventAtUtc = DateTime.MinValue;
    private DateTime partyCooldownNextCandidateMissingObservationAtUtc = DateTime.MinValue;
    private DateTime partyCooldownActiveStatusIndexBuiltAtUtc = DateTime.MinValue;
    private ulong partyCooldownActiveStatusRosterHash;
    private bool partyCooldownActiveStatusAbsenceConfirmed;
    private long partyCooldownCandidateMissingLogCount;
    private long partyCooldownCandidateMissingObservationCount;
    private long partyCooldownLocalPlayerLogSkippedCount;
    private long partyCooldownLocalOwnedObjectLogSkippedCount;
    private long partyCooldownStatusFallbackBatchCount;
    private long partyCooldownTimerRefreshAcceptedCount;
    private long partyCooldownTimerStalePositiveSuppressedCount;
    private long auraSearchResultCacheHitCount;
    private long auraSearchResultCacheMissCount;
    private long partyAuraStatusFallbackBatchCount;
    private long partyAuraTimerRefreshAcceptedCount;
    private long partyAuraExpiredStatusSuppressedCount;
    private long performanceProfileFailureCount;
    private string performanceProfileLastError = string.Empty;
    private string partyAuraTimerLastDecision = string.Empty;
    private string partyCooldownTimerLastDecision = string.Empty;
    private string lastBugDiagnosticEvent = string.Empty;
    private PluginConfig config;
    private bool configVisible;
    private bool zoneLoadActive;
    private DateTime zoneLoadHiddenUntil = DateTime.MinValue;
    private readonly OverlayTooltipResolver overlayTooltipResolver = new();
    private readonly TooltipDiagnostics tooltipDiagnostics = new();
    private readonly PerformanceFrameStats performanceStats = new();
    private readonly PerformanceProfiler performanceProfiler = new();
    private readonly PerformanceProfileWriter performanceProfileWriter = new();
    private readonly ConfigSaveWorker configSaveWorker = new(snapshot => PluginInterface.SavePluginConfig(snapshot));
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

        if (this.config.Version < 4)
        {
            this.config.Version = 4;
            configChanged |= this.EnsureIconWindows();
        }

        if (this.config.Version < 5)
        {
            this.config.Version = 5;
            configChanged |= this.EnsureIconWindows();
        }

        if (this.config.Version < 6)
        {
            this.config.Version = 6;
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
        this.LoadPartyCooldowns();

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
        ChatGui.LogMessage += this.OnLogMessage;
        Condition.ConditionChange += this.OnConditionChange;
        ClientState.ZoneInit += this.OnZoneInit;
    }

    public void Dispose()
    {
        this.FlushConfigSave(force: true);
        var finalSnapshot = this.configSavePending
            ? PluginConfigClone.CreateSnapshot(this.config)
            : null;
        var finalConfigSaved = this.configSaveWorker.CompleteAndSaveLatest(finalSnapshot);
        if (finalSnapshot is not null && finalConfigSaved)
            this.configSavePending = false;

        if (this.configSaveWorker.TakeLastError() is { } finalConfigSaveError)
            Log.Error(finalConfigSaveError, "Failed to save the final FFXIVAura configuration snapshot during unload.");

        PluginInterface.UiBuilder.Draw -= this.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenConfig;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        ChatGui.LogMessage -= this.OnLogMessage;
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
        this.performanceProfileWriter.Dispose();
    }

    private void LoadAbilities()
    {
        try
        {
            var json = PluginDataFiles.ReadText(
                PluginInterface.AssemblyLocation.DirectoryName!,
                Path.Combine("Data", "abilities.json"),
                PluginDataFiles.AbilitiesResourceName);
            var loaded = JsonSerializer.Deserialize<List<AbilityDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (loaded is not null)
                this.abilities.AddRange(loaded.Where(a => a.ActionId > 0 && a.IconId > 0).Select(this.NormalizeAbilityDefinition));

            this.jobCandidatesCache.Clear();
            this.gameActionCandidatesCache.Clear();
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
        this.ResetWorldRuntimeState(WorldRuntimeResetReason.ZoneChange);
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
            {
                this.CloseAuraSearchWindow();
                if (this.EnsureIconWindows())
                    this.QueueConfigSave();
            }

            this.configWasVisible = this.configVisible;

            if (this.configVisible)
                this.DrawConfig();

            var loggedInAndLoaded = ClientState.IsLoggedIn && PlayerState.IsLoaded;
            var wasLoggedInAndLoaded = this.loginStabilizationState.WasLoggedInAndLoaded;
            var loginStarted = this.loginStabilizationState.Update(loggedInAndLoaded, DateTime.UtcNow);
            if (loginStarted)
                this.ResetWorldRuntimeState(WorldRuntimeResetReason.Login);
            else if (!loggedInAndLoaded && wasLoggedInAndLoaded)
                this.ResetWorldRuntimeState(WorldRuntimeResetReason.Logout);

            if (!this.config.Enabled || !loggedInAndLoaded)
            {
                this.FlushConfigSave(force: false);
                return;
            }

            if (loginStarted)
            {
                this.WarmOverlayRuntimeData();
                this.FlushConfigSave(force: false);
                return;
            }

            if (this.config.HideDuringZoneLoad && this.IsLoading())
            {
                this.WarmOverlayRuntimeData();
                this.FlushConfigSave(force: false);
                return;
            }

            this.DrawOverlay();
            this.ShowDeferredOverlayTooltip();
            this.FlushConfigSave(force: false);
        }
        finally
        {
            this.FinishPerformanceFrame(performanceFrameStart);
        }
    }

    private void BeginFrameCache()
    {
        if (this.configSaveWorker.TakeLastError() is { } configSaveError)
            this.HandleConfigSaveError(configSaveError);

        this.overlayTooltipRequestedThisFrame = false;
        this.overlayTooltipResolver.Clear();
        this.cooldownFrameCache.Clear();
        this.playerAuraFrameCacheValid = false;
        this.targetAuraFrameCacheValid = false;
        this.partyAuraFrameAllCacheValid = false;
        this.partyAuraFrameOwnCacheValid = false;
        this.partyCooldownFrameSnapshot = null;
        this.partyCooldownLiveRuntimeKeysFrameCache.Clear();
        this.partyCooldownLiveRuntimeKeysFrameCacheValid = false;
        this.gameObjectOwnerFrameCache.Clear();
        this.overlayWindowDebugSnapshots.Clear();
        var grayscaleProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.GrayscaleProcessing);
        var grayscaleIconCount = this.ProcessGrayscaleIconQueue();
        if (grayscaleIconCount > 0)
            this.performanceProfiler.EndSection(PerformanceProfileSection.GrayscaleProcessing, grayscaleProfileStart);

        this.performanceStats.CountGrayscaleIcons(grayscaleIconCount);
    }

    private void QueueConfigSave()
    {
        var nowUtc = DateTime.UtcNow;
        if (!this.configSavePending)
            this.configSaveQueuedAtUtc = nowUtc;

        this.configSavePending = true;
        this.configSaveAfter = nowUtc.Add(ConfigSaveDebounceDelay);
    }

    private void HandleConfigSaveError(Exception exception)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc >= this.configSaveNextErrorLogAtUtc)
        {
            this.configSaveNextErrorLogAtUtc = nowUtc.AddSeconds(30);
            Log.Error(exception, "Failed to save FFXIVAura configuration; retrying the latest snapshot.");
        }

        if (!this.configSavePending)
            this.configSaveQueuedAtUtc = nowUtc;

        this.configSavePending = true;
        this.configSaveAfter = nowUtc.Add(ConfigSaveCombatRetryDelay);
        this.SetBugDiagnosticEvent("configSaveFailed");
    }

    private void SaveConfigNow()
    {
        var profileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.ConfigSave);
        try
        {
            var snapshot = PluginConfigClone.CreateSnapshot(this.config);
            if (!this.configSaveWorker.TryEnqueue(snapshot))
            {
                this.configSaveAfter = DateTime.UtcNow.Add(ConfigSaveCombatRetryDelay);
                return;
            }
        }
        finally
        {
            this.performanceProfiler.EndSection(PerformanceProfileSection.ConfigSave, profileStart);
        }

        this.SetBugDiagnosticEvent("configSaveQueued");
        this.configSavePending = false;
        this.configSaveAfter = DateTime.MinValue;
        this.configSaveQueuedAtUtc = DateTime.MinValue;
        this.configSaveDeferredInCombat = false;
    }

    private void FlushConfigSave(bool force)
    {
        if (!this.configSavePending)
            return;

        var nowUtc = DateTime.UtcNow;
        if (!force && nowUtc < this.configSaveAfter)
            return;

        if (!force && this.ShouldDeferConfigSaveInCombat(nowUtc))
        {
            this.configSaveDeferredInCombat = true;
            this.configSaveAfter = nowUtc.Add(ConfigSaveCombatRetryDelay);
            return;
        }

        this.SaveConfigNow();
    }

    private bool ShouldDeferConfigSaveInCombat(DateTime nowUtc)
    {
        if (!ClientState.IsLoggedIn || !PlayerState.IsLoaded || !this.IsInCombat())
            return false;

        var queuedAtUtc = this.configSaveQueuedAtUtc == DateTime.MinValue
            ? nowUtc
            : this.configSaveQueuedAtUtc;
        return nowUtc - queuedAtUtc < ConfigSaveMaxCombatDeferDuration;
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
