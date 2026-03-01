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

            // 2. Identify Pinned Slots (SwitchMode in Global profile)
            var pinnedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_config?.Profiles.TryGetValue("Global", out var globalProfile) == true && globalProfile.SwitchMode != null)
            {
                // [Migration] SwitchMode is now List<PluginSlot>
                foreach (var item in globalProfile.SwitchMode)
                {
                    if (item.PluginId == "com.pulsar.winswitcher" && item.Args.TryGetValue("app", out var appName))
                    {
                        pinnedMap[appName] = item.Slot;
                    }
                    else if (!string.IsNullOrEmpty(item.Label))
                    {
                         // Fallback to label
                         pinnedMap[item.Label] = item.Slot;
                    }
                }
            }

            // 3. Separate Pinned vs Others
            var pinnedGroups = new List<IGrouping<string, ProcessWindowInfo>>();
            var otherGroups = new List<IGrouping<string, ProcessWindowInfo>>();

            foreach (var g in groups)
            {
                if (pinnedMap.ContainsKey(g.Key)) pinnedGroups.Add(g);
                else otherGroups.Add(g);
            }

            // 4. Build Page 0 (Pinned + First batch of Others)
            Array.Clear(_page0Slots, 0, 8);
            Array.Clear(_page0Config, 0, 8);

            foreach (var pg in pinnedGroups)
            {
                int slotIdx = pinnedMap[pg.Key];
                if (slotIdx >= 1 && slotIdx <= 8)
                {
                    _page0Slots[slotIdx - 1] = pg.ToList();
                    
                    // Store config for icon/label/color
                    if (_config?.Profiles.TryGetValue("Global", out var gp) == true)
                    {
                        _page0Config[slotIdx - 1] = gp.SwitchMode.FirstOrDefault(s => s.Slot == slotIdx);
                    }
                }
            }

            // Fill gaps in Page 0 with Others
            int otherIdx = 0;
            for (int i = 0; i < 8; i++)
            {
                if (_page0Slots[i] == null && otherIdx < otherGroups.Count)
                {
                    _page0Slots[i] = otherGroups[otherIdx].ToList();
                    otherIdx++;
                }
            }

            // Remaining others form Overflow Pages
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
                    if (group != null && group.Count > 0)
                    {
                        var slot = slots[i];
                        var first = group.First();
                        var config = _page0Config[i];

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

                        string baseLabel = !string.IsNullOrEmpty(config?.Label) ? config.Label : first.ProcessName;
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

                    string baseLabel = first.ProcessName;
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
