namespace FFXIVAura;

public sealed partial class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = ["/fa"];

    private const float DefaultOverlayPositionX = 520f;
    private const float DefaultOverlayPositionY = 280f;
    private const float DefaultOverlayWidth = 760f;
    private const float DefaultOverlayHeight = 170f;
    private const float MinOverlayWidth = OverlayLayoutLimits.MinWidth;
    private const float MaxOverlayWidth = OverlayLayoutLimits.MaxWidth;
    private const float MinOverlayHeight = OverlayLayoutLimits.MinHeight;
    private const float MaxOverlayHeight = OverlayLayoutLimits.MaxHeight;
    private const float DefaultIconSize = 42f;
    private const float MinIconSize = OverlayLayoutLimits.MinIconSize;
    private const float MaxIconSize = OverlayLayoutLimits.MaxIconSize;
    private const float DefaultGap = 5f;
    private const float MinGap = 0f;
    private const float MaxGap = 16f;
    private const float DefaultFontScale = 1f;
    private const float MinFontScale = OverlayLayoutLimits.MinFontScale;
    private const float MaxFontScale = OverlayLayoutLimits.MaxFontScale;
    private const float DefaultOrderEditorHeight = 180f;
    private const float MinOrderEditorHeight = 90f;
    private const float MaxOrderEditorHeight = 520f;
    private const float OverlayWindowMargin = OverlayLayoutLimits.WindowMargin;
    private static readonly TimeSpan LoginSkillAutoAlignSuppressionDuration = TimeSpan.FromSeconds(3);
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

    private readonly PartyCooldownRuntimeStore partyCooldownRuntimeStore = new();
    private readonly PartyCooldownActiveStatusIndex partyCooldownActiveStatusIndex =
        new(PartyCooldownStatusCacheDuration);
    private readonly PartyCooldownCatalog partyCooldownCatalog;
    private readonly IPartyRosterReader partyRosterReader;
    private readonly PartyCooldownRosterService partyCooldownRosterService;
    private readonly PartyCooldownLogObservationBuffer partyCooldownLogObservations = new(64, 8);
    private readonly PartyCooldownLogObservationThrottle partyCooldownCandidateObservationThrottle = new();
    private readonly PartyCooldownSignalDiagnostics partyCooldownSignalDiagnostics = new();
    private readonly PartyCooldownLayoutModeTracker partyCooldownLayoutModeTracker = new(PartyCooldownAllianceLayoutRetention);
    private readonly HashSet<uint> partyCooldownMemberEntityIdsBuffer = [];
    private readonly PartyCooldownOwnedObjectMatcher partyCooldownOwnedObjectMatcher;
    private readonly PartyCooldownLogParser partyCooldownLogParser;
    private readonly IStatusSnapshotRuntime statusSnapshotRuntime;
    private readonly AuraCatalog auraCatalog;
    private readonly AuraFrameService auraFrameService;
    private readonly AuraSearchService auraSearchService;
    private readonly AuraTrackingService auraTrackingService;
    private readonly ActionKeybindService actionKeybindService;
    private readonly TooltipContentService tooltipContentService;
    private readonly IconTextureService iconTextureService;
    private readonly Dictionary<SkillLayoutScopeKey, VisibleAbilityLayoutState> visibleAbilityKeys = new();
    private readonly Dictionary<SkillLayoutScopeKey, Dictionary<string, Vector2>> transientSkillPositionsByGroup = new();
    private readonly Dictionary<string, OverlayFrameBuffers> overlayFrameBuffersByWindow = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> overlayWindowTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string WindowId, PartyCooldownLayoutEditMode LayoutMode), string> partyCooldownWindowTitles = new();
    private readonly Dictionary<string, OverlayWindowDebugSnapshot> overlayWindowDebugSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly OverlayDragSession overlayDragSession = new();
    private readonly AuraSearchWindowSession auraSearchWindowSession = new();
    private bool configWasVisible;
    private PartyCooldownFrameSnapshot? partyCooldownFrameSnapshot;
    private bool overlayTooltipRequestedThisFrame;
    private DateTime lastBugDiagnosticEventAtUtc = DateTime.MinValue;
    private string lastBugDiagnosticEvent = string.Empty;
    private PluginConfig config;
    private bool configVisible;
    private bool zoneLoadActive;
    private DateTime zoneLoadHiddenUntil = DateTime.MinValue;
    private readonly OverlayTooltipResolver overlayTooltipResolver = new();
    private readonly TooltipDiagnostics tooltipDiagnostics = new();
    private readonly IPlayerRuntimeContext playerRuntimeContext;
    private readonly IGameActionRepository gameActionRepository;
    private readonly IGameActionRuntime gameActionRuntime;
    private readonly AbilityCatalog abilityCatalog;
    private readonly AbilityTrackingService abilityTrackingService;
    private readonly TrackedSkillEditorModelBuilder trackedSkillEditorModelBuilder;
    private readonly TrackedSkillEditorSession trackedSkillEditorSession = new();
    private readonly CooldownFrameService cooldownFrameService;
    private readonly PerformanceFrameStats performanceStats = new();
    private readonly PerformanceProfiler performanceProfiler = new();
    private readonly PerformanceProfileIdentityAnonymizer performanceProfileIdentityAnonymizer = new();
    private readonly PluginLifetime lifetime;
    private readonly IPluginLog pluginLog;
    private readonly PerformanceProfileRecordingCoordinator performanceProfileRecordingCoordinator;
    private readonly ConfigSaveCoordinator configSaveCoordinator;
    private readonly LoginStabilizationState loginStabilizationState = new(LoginSkillAutoAlignSuppressionDuration);
    private readonly IFontHandle cooldownFont;
    private readonly IFontHandle chargeFont;
    private readonly IFontHandle auraCountFont;
    private PlayerFrameContext playerFrameContext;
    private bool disposed;

    public Plugin()
    {
        this.pluginLog = Log;
        this.lifetime = new PluginLifetime(
            (exception, message) => this.pluginLog.Error(exception, message));
        try
        {
            this.performanceProfileRecordingCoordinator = this.lifetime.Own(
                new PerformanceProfileRecordingCoordinator(
                    PluginInterface.ConfigDirectory.FullName,
                    this.SetBugDiagnosticEvent,
                    (exception, message) => this.pluginLog.Error(exception, message)),
                "performance profile recorder");
            this.configSaveCoordinator = this.lifetime.Own(
                new ConfigSaveCoordinator(
                    snapshot => PluginInterface.SavePluginConfig(snapshot),
                    this.performanceProfiler,
                    (exception, message) => this.pluginLog.Error(exception, message),
                    this.SetBugDiagnosticEvent),
                "configuration save coordinator");
            this.playerRuntimeContext = new DalamudPlayerRuntimeContext(
                ClientState,
                PlayerState,
                Condition,
                ObjectTable,
                TargetManager);
            this.gameActionRepository = new DalamudGameActionRepository(DataManager, this.pluginLog);
            this.gameActionRuntime = new DalamudGameActionRuntime(
                this.gameActionRepository,
                this.pluginLog);
            this.abilityCatalog = new AbilityCatalog(
                PluginInterface.AssemblyLocation.DirectoryName!,
                this.gameActionRepository,
                this.performanceProfiler,
                this.pluginLog);
            this.abilityTrackingService = new AbilityTrackingService(this.abilityCatalog);
            this.trackedSkillEditorModelBuilder = new TrackedSkillEditorModelBuilder(
                this.abilityCatalog,
                this.abilityTrackingService);
            this.actionKeybindService = new ActionKeybindService(
                GameGui,
                DataManager,
                this.gameActionRuntime,
                this.performanceProfiler,
                this.pluginLog);
            this.tooltipContentService = new TooltipContentService(
                DataManager,
                SeStringEvaluator,
                this.gameActionRepository,
                this.pluginLog);
            this.cooldownFrameService = new CooldownFrameService(this.gameActionRuntime);
            this.partyCooldownCatalog = new PartyCooldownCatalog(
                PluginInterface.AssemblyLocation.DirectoryName!,
                this.abilityCatalog,
                this.gameActionRepository,
                this.gameActionRuntime,
                this.pluginLog);
            this.partyCooldownLogParser = new PartyCooldownLogParser(this.partyCooldownCatalog);
            this.iconTextureService = this.lifetime.Own(
                new IconTextureService(TextureProvider, DataManager, this.pluginLog),
                "icon texture service");
            this.auraCatalog = new AuraCatalog(DataManager, this.pluginLog, this.performanceProfiler);
            this.statusSnapshotRuntime = new DalamudStatusSnapshotRuntime(
                ObjectTable,
                TargetManager,
                PartyList,
                this.SetBugDiagnosticEvent,
                StatusSnapshotFailureRetention);
            this.partyRosterReader = new DalamudPartyRosterReader(
                PartyList,
                ObjectTable,
                PlayerState,
                GameGui,
                this.SetBugDiagnosticEvent);
            this.auraFrameService = new AuraFrameService(
                this.statusSnapshotRuntime,
                this.partyRosterReader,
                this.performanceProfiler,
                this.auraCatalog.EnsureStatusIdentityIndex,
                this.auraCatalog.GetDefinition);
            this.auraSearchService = new AuraSearchService(
                this.auraCatalog,
                this.auraFrameService,
                this.performanceProfiler);
            this.auraTrackingService = new AuraTrackingService(
                this.auraCatalog,
                this.auraSearchService);
            this.partyCooldownRosterService = new PartyCooldownRosterService(
                this.partyRosterReader,
                TimeSpan.FromMilliseconds(100));
            this.partyCooldownOwnedObjectMatcher = new PartyCooldownOwnedObjectMatcher(
                new DalamudPartyCooldownOwnedObjectReader(ObjectTable),
                this.SetBugDiagnosticEvent);
            this.config = PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
            var configChanged = PluginConfigMigrator.Migrate(
                this.config,
                GetConfigNormalizationOptions(),
                message => this.pluginLog.Warning(message));
            this.PruneIconWindowRuntimeState();

            if (configChanged)
                _ = this.configSaveCoordinator.SaveNow(this.config, DateTime.UtcNow);

            this.cooldownFont = this.lifetime.Own(
                PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
                    new GameFontStyle(GameFontFamily.Meidinger, 20f)
                    {
                        Bold = true,
                    }),
                "cooldown font");
            this.chargeFont = this.lifetime.Own(
                PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
                    new GameFontStyle(GameFontFamily.Meidinger, 18f)
                    {
                        Bold = true,
                    }),
                "charge font");
            this.auraCountFont = this.lifetime.Own(
                PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
                    new GameFontStyle(GameFontFamily.Meidinger, 12f)
                    {
                        Bold = true,
                    }),
                "aura count font");
            this.abilityCatalog.Load();
            this.partyCooldownCatalog.Load();
            this.lifetime.Own(this.CreateRuntimeBindings(), "runtime bindings");
        }
        catch
        {
            this.lifetime.Dispose();
            throw;
        }
    }

    private PluginRuntimeBindings CreateRuntimeBindings()
    {
        var bindings = new List<PluginRuntimeBinding>(CommandNames.Length + 6);
        foreach (var commandName in CommandNames)
        {
            bindings.Add(new PluginRuntimeBinding(
                () => CommandManager.AddHandler(commandName, new CommandInfo(this.OnCommand)
                {
                    HelpMessage = "FFXIVAura 설정을 엽니다.",
                }),
                () => CommandManager.RemoveHandler(commandName),
                $"command handler {commandName}"));
        }

        bindings.Add(new PluginRuntimeBinding(
            () => { PluginInterface.UiBuilder.Draw += this.Draw; },
            () => { PluginInterface.UiBuilder.Draw -= this.Draw; },
            "UI draw handler"));
        bindings.Add(new PluginRuntimeBinding(
            () => { PluginInterface.UiBuilder.OpenMainUi += this.OpenConfig; },
            () => { PluginInterface.UiBuilder.OpenMainUi -= this.OpenConfig; },
            "main UI handler"));
        bindings.Add(new PluginRuntimeBinding(
            () => { PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfig; },
            () => { PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfig; },
            "configuration UI handler"));
        bindings.Add(new PluginRuntimeBinding(
            () => { ChatGui.LogMessage += this.OnLogMessage; },
            () => { ChatGui.LogMessage -= this.OnLogMessage; },
            "chat log handler"));
        bindings.Add(new PluginRuntimeBinding(
            () => { Condition.ConditionChange += this.OnConditionChange; },
            () => { Condition.ConditionChange -= this.OnConditionChange; },
            "condition change handler"));
        bindings.Add(new PluginRuntimeBinding(
            () => { ClientState.ZoneInit += this.OnZoneInit; },
            () => { ClientState.ZoneInit -= this.OnZoneInit; },
            "zone initialization handler"));

        return new PluginRuntimeBindings(
            bindings,
            (exception, message) => this.pluginLog.Error(exception, message));
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        try
        {
            _ = this.configSaveCoordinator.Complete(this.config, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            this.pluginLog.Error(ex, "Failed to complete the final configuration save.");
        }
        finally
        {
            this.lifetime.Dispose();
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
        this.NoteCrossThreadEventUse("OnZoneInit");
        this.ResetWorldRuntimeState(WorldRuntimeResetReason.ZoneChange);
        this.zoneLoadHiddenUntil = DateTime.UtcNow.AddMilliseconds(900);
    }

    private void NoteCrossThreadEventUse(string handler)
    {
        if (!FrameThreadGuard.DetectCrossThreadUse())
            return;

        this.pluginLog.Warning(
            "{Handler} ran off the captured UI/Draw thread; per-frame caches assume single-threaded access.",
            handler);
        this.SetBugDiagnosticEvent($"crossThreadEvent:{handler}");
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        this.NoteCrossThreadEventUse("OnConditionChange");
        if (flag is not ConditionFlag.BetweenAreas and not ConditionFlag.BetweenAreas51)
            return;

        if (value)
        {
            this.zoneLoadActive = true;
            this.zoneLoadHiddenUntil = DateTime.UtcNow.AddSeconds(20);
            return;
        }

        this.zoneLoadActive = this.playerRuntimeContext.Capture().IsBetweenAreas;
        this.zoneLoadHiddenUntil = DateTime.UtcNow.AddMilliseconds(220);
    }

    private void Draw()
    {
        FrameThreadGuard.CaptureUiThread();
        var performanceFrameStart = this.BeginPerformanceFrame();
        this.BeginFrameCache(
            this.performanceProfileRecordingCoordinator.CaptureDiagnosticsThisFrame);
        try
        {
            var wasConfigVisible = this.configWasVisible;
            if (this.configVisible && !wasConfigVisible)
                this.InvalidateKeybindCache();

            if (!this.configVisible && wasConfigVisible)
            {
                this.auraSearchWindowSession.Close();
                if (this.EnsureIconWindows())
                    this.QueueConfigSave();
            }

            this.configWasVisible = this.configVisible;

            if (this.configVisible)
                this.DrawConfig();

            var loggedInAndLoaded = this.playerFrameContext.IsReady;
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

    private void BeginFrameCache(bool capturePerformanceDiagnostics)
    {
        this.playerFrameContext = this.playerRuntimeContext.Capture();

        this.configSaveCoordinator.ObserveWorkerError(DateTime.UtcNow);

        this.overlayTooltipRequestedThisFrame = false;
        this.overlayTooltipResolver.Clear();
        this.cooldownFrameService.BeginFrame();
        this.partyCooldownFrameSnapshot = null;
        this.partyCooldownRuntimeStore.BeginFrame();
        this.statusSnapshotRuntime.BeginFrame();
        this.auraFrameService.BeginFrame(this.playerFrameContext);
        if (capturePerformanceDiagnostics)
            this.overlayWindowDebugSnapshots.Clear();
        var grayscaleProfileStart = this.performanceProfiler.BeginSection(PerformanceProfileSection.GrayscaleProcessing);
        var grayscaleIconCount = this.iconTextureService.ProcessGrayscaleQueue();
        if (grayscaleIconCount > 0)
            this.performanceProfiler.EndSection(PerformanceProfileSection.GrayscaleProcessing, grayscaleProfileStart);

        this.performanceStats.CountGrayscaleIcons(grayscaleIconCount);
    }

    private void QueueConfigSave()
        => this.configSaveCoordinator.Queue(DateTime.UtcNow);

    private void FlushConfigSave(bool force)
        => _ = this.configSaveCoordinator.Flush(
            this.config,
            this.playerFrameContext,
            DateTime.UtcNow,
            force);

    private void InvalidateKeybindCache()
        => this.actionKeybindService.Invalidate();

    private bool IsLoading()
    {
        if (this.playerFrameContext.IsBetweenAreas)
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
