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

        private List<ProcessWindowInfo>[] _page0Slots = new List<ProcessWindowInfo>[8];
        private PluginSlot?[] _page0Config = new PluginSlot?[8];
        private bool[] _page0IsRunning = new bool[8];
        private List<List<ProcessWindowInfo>> _overflowGroups = new();

        public override int TotalPages => 1 + (int)Math.Ceiling((double)_overflowGroups.Count / 8.0);

        public ProcessPageProvider(IWindowService windowService, ProfilesConfig config, System.IServiceProvider serviceProvider)
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

            // 2. Build map of ALL configured slots (running or not)
            var allConfiguredSlots = new Dictionary<int, PluginSlot>();
            if (_config?.Profiles.TryGetValue("Global", out var globalProfile) == true && globalProfile.SwitchMode != null)
            {
                foreach (var item in globalProfile.SwitchMode)
                {
                    if (item.PluginId == "com.pulsar.winswitcher" && item.Slot >= 1 && item.Slot <= 8)
                    {
                        allConfiguredSlots[item.Slot] = item;
                    }
                }
            }

            // 3. Build reverse lookup: ProcessName -> SlotIndex
            var pinnedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in allConfiguredSlots)
            {
                if (kvp.Value.Args.TryGetValue("app", out var appName))
                {
                    pinnedMap[appName] = kvp.Key;
                }
                else if (!string.IsNullOrEmpty(kvp.Value.Label))
                {
                    pinnedMap[kvp.Value.Label] = kvp.Key;
                }
            }

            // 4. Separate running processes into Pinned vs Others
            var pinnedGroups = new List<IGrouping<string, ProcessWindowInfo>>();
            var otherGroups = new List<IGrouping<string, ProcessWindowInfo>>();

            foreach (var g in groups)
            {
                if (pinnedMap.ContainsKey(g.Key))
                {
                    pinnedGroups.Add(g);
                }
                else
                {
                    otherGroups.Add(g);
                }
            }

            // 5. Initialize Page 0 arrays
            Array.Clear(_page0Slots, 0, 8);
            Array.Clear(_page0Config, 0, 8);
            Array.Clear(_page0IsRunning, 0, 8);

            // 6. Fill configured slots (running or placeholder)
            foreach (var kvp in allConfiguredSlots)
            {
                int slotIdx = kvp.Key - 1; // Convert 1-based to 0-based
                _page0Config[slotIdx] = kvp.Value;

                // Check if this configured app is currently running
                string? appName = null;
                if (kvp.Value.Args.TryGetValue("app", out var app))
                {
                    appName = app;
                }
                else if (!string.IsNullOrEmpty(kvp.Value.Label))
                {
                    appName = kvp.Value.Label;
                }

                var runningGroup = pinnedGroups.FirstOrDefault(g => 
                    string.Equals(g.Key, appName, StringComparison.OrdinalIgnoreCase));

                if (runningGroup != null)
                {
                    // App is running
                    _page0Slots[slotIdx] = runningGroup.ToList();
                    _page0IsRunning[slotIdx] = true;
                }
                else
                {
                    // App is NOT running - create empty placeholder
                    _page0Slots[slotIdx] = new List<ProcessWindowInfo>();
                    _page0IsRunning[slotIdx] = false;
                }
            }

            // 7. Fill remaining empty slots with unconfigured running processes
            int otherIdx = 0;
            for (int i = 0; i < 8; i++)
            {
                if (_page0Config[i] == null && otherIdx < otherGroups.Count)
                {
                    _page0Slots[i] = otherGroups[otherIdx].ToList();
                    _page0IsRunning[i] = true;
                    otherIdx++;
                }
            }

            // 8. Remaining others form Overflow Pages
            _overflowGroups.Clear();
            for (int i = otherIdx; i < otherGroups.Count; i++)
            {
                _overflowGroups.Add(otherGroups[i].ToList());
            }

            _currentPage = 0;
        }

        public override void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot)
        {
            ClearSlots(slots);

            string centerText = _currentPage == 0 ? "Switch" : $"Page {_currentPage + 1}";
            centerSlot.Label = centerText;
            centerSlot.LoadIconData(string.Empty);
            centerSlot.ActionStrategy = new NoOpStrategy(); // Or Cancel/Back handled by VM

            if (_currentPage == 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    var group = _page0Slots[i];
                    var config = _page0Config[i];
                    var isRunning = _page0IsRunning[i];

                    // Skip empty unconfigured slots
                    if (config == null && (group == null || group.Count == 0))
                    {
                        continue;
                    }

                    var slot = slots[i];

                    if (isRunning && group != null && group.Count > 0)
                    {
                        // Running process - normal display
                        var first = group.First();

                        // Config Priority
                        if (config != null && !string.IsNullOrEmpty(config.IconKey))
                        {
                            slot.LoadIconData(config.IconKey);
                        }
                        else
                        {
                            slot.IconImage = first.AppIcon;
                        }

                        if (config != null && !string.IsNullOrEmpty(config.Color))
                        {
                            slot.SetColor(config.Color);
                        }

                        string baseLabel = !string.IsNullOrEmpty(config?.Label) 
                            ? config.Label 
                            : ProcessNameFormatter.ToDisplayName(first.ProcessName);
                        if (group.Count > 1)
                        {
                            slot.Label = $"{baseLabel} ({group.Count})";
                            slot.BadgeCount = group.Count;
                        }
                        else
                        {
                            slot.Label = baseLabel;
                        }

                        slot.Type = SlotType.Process;
                        slot.DataContext = group;
                        slot.ActionStrategy = new ProcessGroupStrategy(group, _usageTracker, _healthMonitor, _logService);
                        slot.CurrentOpacity = 1.0;
                    }
                    else if (!isRunning && config != null)
                    {
                        // Configured but NOT running - placeholder
                        if (!string.IsNullOrEmpty(config.IconKey))
                        {
                            slot.LoadIconData(config.IconKey);
                        }

                        if (!string.IsNullOrEmpty(config.Color))
                        {
                            slot.SetColor(config.Color);
                        }

                        string baseLabel = !string.IsNullOrEmpty(config.Label) 
                            ? config.Label 
                            : "App";
                        slot.Label = $"{baseLabel} (Not Running)";

                        slot.Type = SlotType.Process;
                        slot.DataContext = config;
                        slot.ActionStrategy = new LaunchApplicationStrategy(config);
                        slot.CurrentOpacity = 0.5;
                    }
                }
            }
            else
            {
                int offset = (_currentPage - 1) * 8;
                var pageItems = _overflowGroups.Skip(offset).Take(8).ToList();

                for (int i = 0; i < pageItems.Count; i++)
                {
                    var group = pageItems[i];
                    var slot = slots[i];
                    var first = group.First();

                    string baseLabel = ProcessNameFormatter.ToDisplayName(first.ProcessName);
                    if (group.Count > 1)
                    {
                        slot.Label = $"{baseLabel} ({group.Count})";
                        slot.BadgeCount = group.Count;
                    }
                    else
                    {
                        slot.Label = baseLabel;
                    }

                    slot.IconImage = first.AppIcon;
                    slot.Type = SlotType.Process;
                    slot.DataContext = group;
                    slot.ActionStrategy = new ProcessGroupStrategy(group, _usageTracker, _healthMonitor, _logService);
                }
            }
        }
    }
}
