using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services
{
    public class PluginRecommendationEngine : IPluginRecommendationEngine
    {
        private readonly IPluginRegistry _registry;
        private readonly IPluginUsageTracker _usageTracker;
        private readonly IPluginHealthMonitor _healthMonitor;
        private readonly ILogger<PluginRecommendationEngine> _logger;
        private readonly ILocalizationService _loc;
        private readonly Func<DateTime> _clock;

        private const int UnusedDaysThreshold = 30;
        private const double HighErrorRateThreshold = 0.2;
        private const int MinExecutionsForRecommendation = 5;
        private const int InactiveDaysThreshold = 7;
        private const int MinExecsForInactivityAlert = 50;
        private const int TrendWindowDays = 7;
        private const int MinExecutionsForTrend = 10;
        private const double TrendChangeThreshold = 0.5;

        public PluginRecommendationEngine(
            IPluginRegistry registry,
            IPluginUsageTracker usageTracker,
            IPluginHealthMonitor healthMonitor,
            ILogger<PluginRecommendationEngine> logger,
            ILocalizationService localizationService,
            Func<DateTime>? clock = null)
        {
            _registry = registry;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger;
            _loc = localizationService;
            _clock = clock ?? (() => DateTime.Now);
        }

        public List<PluginRecommendation> GetRecommendations()
        {
            var recommendations = new List<PluginRecommendation>();

            try
            {
                var allPlugins = _registry.GetAllPlugins();
                var allStats = _usageTracker.GetAllStats();
                var allHealthReports = _healthMonitor.GetAllHealthReports();

                foreach (var plugin in allPlugins)
                {
                    // 跳过 Core 插件
                    if (!plugin.CanDisable)
                        continue;

                    var stats = allStats.GetValueOrDefault(plugin.Id) ?? new Models.PluginUsageStats { PluginId = plugin.Id };
                    var health = allHealthReports.GetValueOrDefault(plugin.Id) ?? new Models.PluginHealthReport { PluginId = plugin.Id };

                    CheckUnusedPlugin(plugin, stats, recommendations);
                    CheckHighErrorRate(plugin, stats, health, recommendations);
                    CheckInactivePlugin(plugin, stats, recommendations);
                    CheckSlotOptimization(plugin, stats, recommendations);
                    CheckUsageTrend(plugin, stats, recommendations);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate plugin recommendations");
            }

            return recommendations;
        }

        public List<PluginRecommendation> GetRecommendationsForPlugin(string pluginId)
        {
            var recommendations = new List<PluginRecommendation>();

            try
            {
                var plugin = _registry.GetAllPlugins().FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null || !plugin.CanDisable)
                    return recommendations;

                var stats = _usageTracker.GetStats(pluginId);
                var health = _healthMonitor.GetHealthReport(pluginId);

                CheckUnusedPlugin(plugin, stats, recommendations);
                CheckHighErrorRate(plugin, stats, health, recommendations);
                CheckInactivePlugin(plugin, stats, recommendations);
                CheckSlotOptimization(plugin, stats, recommendations);
                CheckUsageTrend(plugin, stats, recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate recommendations for plugin {PluginId}", pluginId);
            }

            return recommendations;
        }

        private string LocalizePluginName(IPulsarPlugin plugin)
            => PluginLocalization.LocalizePluginName(_loc, plugin.DisplayName);

        private void CheckUnusedPlugin(IPulsarPlugin plugin, Models.PluginUsageStats stats, List<PluginRecommendation> recommendations)
        {
            if (stats.TotalExecutions == 0 || 
                (stats.LastUsed.HasValue && (DateTime.UtcNow - stats.LastUsed.Value).TotalDays > UnusedDaysThreshold))
            {
                var daysSinceLastUse = stats.LastUsed.HasValue 
                    ? (int)(DateTime.UtcNow - stats.LastUsed.Value).TotalDays 
                    : -1;

                var displayName = LocalizePluginName(plugin);
                var message = stats.TotalExecutions == 0
                    ? string.Format(_loc["Plugin.Recommendation.UnusedNeverUsed"], displayName)
                    : string.Format(_loc["Plugin.Recommendation.UnusedDaysFormat"], displayName, daysSinceLastUse);

                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.DisableUnusedPlugin,
                    Title = _loc["Plugin.Recommendation.UnusedTitle"],
                    Message = message,
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = _loc["Plugin.Recommendation.DisableAction"],
                    ActionCommand = "DisablePlugin",
                    ActionParameter = plugin.Id,
                    Icon = "\U0001f4a4",
                    Severity = "Info"
                });
            }
        }

        private void CheckHighErrorRate(IPulsarPlugin plugin, Models.PluginUsageStats stats, Models.PluginHealthReport health, List<PluginRecommendation> recommendations)
        {
            if (stats.TotalExecutions < MinExecutionsForRecommendation)
                return;

            if (health.ErrorRate > HighErrorRateThreshold)
            {
                var errorPercentage = (health.ErrorRate * 100).ToString("F1");
                var displayName = LocalizePluginName(plugin);
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = _loc["Plugin.Recommendation.HighErrorTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.HighErrorFormat"], displayName, errorPercentage),
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = _loc["Plugin.Recommendation.ViewLogsAction"],
                    ActionCommand = "ViewLogs",
                    ActionParameter = plugin.Id,
                    Icon = "\u26A0\uFE0F",
                    Severity = "Warning"
                });
            }

            if (health.CircuitBreakerTrips > 0)
            {
                var displayName = LocalizePluginName(plugin);
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = _loc["Plugin.Recommendation.CircuitBreakerTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.CircuitBreakerFormat"], displayName, health.CircuitBreakerTrips),
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = _loc["Plugin.Recommendation.ViewLogsAction"],
                    ActionCommand = "ViewLogs",
                    ActionParameter = plugin.Id,
                    Icon = "\U0001f534",
                    Severity = "Error"
                });
            }
        }

        private void CheckInactivePlugin(IPulsarPlugin plugin, Models.PluginUsageStats stats, List<PluginRecommendation> recommendations)
        {
            if (stats.TotalExecutions < MinExecsForInactivityAlert)
                return;

            if (!stats.LastUsed.HasValue)
                return;

            var daysSinceLastUse = (DateTime.UtcNow - stats.LastUsed.Value).TotalDays;
            if (daysSinceLastUse > InactiveDaysThreshold)
            {
                var displayName = LocalizePluginName(plugin);
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.InactivePlugin,
                    Title = _loc["Plugin.Recommendation.InactiveTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.InactiveFormat"], displayName, (int)daysSinceLastUse),
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = _loc["Plugin.Recommendation.DisableAction"],
                    ActionCommand = "DisablePlugin",
                    ActionParameter = plugin.Id,
                    Icon = "\u23F3",
                    Severity = "Warning"
                });
            }
        }

        private void CheckSlotOptimization(IPulsarPlugin plugin, Models.PluginUsageStats stats, List<PluginRecommendation> recommendations)
        {
            if (stats.TotalExecutions < 100)
                return;

            if (stats.FavoriteSlot >= 3)
            {
                var displayName = LocalizePluginName(plugin);
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.OptimizeSlotPlacement,
                    Title = string.Format(_loc["Plugin.Recommendation.SlotOptimizationTitle"], displayName),
                    Message = _loc["Settings.Analytics.Recommendation.MoveSlot"],
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = "",
                    Icon = "\U0001f4c8",
                    Severity = "Info"
                });
            }
        }

        /// <summary>
        /// 用量趋势洞察：比较近 7 天与前一 7 天（DailyStats 本地日期键）的执行量，
        /// 任一窗口 ≥ <see cref="MinExecutionsForTrend"/> 且变化幅度 ≥ <see cref="TrendChangeThreshold"/> 时给出增长/下滑推荐。
        /// 纯信息型推荐（无动作按钮），由 UI 的 ActionCommand 为空分支呈现。
        /// </summary>
        private void CheckUsageTrend(IPulsarPlugin plugin, Models.PluginUsageStats stats, List<PluginRecommendation> recommendations)
        {
            if (stats.DailyStats == null || stats.DailyStats.Count == 0)
                return;

            var now = _clock();
            var recent = 0;
            var previous = 0;
            for (int i = 0; i < TrendWindowDays; i++)
            {
                var recentKey = now.AddDays(-i).ToString("yyyy-MM-dd");
                var previousKey = now.AddDays(-i - TrendWindowDays).ToString("yyyy-MM-dd");
                recent += stats.DailyStats.GetValueOrDefault(recentKey);
                previous += stats.DailyStats.GetValueOrDefault(previousKey);
            }

            if (previous <= 0 || Math.Max(recent, previous) < MinExecutionsForTrend)
                return;

            var change = (double)(recent - previous) / previous;
            if (Math.Abs(change) < TrendChangeThreshold)
                return;

            var percentChange = (int)Math.Round(Math.Abs(change) * 100);
            var displayName = LocalizePluginName(plugin);

            if (change > 0)
            {
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.UsageTrendUp,
                    Title = _loc["Plugin.Recommendation.UsageTrendUpTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.UsageTrendUpFormat"], displayName, percentChange, recent, previous),
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = "",
                    Icon = "\U0001f4c8",
                    Severity = "Info"
                });
            }
            else
            {
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.UsageTrendDown,
                    Title = _loc["Plugin.Recommendation.UsageTrendDownTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.UsageTrendDownFormat"], displayName, percentChange, recent, previous),
                    PluginId = plugin.Id,
                    PluginName = displayName,
                    ActionLabel = "",
                    Icon = "\U0001f4c9",
                    Severity = "Info"
                });
            }
        }
    }
}
