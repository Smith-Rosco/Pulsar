using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.ActionFeedback;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.ViewModels;

namespace Pulsar.ViewModels.Strategies
{
    public class ProcessPageProvider : BasePageProvider
    {
        private readonly IWindowService _windowService;
        private readonly ILocalizationService? _loc;
        private readonly ProfilesConfig _config;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly ITrayService _trayService;
        private readonly ProcessWindowMatcher _matcher;
        private readonly PulsarContext _context;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IActionFeedbackService _feedbackService;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;

        private List<MatchedWindowGroup> _matchedSlots = new();
        private readonly List<ProcessWindowInfo>? _seededWindows;

        public override int TotalPages => (int)Math.Ceiling((double)_matchedSlots.Count / (double)ItemsPerPage);

        public ProcessPageProvider(
            IWindowService windowService,
            ProfilesConfig config,
            System.IServiceProvider serviceProvider,
            PulsarContext context,
            List<ProcessWindowInfo>? seededWindows = null)
            : base(serviceProvider.GetService(typeof(IConfigService)) as IConfigService)
        {
            _windowService = windowService;
            _config = config;
            _serviceProvider = serviceProvider;
            _context = context;
            _seededWindows = seededWindows;
            _loc = serviceProvider.GetService(typeof(ILocalizationService)) as ILocalizationService;
            _matcher = new ProcessWindowMatcher(config);
            
            // Resolve analytics + plugin-pipe services
            _usageTracker = serviceProvider.GetService(typeof(IPluginUsageTracker)) as IPluginUsageTracker;
            _healthMonitor = serviceProvider.GetService(typeof(IPluginHealthMonitor)) as IPluginHealthMonitor;
            _logService = serviceProvider.GetService(typeof(IPluginLogService)) as IPluginLogService;
            _trayService = (ITrayService)serviceProvider.GetService(typeof(ITrayService))!;
            _pluginRegistry = (IPluginRegistry)serviceProvider.GetService(typeof(IPluginRegistry))!;
            _feedbackService = (IActionFeedbackService)serviceProvider.GetService(typeof(IActionFeedbackService))!;
            _feedbackPresenter = serviceProvider.GetService(typeof(IActionFeedbackPresenter)) as IActionFeedbackPresenter;
        }

        public override async Task LoadAsync()
        {
            if (_seededWindows != null)
            {
                // Warm-cache fast path: the caller already holds a fresh inventory
                // snapshot (served from WindowInventoryCache), so skip the desktop
                // enumeration entirely and build the matched slot list from it.
                _matchedSlots = _matcher.BuildSlotList(_seededWindows);
                _currentPage = 0;
                return;
            }

            var windows = await _windowService.GetActiveWindowsAsync();
            _matchedSlots = _matcher.BuildSlotList(windows);
            _currentPage = 0;
        }

        public override void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot)
        {
            ClearSlots(slots);

            string centerText = _currentPage == 0
                ? (_loc?["RadialMenu.Switch"] ?? "Switch")
                : string.Format(_loc?["RadialMenu.PageFormat"] ?? "Page {0}", _currentPage + 1);
            centerSlot.Label = centerText;
            centerSlot.LoadIconData(string.Empty);
            centerSlot.ActionStrategy = NoOpStrategy.Instance;
            centerSlot.Type = SlotType.Action;
            centerSlot.BadgeCount = 0;

            // Calculate which slots to display on current page (use dynamic ItemsPerPage)
            int startIndex = _currentPage * ItemsPerPage;
            int endIndex = Math.Min(startIndex + ItemsPerPage, _matchedSlots.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var slotItem = _matchedSlots[i];
                var slotViewModel = slots[i - startIndex]; // Map to visual slot position (0-N)
                slotViewModel.ResetAnimation();

                // Skip completely empty slots
                if (!slotItem.IsConfigured && !slotItem.IsRunning)
                {
                    continue;
                }

                if (slotItem.IsRunning && slotItem.Windows != null && slotItem.Windows.Count > 0)
                {
                    // Running process (configured or unconfigured)
                    var first = slotItem.Windows.First();

                    // Use config icon/color if available
                    if (slotItem.Config != null && !string.IsNullOrEmpty(slotItem.Config.IconKey))
                    {
                        slotViewModel.LoadIconData(slotItem.Config.IconKey);
                    }
                    else
                    {
                        slotViewModel.IconImage = first.AppIcon;
                    }

                    if (slotItem.Config != null)
                    {
                        var presentation = SlotPresentationBuilder.Build(slotItem.Config);
                        slotItem.Config.SetPresentation(presentation);
                        slotViewModel.ApplyPresentation(presentation);
                    }

                    string baseLabel = !string.IsNullOrEmpty(slotItem.Config?.Label) 
                        ? slotItem.Config.Label 
                        : ProcessNameFormatter.ToDisplayName(first.ProcessName);
                    
                    if (slotItem.Windows.Count > 1)
                    {
                        slotViewModel.Label = $"{baseLabel} ({slotItem.Windows.Count})";
                        slotViewModel.BadgeCount = slotItem.Windows.Count;
                    }
                    else
                    {
                        slotViewModel.Label = baseLabel;
                    }

                    slotViewModel.Type = SlotType.Process;
                    slotViewModel.DataContext = slotItem.Windows;
                    slotViewModel.ActionStrategy = new ProcessGroupStrategy(slotItem.Windows, _windowService, _usageTracker, _healthMonitor, _logService,
                        feedbackService: _feedbackService,
                        feedbackPresenter: _feedbackPresenter);
                    slotViewModel.CurrentOpacity = 1.0;
                }
                else if (slotItem.IsConfigured && !slotItem.IsRunning && slotItem.Config != null)
                {
                    // Configured but NOT running - placeholder
                    if (!string.IsNullOrEmpty(slotItem.Config.IconKey))
                    {
                        slotViewModel.LoadIconData(slotItem.Config.IconKey);
                    }

                    var presentation = SlotPresentationBuilder.Build(slotItem.Config);
                    slotItem.Config.SetPresentation(presentation);
                    slotViewModel.ApplyPresentation(presentation);

                    string baseLabel = !string.IsNullOrEmpty(slotItem.Config.Label) 
                        ? slotItem.Config.Label 
                        : (_loc?["RadialMenu.App"] ?? "App");
                    slotViewModel.Label = string.Format(_loc?["RadialMenu.NotRunningFormat"] ?? "{0} (Not Running)", baseLabel);

                    slotViewModel.Type = SlotType.Process;
                    slotViewModel.DataContext = slotItem.Config;
                    slotViewModel.ActionStrategy = new PluginActionStrategy(
                        slotItem.Config, _pluginRegistry, _context, _trayService, _feedbackService,
                        _usageTracker, _feedbackPresenter);
                    slotViewModel.CurrentOpacity = 0.5;
                }
            }
        }
    }
}
