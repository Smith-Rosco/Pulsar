using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Plugin;
using Pulsar.Core.Localization;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Services.ActionFeedback;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Core.Debug;
using Pulsar.Services.WindowSwitching;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings; // Added
using Pulsar.ViewModels.Strategies;
using Pulsar.ViewModels.Dialogs;
using Pulsar.Views;
using Pulsar.Views.Pages; // Added
using Pulsar.Helpers;
using System;
using System.Windows;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Threading;

namespace Pulsar
{
    public partial class App : System.Windows.Application
    {
        public new static App Current => (App)System.Windows.Application.Current;

        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // [UI Debug Mode] Parse the --ui-debug flag FIRST so logging, config
            // isolation and hook suppression are decided before any state is created.
            var debugOptions = DebugModeOptions.FromArgs(e.Args);

            // 0. Initialize Logging (Pulsar Sentinel - Unified Architecture)
            // Note: We use default settings here, will update from config later
            // Debug runs write to the isolated Pulsar.Debug directory at Verbose level
            // so E2E diagnostics can excerpt a full-trace log.
            var logsBaseDir = debugOptions.IsUiDebug
                ? debugOptions.LogDirectory
                : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Pulsar", 
                "Logs");
            
            var pluginLogsDir = Path.Combine(logsBaseDir, "Plugins");
            Directory.CreateDirectory(pluginLogsDir);

            // Create a level switch for runtime log level control
            // Debug mode forces Verbose regardless of the config-driven level applied later.
            var levelSwitch = new LoggingLevelSwitch(
                debugOptions.IsUiDebug ? LogEventLevel.Verbose : LogEventLevel.Information);

            // Build logger configuration with default values
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.With<Pulsar.Logging.PluginContextEnricher>();

            // Conditionally add Debug sink (will be updated from config later)
            loggerConfig = loggerConfig.WriteTo.Debug();

