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

        private const int UnusedDaysThreshold = 30;
        private const double HighErrorRateThreshold = 0.2;
        private const int MinExecutionsForRecommendation = 5;

        public PluginRecommendationEngine(
            IPluginRegistry registry,
            IPluginUsageTracker usageTracker,
            IPluginHealthMonitor healthMonitor,
            ILogger<PluginRecommendationEngine> logger,
            ILocalizationService localizationService)
        {
            _registry = registry;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger;
            _loc = localizationService;
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

                    // 检查未使用的插件
                    CheckUnusedPlugin(plugin, stats, recommendations);

                    // 检查高错误率插件
                    CheckHighErrorRate(plugin, stats, health, recommendations);
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate recommendations for plugin {PluginId}", pluginId);
            }

            return recommendations;
        }

        private void CheckUnusedPlugin(IPulsarPlugin plugin, Models.PluginUsageStats stats, List<PluginRecommendation> recommendations)
        {
            if (stats.TotalExecutions == 0 || 
                (stats.LastUsed.HasValue && (DateTime.UtcNow - stats.LastUsed.Value).TotalDays > UnusedDaysThreshold))
            {
                var daysSinceLastUse = stats.LastUsed.HasValue 
                    ? (int)(DateTime.UtcNow - stats.LastUsed.Value).TotalDays 
                    : -1;

                var message = stats.TotalExecutions == 0
                    ? string.Format(_loc["Plugin.Recommendation.UnusedNeverUsed"], plugin.DisplayName)
                    : string.Format(_loc["Plugin.Recommendation.UnusedDaysFormat"], plugin.DisplayName, daysSinceLastUse);

                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.DisableUnusedPlugin,
                    Title = _loc["Plugin.Recommendation.UnusedTitle"],
                    Message = message,
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = _loc["Plugin.Recommendation.DisableAction"],
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
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = _loc["Plugin.Recommendation.HighErrorTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.HighErrorFormat"], plugin.DisplayName, errorPercentage),
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = _loc["Plugin.Recommendation.ViewLogsAction"],
                    Icon = "\u26A0\uFE0F",
                    Severity = "Warning"
                });
            }

            if (health.CircuitBreakerTrips > 0)
            {
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = _loc["Plugin.Recommendation.CircuitBreakerTitle"],
                    Message = string.Format(_loc["Plugin.Recommendation.CircuitBreakerFormat"], plugin.DisplayName, health.CircuitBreakerTrips),
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = _loc["Plugin.Recommendation.ViewLogsAction"],
                    Icon = "\U0001f534",
                    Severity = "Error"
                });
            }
        }
    }
}
