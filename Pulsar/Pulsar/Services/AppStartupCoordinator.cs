using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Tutorial.Services;
using Pulsar.ViewModels.Dialogs;
using Pulsar.Views;
using Pulsar.Core.Localization;
using Pulsar.Core.Debug;
using Wpf.Ui.Appearance;

namespace Pulsar.Services
{
    public class AppStartupCoordinator : IAppStartupCoordinator
    {
        // Construction-time dependencies — all safe to resolve eagerly at the moment
        // the coordinator is built (the container is already populated).
        private readonly IConfigService _configService;
        private readonly DebugModeOptions _debugOptions;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly ITrayService _trayService;
        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localizationService;
        private readonly Features.Tutorial.Services.StartupCoordinator _tutorialStartupCoordinator;
        private readonly IDialogService _dialogService;
        private readonly Validation.ConfigValidationPipeline _validationPipeline;
        private readonly IOnboardingStateService _onboardingStateService;

        // Lazy / Func dependencies — defer until their owning step. See
        // architecture review 2026-09-04 candidate K: the previous implementation
        // resolved these inside the methods via `IServiceProvider`; that made the
        // module impossible to construct in tests, and it leaked construction
        // ordering (WPF window init, native hook installation, transient VM
        // capture) across the seam.
        private readonly Lazy<IProcessRegistryService> _processRegistryService;
        private readonly Lazy<PluginBreakerNotificationService> _breakerRelay;          // ADR-013 timing: must be resolved after tray init.
        private readonly Lazy<IHotkeyService> _hotkeyService;
        private readonly Lazy<IGlobalMouseService> _globalMouseService;
        private readonly Lazy<ITutorialService> _tutorialService;
        private readonly Func<RadialMenuWindow> _mainWindowFactory;
        private readonly Func<FirstLaunchSetupWizardViewModel> _wizardFactory;
        private readonly Func<IDebugStatePublisher>? _debugStatePublisherFactory;          // registered only in ui-debug
        private readonly Func<IDebugCommandServer>? _debugCommandServerFactory;           // registered only in ui-debug
        // Unit-test seam over the WPF Application.Current global static: the deferred
        // warm-up's tutorial branch must not depend on process-global WPF state, or a
        // sibling test that constructs `new Application()` (ThemeServiceTests etc.)
        // leaves a non-null Current whose dead Dispatcher makes InvokeAsync hang forever.
        private readonly Func<System.Windows.Threading.Dispatcher> _dispatcherProvider;

        private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly ILogger<AppStartupCoordinator> _logger;

