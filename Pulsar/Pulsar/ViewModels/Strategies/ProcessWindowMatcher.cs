using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Strategies
{
    public class MatchedWindowGroup
    {
        public PluginSlot? Config { get; set; }
        public List<ProcessWindowInfo> Windows { get; set; } = new();
        public bool IsRunning => Windows.Count > 0;
        public bool IsConfigured => Config != null;
    }

    public class ProcessWindowMatcher
    {
        private readonly ProfilesConfig _config;

        public ProcessWindowMatcher(ProfilesConfig config)
        {
            _config = config;
        }

        public List<MatchedWindowGroup> BuildSlotList(List<ProcessWindowInfo> windows)
        {
            var groups = windows.GroupBy(w => w.ProcessName).ToList();

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

            var configByAppName = new Dictionary<string, PluginSlot>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in allConfiguredSlots.Values)
            {
                if (config.Args.TryGetValue("app", out var appName))
                    configByAppName[appName] = config;
                else if (!string.IsNullOrEmpty(config.Label))
                    configByAppName[config.Label] = config;
            }

            var matchedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unconfiguredGroups = new List<List<ProcessWindowInfo>>();

            foreach (var g in groups)
            {
                if (configByAppName.ContainsKey(g.Key))
                    matchedProcesses.Add(g.Key);
                else
                    unconfiguredGroups.Add(g.ToList());
            }

            var slotsByPosition = new Dictionary<int, MatchedWindowGroup>();
            int maxConfiguredSlot = allConfiguredSlots.Keys.Any() ? allConfiguredSlots.Keys.Max() : 0;
            int totalSlotsNeeded = Math.Max(maxConfiguredSlot, unconfiguredGroups.Count);

            foreach (var kvp in allConfiguredSlots)
            {
                int position = kvp.Key;
                var config = kvp.Value;
                string? appName = config.Args.TryGetValue("app", out var app) 
                    ? app 
                    : (!string.IsNullOrEmpty(config.Label) ? config.Label : null);

                List<ProcessWindowInfo>? matchedWindows = null;
                if (appName != null)
                {
                    var matchingGroup = groups.FirstOrDefault(g => 
                        string.Equals(g.Key, appName, StringComparison.OrdinalIgnoreCase));
                    if (matchingGroup != null)
                        matchedWindows = matchingGroup.ToList();
                }

                slotsByPosition[position] = new MatchedWindowGroup
                {
                    Config = config,
                    Windows = matchedWindows ?? new List<ProcessWindowInfo>()
                };
            }

            int unconfiguredIndex = 0;
            int currentPosition = 1;
            while (unconfiguredIndex < unconfiguredGroups.Count || slotsByPosition.ContainsKey(currentPosition))
            {
                if (!slotsByPosition.ContainsKey(currentPosition) && unconfiguredIndex < unconfiguredGroups.Count)
                {
                    slotsByPosition[currentPosition] = new MatchedWindowGroup
                    {
                        Config = null,
                        Windows = unconfiguredGroups[unconfiguredIndex]
                    };
                    unconfiguredIndex++;
                }
                currentPosition++;
            }

            return slotsByPosition.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        }
    }
}