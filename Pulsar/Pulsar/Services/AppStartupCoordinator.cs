using System;
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
using Pulsar.Services.Tutorial;
using Pulsar.ViewModels.Dialogs;
using Pulsar.Views;
using Wpf.Ui.Appearance;

namespace Pulsar.Services
{
    public class AppStartupCoordinator : IAppStartupCoordinator
    {
        private readonly IServiceProvider _services;
        private readonly LoggingLevelSwitch _levelSwitch;
        private readonly ILogger<AppStartupCoordinator> _logger;

        public AppStartupCoordinator(
            IServiceProvider services,
            LoggingLevelSwitch levelSwitch,
            ILogger<AppStartupCoordinator> logger)
        {
            _services = services;
            _levelSwitch = levelSwitch;
            _logger = logger;
        }

        public async Task RunBlockingInitializationAsync()
        {
            _logger.LogInformation("[Startup] Running blocking startup responsibilities");

            var pluginRegistry = _services.GetRequiredService<PluginRegistry>();
            await pluginRegistry.LoadAllAsync();

            var configService = _services.GetRequiredService<IConfigService>();
            ConfigureValidationPipeline(configService);
            await ApplyLoggingConfigurationAsync(configService);

            var processRegistryService = _services.GetRequiredService<IProcessRegistryService>();
            await processRegistryService.InitializeAsync();
            _logger.LogInformation("[Startup] ProcessRegistryService initialized");

            var trayService = _services.GetRequiredService<ITrayService>();
            trayService.Initialize();
            _logger.LogInformation("[Startup] Tray service initialized");

            var mainWindow = _services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();
            _logger.LogInformation("[Startup] Radial menu window shown");

            await RunOnboardingStartupAsync();

            var hotkeyService = _services.GetRequiredService<IHotkeyService>();
            await hotkeyService.InitializeAsync();
            _logger.LogInformation("[Startup] Hotkey service initialized");

            var globalMouseWheelService = _services.GetRequiredService<IGlobalMouseWheelService>();
            globalMouseWheelService.Initialize();
            _logger.LogInformation("[Startup] Global mouse wheel service initialized");

            await ConfigureKeyboardHookAsync(configService);
            _logger.LogInformation("[Startup] Blocking startup responsibilities complete");
        }

        public void StartDeferredInitialization()
        {
            _logger.LogInformation("[Startup] Starting deferred warm-up responsibilities");

            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var configService = _services.GetRequiredService<IConfigService>();
                    var config = await configService.LoadAsync();
                    if (config.Settings.HasCompletedTutorial
                        || string.Equals(config.Settings.LastTutorialStep, "Skipped", StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(config.Settings.OnboardingState, "SetupWizardComplete", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    Log.Information("First launch detected, starting tutorial");
                    await Task.Delay(1500);

                    var tutorialService = _services.GetRequiredService<ITutorialService>();
                    await tutorialService.CheckResumeAsync();

                    if (!tutorialService.IsTutorialActive)
                    {
                        await tutorialService.StartTutorialAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Startup] Deferred startup task failed");
                }
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

        private async Task ApplyLoggingConfigurationAsync(IConfigService configService)
        {
            try
            {
                var config = await configService.LoadAsync();
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

        private async Task ConfigureKeyboardHookAsync(IConfigService configService)
        {
            var keyboardHook = _services.GetRequiredService<GlobalKeyboardHook>();
            var config = await configService.LoadAsync();
            if (config?.Settings?.Input != null)
            {
                keyboardHook.UseHybridMode = config.Settings.Input.IsHybridMode;
                Log.Information("GlobalKeyboardHook configured: ModifierStateMode={Mode}", config.Settings.Input.ModifierStateMode);
                return;
            }

            keyboardHook.UseHybridMode = true;
            Log.Information("GlobalKeyboardHook using default Hybrid mode");
        }

        private async Task RunOnboardingStartupAsync()
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
            await dialogService.ShowCustomAsync("欢迎使用 Pulsar", wizard, DialogButtons.None, DialogSizeConstraints.LargeResizable, AppTheme.Light);
        }
    }
}
