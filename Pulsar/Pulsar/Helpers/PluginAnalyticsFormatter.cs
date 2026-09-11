using System;
using Pulsar.Core.Formatting;
using Pulsar.Core.Localization;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public class PluginAnalyticsFormatter
    {
        /// <summary>
        /// Resource-key family this formatter renders relative times through
        /// (<c>Plugin.JustNow</c> / <c>Plugin.MinutesAgoFormat</c> / …). Validated
        /// against both resx by <c>RelativeTimeFormatterTests</c>.
        /// </summary>
        public const string RelativeTimeKeyPrefix = "Plugin";

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

        /// <summary>
        /// Delegates to the single relative-time ladder (ADR-034). The full-date
        /// fallback style is this surface's own; the ceiling and the bucket boundaries
        /// come from <see cref="RelativeTimeFormatter"/>.
        /// </summary>
        private string FormatTimeAgo(DateTime dateTime)
            => RelativeTimeFormatter.Format(dateTime, DateTime.UtcNow, _loc, RelativeTimeKeyPrefix, "yyyy-MM-dd");
    }
}
