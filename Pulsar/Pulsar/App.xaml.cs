using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Services;
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

using System.Windows.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Pulsar
{
    public partial class App : System.Windows.Application
    {
        public new static App Current => (App)System.Windows.Application.Current;

        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 0. Initialize Logging (Pulsar Sentinel - Unified Architecture)
            var logsBaseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Pulsar", 
                "Logs");
            
            var pluginLogsDir = Path.Combine(logsBaseDir, "Plugins");
            Directory.CreateDirectory(pluginLogsDir);

            // Create a level switch for runtime log level control
            var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.With<Pulsar.Logging.PluginContextEnricher>()
                .WriteTo.Debug()
                // 主程序日志（不包含插件日志）
                .WriteTo.Logger(lc => lc
                    .Filter.ByExcluding(evt => evt.Properties.ContainsKey("PluginId"))
                    .WriteTo.File(
                        path: Path.Combine(logsBaseDir, "pulsar-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    ))
                // 插件日志（按插件 ID 分文件）
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(evt => evt.Properties.ContainsKey("PluginId"))
                    .WriteTo.Map(
                        keyPropertyName: "PluginId",
                        configure: (pluginId, wt) => wt.File(
                            path: Path.Combine(pluginLogsDir, $"{pluginId}-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            fileSizeLimitBytes: 100_000_000, // 100MB per file
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Action}] [ExecId:{ExecutionId}] [Elapsed:{ElapsedMs}ms] {Message:lj}{NewLine}{Exception}"
                        )
                    ))
                .CreateLogger();

            Log.Information("=== Pulsar Application Starting (Log Level: {Level}) ===", levelSwitch.MinimumLevel);
            
            // [New] Check System Integrity (焦点锁定设置)
            WindowHelper.CheckSystemIntegrity();
            Log.Information("[SystemIntegrity] Focus lock timeout checked and restored if needed");

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

            // 1. Core Services
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IProcessRegistryService, ProcessRegistryService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<IHotkeyService, HotkeyService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            
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
            serviceCollection.AddSingleton<PluginRegistry>();
            serviceCollection.AddSingleton<IPluginMetadataRegistry, PluginMetadataRegistry>();
            
            // [New] Plugin Monitoring & Analytics Services
            serviceCollection.AddSingleton<IPluginUsageTracker, PluginUsageTracker>();
            serviceCollection.AddSingleton<IPluginHealthMonitor, PluginHealthMonitor>();
            serviceCollection.AddSingleton<IPluginLogService, PluginLogService>();
            serviceCollection.AddSingleton<IPluginRecommendationEngine, PluginRecommendationEngine>();
            
            // [New] Configuration Validation
            serviceCollection.AddSingleton<Services.Validation.ConfigValidationPipeline>();

            // 3. PKI Service
            serviceCollection.AddSingleton<CredentialsManager>();
            serviceCollection.AddSingleton<SecretRepository>();

            // 4. UI Services
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            
            // [Fix] Register SettingsViewModel as Transient for fresh state on every open
            serviceCollection.AddTransient<SettingsViewModel>();
            
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

            serviceCollection.AddTransient<SettingsWindow>();
            
            // Build Container
            Services = serviceCollection.BuildServiceProvider();

            // Initialize static helpers that need logging
            IconHelper.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("IconHelper");
            UiaHelper.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("UiaHelper");
            Pulsar.Plugins.Extensions.BookmarkletRunner.BrowserHelper.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("BrowserHelper");

            // VBA runner internals
            Pulsar.Plugins.Extensions.VbaRunner.ScriptEngine.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("VbaRunner.ScriptEngine");
            Pulsar.Plugins.Extensions.VbaRunner.ComRetryHelper.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("VbaRunner.ComRetryHelper");
            Pulsar.Plugins.Extensions.VbaRunner.ComConnectionManager.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("VbaRunner.ComConnectionManager");
            Pulsar.Plugins.Extensions.VbaRunner.VbaModuleInjector.Logger = Services.GetService<ILoggerFactory>()?.CreateLogger("VbaRunner.VbaModuleInjector");

            // ================================================
            // 5. Initialize Plugin System
            // ================================================
            var pluginRegistry = Services.GetRequiredService<PluginRegistry>();
            
            // Load plugins asynchronously (blocks startup, but ensures plugins are ready)
            Task.Run(async () => await pluginRegistry.LoadAllAsync()).GetAwaiter().GetResult();

            // [New] Setup validation pipeline for ConfigService
            var configService = Services.GetRequiredService<IConfigService>();
            var validationPipeline = Services.GetRequiredService<Services.Validation.ConfigValidationPipeline>();
            if (configService is ConfigService concreteConfigService)
            {
                concreteConfigService.SetValidationPipeline(validationPipeline);
                Log.Information("[App] Validation pipeline configured for ConfigService");
            }

            // [New] Initialize ProcessRegistryService (migrate from legacy config)
            var processRegistryService = Services.GetRequiredService<IProcessRegistryService>();
            Task.Run(async () => await processRegistryService.InitializeAsync()).GetAwaiter().GetResult();
            Log.Information("[App] ProcessRegistryService initialized");

            // 6. Start Services
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 7. Warm up Main Window
            var mainWindow = Services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();

            // 8. Initialize Hotkey Service
            var hotkeyService = Services.GetRequiredService<IHotkeyService>();
            hotkeyService.Initialize();

            // [RDP Fix] Apply input configuration to GlobalKeyboardHook
            var keyboardHook = Services.GetRequiredService<GlobalKeyboardHook>();
            Task.Run(async () =>
            {
                var config = await configService.LoadAsync();
                if (config?.Settings?.Input != null)
                {
                    keyboardHook.UseHybridMode = config.Settings.Input.IsHybridMode;
                    Log.Information("[App] GlobalKeyboardHook configured: ModifierStateMode={Mode}", 
                        config.Settings.Input.ModifierStateMode);
                }
                else
                {
                    // Default to Hybrid mode if no config
                    keyboardHook.UseHybridMode = true;
                    Log.Information("[App] GlobalKeyboardHook using default Hybrid mode");
                }
            }).GetAwaiter().GetResult();

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
                var pluginRegistry = Services.GetService<PluginRegistry>();
                if (pluginRegistry != null)
                {
                    Task.Run(async () => await pluginRegistry.UnloadAllAsync()).GetAwaiter().GetResult();
                }
                
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();

            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "[CRITICAL] Unhandled Dispatcher Exception");
            
            // [New] Emergency restore system settings
            WindowHelper.EmergencyRestore();
            
            // Optionally: Prevent crash if recoverable
            // e.Handled = true; 
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "[CRITICAL] Unobserved Task Exception");
            // Prevent process termination
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             if (e.ExceptionObject is Exception ex)
             {
                 Log.Fatal(ex, "[CRITICAL] Unhandled AppDomain Exception (IsTerminating={IsTerminating})", e.IsTerminating);
                 
                 // [New] Emergency restore system settings before crash
                 WindowHelper.EmergencyRestore();
                 
                 Log.CloseAndFlush();
             }
        }
    }
}
