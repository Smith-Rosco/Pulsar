using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件推荐引擎实现
    /// </summary>
    public class PluginRecommendationEngine : IPluginRecommendationEngine
    {
        private readonly PluginRegistry _registry;
        private readonly IPluginUsageTracker _usageTracker;
        private readonly IPluginHealthMonitor _healthMonitor;
        private readonly ILogger<PluginRecommendationEngine> _logger;

        // 推荐阈值
        private const int UnusedDaysThreshold = 30;
        private const double HighErrorRateThreshold = 0.2; // 20% 错误率
        private const int MinExecutionsForRecommendation = 5;

        public PluginRecommendationEngine(
            PluginRegistry registry,
            IPluginUsageTracker usageTracker,
            IPluginHealthMonitor healthMonitor,
            ILogger<PluginRecommendationEngine> logger)
        {
            _registry = registry;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger;
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
            // 如果插件从未使用或超过 30 天未使用
            if (stats.TotalExecutions == 0 || 
                (stats.LastUsed.HasValue && (DateTime.UtcNow - stats.LastUsed.Value).TotalDays > UnusedDaysThreshold))
            {
                var daysSinceLastUse = stats.LastUsed.HasValue 
                    ? (int)(DateTime.UtcNow - stats.LastUsed.Value).TotalDays 
                    : -1;

                var message = stats.TotalExecutions == 0
                    ? $"{plugin.DisplayName} has never been used. Consider disabling it to improve performance."
                    : $"{plugin.DisplayName} hasn't been used for {daysSinceLastUse} days. Consider disabling it.";

                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.DisableUnusedPlugin,
                    Title = "Unused Plugin Detected",
                    Message = message,
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = "Disable Plugin",
                    Icon = "💤",
                    Severity = "Info"
                });
            }
        }

        private void CheckHighErrorRate(IPulsarPlugin plugin, Models.PluginUsageStats stats, Models.PluginHealthReport health, List<PluginRecommendation> recommendations)
        {
            // 只对有足够执行次数的插件进行检查
            if (stats.TotalExecutions < MinExecutionsForRecommendation)
                return;

            // 检查错误率
            if (health.ErrorRate > HighErrorRateThreshold)
            {
                var errorPercentage = (health.ErrorRate * 100).ToString("F1");
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = "High Error Rate Detected",
                    Message = $"{plugin.DisplayName} has a {errorPercentage}% error rate. Check logs for details.",
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = "View Logs",
                    Icon = "⚠️",
                    Severity = "Warning"
                });
            }

            // 检查 Circuit Breaker 触发
            if (health.CircuitBreakerTrips > 0)
            {
                recommendations.Add(new PluginRecommendation
                {
                    Type = RecommendationType.CheckPluginErrors,
                    Title = "Circuit Breaker Triggered",
                    Message = $"{plugin.DisplayName} has been temporarily disabled {health.CircuitBreakerTrips} time(s) due to repeated failures.",
                    PluginId = plugin.Id,
                    PluginName = plugin.DisplayName,
                    ActionLabel = "View Logs",
                    Icon = "🔴",
                    Severity = "Error"
                });
            }
        }
    }
}
