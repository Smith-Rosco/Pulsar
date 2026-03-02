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

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
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

            Log.Information("=== Pulsar Application Starting (Unified Logging Architecture) ===");

            // Global Exception Handling
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            // 0. Logging Services
            serviceCollection.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            // 1. Core Services
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<IHotkeyService, HotkeyService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            
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

            // 6. Start Services
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 7. Warm up Main Window
            var mainWindow = Services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();

            // 8. Initialize Hotkey Service
            var hotkeyService = Services.GetRequiredService<IHotkeyService>();
            hotkeyService.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== Pulsar Application Exiting ===");
            
            if (Services != null)
            {
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
                 Log.Fatal(ex, "[CRITICAL] Unhandled Domain Exception");
                 Log.CloseAndFlush();
             }
        }
    }
}
