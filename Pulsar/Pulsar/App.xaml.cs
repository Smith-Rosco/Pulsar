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
            // Global Exception Handling
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            // 1. Core Services
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            
            // 2. Plugin System (New Architecture)
            serviceCollection.AddSingleton<PluginRegistry>();

            // 3. PKI Service
            serviceCollection.AddSingleton<CredentialsManager>();

            // 4. UI Services
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsWindow>();

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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Services != null)
            {
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();
            }
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[CRITICAL] Unhandled Dispatcher Exception: {e.Exception.Message}");
            Debug.WriteLine(e.Exception.StackTrace);
            // Optionally: Prevent crash if recoverable
            // e.Handled = true; 
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine($"[CRITICAL] Unobserved Task Exception: {e.Exception.Message}");
            // Prevent process termination
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             if (e.ExceptionObject is Exception ex)
             {
                 Debug.WriteLine($"[CRITICAL] Unhandled Domain Exception: {ex.Message}");
                 Debug.WriteLine(ex.StackTrace);
             }
        }
    }
}