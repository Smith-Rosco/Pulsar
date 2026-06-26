using System;
using Pulsar.Core.Localization;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public class PluginAnalyticsFormatter
    {
        private readonly ILocalizationService _loc;

        public PluginAnalyticsFormatter(ILocalizationService loc)
        {
            _loc = loc;
        }

        public string FormatUsageSummary(PluginUsageStats stats)
        {
            return $"{stats.TotalExecutions} uses";
        }

        public string FormatProfilesSummary(PluginUsageStats stats)
        {
            return $"{stats.UsedInProfiles.Count} profiles";
        }

        public string FormatLastUsedSummary(PluginUsageStats stats)
        {
            if (!stats.LastUsed.HasValue)
                return _loc["Plugin.NeverUsed"];

            return FormatTimeAgo(stats.LastUsed.Value);
        }

        public string FormatHealthBadge(PluginHealthReport health)
        {
            return health.Status switch
            {
                PluginHealthStatus.Healthy => "\u2705",
                PluginHealthStatus.Warning => "\u26A0\uFE0F",
                PluginHealthStatus.Critical => "\uD83D\uDD34",
                PluginHealthStatus.Unused => "\uD83D\uDCA4",
                PluginHealthStatus.Disabled => "\uD83D\uDEAB",
                _ => ""
            };
        }

        public string FormatHealthScoreText(PluginHealthReport health)
        {
            return $"{health.HealthScore}/100";
        }

        public string FormatHealthScoreColor(PluginHealthReport health)
        {
            return health.HealthScore switch
            {
                >= 90 => "#28a745",
                >= 70 => "#ffc107",
                _ => "#dc3545"
            };
        }

        public string FormatSuccessRateText(PluginUsageStats stats)
        {
            if (stats.TotalExecutions > 0)
                return $"{(double)stats.SuccessCount / stats.TotalExecutions * 100:F1}%";

            return _loc["Plugin.NA"];
        }

        public string FormatAvgExecutionTimeText(PluginUsageStats stats)
        {
            return $"{stats.AverageExecutionTimeMs:F0}ms";
        }

        private string FormatTimeAgo(DateTime dateTime)
        {
            var span = DateTime.UtcNow - dateTime;
            if (span.TotalMinutes < 1) return _loc["Plugin.JustNow"];
            if (span.TotalMinutes < 60) return string.Format(_loc["Plugin.MinutesAgoFormat"], (int)span.TotalMinutes);
            if (span.TotalHours < 24) return string.Format(_loc["Plugin.HoursAgoFormat"], (int)span.TotalHours);
            if (span.TotalDays < 30) return string.Format(_loc["Plugin.DaysAgoFormat"], (int)span.TotalDays);
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }
}