            // Main application logs (excluding plugin logs)
            loggerConfig = loggerConfig.WriteTo.Logger(lc => lc
                .Filter.ByExcluding(evt => evt.Properties.ContainsKey("PluginId"))
                .WriteTo.File(
                    path: Path.Combine(logsBaseDir, "pulsar-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                ));

            // Plugin logs (separated by plugin ID)
            loggerConfig = loggerConfig.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(evt => evt.Properties.ContainsKey("PluginId"))
                .WriteTo.Map(
                    keyPropertyName: "PluginId",
                    configure: (pluginId, wt) => wt.File(
                        path: Path.Combine(pluginLogsDir, $"{pluginId}-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        fileSizeLimitBytes: 100_000_000,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Action}] [ExecId:{ExecutionId}] [Elapsed:{ElapsedMs}ms] {Message:lj}{NewLine}{Exception}"
                    )
                ));

            Log.Logger = loggerConfig.CreateLogger();

            // [UI Debug Mode] Arm PKI/secret display redaction for capture output.
            DebugPkiRedaction.IsActive = debugOptions.IsUiDebug;

            Log.Information("=== Pulsar Application Starting (Log Level: {Level}) ===", levelSwitch.MinimumLevel);
            if (debugOptions.IsUiDebug)
            {
                Log.Information("[UIDebug] UI debug mode active: config={ConfigPath}, pipe={PipeName}",
                    debugOptions.ConfigFilePath, debugOptions.PipeName);
            }
            
            // [New] Check System Integrity (焦点锁定设置)
            PulsarNative.CheckSystemIntegrity();
            Log.Information("System integrity check completed");

            // Global Exception Handling
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            // 0. Logging Services
            serviceCollection.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            serviceCollection.AddSingleton(levelSwitch);
            serviceCollection.AddSingleton<ILoggingConfigService, LoggingConfigService>();
            serviceCollection.AddSingleton<IBackgroundWorkScheduler, BackgroundWorkScheduler>();

            // 1. Core Services
            serviceCollection.AddSingleton<IPluginMetadataRegistry, PluginMetadataRegistry>();

            // [UI Debug Mode] Expose the parsed debug options to every service that
            // needs to branch on them (startup coordinator, PKI redaction, ...).
            serviceCollection.AddSingleton(debugOptions);

            // [UI Debug Mode] Redirect Profiles.json to the isolated Pulsar.Debug
            // directory by reusing the existing configPath constructor override, so a
            // debug run never reads or writes the production configuration.
            serviceCollection.AddSingleton<IConfigService>(sp => new ConfigService(
                sp.GetRequiredService<ILogger<ConfigService>>(),
                sp.GetRequiredService<IPluginMetadataRegistry>(),
                sp.GetRequiredService<IBackgroundWorkScheduler>(),
                configPath: debugOptions.IsUiDebug ? debugOptions.ConfigFilePath : null));

            // [UI Debug Mode] Named-pipe state publisher + explicit command channel
            // (debug builds only; resolve to null in production so wiring is inert).
            if (debugOptions.IsUiDebug)
            {
                serviceCollection.AddSingleton<IDebugStatePublisher, DebugStatePublisher>();
                serviceCollection.AddSingleton<IDebugCommandServer, DebugCommandServer>();
            }
            serviceCollection.AddSingleton<IProcessRegistryService, ProcessRegistryService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<IWindowDiscoveryService>(sp => sp.GetRequiredService<IWindowService>());
            serviceCollection.AddSingleton<IWindowActivationService>(sp => sp.GetRequiredService<IWindowService>());
            serviceCollection.AddSingleton<IWindowFocusContextService>(sp => sp.GetRequiredService<IWindowService>());
            serviceCollection.AddSingleton<IWindowShellService>(sp => sp.GetRequiredService<IWindowService>());

            // [WindowService Deepening] 纯逻辑协作者注册为单例；WindowService 通过构造注入而非手 new。
            serviceCollection.AddSingleton<IWindowEligibilityPolicy>(sp =>
                new WindowEligibilityPolicy((uint)Process.GetCurrentProcess().Id));
            serviceCollection.AddSingleton<IWindowEligibilityEvaluator, WindowEligibilityEvaluator>();
            serviceCollection.AddSingleton<IWindowCaptureService, WindowCaptureService>();
            serviceCollection.AddSingleton<WindowInventoryCache>();
            serviceCollection.AddSingleton<IWindowInventoryCoordinator>(sp =>
                new WindowInventoryCoordinator(
                    sp.GetRequiredService<IWindowInventoryService>(),
                    sp.GetRequiredService<IWindowEligibilityEvaluator>(),
                    sp.GetRequiredService<WindowTrackingService>(),
                    sp.GetRequiredService<IWindowCaptureService>(),
                    sp.GetRequiredService<WindowInventoryCache>(),
                    sp.GetRequiredService<ILogger<WindowInventoryCoordinator>>(),
                    Process.GetCurrentProcess().Id));
            serviceCollection.AddSingleton<QuickSwitchEngine>();
            serviceCollection.AddSingleton<WindowTrackingService>();
            serviceCollection.AddSingleton<IWindowInventoryService, WindowInventoryService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<IActionFeedbackService, ActionFeedbackService>();
            serviceCollection.AddSingleton<IActionFeedbackPresenter, ActionFeedbackPresenter>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            // [RadialRenderer] Pluggable rendering seam + theme preset resolution.
            // Every renderer registers as a singleton; Default is registered LAST so
            // the legacy GetService<IRadialRenderer>() (SlotOrb fallback) resolves to
            // the Default renderer. StyleRendererFactory receives all three and picks
            // the active one from ProfileSettings.RadialRenderer at menu open.
            serviceCollection.AddSingleton<Core.Rendering.IRadialRenderer, Core.Rendering.ClassicRingRadialRenderer>();
            serviceCollection.AddSingleton<Core.Rendering.IRadialRenderer, Core.Rendering.GlassmorphismRadialRenderer>();
            serviceCollection.AddSingleton<Core.Rendering.IRadialRenderer, Core.Rendering.DefaultRadialRenderer>();
            // [RadialRenderer] Plugin contributions: mutable registry with built-in ids
            // reserved (a plugin can never shadow Default/ClassicRing/Glassmorphism) and
            // owner gating on the ui.render permission from PluginProfile.GrantedPermissions.
            serviceCollection.AddSingleton<Core.Rendering.IRadialRendererRegistry>(sp =>
                new Core.Rendering.RadialRendererRegistry(
                    reservedIds: new[]
                    {
                        Core.Rendering.DefaultRadialRenderer.RendererId,
                        Core.Rendering.ClassicRingRadialRenderer.RendererId,
                        Core.Rendering.GlassmorphismRadialRenderer.RendererId
                    },
                    canRegisterOwner: ownerId =>
                    {
                        if (string.IsNullOrWhiteSpace(ownerId))
                        {
                            return false;
                        }

                        var config = sp.GetRequiredService<IConfigService>();
                        var snapshot = config.GetSnapshot();
                        return snapshot.Plugins.TryGetValue(ownerId, out var profile)
                            && profile.GrantedPermissions.Contains(Pulsar.Core.Plugin.PluginPermissions.UiRender);
                    }));
            serviceCollection.AddSingleton<Core.Rendering.StyleRendererFactory>();
            serviceCollection.AddSingleton<Core.Rendering.RadialThemePresetResolver>();
            serviceCollection.AddSingleton<Func<Pulsar.Models.AppTheme, Core.Rendering.IRadialThemeTokens>>(
                _ => Core.Rendering.RadialThemeTokenSet.FromTheme);
            serviceCollection.AddSingleton<IWindowPlacementService, WindowPlacementService>();
            serviceCollection.AddSingleton<IMenuViewportService, MenuViewportService>();
            serviceCollection.AddSingleton<IAnimationController, AnimationController>();
            serviceCollection.AddSingleton<ISlotLayoutEngine, SlotLayoutEngine>();
            serviceCollection.AddSingleton<ISubMenuLayoutEngine, SubMenuLayoutEngine>();
            serviceCollection.AddSingleton<IMouseTrackingService, MouseTrackingService>();
            serviceCollection.AddSingleton<IPagingController, PagingController>();
            serviceCollection.AddSingleton<IPreviewService, PreviewService>();
            serviceCollection.AddSingleton<ILocalUiPreferencesService, LocalUiPreferencesService>();
            serviceCollection.AddSingleton<ISettingsNavigationGuard, SettingsNavigationGuard>();
            serviceCollection.AddSingleton<Services.Interfaces.ICustomIconStore, Services.CustomIconStore>();
            serviceCollection.AddSingleton<SettingsPageCatalog>();
            serviceCollection.AddSingleton<IAppStartupCoordinator, AppStartupCoordinator>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<GlobalMouseHook>();
            serviceCollection.AddSingleton<IHotkeyService, HotkeyService>();
            serviceCollection.AddSingleton<IGlobalMouseService, GlobalMouseService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<Services.Interfaces.IScriptFileService, Services.ScriptFileService>();
            serviceCollection.AddSingleton<Services.Interfaces.IScriptValidationService, Services.ScriptValidationService>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.IOnboardingTemplateService, Pulsar.Features.Tutorial.Services.OnboardingTemplateService>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.IOnboardingStateService, Pulsar.Features.Tutorial.Services.OnboardingStateService>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.TutorialScenarioRegistry>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.Prerequisites.ExcelPrerequisiteProvider>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.Prerequisites.BrowserPrerequisiteProvider>();
            serviceCollection.AddSingleton<Pulsar.Services.ExampleLibraryService>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.StartupCoordinator>();
            
            // Tutorial Service
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.TutorialStepLoader>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.TriggerHandlers.ITriggerHandlerFactory, Pulsar.Features.Tutorial.Services.TriggerHandlers.TriggerHandlerFactory>();
            serviceCollection.AddSingleton<ITargetLocator, Pulsar.Features.Tutorial.Services.TargetLocator>();
            serviceCollection.AddSingleton<IOverlayManager, Pulsar.Features.Tutorial.Services.OverlayManager>();
            serviceCollection.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.ISettingsWindowAccessor, Pulsar.Features.Tutorial.Services.SettingsWindowAccessor>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.ITutorialTriggerEngine, Pulsar.Features.Tutorial.Services.TutorialTriggerEngine>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.ITutorialSpotlightController, Pulsar.Features.Tutorial.Services.TutorialSpotlightController>();
            serviceCollection.AddSingleton<Pulsar.Features.Tutorial.Services.IWaitStepHintTimeout, Pulsar.Features.Tutorial.Services.WaitStepHintTimeout>();
            serviceCollection.AddSingleton<ILocalizationService, LocalizationService>();
            serviceCollection.AddSingleton<Features.Tutorial.Services.StartupCoordinator>();
            serviceCollection.AddSingleton<ITutorialService, TutorialService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<ILogger<Pulsar.Features.Tutorial.Services.TutorialOrchestrator>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<Pulsar.Features.Tutorial.Services.TutorialOrchestrator>());

            // Lazy<T> / Func<T> factories for AppStartupCoordinator (architecture review
            // 2026-09-04 candidate K): preserve the late-bound timing constraints that
            // were previously encoded as `GetRequiredService<T>()` calls inside
            // AppStartupCoordinator.Run*. The coordinator's ctor now takes these
            // directly so the IServiceProvider dependency can be deleted.
            serviceCollection.AddSingleton<Lazy<IProcessRegistryService>>(sp => new Lazy<IProcessRegistryService>(() => sp.GetRequiredService<IProcessRegistryService>()));
            serviceCollection.AddSingleton<Lazy<PluginBreakerNotificationService>>(sp => new Lazy<PluginBreakerNotificationService>(() => sp.GetRequiredService<PluginBreakerNotificationService>()));  // ADR-013
            serviceCollection.AddSingleton<Lazy<IHotkeyService>>(sp => new Lazy<IHotkeyService>(() => sp.GetRequiredService<IHotkeyService>()));
            // [Candidate O] No Lazy<GlobalKeyboardHook> factory: the hook is only ever
            // constructed through the HotkeyService dependency chain (its mode is
            // configured there too), so the startup module no longer holds it.
            serviceCollection.AddSingleton<Lazy<IGlobalMouseService>>(sp => new Lazy<IGlobalMouseService>(() => sp.GetRequiredService<IGlobalMouseService>()));
            serviceCollection.AddSingleton<Lazy<ITutorialService>>(sp => new Lazy<ITutorialService>(() => sp.GetRequiredService<ITutorialService>()));
            serviceCollection.AddSingleton<Func<RadialMenuWindow>>(sp => () => sp.GetRequiredService<RadialMenuWindow>());  // WPF InitializeComponent
            serviceCollection.AddSingleton<Func<FirstLaunchSetupWizardViewModel>>(sp => () => sp.GetRequiredService<FirstLaunchSetupWizardViewModel>());  // AddTransient → avoid captive
            // Debug-only factories: register a no-op lambda when ui-debug is off so the
            // AppStartupCoordinator constructor can stay free of conditional logic. The
            // coordinator only invokes the factory inside an `if (IsUiDebug)` guard.
            serviceCollection.AddSingleton<Func<IDebugStatePublisher>>(sp =>
                debugOptions.IsUiDebug
                    ? (Func<IDebugStatePublisher>)(() => sp.GetRequiredService<IDebugStatePublisher>())
                    : () => throw new InvalidOperationException("DebugStatePublisher only available in ui-debug mode."));
            serviceCollection.AddSingleton<Func<IDebugCommandServer>>(sp =>
                debugOptions.IsUiDebug
                    ? (Func<IDebugCommandServer>)(() => sp.GetRequiredService<IDebugCommandServer>())
                    : () => throw new InvalidOperationException("DebugCommandServer only available in ui-debug mode."));

            // Office Action Presets
            serviceCollection.AddSingleton<Pulsar.Features.Presets.Services.IPresetCatalogService, Pulsar.Features.Presets.Services.PresetCatalogService>();
            serviceCollection.AddSingleton<Pulsar.Features.Presets.Services.IPresetInstallService, Pulsar.Features.Presets.Services.PresetInstallService>();

            
            // Fuzzy Search Service
            serviceCollection.AddSingleton(typeof(Pulsar.Services.Interfaces.IFuzzySearchService<>), typeof(Pulsar.Services.FuzzySearch.FuzzySearchService<>));
            
            // 2. Plugin System (New Architecture)
            // External plugin packages are installed under %AppData% by
            // PluginPackageManager. The runtime loader MUST scan the same store,
            // otherwise "installed" plugins are visible in Settings but never loaded.
            var externalPluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "Plugins");

            serviceCollection.AddSingleton<Core.Plugin.Runtime.ICorePluginFailureHandler, AppShutdownCorePluginFailureHandler>();
            serviceCollection.AddSingleton<Core.Plugin.IPluginPermissionService, Core.Plugin.PluginPermissionService>();
            serviceCollection.AddSingleton<Core.Plugin.IPluginPackageIntegrityVerifier, PluginPackageIntegrityService>();
            serviceCollection.AddPluginFoundation(externalPluginDirectory);
            
            // [New] Plugin Monitoring & Analytics Services
            serviceCollection.AddSingleton<IPluginUsageTracker, PluginUsageTracker>();
            serviceCollection.AddSingleton<IPluginHealthMonitor, PluginHealthMonitor>();
            serviceCollection.AddSingleton<IPluginLogService, PluginLogService>();
            serviceCollection.AddSingleton<IPluginRecommendationEngine, PluginRecommendationEngine>();
            
            // [New] Configuration Validation
            serviceCollection.AddSingleton<Services.Validation.ConfigValidationPipeline>();

            // [New] Configuration Backup / Restore
            serviceCollection.AddSingleton<IConfigBackupService, ConfigBackupService>();

            // [New] Smart sub-action defaults for newly created slot types
            serviceCollection.AddSingleton<ISmartSubActionDefaults, SmartSubActionDefaults>();

            // 3. Focus Management
            serviceCollection.AddSingleton<IFocusNativeAdapter, WindowsFocusNativeAdapter>();
            serviceCollection.AddSingleton<IModifierStateTracker>(sp => sp.GetRequiredService<GlobalKeyboardHook>());
            serviceCollection.AddSingleton<IFocusManager, Services.FocusManager>();
            serviceCollection.AddSingleton<IFocusHistory>(sp => (IFocusHistory)sp.GetRequiredService<IFocusManager>());

            // 3b. Gesture Isolation Filter (pre-takeover right-drag gating)
            serviceCollection.AddSingleton<IGestureIsolationNative, GestureIsolationNative>();
            serviceCollection.AddSingleton<IGestureIsolationService, GestureIsolationService>();

            // 4. UI Services
            serviceCollection.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            serviceCollection.AddSingleton<MenuSession>();
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();

            // SubMenu strategies: window switching plus the cascade form (Change B);
            // both are routed by the coordinator via their StrategyId.
            serviceCollection.AddSingleton<ISubMenuStrategy, WindowSwitchSubMenuStrategy>();
            serviceCollection.AddSingleton<ISubMenuStrategy, CascadeSubMenuStrategy>();
            
            // [Fix] Register SettingsViewModel as Transient for fresh state on every open
            serviceCollection.AddTransient<AboutViewModel>();
            serviceCollection.AddSingleton<SettingsShellViewModel>();
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsPageFactory>();
            serviceCollection.AddTransient<SlotWheelEditorViewModel>();
            
            // [New] Plugin Management UI
            serviceCollection.AddTransient<PluginManagerViewModel>();
            serviceCollection.AddTransient<SettingsPluginsPage>();
            
            // [New] Usage Analytics UI
            serviceCollection.AddTransient<UsageStatsReadModel>();
            serviceCollection.AddTransient<SettingsAnalyticsPageViewModel>();
            serviceCollection.AddTransient<SettingsAnalyticsPage>();

            // [External Plugins] External Plugin Management Services
            serviceCollection.AddSingleton<LocalPluginScanner>(sp =>
            {
                var logger = sp.GetService<ILogger<LocalPluginScanner>>();
                return new LocalPluginScanner(externalPluginDirectory, logger);
            });

            serviceCollection.AddSingleton<IPluginPackageManager>(sp =>
            {
                var logger = sp.GetService<ILogger<PluginPackageManager>>();
                var integrityVerifier = sp.GetRequiredService<Core.Plugin.IPluginPackageIntegrityVerifier>();

                return new PluginPackageManager(externalPluginDirectory, logger, integrityVerifier);
            });

            // External Plugin lifecycle ops: owns install/uninstall/enable sequences
            // (refresh→grant→activate, revoke→deactivate→delete). Settings UI calls
            // its commands and renders results; the sequences live only here.
            serviceCollection.AddSingleton<IExternalPluginLifecycleOps, ExternalPluginLifecycleOps>();
            
            serviceCollection.AddTransient<ExternalPluginManagerViewModel>();
            
            serviceCollection.AddTransient<Pulsar.ViewModels.Dialogs.FirstLaunchSetupWizardViewModel>();

            serviceCollection.AddTransient<SettingsWindow>();
            
            // Build Container
            Services = serviceCollection.BuildServiceProvider();

            // Initialize static helpers that need logging
            var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
            IconHelper.Initialize(loggerFactory);
            UiaHelper.Initialize(loggerFactory);
            Pulsar.Plugins.Extensions.BookmarkletRunner.BrowserHelper.Initialize(loggerFactory);

            // VBA runner internals
            Pulsar.Plugins.Extensions.VbaRunner.ScriptEngine.Initialize(loggerFactory);
            Pulsar.Plugins.Extensions.VbaRunner.ComRetryHelper.Initialize(loggerFactory);
            Pulsar.Plugins.Extensions.VbaRunner.ComConnectionManager.Initialize(loggerFactory);
            Pulsar.Plugins.Extensions.VbaRunner.VbaModuleInjector.Initialize(loggerFactory);

            var startupCoordinator = Services.GetRequiredService<IAppStartupCoordinator>();
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await startupCoordinator.RunBlockingInitializationAsync();
                    startupCoordinator.StartDeferredInitialization();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Blocking startup initialization failed");
                    Shutdown();
                }
            }, DispatcherPriority.Loaded);

        }

        private void RunShutdownTask(string phase, Func<Task> taskFactory)
        {
            Log.Information("[Shutdown] Starting {Phase}", phase);

            try
            {
                Task.Run(taskFactory).GetAwaiter().GetResult();
                Log.Information("[Shutdown] Completed {Phase}", phase);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Shutdown] {Phase} failed", phase);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== Pulsar Application Exiting ===");
            
            if (Services != null)
            {
                // IMPORTANT: OnExit runs on the WPF Dispatcher thread. Persistence
                // methods below await file I/O; if invoked inline, their
                // continuations are posted back to the Dispatcher while this
                // thread is blocked on GetAwaiter().GetResult() — a guaranteed
                // async deadlock. Run each shutdown phase on the thread pool and
                // block only for completion.
                RunShutdownTask(
                    "ProcessRegistry flush",
                    () =>
                    {
                        var processRegistry = Services.GetService<IProcessRegistryService>();
                        return processRegistry?.FlushAsync() ?? Task.CompletedTask;
                    });

                RunShutdownTask(
                    "PluginUsageTracker flush",
                    () =>
                    {
                        var usageTracker = Services.GetService<IPluginUsageTracker>();
                        return usageTracker?.FlushAsync() ?? Task.CompletedTask;
                    });

                RunShutdownTask(
                    "Plugin unload",
                    () =>
                    {
                        var runtimeOps = Services.GetService<IPluginRuntimeOps>();
                        return runtimeOps?.UnloadAllAsync() ?? Task.CompletedTask;
                    });

                var backgroundWorkScheduler = Services.GetService<IBackgroundWorkScheduler>();
                backgroundWorkScheduler?.CancelAll();

                // All singletons that need deterministic cleanup implement IDisposable.
                // Let the container dispose them once after plugin unload and
                // persistence flush; avoid manual disposal followed by a second
                // container disposal of the same instance.
                (Services as IDisposable)?.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled Dispatcher Exception");
            
            // [New] Emergency restore system settings
#pragma warning disable CS0618
            PulsarNative.EmergencyRestore();
#pragma warning restore CS0618
            
            // Optionally: Prevent crash if recoverable
            // e.Handled = true; 
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved Task Exception");
            // Prevent process termination
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             if (e.ExceptionObject is Exception ex)
             {
                 Log.Fatal(ex, "Unhandled AppDomain Exception (IsTerminating={IsTerminating})", e.IsTerminating);
                 
                 // [New] Emergency restore system settings before crash
#pragma warning disable CS0618
                  PulsarNative.EmergencyRestore();
#pragma warning restore CS0618
                  
                  Log.CloseAndFlush();
             }
        }
    }
}
