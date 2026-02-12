using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Plugin;
using Pulsar.Features.Pki;
using Pulsar.Features.Pki.Services;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Views;
using System;
using System.Windows;
using System.IO;
using Serilog;

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
            // 0. Initialize Logging (Pulsar Sentinel)
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Pulsar", 
                "Logs", 
                "pulsar-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            Log.Information("=== Pulsar Application Starting ===");

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
            
            // 2. Plugin System (New Architecture)
            serviceCollection.AddSingleton<PluginRegistry>();

            // 3. PKI Service
            serviceCollection.AddSingleton<CredentialsManager>();

            // 4. UI Services
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            // [Fix] Register SettingsViewModel with new dependency
            serviceCollection.AddSingleton<SettingsViewModel>();
            serviceCollection.AddSingleton<SettingsWindow>();
            
            // Build Container
            Services = serviceCollection.BuildServiceProvider();

            // ================================================
            // 5. Initialize Plugin System
            // ================================================
            var pluginRegistry = Services.GetRequiredService<PluginRegistry>();
            pluginRegistry.LoadAll();

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