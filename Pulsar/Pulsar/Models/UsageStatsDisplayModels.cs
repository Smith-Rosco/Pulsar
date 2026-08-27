using System;
using System.Collections.Generic;

namespace Pulsar.Models
{
    /// <summary>
    /// 分析页的时间范围筛选选项
    /// </summary>
    public enum AnalyticsTimeRange
    {
        AllTime,
        Today,
        ThisWeek,
        ThisMonth
    }

    /// <summary>
    /// 分析页的排序列选项
    /// </summary>
    public enum SortColumn
    {
        Executions,
        SuccessRate,
        Duration,
        LastUsed
    }

    /// <summary>
    /// 统计读模型输出的单个插件展示行（原始值 + 已格式化字符串并存，供 UI 与 CSV 消费）
    /// </summary>
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
        public List<DailyTrendItem> TrendData { get; set; } = new();
    }

    public class DailyTrendItem
    {
        public string Date { get; init; } = string.Empty;
        public int Count { get; init; }
        public int MaxCount { get; init; }
        public double BarHeight => MaxCount > 0 ? Math.Max(2, (double)Count / MaxCount * 14) : 0;
        public bool HasData => Count > 0;
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
}
