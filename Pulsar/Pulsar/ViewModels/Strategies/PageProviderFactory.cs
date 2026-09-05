using System;
using System.Collections.Generic;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Pulsar.Views;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Production implementation of <see cref="IPageProviderFactory"/>. Holds every
    /// fixed singleton dependency the page providers need, so <see cref="MenuSession"/>
    /// only passes per-session data (slots, context, config, seeded windows). This
    /// replaces the previous IServiceProvider service-locator pattern where each
    /// provider resolved its own deps from the container (20 GetService calls across
    /// the menu execution path).
    /// </summary>
    public class PageProviderFactory : IPageProviderFactory
    {
        private readonly IConfigService? _configService;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IPluginExecutor _executor;
        private readonly IActionFeedbackService _feedbackService;
        private readonly ILocalizationService? _loc;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;
        private readonly ITrayService _trayService;
        private readonly IWindowService _windowService;
        private readonly IWindowInventoryCoordinator _inventoryCoordinator;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly Func<SettingsWindow>? _settingsWindowFactory;

        public PageProviderFactory(
            IConfigService? configService,
            IPluginRegistry pluginRegistry,
            IPluginExecutor executor,
            IActionFeedbackService feedbackService,
            ILocalizationService? loc,
            IPluginUsageTracker? usageTracker,
            IActionFeedbackPresenter? feedbackPresenter,
            ITrayService trayService,
            IWindowService windowService,
            IWindowInventoryCoordinator inventoryCoordinator,
            IPluginHealthMonitor? healthMonitor,
            IPluginLogService? logService,
            Func<SettingsWindow>? settingsWindowFactory = null)
        {
            _configService = configService;
            _pluginRegistry = pluginRegistry;
            _executor = executor;
            _feedbackService = feedbackService;
            _loc = loc;
            _usageTracker = usageTracker;
            _feedbackPresenter = feedbackPresenter;
            _trayService = trayService;
            _windowService = windowService;
            _inventoryCoordinator = inventoryCoordinator;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _settingsWindowFactory = settingsWindowFactory;
        }

        public IPageProvider CreateCommandPage(List<PluginSlot> slots, PulsarContext context)
        {
            return new CommandPageProvider(
                slots,
                _pluginRegistry,
                context,
                _trayService,
                _configService,
                _executor,
                _feedbackService,
                _loc,
                _usageTracker,
                _feedbackPresenter,
                _settingsWindowFactory);
        }

        public IPageProvider CreateProcessPage(ProfilesConfig config, PulsarContext context, List<ProcessWindowInfo>? seededWindows = null)
        {
            return new ProcessPageProvider(
                _windowService,
                _inventoryCoordinator,
                config,
                context,
                _configService,
                _loc,
                _usageTracker,
                _healthMonitor,
                _logService,
                _trayService,
                _executor,
                _feedbackService,
                _feedbackPresenter,
                seededWindows);
        }
    }
}
