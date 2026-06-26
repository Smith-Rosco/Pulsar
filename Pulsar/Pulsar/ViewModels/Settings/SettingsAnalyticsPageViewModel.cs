using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    public class AnalyticsItem
    {
        public string PluginId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Rank { get; set; }
        public int TotalExecutions { get; set; }
        public int TodayExecutions { get; set; }
        public int RecentExecutions { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public int FavoriteSlot { get; set; }
        public string PrimaryMode { get; set; } = string.Empty;
        public int TaskModeCount { get; set; }
        public int ActionModeCount { get; set; }
        public DateTime? LastUsed { get; set; }
        public Dictionary<int, int> SlotUsage { get; set; } = new();

        public string TotalFormatted { get; set; } = string.Empty;
        public string TodayFormatted { get; set; } = string.Empty;
        public string RecentFormatted { get; set; } = string.Empty;
        public string DurationFormatted { get; set; } = string.Empty;

        public bool IsTopThree => Rank <= 3;
        public string RankLabel { get; set; } = string.Empty;

        public string SuccessRateColor { get; set; } = "Green";

        public string SlotBreakdown { get; set; } = string.Empty;
        public string SlotSummary { get; set; } = string.Empty;
        public string ModeSummary { get; set; } = string.Empty;
        public string LastUsedFormatted { get; set; } = string.Empty;
    }

    public class SlotHeatmapItem
    {
        public int SlotIndex { get; init; }
        public int TotalExecutions { get; init; }
        public int PluginCount { get; init; }
        public double Percentage { get; init; }

        public double BarWidth => Math.Max(4, Percentage * 2.2);
        public string Label => $"Slot #{SlotIndex}";
        public string PercentageText => $"{Percentage:F0}%";
    }

    public class HourlyHeatmapItem
    {
        public int Hour { get; init; }
        public int TotalExecutions { get; init; }
        public double Percentage { get; init; }

        public double BarWidth => Math.Max(4, Percentage * 2.2);
        public string Label => $"{Hour:D2}:00";
        public string PercentageText => $"{Percentage:F0}%";
    }

    public partial class SettingsAnalyticsPageViewModel : ObservableObject
    {
        private readonly IPluginUsageTracker _usageTracker;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IPluginRecommendationEngine? _recommendationEngine;
        private readonly ILogger<SettingsAnalyticsPageViewModel> _logger;
        private readonly ILocalizationService _loc;

        public ObservableCollection<AnalyticsItem> MostUsedPlugins { get; } = new();
        public ObservableCollection<SlotHeatmapItem> SlotHeatmap { get; } = new();
        public ObservableCollection<HourlyHeatmapItem> HourlyHeatmap { get; } = new();
        public ObservableCollection<PluginRecommendation> Recommendations { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasData;

        [ObservableProperty]
        private bool _hasRecommendations;

        [ObservableProperty]
        private bool _hasHeatmap;

        [ObservableProperty]
        private bool _hasHourlyHeatmap;

        [ObservableProperty]
        private int _totalOverallExecutions;

        [ObservableProperty]
        private int _activePluginCount;

        [ObservableProperty]
        private int _totalTodayExecutions;

        [ObservableProperty]
        private int _totalWeekExecutions;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public SettingsAnalyticsPageViewModel(
            IPluginUsageTracker usageTracker,
            IPluginRegistry pluginRegistry,
            ILogger<SettingsAnalyticsPageViewModel> logger,
            ILocalizationService localizationService,
            IPluginRecommendationEngine? recommendationEngine = null)
        {
            _usageTracker = usageTracker;
            _pluginRegistry = pluginRegistry;
            _logger = logger;
            _loc = localizationService;
            _recommendationEngine = recommendationEngine;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            try
            {
                MostUsedPlugins.Clear();
                SlotHeatmap.Clear();
                HourlyHeatmap.Clear();
                Recommendations.Clear();

                var stats = await Task.Run(() => _usageTracker.GetMostUsedPlugins(20));
                var allStats = await Task.Run(() => _usageTracker.GetAllStats());
                var allPlugins = _pluginRegistry.GetAllPlugins();
                var displayNames = allPlugins.ToDictionary(p => p.Id, p => p.DisplayName, StringComparer.OrdinalIgnoreCase);

                var activeStats = stats.Where(s => s.TotalExecutions > 0).OrderByDescending(s => s.TotalExecutions).ToList();

                int rank = 0;
                foreach (var stat in activeStats)
                {
                    rank++;
                    var displayName = displayNames.TryGetValue(stat.PluginId, out var name) ? name : stat.PluginId;
                    MostUsedPlugins.Add(new AnalyticsItem
                    {
                        PluginId = stat.PluginId,
                        DisplayName = displayName,
                        Rank = rank,
                        TotalExecutions = stat.TotalExecutions,
                        TodayExecutions = stat.TodayExecutions,
                        RecentExecutions = stat.RecentExecutions,
                        AverageExecutionTimeMs = stat.AverageExecutionTimeMs,
                        SuccessRate = stat.SuccessRate,
                        FavoriteSlot = stat.FavoriteSlot,
                        PrimaryMode = stat.PrimaryMode,
                        TaskModeCount = stat.TaskModeExecutions,
                        ActionModeCount = stat.ActionModeExecutions,
                        LastUsed = stat.LastUsed,
                        SlotUsage = new Dictionary<int, int>(stat.SlotUsage),
                        TotalFormatted = FormatCount(stat.TotalExecutions),
                        TodayFormatted = FormatCount(stat.TodayExecutions),
                        RecentFormatted = FormatCount(stat.RecentExecutions),
                        DurationFormatted = stat.AverageExecutionTimeMs < 1000
                            ? string.Format(_loc["Settings.Analytics.DurationMs"], $"{stat.AverageExecutionTimeMs:F0}")
                            : string.Format(_loc["Settings.Analytics.DurationS"], $"{stat.AverageExecutionTimeMs / 1000:F1}"),
                        RankLabel = rank switch { 1 => "#1", 2 => "#2", 3 => "#3", _ => $"#{rank}" },
                        SuccessRateColor = stat.SuccessRate >= 95 ? "Green" : stat.SuccessRate >= 80 ? "Orange" : "Red",
                        SlotBreakdown = stat.SlotUsage.Count > 0
                            ? string.Join("  ", stat.SlotUsage.OrderBy(kv => kv.Key).Select(kv => $"#{kv.Key}:{kv.Value}"))
                            : "",
                        SlotSummary = stat.FavoriteSlot > 0
                            ? string.Format(_loc["Settings.Analytics.FavoriteSlotFormat"], stat.FavoriteSlot)
                            : "",
                        ModeSummary = (stat.TaskModeExecutions > 0 || stat.ActionModeExecutions > 0)
                            ? $"{stat.PrimaryMode} ({Math.Max(stat.TaskModeExecutions, stat.ActionModeExecutions)})"
                            : "",
                        LastUsedFormatted = FormatLastUsed(stat.LastUsed)
                    });
                }

                var aggregatedSlots = new Dictionary<int, (int Total, int Plugins)>();
                foreach (var stat in allStats.Values.Where(s => s.SlotUsage.Count > 0))
                {
                    foreach (var kv in stat.SlotUsage)
                    {
                        if (!aggregatedSlots.ContainsKey(kv.Key))
                            aggregatedSlots[kv.Key] = (0, 0);
                        aggregatedSlots[kv.Key] = (
                            aggregatedSlots[kv.Key].Total + kv.Value,
                            aggregatedSlots[kv.Key].Plugins + 1);
                    }
                }

                var totalAllSlotExecutions = aggregatedSlots.Values.Sum(v => v.Total);
                foreach (var kv in aggregatedSlots.OrderBy(kv => kv.Key))
                {
                    SlotHeatmap.Add(new SlotHeatmapItem
                    {
                        SlotIndex = kv.Key,
                        TotalExecutions = kv.Value.Total,
                        PluginCount = kv.Value.Plugins,
                        Percentage = totalAllSlotExecutions > 0 ? (double)kv.Value.Total / totalAllSlotExecutions * 100.0 : 0
                    });
                }

                var hourlyData = new Dictionary<int, int>();
                foreach (var stat in allStats.Values.Where(s => s.HourlyUsage.Count > 0))
                {
                    foreach (var kv in stat.HourlyUsage)
                    {
                        if (hourlyData.ContainsKey(kv.Key))
                            hourlyData[kv.Key] += kv.Value;
                        else
                            hourlyData[kv.Key] = kv.Value;
                    }
                }

                var maxHourly = hourlyData.Values.Any() ? hourlyData.Values.Max() : 1;
                for (int h = 0; h < 24; h++)
                {
                    var count = hourlyData.GetValueOrDefault(h, 0);
                    HourlyHeatmap.Add(new HourlyHeatmapItem
                    {
                        Hour = h,
                        TotalExecutions = count,
                        Percentage = maxHourly > 0 ? (double)count / maxHourly * 100.0 : 0
                    });
                }

                HasData = MostUsedPlugins.Count > 0;
                HasHeatmap = SlotHeatmap.Count > 0;
                HasHourlyHeatmap = hourlyData.Values.Any(v => v > 0);

                TotalOverallExecutions = activeStats.Sum(s => s.TotalExecutions);
                ActivePluginCount = activeStats.Count;
                TotalTodayExecutions = activeStats.Sum(s => s.TodayExecutions);
                TotalWeekExecutions = activeStats.Sum(s => s.RecentExecutions);

                if (_recommendationEngine != null)
                {
                    var recs = _recommendationEngine.GetRecommendations();
                    foreach (var rec in recs)
                        Recommendations.Add(rec);
                    HasRecommendations = Recommendations.Count > 0;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = _loc["Settings.Analytics.ErrorLoading"];
                _logger.LogError(ex, "[SettingsAnalyticsPageViewModel] Failed to load usage stats");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string FormatCount(int count)
        {
            if (count >= 1_000_000) return $"{(double)count / 1_000_000:F1}M";
            if (count >= 1_000) return $"{(double)count / 1_000:F1}K";
            return count.ToString();
        }

        private string FormatLastUsed(DateTime? lastUsed)
        {
            if (!lastUsed.HasValue) return "";
            var local = lastUsed.Value.ToLocalTime();
            var diff = DateTime.Now - local;
            if (diff.TotalMinutes < 1) return _loc["Settings.Analytics.JustNow"];
            if (diff.TotalMinutes < 60) return string.Format(_loc["Settings.Analytics.MinutesAgoFormat"], (int)diff.TotalMinutes);
            if (diff.TotalHours < 24) return string.Format(_loc["Settings.Analytics.HoursAgoFormat"], (int)diff.TotalHours);
            if (diff.TotalDays < 7) return string.Format(_loc["Settings.Analytics.DaysAgoFormat"], (int)diff.TotalDays);
            return local.ToString("MM-dd");
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadAsync();
        }
    }
}
