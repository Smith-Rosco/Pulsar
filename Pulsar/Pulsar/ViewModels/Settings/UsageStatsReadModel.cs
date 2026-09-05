using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 统计读模型：插件使用统计的只读投影模块。
    /// 一次查询全部统计，在内存快照上按时间范围/排序/升序重投影出展示行、热力图与汇总指标。
    /// ViewModel 只持有绑定集合并转发意图；本模块不依赖 WPF shell。
    /// </summary>
    public class UsageStatsReadModel
    {
        private readonly IPluginUsageTracker _usageTracker;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly ILocalizationService _loc;
        private readonly Func<DateTime> _clock;

        private List<PluginUsageStats> _allPluginStats = new();
        private Dictionary<string, string> _displayNames = new();

        public UsageStatsReadModel(
            IPluginUsageTracker usageTracker,
            IPluginRegistry pluginRegistry,
            ILocalizationService localizationService,
            Func<DateTime>? clock = null)
        {
            _usageTracker = usageTracker;
            _pluginRegistry = pluginRegistry;
            _loc = localizationService;
            _clock = clock ?? (() => DateTime.Now);
        }

        /// <summary>
        /// 从 tracker 与 registry 加载统计快照。切换时间范围/排序在内存重投影，不回查。
        /// </summary>
        public async Task LoadAsync()
        {
            var allStats = await Task.Run(() => _usageTracker.GetAllStats());
            var allPlugins = _pluginRegistry.GetAllPlugins();
            _displayNames = allPlugins.ToDictionary(
                p => p.Id,
                p => PluginLocalization.LocalizePluginName(_loc, p.DisplayName),
                StringComparer.OrdinalIgnoreCase);
            _allPluginStats = allStats.Values.ToList();
        }

        /// <summary>
        /// 在内存快照上按时间范围过滤、排序、重排 rank，并聚合热力图与汇总指标。
        /// 时间口径：Today=今天零点起；ThisWeek=本周一零点起；ThisMonth=本月 1 号零点起（自然周/月，非滚动窗口）。
        /// </summary>
        public AnalyticsProjection Project(AnalyticsTimeRange range, SortColumn sort, bool ascending)
        {
            var now = _clock();
            var cutoff = range switch
            {
                AnalyticsTimeRange.Today => now.Date,
                AnalyticsTimeRange.ThisWeek => StartOfWeek(now),
                AnalyticsTimeRange.ThisMonth => new DateTime(now.Year, now.Month, 1),
                _ => DateTime.MinValue
            };

            var filteredStats = _allPluginStats
                .Where(s => s.TotalExecutions > 0)
                .Select(s => new
                {
                    Stats = s,
                    FilteredExecutions = range == AnalyticsTimeRange.AllTime
                        ? s.TotalExecutions
                        : SumDailySince(s.DailyStats, cutoff)
                })
                .Where(x => x.FilteredExecutions > 0)
                .OrderByDescending(x => x.FilteredExecutions)
                .ToList();

            var activeStats = filteredStats.Select(x => x.Stats).ToList();
            var rows = filteredStats.Select(x => BuildRow(x.Stats, x.FilteredExecutions)).ToList();
            rows = ApplySort(rows, sort, ascending);

            var slotHeatmap = BuildSlotHeatmap(activeStats, cutoff);
            var hourlyHeatmap = BuildHourlyHeatmap(activeStats, cutoff);

            var todayKey = now.ToString("yyyy-MM-dd");

            return new AnalyticsProjection
            {
                Rows = rows,
                SlotHeatmap = slotHeatmap,
                HourlyHeatmap = hourlyHeatmap,
                HasData = rows.Count > 0,
                HasHeatmap = slotHeatmap.Count > 0,
                HasHourlyHeatmap = hourlyHeatmap.Any(h => h.TotalExecutions > 0),
                TotalOverallExecutions = filteredStats.Sum(x => x.FilteredExecutions),
                ActivePluginCount = filteredStats.Count,
                TotalTodayExecutions = filteredStats.Sum(x => x.Stats.DailyStats.GetValueOrDefault(todayKey)),
                TotalWeekExecutions = filteredStats.Sum(x => SumDailySince(x.Stats.DailyStats, StartOfWeek(now)))
            };
        }

        /// <summary>
        /// 把投影行渲染为 CSV 字符串（纯函数，供导出命令消费）。
        /// </summary>
        public string GenerateCsv(IEnumerable<AnalyticsItem> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Rank,PluginId,DisplayName,TotalExecutions,SuccessRate,AvgDurationMs,FavoriteSlot,PrimaryMode,LastUsed");

            foreach (var item in rows)
            {
                var name = EscapeCsvField(item.DisplayName);
                var lastUsed = item.LastUsed?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                sb.AppendLine($"{item.Rank},{item.PluginId},{name},{item.TotalExecutions},{item.SuccessRate:F1},{item.AverageExecutionTimeMs:F0},{item.FavoriteSlot},{item.PrimaryMode},{lastUsed}");
            }

            return sb.ToString();
        }

        private AnalyticsItem BuildRow(PluginUsageStats stat, int filteredExecutions)
        {
            var displayName = _displayNames.TryGetValue(stat.PluginId, out var name) ? name : stat.PluginId;
            return new AnalyticsItem
            {
                PluginId = stat.PluginId,
                DisplayName = displayName,
                TotalExecutions = filteredExecutions,
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
                TrendData = BuildTrendData(stat),
                TotalFormatted = FormatCount(filteredExecutions),
                TodayFormatted = FormatCount(stat.TodayExecutions),
                RecentFormatted = FormatCount(stat.RecentExecutions),
                DurationFormatted = stat.AverageExecutionTimeMs < 1000
                    ? string.Format(_loc["Settings.Analytics.DurationMs"], $"{stat.AverageExecutionTimeMs:F0}")
                    : string.Format(_loc["Settings.Analytics.DurationS"], $"{stat.AverageExecutionTimeMs / 1000:F1}"),
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
            };
        }

        private static List<AnalyticsItem> ApplySort(List<AnalyticsItem> rows, SortColumn sort, bool ascending)
        {
            var sorted = sort switch
            {
                SortColumn.SuccessRate => ascending
                    ? rows.OrderBy(x => x.SuccessRate).ToList()
                    : rows.OrderByDescending(x => x.SuccessRate).ToList(),
                SortColumn.Duration => ascending
                    ? rows.OrderBy(x => x.AverageExecutionTimeMs).ToList()
                    : rows.OrderByDescending(x => x.AverageExecutionTimeMs).ToList(),
                SortColumn.LastUsed => ascending
                    ? rows.OrderBy(x => x.LastUsed ?? DateTime.MinValue).ToList()
                    : rows.OrderByDescending(x => x.LastUsed ?? DateTime.MinValue).ToList(),
                _ => ascending
                    ? rows.OrderBy(x => x.TotalExecutions).ToList()
                    : rows.OrderByDescending(x => x.TotalExecutions).ToList()
            };

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].Rank = i + 1;
                sorted[i].RankLabel = (i + 1) switch { 1 => "#1", 2 => "#2", 3 => "#3", _ => $"#{i + 1}" };
            }

            return sorted;
        }

        private List<SlotHeatmapItem> BuildSlotHeatmap(IEnumerable<PluginUsageStats> activeStats, DateTime cutoff)
        {
            var aggregatedSlots = new Dictionary<int, (int Total, int Plugins)>();
            foreach (var stat in activeStats)
            {
                foreach (var kv in GetSlotUsage(stat, cutoff))
                {
                    if (!aggregatedSlots.ContainsKey(kv.Key))
                        aggregatedSlots[kv.Key] = (0, 0);
                    aggregatedSlots[kv.Key] = (
                        aggregatedSlots[kv.Key].Total + kv.Value,
                        aggregatedSlots[kv.Key].Plugins + 1);
                }
            }

            var totalAllSlotExecutions = aggregatedSlots.Values.Sum(v => v.Total);
            return aggregatedSlots.OrderBy(kv => kv.Key).Select(kv => new SlotHeatmapItem
            {
                SlotIndex = kv.Key,
                TotalExecutions = kv.Value.Total,
                PluginCount = kv.Value.Plugins,
                Percentage = totalAllSlotExecutions > 0 ? (double)kv.Value.Total / totalAllSlotExecutions * 100.0 : 0
            }).ToList();
        }

        private List<HourlyHeatmapItem> BuildHourlyHeatmap(IEnumerable<PluginUsageStats> activeStats, DateTime cutoff)
        {
            var hourlyData = new Dictionary<int, int>();
            foreach (var stat in activeStats)
            {
                foreach (var kv in GetHourlyUsage(stat, cutoff))
                {
                    if (hourlyData.ContainsKey(kv.Key))
                        hourlyData[kv.Key] += kv.Value;
                    else
                        hourlyData[kv.Key] = kv.Value;
                }
            }

            var maxHourly = hourlyData.Values.Any() ? hourlyData.Values.Max() : 1;
            var result = new List<HourlyHeatmapItem>(24);
            for (int h = 0; h < 24; h++)
            {
                var count = hourlyData.GetValueOrDefault(h, 0);
                result.Add(new HourlyHeatmapItem
                {
                    Hour = h,
                    TotalExecutions = count,
                    Percentage = maxHourly > 0 ? (double)count / maxHourly * 100.0 : 0
                });
            }
            return result;
        }

        /// <summary>
        /// 取某插件在时间范围内的插槽使用分布：AllTime 用全量 <see cref="PluginUsageStats.SlotUsage"/>，
        /// 否则从 <see cref="PluginUsageStats.DailySlotUsage"/> 按日期键过滤聚合。
        /// </summary>
        private static Dictionary<int, int> GetSlotUsage(PluginUsageStats stat, DateTime cutoff)
        {
            if (cutoff == DateTime.MinValue)
            {
                return stat.SlotUsage;
            }

            var cutoffKey = cutoff.ToString("yyyy-MM-dd");
            var result = new Dictionary<int, int>();
            if (stat.DailySlotUsage == null)
            {
                return result;
            }

            foreach (var day in stat.DailySlotUsage)
            {
                if (string.Compare(day.Key, cutoffKey) < 0)
                    continue;
                foreach (var slot in day.Value)
                {
                    result[slot.Key] = result.GetValueOrDefault(slot.Key) + slot.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// 取某插件在时间范围内的小时使用分布：AllTime 用全量 <see cref="PluginUsageStats.HourlyUsage"/>，
        /// 否则从 <see cref="PluginUsageStats.DailyHourlyUsage"/> 按日期键过滤聚合。
        /// </summary>
        private static Dictionary<int, int> GetHourlyUsage(PluginUsageStats stat, DateTime cutoff)
        {
            if (cutoff == DateTime.MinValue)
            {
                return stat.HourlyUsage;
            }

            var cutoffKey = cutoff.ToString("yyyy-MM-dd");
            var result = new Dictionary<int, int>();
            if (stat.DailyHourlyUsage == null)
            {
                return result;
            }

            foreach (var day in stat.DailyHourlyUsage)
            {
                if (string.Compare(day.Key, cutoffKey) < 0)
                    continue;
                foreach (var hour in day.Value)
                {
                    result[hour.Key] = result.GetValueOrDefault(hour.Key) + hour.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// 统计 DailyStats 中日期键 >= cutoff 的执行次数总和。
        /// </summary>
        private static int SumDailySince(Dictionary<string, int> dailyStats, DateTime cutoff)
        {
            if (dailyStats == null || dailyStats.Count == 0)
            {
                return 0;
            }

            var cutoffKey = cutoff.ToString("yyyy-MM-dd");
            return dailyStats
                .Where(kvp => string.Compare(kvp.Key, cutoffKey) >= 0)
                .Sum(kvp => kvp.Value);
        }

        /// <summary>
        /// 本周一零点（自然周起点，周一为一周第一天）。
        /// </summary>
        private static DateTime StartOfWeek(DateTime now)
        {
            var daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
            return now.Date.AddDays(-daysSinceMonday);
        }

        private List<DailyTrendItem> BuildTrendData(PluginUsageStats stat)
        {
            var now = _clock();
            var entries = new List<(DateTime Date, int Count)>();
            for (int i = 6; i >= 0; i--)
            {
                var key = now.AddDays(-i).ToString("yyyy-MM-dd");
                var count = stat.DailyStats.TryGetValue(key, out var c) ? c : 0;
                entries.Add((now.AddDays(-i), count));
            }
            var maxCount = entries.Any() ? entries.Max(e => e.Count) : 1;
            return entries.Select(e => new DailyTrendItem
            {
                Date = e.Date.ToString("MM-dd"),
                Count = e.Count,
                MaxCount = maxCount
            }).ToList();
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

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }

    /// <summary>
    /// 读模型一次投影的完整输出：展示行 + 热力图 + 汇总指标。
    /// </summary>
    public class AnalyticsProjection
    {
        public List<AnalyticsItem> Rows { get; init; } = new();
        public List<SlotHeatmapItem> SlotHeatmap { get; init; } = new();
        public List<HourlyHeatmapItem> HourlyHeatmap { get; init; } = new();
        public bool HasData { get; init; }
        public bool HasHeatmap { get; init; }
        public bool HasHourlyHeatmap { get; init; }
        public int TotalOverallExecutions { get; init; }
        public int ActivePluginCount { get; init; }
        public int TotalTodayExecutions { get; init; }
        public int TotalWeekExecutions { get; init; }
    }
}