        public AppStartupCoordinator(
            IConfigService configService,
            DebugModeOptions debugOptions,
            IPluginRegistry pluginRegistry,
            ITrayService trayService,
            IThemeService themeService,
            ILocalizationService localizationService,
            Features.Tutorial.Services.StartupCoordinator tutorialStartupCoordinator,
            IDialogService dialogService,
            Validation.ConfigValidationPipeline validationPipeline,
            IOnboardingStateService onboardingStateService,
            IProcessRegistryService processRegistryService,                  // wrapped in Lazy below — keep ctor empty
            IBackgroundWorkScheduler backgroundWorkScheduler,
            LoggingLevelSwitch levelSwitch,
            ILogger<AppStartupCoordinator> logger,
            Lazy<PluginBreakerNotificationService>? breakerRelay = null,
            Lazy<IHotkeyService>? hotkeyService = null,
            Lazy<IGlobalMouseService>? globalMouseService = null,
            Lazy<ITutorialService>? tutorialService = null,
            Func<RadialMenuWindow>? mainWindowFactory = null,
            Func<FirstLaunchSetupWizardViewModel>? wizardFactory = null,
            Func<IDebugStatePublisher>? debugStatePublisherFactory = null,
            Func<IDebugCommandServer>? debugCommandServerFactory = null,
            Func<System.Windows.Threading.Dispatcher>? dispatcherProvider = null)
        {
            _configService = configService;
            _debugOptions = debugOptions;
            _pluginRegistry = pluginRegistry;
            _trayService = trayService;
            _themeService = themeService;
            _localizationService = localizationService;
            _tutorialStartupCoordinator = tutorialStartupCoordinator;
            _dialogService = dialogService;
            _validationPipeline = validationPipeline;
            _onboardingStateService = onboardingStateService;
            _backgroundWorkScheduler = backgroundWorkScheduler;
            _levelSwitch = levelSwitch;
            _logger = logger;

            // Lazy<T> cannot be injected directly — DI containers refuse to hand out
            // a Lazy<T> unless someone asked for one. Wrap the eager deps we received
            // so the existing call sites can keep their late-bound semantics.
            _processRegistryService = new Lazy<IProcessRegistryService>(() => processRegistryService);
            _breakerRelay = breakerRelay ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Lazy<PluginBreakerNotificationService> from DI " +
                "because PluginBreakerNotificationService subscribes in its constructor (ADR-013).");
            _hotkeyService = hotkeyService ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Lazy<IHotkeyService> from DI to preserve the " +
                "--ui-debug input-capture invariant.");
            // [Candidate O] No Lazy<GlobalKeyboardHook> here anymore: the hook mode is
            // configured by HotkeyService.InitializeAsync (its own module), and the hook
            // instance is only ever constructed through the HotkeyService dependency chain.
            _globalMouseService = globalMouseService ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Lazy<IGlobalMouseService> from DI to preserve " +
                "the --ui-debug input-capture invariant.");
            _tutorialService = tutorialService ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Lazy<ITutorialService> from DI to avoid eager " +
                "construction of TutorialOrchestrator and its 9 dependencies.");
            _mainWindowFactory = mainWindowFactory ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Func<RadialMenuWindow> from DI to defer WPF " +
                "InitializeComponent until after theme and tray are initialized.");
            _wizardFactory = wizardFactory ?? throw new InvalidOperationException(
                "AppStartupCoordinator requires a Func<FirstLaunchSetupWizardViewModel> from DI " +
                "because FirstLaunchSetupWizardViewModel is AddTransient and would otherwise be " +
                "captured by this singleton.");
            _debugStatePublisherFactory = debugStatePublisherFactory;
            _debugCommandServerFactory = debugCommandServerFactory;
            // Production default: the live WPF Application.Current dispatcher (same
            // object the previous inline access used). Tests inject a fixed provider
            // so the tutorial path is deterministic regardless of process-global state.
            _dispatcherProvider = dispatcherProvider
                ?? (() => System.Windows.Application.Current?.Dispatcher!);
        }

        public async Task RunBlockingInitializationAsync()
        {
            var startupStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("[Startup] Running blocking startup responsibilities");

            await ApplyLoggingConfigurationAsync();

            // [UI Debug Mode] Start the named-pipe state publisher before any UI can
            // summon so the E2E driver cannot miss early state events.
            if (_debugOptions.IsUiDebug)
            {
                var statePublisher = _debugStatePublisherFactory?.Invoke()
                    ?? throw new InvalidOperationException(
                        "DebugStatePublisher factory not registered; ui-debug mode requires it.");
                statePublisher.Start(_debugOptions.PipeName);
                _logger.LogInformation("[Startup] Debug state publisher started on pipe {PipeName}", _debugOptions.PipeName);
            }

            await ConfigureLocalizationAsync();

            // Theme must be established before the tray icon builds its ContextMenu.
            // Otherwise ThemeService falls back to its in-memory default and the first
            // tray menu is rendered with the wrong theme until Settings opens.
            await ConfigureThemeAsync();

            var processRegistryService = _processRegistryService.Value;
            await processRegistryService.InitializeAsync();
            _logger.LogInformation("[Startup] ProcessRegistryService initialized");

            _trayService.Initialize();
            _logger.LogInformation("[Startup] Tray service initialized");

            // Circuit breaker transitions must reach telemetry + tray notifications.
            // The relay subscribes to PluginCircuitBreakerPolicy events in its
            // constructor, so resolving it after tray init activates the wiring
            // before any plugin execution can trip a breaker (ADR-013).
            _ = _breakerRelay.Value;
            _logger.LogInformation("[Startup] Circuit breaker notification relay activated");

            await _pluginRegistry.LoadCoreAsync();
            _logger.LogInformation("[Startup] Core plugins activated");

            var mainWindow = _mainWindowFactory();
            mainWindow.Show();
            _logger.LogInformation("[Startup] Radial menu window shown");

            // [UI Debug Mode] Start the command server only after the menu window and
            // its view-model exist, so early 'menu-open' commands cannot race window
            // construction.
            if (_debugOptions.IsUiDebug)
            {
                var commandServer = _debugCommandServerFactory?.Invoke()
                    ?? throw new InvalidOperationException(
                        "DebugCommandServer factory not registered; ui-debug mode requires it.");
                commandServer.Start(_debugOptions.CommandPipeName);
                _logger.LogInformation("[Startup] Debug command server started on pipe {PipeName}", _debugOptions.CommandPipeName);
            }

            // [UI Debug Mode] Real global hotkeys and mouse-gesture hooks must NOT be
            // registered in a debug instance by default: the E2E driver drives input
            // via the explicit command channel (menu-open/menu-close) and, when opted
            // in via --ui-debug-hooks, real SendInput from a separate process. A debug
            // run must never capture the user's actual desktop input by surprise.
            //
            // --ui-debug-hooks opts a debug run INTO the real global-hotkey + keyboard
            // hook path (for workflows exercising the SendInput trigger); the
            // mouse-gesture hook stays off in every debug run.
            bool registerHotkeys = !_debugOptions.IsUiDebug || _debugOptions.EnableHotkeyHooks;
            if (registerHotkeys)
            {
                var hotkeyService = _hotkeyService.Value;
                await hotkeyService.InitializeAsync();
                _logger.LogInformation("[Startup] Hotkey service initialized{Suffix}",
                    _debugOptions.IsUiDebug ? " (ui-debug-hooks opt-in)" : string.Empty);
            }

            if (!_debugOptions.IsUiDebug)
            {
                var globalMouseWheelService = _globalMouseService.Value;
                globalMouseWheelService.Initialize();
                _logger.LogInformation("[Startup] Global mouse wheel service initialized");
            }

            if (!registerHotkeys)
            {
                // [Candidate O] The hook-mode configuration now lives inside
                // HotkeyService.InitializeAsync — nothing to do here when hotkeys
                // are registered; ui-debug (no hooks) only logs.
                _logger.LogInformation("[Startup] UI debug mode: skipping hotkey, mouse-gesture and keyboard-hook registration");
            }

            startupStopwatch.Stop();
            _logger.LogInformation("[Startup] Blocking startup responsibilities complete in {ElapsedMs}ms", startupStopwatch.ElapsedMilliseconds);
        }

        public void StartDeferredInitialization()
        {
            _logger.LogInformation("[Startup] Starting deferred warm-up responsibilities");

            _ = _backgroundWorkScheduler.ScheduleAsync(
                "startup.deferred-warmup",
                async cancellationToken =>
            {
                var deferredStopwatch = Stopwatch.StartNew();
                try
                {
                    await _pluginRegistry.DiscoverDeferredAsync();
                    await ActivateEnabledExternalPluginsAsync();

                    ConfigureValidationPipeline();

                    await RunOnboardingStartupAsync(cancellationToken);

                    // First-launch decision: defer to IOnboardingStateService — the
                    // single source of truth for OnboardingState → semantic flags. This
                    // replaces the prior inline 3-way string/flag check (candidate I,
                    // ADR-018). Behaviour change is intentional: an illegal config
                    // combination (OnboardingState=Complete with HasCompletedTutorial
                    // =false, documented at ProfilesConfig.cs:354-357) now returns
                    // instead of silently entering the tutorial.
                    var onboardingState = await _onboardingStateService.GetStateAsync();
                    if (onboardingState.HasCompletedTutorial
                        || onboardingState.HasSkippedTutorial
                        || !onboardingState.HasCompletedSetup)
                    {
                        return;
                    }

                    Log.Information("First launch detected, starting tutorial");
                    await Task.Delay(1500, cancellationToken);

                    // Via the dispatcher seam (ctor-injected; defaults to
                    // Application.Current?.Dispatcher). In hosts without a WPF
                    // Application the seam yields null → NRE → caught and logged
                    // below, exactly like the previous inline access.
                    var uiDispatcher = _dispatcherProvider();
                    await await uiDispatcher.InvokeAsync(
                        async () =>
                        {
                            var tutorialService = _tutorialService.Value;
                            await tutorialService.CheckResumeAsync();

                            if (!tutorialService.IsTutorialActive)
                            {
                                await tutorialService.StartTutorialAsync();
                            }
                        },
                        System.Windows.Threading.DispatcherPriority.Normal,
                        cancellationToken);

                    deferredStopwatch.Stop();
                    _logger.LogInformation("[Startup] Deferred startup responsibilities complete in {ElapsedMs}ms", deferredStopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    deferredStopwatch.Stop();
                    _logger.LogError(ex, "[Startup] Deferred startup task failed");
                }
            },
                new BackgroundWorkOptions
                {
                    Priority = BackgroundWorkPriority.Normal,
                    DuplicateBehavior = BackgroundWorkDuplicateBehavior.ReuseExisting
                });
        }

        private void ConfigureValidationPipeline()
        {
            if (_configService is ConfigService concreteConfigService)
            {
                concreteConfigService.SetValidationPipeline(_validationPipeline);
                Log.Information("Validation pipeline configured for ConfigService");
            }
        }

        /// <summary>
        /// Activates every enabled external plugin after deferred discovery.
        /// Action plugins could stay lazily activated, but plugins that
        /// contribute ambient state (e.g. renderer registrations via
        /// OnEnableAsync) must run their lifecycle at startup or their
        /// contributions silently disappear after every restart.
        /// </summary>
        private async Task ActivateEnabledExternalPluginsAsync()
        {
            foreach (var descriptor in _pluginRegistry.GetAllPluginDescriptors().ToList())
            {
                if (!descriptor.IsExternal)
                {
                    continue;
                }

                try
                {
                    if (!_pluginRegistry.IsPluginEnabled(descriptor.Id))
                    {
                        continue;
                    }

                    await _pluginRegistry.GetOrActivatePluginAsync(descriptor.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Startup] Failed to activate external plugin {PluginId} at startup", descriptor.Id);
                }
            }
        }

        private async Task ApplyLoggingConfigurationAsync()
        {
            // [UI Debug Mode] Keep the forced Verbose level from App.OnStartup so E2E
            // log excerpts are always full-trace.
            if (_debugOptions.IsUiDebug)
            {
                return;
            }

            try
            {
                var config = await _configService.LoadSnapshotAsync();
                if (config?.Settings?.Logging == null)
                {
                    return;
                }

                if (Enum.TryParse<LogEventLevel>(config.Settings.Logging.MinimumLevel, true, out var logLevel))
                {
                    _levelSwitch.MinimumLevel = logLevel;
                    Log.Information("Log level updated from config: {Level}", logLevel);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply logging configuration from Profiles.json, using defaults");
            }
        }

        private async Task ConfigureThemeAsync()
        {
            try
            {
                var config = await _configService.LoadSnapshotAsync();
                var theme = config?.Settings?.ThemeEnum ?? AppTheme.Light;
                _themeService.Initialize(theme);
                _logger.LogInformation("[Startup] Theme initialized to {Theme} from configuration", theme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Startup] Failed to initialize theme from config; using ThemeService default");
            }
        }

        private async Task ConfigureLocalizationAsync()
        {
            try
            {
                var config = await _configService.LoadSnapshotAsync();
                var language = config?.Settings?.Language;
                if (!string.IsNullOrEmpty(language))
                {
                    _localizationService.SetLanguage(language);
                    Log.Information("Localization initialized with language: {Language}", language);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize localization from config, using default English");
            }
        }

        private async Task RunOnboardingStartupAsync(CancellationToken cancellationToken)
        {
            var action = await _tutorialStartupCoordinator.HandleStartupAsync();

            if (action != StartupAction.ShowWizard)
            {
                return;
            }

            _logger.LogInformation("[Startup] Launching first-run setup wizard");
            var wizard = _wizardFactory();

            try
            {
                await await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => _dialogService.ShowCustomAsync(_localizationService["FirstLaunch.SetupTitle"], wizard, DialogButtons.None, DialogSizeConstraints.LargeResizable, AppTheme.Light),
                    System.Windows.Threading.DispatcherPriority.Normal,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Startup] Failed to display first-run setup wizard");
                _trayService.ShowNotification(
                    _localizationService["Notification.OnboardingWizardFailedTitle"],
                    _localizationService["Notification.OnboardingWizardFailed"],
                    PulsarNotificationIcon.Warning);
            }
        }
    }
}