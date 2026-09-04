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
using Pulsar.Native;
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
        private readonly IServiceProvider _services;
        private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly ILogger<AppStartupCoordinator> _logger;

        public AppStartupCoordinator(
            IServiceProvider services,
            IBackgroundWorkScheduler backgroundWorkScheduler,
            LoggingLevelSwitch levelSwitch,
            ILogger<AppStartupCoordinator> logger)
        {
            _services = services;
            _backgroundWorkScheduler = backgroundWorkScheduler;
            _levelSwitch = levelSwitch;
            _logger = logger;
        }

        public async Task RunBlockingInitializationAsync()
        {
            var startupStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("[Startup] Running blocking startup responsibilities");

            var configService = _services.GetRequiredService<IConfigService>();
            await ApplyLoggingConfigurationAsync(configService);

            // [UI Debug Mode] Start the named-pipe state publisher before any UI can
            // summon so the E2E driver cannot miss early state events.
            var debugOptions = _services.GetService<DebugModeOptions>() ?? DebugModeOptions.Disabled;
            if (debugOptions.IsUiDebug)
            {
                var statePublisher = _services.GetRequiredService<IDebugStatePublisher>();
                statePublisher.Start(debugOptions.PipeName);
                _logger.LogInformation("[Startup] Debug state publisher started on pipe {PipeName}", debugOptions.PipeName);
            }

            await ConfigureLocalizationAsync(configService);

            // Theme must be established before the tray icon builds its ContextMenu.
            // Otherwise ThemeService falls back to its in-memory default and the first
            // tray menu is rendered with the wrong theme until Settings opens.
            await ConfigureThemeAsync(configService);

            var processRegistryService = _services.GetRequiredService<IProcessRegistryService>();
            await processRegistryService.InitializeAsync();
            _logger.LogInformation("[Startup] ProcessRegistryService initialized");

            var trayService = _services.GetRequiredService<ITrayService>();
            trayService.Initialize();
            _logger.LogInformation("[Startup] Tray service initialized");

            // Circuit breaker transitions must reach telemetry + tray notifications.
            // The relay subscribes to PluginCircuitBreakerPolicy events in its
            // constructor, so resolving it after tray init activates the wiring
            // before any plugin execution can trip a breaker (ADR-013).
            _services.GetRequiredService<PluginBreakerNotificationService>();
            _logger.LogInformation("[Startup] Circuit breaker notification relay activated");

            var pluginRegistry = _services.GetRequiredService<IPluginRegistry>();
            await pluginRegistry.LoadCoreAsync();
            _logger.LogInformation("[Startup] Core plugins activated");

            var mainWindow = _services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();
            _logger.LogInformation("[Startup] Radial menu window shown");

            // [UI Debug Mode] Start the command server only after the menu window and
            // its view-model exist, so early 'menu-open' commands cannot race window
            // construction.
            if (debugOptions.IsUiDebug)
            {
                var commandServer = _services.GetRequiredService<IDebugCommandServer>();
                commandServer.Start(debugOptions.CommandPipeName);
                _logger.LogInformation("[Startup] Debug command server started on pipe {PipeName}", debugOptions.CommandPipeName);
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
            bool registerHotkeys = !debugOptions.IsUiDebug || debugOptions.EnableHotkeyHooks;
            if (registerHotkeys)
            {
                var hotkeyService = _services.GetRequiredService<IHotkeyService>();
                await hotkeyService.InitializeAsync();
                _logger.LogInformation("[Startup] Hotkey service initialized{Suffix}",
                    debugOptions.IsUiDebug ? " (ui-debug-hooks opt-in)" : string.Empty);
            }

            if (!debugOptions.IsUiDebug)
            {
                var globalMouseWheelService = _services.GetRequiredService<IGlobalMouseService>();
                globalMouseWheelService.Initialize();
                _logger.LogInformation("[Startup] Global mouse wheel service initialized");
            }

            if (registerHotkeys)
            {
                await ConfigureKeyboardHookAsync(configService);
            }
            else
            {
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
                    var pluginRegistry = _services.GetRequiredService<IPluginRegistry>();
                    await pluginRegistry.DiscoverDeferredAsync();
                    await ActivateEnabledExternalPluginsAsync(pluginRegistry);

                    var configService = _services.GetRequiredService<IConfigService>();
                    ConfigureValidationPipeline(configService);

                    await RunOnboardingStartupAsync(cancellationToken);

                    var config = await configService.LoadSnapshotAsync();
                    if (config.Settings.HasCompletedTutorial
                        || string.Equals(config.Settings.LastTutorialStep, "Skipped", StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(config.Settings.OnboardingState, "SetupWizardComplete", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    Log.Information("First launch detected, starting tutorial");
                    await Task.Delay(1500, cancellationToken);

                    await await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        async () =>
                        {
                            var tutorialService = _services.GetRequiredService<ITutorialService>();
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

        private void ConfigureValidationPipeline(IConfigService configService)
        {
            var validationPipeline = _services.GetRequiredService<Validation.ConfigValidationPipeline>();
            if (configService is ConfigService concreteConfigService)
            {
                concreteConfigService.SetValidationPipeline(validationPipeline);
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
        private async Task ActivateEnabledExternalPluginsAsync(IPluginRegistry pluginRegistry)
        {
            foreach (var descriptor in pluginRegistry.GetAllPluginDescriptors().ToList())
            {
                if (!descriptor.IsExternal)
                {
                    continue;
                }

                try
                {
                    if (!pluginRegistry.IsPluginEnabled(descriptor.Id))
                    {
                        continue;
                    }

                    await pluginRegistry.GetOrActivatePluginAsync(descriptor.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Startup] Failed to activate external plugin {PluginId} at startup", descriptor.Id);
                }
            }
        }

        private async Task ApplyLoggingConfigurationAsync(IConfigService configService)
        {
            // [UI Debug Mode] Keep the forced Verbose level from App.OnStartup so E2E
            // log excerpts are always full-trace.
            var debugOptions = _services.GetService<DebugModeOptions>() ?? DebugModeOptions.Disabled;
            if (debugOptions.IsUiDebug)
            {
                return;
            }

            try
            {
                var config = await configService.LoadSnapshotAsync();
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

        private async Task ConfigureThemeAsync(IConfigService configService)
        {
            try
            {
                var config = await configService.LoadSnapshotAsync();
                var theme = config?.Settings?.ThemeEnum ?? AppTheme.Light;
                var themeService = _services.GetRequiredService<IThemeService>();
                themeService.Initialize(theme);
                _logger.LogInformation("[Startup] Theme initialized to {Theme} from configuration", theme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Startup] Failed to initialize theme from config; using ThemeService default");
            }
        }

        private async Task ConfigureLocalizationAsync(IConfigService configService)
        {
            try
            {
                var localizationService = _services.GetRequiredService<ILocalizationService>();
                var config = await configService.LoadSnapshotAsync();
                var language = config?.Settings?.Language;
                if (!string.IsNullOrEmpty(language))
                {
                    localizationService.SetLanguage(language);
                    Log.Information("Localization initialized with language: {Language}", language);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize localization from config, using default English");
            }
        }

        private async Task ConfigureKeyboardHookAsync(IConfigService configService)
        {
            var keyboardHook = _services.GetRequiredService<GlobalKeyboardHook>();
            var config = await configService.LoadSnapshotAsync();
            if (config?.Settings?.Input != null)
            {
                keyboardHook.UseHybridMode = config.Settings.Input.IsHybridMode;
                Log.Information("GlobalKeyboardHook configured: ModifierStateMode={Mode}", config.Settings.Input.ModifierStateMode);
                return;
            }

            keyboardHook.UseHybridMode = true;
            Log.Information("GlobalKeyboardHook using default Hybrid mode");
        }

        private async Task RunOnboardingStartupAsync(CancellationToken cancellationToken)
        {
            var startupCoordinator = _services.GetRequiredService<StartupCoordinator>();
            var action = await startupCoordinator.HandleStartupAsync();

            if (action != StartupAction.ShowWizard)
            {
                return;
            }

            _logger.LogInformation("[Startup] Launching first-run setup wizard");
            var dialogService = _services.GetRequiredService<IDialogService>();
            var wizard = _services.GetRequiredService<FirstLaunchSetupWizardViewModel>();
            var loc = _services.GetRequiredService<ILocalizationService>();

            try
            {
                await await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => dialogService.ShowCustomAsync(loc["FirstLaunch.SetupTitle"], wizard, DialogButtons.None, DialogSizeConstraints.LargeResizable, AppTheme.Light),
                    System.Windows.Threading.DispatcherPriority.Normal,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Startup] Failed to display first-run setup wizard");
                var trayService = _services.GetRequiredService<ITrayService>();
                trayService.ShowNotification(
                    loc["Notification.OnboardingWizardFailedTitle"],
                    loc["Notification.OnboardingWizardFailed"],
                    PulsarNotificationIcon.Warning);
            }
        }
    }
}
