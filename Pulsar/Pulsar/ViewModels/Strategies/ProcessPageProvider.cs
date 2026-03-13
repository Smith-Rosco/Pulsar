using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.ViewModels; // Added

namespace Pulsar.ViewModels.Strategies
{
    public class ProcessPageProvider : BasePageProvider
    {
        private readonly IWindowService _windowService;
        private readonly ProfilesConfig _config;
        private readonly System.IServiceProvider _serviceProvider; // If needed for Plugin Registry etc.
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;

        // Slot item: represents one position in the radial menu
        private class SlotItem
        {
            public PluginSlot? Config { get; set; }              // Configuration (if pinned)
            public List<ProcessWindowInfo>? Windows { get; set; } // Running windows (if any)
            public bool IsRunning => Windows != null && Windows.Count > 0;
            public bool IsConfigured => Config != null;
        }

        // All slots across all pages (configured positions are fixed, unconfigured fill gaps)
        private List<SlotItem> _allSlots = new();

        public override int TotalPages => (int)Math.Ceiling((double)_allSlots.Count / (double)ItemsPerPage);

        public ProcessPageProvider(IWindowService windowService, ProfilesConfig config, System.IServiceProvider serviceProvider)
            : base(serviceProvider.GetService(typeof(IConfigService)) as IConfigService)
        {
            _windowService = windowService;
            _config = config;
            _serviceProvider = serviceProvider;
            
            // Resolve analytics services
            _usageTracker = serviceProvider.GetService(typeof(IPluginUsageTracker)) as IPluginUsageTracker;
            _healthMonitor = serviceProvider.GetService(typeof(IPluginHealthMonitor)) as IPluginHealthMonitor;
            _logService = serviceProvider.GetService(typeof(IPluginLogService)) as IPluginLogService;
        }

        public override async Task LoadAsync()
        {
            var windows = await _windowService.GetActiveWindowsAsync();
            
            // 1. Group by Process
            var groups = windows.GroupBy(w => w.ProcessName).ToList();

            // 2. Load ALL configured slots (no limit on Slot number)
            var allConfiguredSlots = new Dictionary<int, PluginSlot>();
            if (_config?.Profiles.TryGetValue("Global", out var globalProfile) == true && globalProfile.SwitchMode != null)
            {
                foreach (var item in globalProfile.SwitchMode)
                {
                    if (item.PluginId == "com.pulsar.winswitcher" && item.Slot >= 1)
                    {
                        allConfiguredSlots[item.Slot] = item;
                    }
                }
            }

            // 3. Build reverse lookup: ProcessName -> PluginSlot
            var configByAppName = new Dictionary<string, PluginSlot>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in allConfiguredSlots.Values)
            {
                if (config.Args.TryGetValue("app", out var appName))
                {
                    configByAppName[appName] = config;
                }
                else if (!string.IsNullOrEmpty(config.Label))
                {
                    configByAppName[config.Label] = config;
                }
            }

            // 4. Match running processes to configurations
            var matchedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unconfiguredGroups = new List<List<ProcessWindowInfo>>();

            foreach (var g in groups)
            {
                if (configByAppName.ContainsKey(g.Key))
                {
                    matchedProcesses.Add(g.Key);
                }
                else
                {
                    unconfiguredGroups.Add(g.ToList());
                }
            }

            // 5. Build slot list with fixed positions for configured items
            _allSlots.Clear();
            
            // Determine the maximum slot index needed
            int maxConfiguredSlot = allConfiguredSlots.Keys.Any() ? allConfiguredSlots.Keys.Max() : 0;
            int totalSlotsNeeded = Math.Max(maxConfiguredSlot, unconfiguredGroups.Count);
            
            // Create slot items for all positions
            var slotsByPosition = new Dictionary<int, SlotItem>();
            
            // First, place all configured items at their designated positions
            foreach (var kvp in allConfiguredSlots)
            {
                int position = kvp.Key; // 1-based position
                var config = kvp.Value;
                
                // Find matching running process
                string? appName = null;
                if (config.Args.TryGetValue("app", out var app))
                {
                    appName = app;
                }
                else if (!string.IsNullOrEmpty(config.Label))
                {
                    appName = config.Label;
                }

                List<ProcessWindowInfo>? matchedWindows = null;
                if (appName != null)
                {
                    var matchingGroup = groups.FirstOrDefault(g => 
                        string.Equals(g.Key, appName, StringComparison.OrdinalIgnoreCase));
                    if (matchingGroup != null)
                    {
                        matchedWindows = matchingGroup.ToList();
                    }
                }

                slotsByPosition[position] = new SlotItem
                {
                    Config = config,
                    Windows = matchedWindows
                };
            }

            // Second, fill gaps with unconfigured running processes
            int unconfiguredIndex = 0;
            int currentPosition = 1;
            
            while (unconfiguredIndex < unconfiguredGroups.Count || slotsByPosition.ContainsKey(currentPosition))
            {
                if (!slotsByPosition.ContainsKey(currentPosition) && unconfiguredIndex < unconfiguredGroups.Count)
                {
                    // This position is empty, fill with unconfigured process
                    slotsByPosition[currentPosition] = new SlotItem
                    {
                        Config = null,
                        Windows = unconfiguredGroups[unconfiguredIndex]
                    };
                    unconfiguredIndex++;
                }
                currentPosition++;
            }

            // Convert to sorted list
            _allSlots = slotsByPosition
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();

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
            int endIndex = Math.Min(startIndex + ItemsPerPage, _allSlots.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var slotItem = _allSlots[i];
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
