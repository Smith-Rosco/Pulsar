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
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings; // Added
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
            // 0. Initialize Logging (Pulsar Sentinel - Unified Architecture)
            // Note: We use default settings here, will update from config later
            var logsBaseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Pulsar", 
                "Logs");
            
            var pluginLogsDir = Path.Combine(logsBaseDir, "Plugins");
            Directory.CreateDirectory(pluginLogsDir);

            // Create a level switch for runtime log level control
            var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

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

            Log.Information("=== Pulsar Application Starting (Log Level: {Level}) ===", levelSwitch.MinimumLevel);
            
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
            serviceCollection.AddSingleton<ILocalizationService, LocalizationService>();
            serviceCollection.AddSingleton<ILoggingConfigService, LoggingConfigService>();
            serviceCollection.AddSingleton<IBackgroundWorkScheduler, BackgroundWorkScheduler>();

            // 1. Core Services
            serviceCollection.AddSingleton<IPluginMetadataRegistry, PluginMetadataRegistry>();
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IProcessRegistryService, ProcessRegistryService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<IActionFeedbackService, ActionFeedbackService>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            serviceCollection.AddSingleton<IAnimationController, AnimationController>();
            serviceCollection.AddSingleton<ISlotLayoutEngine, SlotLayoutEngine>();
            serviceCollection.AddSingleton<IMouseTrackingService, MouseTrackingService>();
            serviceCollection.AddSingleton<IPagingController, PagingController>();
            serviceCollection.AddSingleton<IPreviewService, PreviewService>();
            serviceCollection.AddSingleton<ILocalUiPreferencesService, LocalUiPreferencesService>();
            serviceCollection.AddSingleton<ISettingsNavigationGuard, SettingsNavigationGuard>();
            serviceCollection.AddSingleton<SettingsPageCatalog>();
            serviceCollection.AddSingleton<IAppStartupCoordinator, AppStartupCoordinator>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<GlobalMouseHook>();
            serviceCollection.AddSingleton<IHotkeyService, HotkeyService>();
            serviceCollection.AddSingleton<IGlobalMouseService, GlobalMouseService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.IOnboardingTemplateService, Pulsar.Services.Tutorial.OnboardingTemplateService>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.IOnboardingStateService, Pulsar.Services.Tutorial.OnboardingStateService>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.TutorialScenarioRegistry>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.Prerequisites.ExcelPrerequisiteProvider>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.Prerequisites.BrowserPrerequisiteProvider>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.StartupCoordinator>();
            
            // Tutorial Service
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.TutorialStepLoader>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.TriggerHandlers.ITriggerHandlerFactory, Pulsar.Services.Tutorial.TriggerHandlers.TriggerHandlerFactory>();
            serviceCollection.AddSingleton<ITargetLocator, Pulsar.Services.Tutorial.TargetLocator>();
            serviceCollection.AddSingleton<IOverlayManager, Pulsar.Services.Tutorial.OverlayManager>();
            serviceCollection.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.ISettingsWindowAccessor, Pulsar.Services.Tutorial.SettingsWindowAccessor>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.ITutorialTriggerEngine, Pulsar.Services.Tutorial.TutorialTriggerEngine>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.ITutorialSpotlightController, Pulsar.Services.Tutorial.TutorialSpotlightController>();
            serviceCollection.AddSingleton<Pulsar.Services.Tutorial.IWaitStepHintTimeout, Pulsar.Services.Tutorial.WaitStepHintTimeout>();
            serviceCollection.AddSingleton<ITutorialService, TutorialService>();
            serviceCollection.AddSingleton<ILogger<Pulsar.Services.Tutorial.TutorialOrchestrator>>(sp =>
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<Pulsar.Services.Tutorial.TutorialOrchestrator>());
            
            // [New] Fuzzy Search Service (for IconPicker and future use)
            serviceCollection.AddSingleton(typeof(Pulsar.Services.Interfaces.IFuzzySearchService<>), typeof(Pulsar.Services.FuzzySearch.FuzzySearchService<>));
            serviceCollection.AddSingleton<Pulsar.Services.Interfaces.IFuzzySearchService<Pulsar.Helpers.IconItem>>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Pulsar.Services.FuzzySearch.FuzzySearchService<Pulsar.Helpers.IconItem>>>();
                var innerService = new Pulsar.Services.FuzzySearch.FuzzySearchService<Pulsar.Helpers.IconItem>(logger);
                
                var cachedLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Pulsar.Services.FuzzySearch.CachedFuzzySearchService<Pulsar.Helpers.IconItem>>>();
                return new Pulsar.Services.FuzzySearch.CachedFuzzySearchService<Pulsar.Helpers.IconItem>(innerService, cachedLogger);
            });
            
            // 2. Plugin System (New Architecture)
            serviceCollection.AddPluginRuntime(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"));
            
            // [New] Plugin Monitoring & Analytics Services
            serviceCollection.AddSingleton<IPluginUsageTracker, PluginUsageTracker>();
            serviceCollection.AddSingleton<IPluginHealthMonitor, PluginHealthMonitor>();
            serviceCollection.AddSingleton<IPluginLogService, PluginLogService>();
            serviceCollection.AddSingleton<IPluginRecommendationEngine, PluginRecommendationEngine>();
            
            // [New] Configuration Validation
            serviceCollection.AddSingleton<Services.Validation.ConfigValidationPipeline>();

            // 3. Focus Management
            serviceCollection.AddSingleton<IFocusNativeAdapter, WindowsFocusNativeAdapter>();
            serviceCollection.AddSingleton<IModifierStateTracker>(sp => sp.GetRequiredService<GlobalKeyboardHook>());
            serviceCollection.AddSingleton<IFocusManager, Services.FocusManager>();
            serviceCollection.AddSingleton<IFocusHistory>(sp => (IFocusHistory)sp.GetRequiredService<IFocusManager>());

            // 4. PKI Service
            serviceCollection.AddSingleton<ISecretProtector, CredentialsManager>();
            serviceCollection.AddSingleton<IPkiSecretStore, SecretRepository>();
            serviceCollection.AddSingleton<IPkiSecretMetadataResolver, PkiSecretMetadataResolver>();
            serviceCollection.AddSingleton<IInjectionExecutor, SendKeysInjectionExecutor>();
            serviceCollection.AddSingleton<IPkiExecutionService, PkiExecutionService>();

            // PKI Input Simulators
            serviceCollection.AddSingleton<Pulsar.Plugins.Core.Pki.Services.Input.ISendKeysWriter, Pulsar.Plugins.Core.Pki.Services.Input.WindowsSendKeysWriter>();

            // Command Plugin Abstractions
            serviceCollection.AddTransient<Core.Plugin.IKeySender, Plugins.Extensions.Command.KeySender>();
            serviceCollection.AddTransient<Core.Plugin.IProcessLauncher, Plugins.Extensions.Command.ProcessLauncher>();

            // 4. UI Services
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            
            // [Fix] Register SettingsViewModel as Transient for fresh state on every open
            serviceCollection.AddTransient<AboutViewModel>();
            serviceCollection.AddTransient<SettingsShellViewModel>();
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsPageFactory>();
            
            // [New] Plugin Management UI
            serviceCollection.AddTransient<PluginManagerViewModel>();
            serviceCollection.AddTransient<SettingsPluginsPage>();

            // [External Plugins] External Plugin Management Services
            serviceCollection.AddSingleton<LocalPluginScanner>(sp =>
            {
                var pluginDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Plugins");
                var logger = sp.GetService<ILogger<LocalPluginScanner>>();
                return new LocalPluginScanner(pluginDirectory, logger);
            });
            
            serviceCollection.AddSingleton<PluginPackageManager>(sp =>
            {
                var pluginInstallDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Plugins");
                var logger = sp.GetService<ILogger<PluginPackageManager>>();
                
                return new PluginPackageManager(pluginInstallDirectory, logger);
            });
            
            serviceCollection.AddTransient<ExternalPluginManagerViewModel>();
            serviceCollection.AddTransient<SettingsExternalPluginsPage>();
            
            // [Deprecated] Keep old marketplace services for backward compatibility
#pragma warning disable CS0618 // Type or member is obsolete
            serviceCollection.AddSingleton<PluginRepository>(sp =>
            {
                var repositoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "PluginRepository");
                var logger = sp.GetService<ILogger<PluginRepository>>();
                return new PluginRepository(repositoryPath, logger);
            });
#pragma warning restore CS0618
            serviceCollection.AddTransient<PluginMarketViewModel>();
            serviceCollection.AddTransient<SettingsMarketplacePage>();
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

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== Pulsar Application Exiting ===");
            
            if (Services != null)
            {
                // Flush pending ProcessRegistry changes
                var processRegistry = Services.GetService<IProcessRegistryService>();
                if (processRegistry != null)
                {
                    Task.Run(async () => await processRegistry.FlushAsync()).GetAwaiter().GetResult();
                }
                
                // Unload all plugins
                var pluginRegistry = Services.GetService<IPluginRegistry>();
                if (pluginRegistry != null)
                {
                    Task.Run(async () => await pluginRegistry.UnloadAllAsync()).GetAwaiter().GetResult();
                }

                var backgroundWorkScheduler = Services.GetService<IBackgroundWorkScheduler>();
                backgroundWorkScheduler?.CancelAll();
                
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();

                var mouseWheelHook = Services.GetService<GlobalMouseHook>();
                mouseWheelHook?.Dispose();

                var keyboardHook = Services.GetService<GlobalKeyboardHook>();
                keyboardHook?.Dispose();

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
