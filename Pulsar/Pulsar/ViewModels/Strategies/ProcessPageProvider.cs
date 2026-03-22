using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.ViewModels;

namespace Pulsar.ViewModels.Strategies
{
    public class ProcessPageProvider : BasePageProvider
    {
        private readonly IWindowService _windowService;
        private readonly ProfilesConfig _config;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly ProcessWindowMatcher _matcher;

        private List<MatchedWindowGroup> _matchedSlots = new();

        public override int TotalPages => (int)Math.Ceiling((double)_matchedSlots.Count / (double)ItemsPerPage);

        public ProcessPageProvider(IWindowService windowService, ProfilesConfig config, System.IServiceProvider serviceProvider)
            : base(serviceProvider.GetService(typeof(IConfigService)) as IConfigService)
        {
            _windowService = windowService;
            _config = config;
            _serviceProvider = serviceProvider;
            _matcher = new ProcessWindowMatcher(config);
            
            // Resolve analytics services
            _usageTracker = serviceProvider.GetService(typeof(IPluginUsageTracker)) as IPluginUsageTracker;
            _healthMonitor = serviceProvider.GetService(typeof(IPluginHealthMonitor)) as IPluginHealthMonitor;
            _logService = serviceProvider.GetService(typeof(IPluginLogService)) as IPluginLogService;
        }

        public override async Task LoadAsync()
        {
            var windows = await _windowService.GetActiveWindowsAsync();
            _matchedSlots = _matcher.BuildSlotList(windows);
            _currentPage = 0;
        }

        public override void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot)
        {
            ClearSlots(slots);

            string centerText = _currentPage == 0 ? "Switch" : $"Page {_currentPage + 1}";
            centerSlot.Label = centerText;
            centerSlot.LoadIconData(string.Empty);
            centerSlot.ActionStrategy = new NoOpStrategy();

            // Calculate which slots to display on current page (use dynamic ItemsPerPage)
            int startIndex = _currentPage * ItemsPerPage;
            int endIndex = Math.Min(startIndex + ItemsPerPage, _matchedSlots.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var slotItem = _matchedSlots[i];
                var slotViewModel = slots[i - startIndex]; // Map to visual slot position (0-N)

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

                    if (slotItem.Config != null && !string.IsNullOrEmpty(slotItem.Config.Color))
                    {
                        slotViewModel.SetColor(slotItem.Config.Color);
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
                    slotViewModel.ActionStrategy = new ProcessGroupStrategy(slotItem.Windows, _windowService, _usageTracker, _healthMonitor, _logService);
                    slotViewModel.CurrentOpacity = 1.0;
                }
                else if (slotItem.IsConfigured && !slotItem.IsRunning && slotItem.Config != null)
                {
                    // Configured but NOT running - placeholder
                    if (!string.IsNullOrEmpty(slotItem.Config.IconKey))
                    {
                        slotViewModel.LoadIconData(slotItem.Config.IconKey);
                    }

                    if (!string.IsNullOrEmpty(slotItem.Config.Color))
                    {
                        slotViewModel.SetColor(slotItem.Config.Color);
                    }

                    string baseLabel = !string.IsNullOrEmpty(slotItem.Config.Label) 
                        ? slotItem.Config.Label 
                        : "App";
                    slotViewModel.Label = $"{baseLabel} (Not Running)";

                    slotViewModel.Type = SlotType.Process;
                    slotViewModel.DataContext = slotItem.Config;
                    slotViewModel.ActionStrategy = new LaunchApplicationStrategy(slotItem.Config);
                    slotViewModel.CurrentOpacity = 0.5;
                }
            }
        }
    }
}
