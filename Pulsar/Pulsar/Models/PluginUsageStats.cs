// [Path]: Pulsar/Pulsar/Models/PluginUsageStats.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Pulsar.Models
{
    /// <summary>
    /// 插件使用统计数据模型
    /// 存储位置: %AppData%\Pulsar\PluginUsageStats.json
    /// </summary>
    public class PluginUsageStats
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 总执行次数（自安装以来）
        /// </summary>
        public int TotalExecutions { get; set; }

        /// <summary>
        /// 成功执行次数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败执行次数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime? LastUsed { get; set; }

        /// <summary>
        /// 首次使用时间
        /// </summary>
        public DateTime? FirstUsed { get; set; }

        /// <summary>
        /// 每日统计（最近 30 天）
        /// Key: "yyyy-MM-dd", Value: 执行次数
        /// </summary>
        public Dictionary<string, int> DailyStats { get; set; } = new();

        /// <summary>
        /// 平均执行时长（毫秒）
        /// </summary>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// 总执行时长（毫秒）- 用于计算平均值
        /// </summary>
        public long TotalExecutionTimeMs { get; set; }

        /// <summary>
        /// 使用该插件的 Profile 列表
        /// </summary>
        public HashSet<string> UsedInProfiles { get; set; } = new();

        /// <summary>
        /// Per-slot index execution count
        /// Key: slot position (1-N), Value: times used from this slot
        /// </summary>
        public Dictionary<int, int> SlotUsage { get; set; } = new();

        /// <summary>
        /// Per-mode execution counts
        /// </summary>
        public int TaskModeExecutions { get; set; }

        public int ActionModeExecutions { get; set; }

        /// <summary>
        /// Per-hour usage heatmap (0-23)
        /// </summary>
        public Dictionary<int, int> HourlyUsage { get; set; } = new();

        /// <summary>
        /// Success rate as percentage
        /// </summary>
        [JsonIgnore]
        public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100.0 : 0;

        /// <summary>
        /// Most used slot index
        /// </summary>
        [JsonIgnore]
        public int FavoriteSlot => SlotUsage.Count > 0
            ? SlotUsage.OrderByDescending(kvp => kvp.Value).First().Key
            : 0;

        /// <summary>
        /// Primary mode
        /// </summary>
        [JsonIgnore]
        public string PrimaryMode => TaskModeExecutions >= ActionModeExecutions ? "Task" : "Action";

        /// <summary>
        /// Last 7 days execution count
        /// </summary>
        [JsonIgnore]
        public int RecentExecutions
        {
            get
            {
                var cutoff = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
                return DailyStats.Where(kvp => string.Compare(kvp.Key, cutoff) >= 0).Sum(kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Today's execution count
        /// </summary>
        [JsonIgnore]
        public int TodayExecutions
        {
            get
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                return DailyStats.TryGetValue(today, out var count) ? count : 0;
            }
        }
    }
}
