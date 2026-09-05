using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Tests.ViewModels
{
    public class UsageStatsReadModelTests
    {
        private readonly Mock<IPluginUsageTracker> _usageTrackerMock = new();
        private readonly Mock<IPluginRegistry> _registryMock = new();
        private readonly ILocalizationService _loc;

        public UsageStatsReadModelTests()
        {
            _loc = new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
        }

        private static IPulsarPlugin CreatePlugin(string id, string displayName)
        {
            var mock = new Mock<IPulsarPlugin>();
            mock.Setup(p => p.Id).Returns(id);
            mock.Setup(p => p.DisplayName).Returns(displayName);
            return mock.Object;
        }

        private static PluginUsageStats CreateStats(string id, int totalExecs,
            int today = 0, int recent = 0, double avgTime = 100,
            double successRate = 100, int favoriteSlot = 1,
            string primaryMode = "Task", int taskMode = 10,
            int actionMode = 5, DateTime? lastUsed = null)
        {
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            var dailyStats = new Dictionary<string, int>();
            if (today > 0) dailyStats[todayKey] = today;
            if (recent > 0 && !dailyStats.ContainsKey(todayKey)) dailyStats[todayKey] = recent;

            return new PluginUsageStats
            {
                PluginId = id,
                TotalExecutions = totalExecs,
                SuccessCount = (int)(totalExecs * successRate / 100),
                FailureCount = totalExecs - (int)(totalExecs * successRate / 100),
                AverageExecutionTimeMs = avgTime,
                TaskModeExecutions = taskMode,
                ActionModeExecutions = actionMode,
                LastUsed = lastUsed ?? DateTime.UtcNow,
                SlotUsage = new Dictionary<int, int> { { favoriteSlot, totalExecs } },
                HourlyUsage = new Dictionary<int, int> { { DateTime.Now.Hour, totalExecs } },
                DailyStats = dailyStats
            };
        }

        private UsageStatsReadModel CreateReadModel(List<IPulsarPlugin> plugins, Dictionary<string, PluginUsageStats> allStats, DateTime? clock = null)
        {
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);
            return new UsageStatsReadModel(_usageTrackerMock.Object, _registryMock.Object, _loc, clock: clock == null ? null : () => clock.Value);
        }

        [Fact]
        public async Task LoadAsync_ThenProject_PopulatesRows_FromTrackerData()
        {
            var plugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.a", "Plugin A"),
                CreatePlugin("plugin.b", "Plugin B"),
                CreatePlugin("plugin.c", "Plugin C")
            };

            var stats = new List<PluginUsageStats>
            {
                CreateStats("plugin.a", 100),
                CreateStats("plugin.b", 50),
                CreateStats("plugin.c", 25)
            };

            var readModel = CreateReadModel(plugins, stats.ToDictionary(s => s.PluginId));
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);

            projection.Rows.Should().HaveCount(3);
            projection.Rows[0].PluginId.Should().Be("plugin.a");
            projection.Rows[0].DisplayName.Should().Be("Plugin A");
            projection.Rows[0].Rank.Should().Be(1);
            projection.Rows[0].TotalExecutions.Should().Be(100);
            projection.HasData.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_ThenProject_PopulatesSlotAndHourlyHeatmaps()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stats = new List<PluginUsageStats> { CreateStats("plugin.a", 100, favoriteSlot: 2) };

            var readModel = CreateReadModel(plugins, stats.ToDictionary(s => s.PluginId));
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);

            projection.SlotHeatmap.Should().NotBeEmpty();
            projection.HasHeatmap.Should().BeTrue();
            projection.HourlyHeatmap.Should().HaveCount(24);
            projection.HasHourlyHeatmap.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_ThenProject_ComputesSummaryMetrics()
        {
            // 固定时钟为周三 2026-09-02，让"自然周"口径断言确定性（周一 08-31 为一周起点）。
            var clock = new DateTime(2026, 9, 2, 10, 30, 0);
            var todayKey = "2026-09-02";
            var day2Key = "2026-09-01";
            var day3Key = "2026-08-31";

            var plugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.a", "Plugin A"),
                CreatePlugin("plugin.b", "Plugin B")
            };

            var statA = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 100,
                SuccessCount = 100,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { todayKey, 10 },
                    { day2Key, 10 },
                    { day3Key, 10 }
                },
                SlotUsage = new Dictionary<int, int> { { 1, 100 } },
                HourlyUsage = new Dictionary<int, int> { { 9, 100 } }
            };

            var statB = new PluginUsageStats
            {
                PluginId = "plugin.b",
                TotalExecutions = 50,
                SuccessCount = 50,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { todayKey, 5 },
                    { day2Key, 5 },
                    { day3Key, 5 }
                },
                SlotUsage = new Dictionary<int, int> { { 2, 50 } },
                HourlyUsage = new Dictionary<int, int> { { 9, 50 } }
            };

            var allStats = new Dictionary<string, PluginUsageStats> { { statA.PluginId, statA }, { statB.PluginId, statB } };

            var readModel = CreateReadModel(plugins, allStats, clock);
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);

            projection.TotalOverallExecutions.Should().Be(150);
            projection.ActivePluginCount.Should().Be(2);
            projection.TotalTodayExecutions.Should().Be(15);
            projection.TotalWeekExecutions.Should().Be(45);
        }

        [Fact]
        public async Task LoadAsync_ThenProject_HandlesEmptyData_Gracefully()
        {
            var readModel = CreateReadModel(new List<IPulsarPlugin>(), new Dictionary<string, PluginUsageStats>());
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);

            projection.HasData.Should().BeFalse();
            projection.Rows.Should().BeEmpty();
        }

        [Fact]
        public async Task Project_FiltersByTimeRange_UsingDailyStats()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            var oldKey = DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd");

            var stat = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 30,
                SuccessCount = 30,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { todayKey, 5 },
                    { oldKey, 25 }
                },
                SlotUsage = new Dictionary<int, int> { { 1, 30 } },
                HourlyUsage = new Dictionary<int, int> { { DateTime.Now.Hour, 30 } }
            };

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } });
            await readModel.LoadAsync();

            var allTime = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);
            allTime.Rows.Should().HaveCount(1);
            allTime.Rows[0].TotalExecutions.Should().Be(30);

            var today = readModel.Project(AnalyticsTimeRange.Today, SortColumn.Executions, ascending: false);
            today.Rows.Should().HaveCount(1);
            today.Rows[0].TotalExecutions.Should().Be(5);
        }

        [Fact]
        public async Task Project_SortsByColumn_AndRenumbersRanks()
        {
            var plugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.a", "Plugin A"),
                CreatePlugin("plugin.b", "Plugin B")
            };

            var statA = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 100,
                SuccessCount = 90,
                AverageExecutionTimeMs = 500,
                LastUsed = DateTime.UtcNow.AddDays(-5),
                SlotUsage = new Dictionary<int, int> { { 1, 100 } },
                HourlyUsage = new Dictionary<int, int> { { DateTime.Now.Hour, 100 } },
                DailyStats = new Dictionary<string, int> { { DateTime.Now.ToString("yyyy-MM-dd"), 100 } }
            };

            var statB = new PluginUsageStats
            {
                PluginId = "plugin.b",
                TotalExecutions = 50,
                SuccessCount = 10,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                SlotUsage = new Dictionary<int, int> { { 1, 50 } },
                HourlyUsage = new Dictionary<int, int> { { DateTime.Now.Hour, 50 } },
                DailyStats = new Dictionary<string, int> { { DateTime.Now.ToString("yyyy-MM-dd"), 50 } }
            };

            var allStats = new Dictionary<string, PluginUsageStats> { { statA.PluginId, statA }, { statB.PluginId, statB } };

            var readModel = CreateReadModel(plugins, allStats);
            await readModel.LoadAsync();

            var byDuration = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Duration, ascending: true);
            byDuration.Rows.Select(r => r.PluginId).Should().ContainInOrder("plugin.b", "plugin.a");
            byDuration.Rows[0].Rank.Should().Be(1);
            byDuration.Rows[1].Rank.Should().Be(2);
            byDuration.Rows[1].RankLabel.Should().Be("#2");

            var bySuccess = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.SuccessRate, ascending: false);
            bySuccess.Rows.Select(r => r.PluginId).Should().ContainInOrder("plugin.a", "plugin.b");
        }

        [Fact]
        public async Task Project_FormatsRows_WithLocalizedStrings()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = CreateStats("plugin.a", 1500, today: 5, recent: 5, avgTime: 800);

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } });
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);

            projection.Rows[0].TotalFormatted.Should().Be("1.5K");
            projection.Rows[0].RankLabel.Should().Be("#1");
            projection.Rows[0].SuccessRateColor.Should().Be("Green");
            projection.Rows[0].DurationFormatted.Should().NotBeNullOrEmpty();
            projection.Rows[0].ModeSummary.Should().NotBeNullOrEmpty();
            projection.Rows[0].LastUsedFormatted.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GenerateCsv_ProducesHeaderAndRows()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = CreateStats("plugin.a", 100);

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } });
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);
            var csv = readModel.GenerateCsv(projection.Rows);

            csv.Should().StartWith("Rank,PluginId,DisplayName,TotalExecutions,SuccessRate,AvgDurationMs,FavoriteSlot,PrimaryMode,LastUsed");
            csv.Should().Contain("plugin.a");
        }

        [Fact]
        public async Task Project_ThisWeek_UsesNaturalWeekWindow()
        {
            // 固定时钟：周三 2026-09-02。自然周起点为周一 2026-08-31。
            var clock = new DateTime(2026, 9, 2, 12, 0, 0);
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 20,
                SuccessCount = 20,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { "2026-09-02", 5 },
                    { "2026-08-31", 5 },
                    { "2026-08-30", 10 } // 上周日，不在自然周内
                },
                SlotUsage = new Dictionary<int, int> { { 1, 20 } },
                HourlyUsage = new Dictionary<int, int> { { 9, 20 } }
            };

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } }, clock);
            await readModel.LoadAsync();

            var thisWeek = readModel.Project(AnalyticsTimeRange.ThisWeek, SortColumn.Executions, ascending: false);
            thisWeek.Rows.Should().HaveCount(1);
            thisWeek.Rows[0].TotalExecutions.Should().Be(10); // 09-02 + 08-31，排除 08-30
            thisWeek.TotalOverallExecutions.Should().Be(10);

            var allTime = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);
            allTime.Rows[0].TotalExecutions.Should().Be(20);
        }

        [Fact]
        public async Task Project_ThisMonth_UsesNaturalMonthWindow()
        {
            var clock = new DateTime(2026, 9, 2, 12, 0, 0);
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 35,
                SuccessCount = 35,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { "2026-09-01", 5 },
                    { "2026-08-31", 10 }, // 上月，不在自然月内
                    { "2026-07-15", 20 }
                },
                SlotUsage = new Dictionary<int, int> { { 1, 35 } },
                HourlyUsage = new Dictionary<int, int> { { 9, 35 } }
            };

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } }, clock);
            await readModel.LoadAsync();

            var thisMonth = readModel.Project(AnalyticsTimeRange.ThisMonth, SortColumn.Executions, ascending: false);
            thisMonth.Rows.Should().HaveCount(1);
            thisMonth.Rows[0].TotalExecutions.Should().Be(5); // 仅 09-01
        }

        [Fact]
        public async Task Project_HeatmapsAndKPIs_RespectTimeRange()
        {
            var clock = new DateTime(2026, 9, 2, 12, 0, 0);
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 16,
                SuccessCount = 16,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { "2026-09-02", 3 },
                    { "2026-09-01", 4 },
                    { "2026-08-25", 9 }
                },
                SlotUsage = new Dictionary<int, int> { { 1, 3 }, { 2, 4 }, { 3, 9 } },
                HourlyUsage = new Dictionary<int, int> { { 9, 3 }, { 10, 4 }, { 11, 9 } },
                DailySlotUsage = new Dictionary<string, Dictionary<int, int>>
                {
                    { "2026-09-02", new Dictionary<int, int> { { 1, 3 } } },
                    { "2026-09-01", new Dictionary<int, int> { { 2, 4 } } },
                    { "2026-08-25", new Dictionary<int, int> { { 3, 9 } } }
                },
                DailyHourlyUsage = new Dictionary<string, Dictionary<int, int>>
                {
                    { "2026-09-02", new Dictionary<int, int> { { 9, 3 } } },
                    { "2026-09-01", new Dictionary<int, int> { { 10, 4 } } },
                    { "2026-08-25", new Dictionary<int, int> { { 11, 9 } } }
                }
            };

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } }, clock);
            await readModel.LoadAsync();

            var thisWeek = readModel.Project(AnalyticsTimeRange.ThisWeek, SortColumn.Executions, ascending: false);
            thisWeek.TotalOverallExecutions.Should().Be(7);
            thisWeek.TotalTodayExecutions.Should().Be(3);
            thisWeek.TotalWeekExecutions.Should().Be(7);
            thisWeek.SlotHeatmap.Select(s => s.SlotIndex).Should().Equal(1, 2); // 排除 08-25 的 slot 3
            thisWeek.SlotHeatmap.First(s => s.SlotIndex == 1).TotalExecutions.Should().Be(3);
            thisWeek.SlotHeatmap.First(s => s.SlotIndex == 2).TotalExecutions.Should().Be(4);
            thisWeek.HourlyHeatmap.First(h => h.Hour == 9).TotalExecutions.Should().Be(3);
            thisWeek.HourlyHeatmap.First(h => h.Hour == 10).TotalExecutions.Should().Be(4);
            thisWeek.HourlyHeatmap.First(h => h.Hour == 11).TotalExecutions.Should().Be(0);

            var allTime = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);
            allTime.SlotHeatmap.Select(s => s.SlotIndex).Should().Equal(1, 2, 3);
            allTime.HourlyHeatmap.First(h => h.Hour == 11).TotalExecutions.Should().Be(9);
        }

        [Fact]
        public async Task Project_TrendData_UsesLocalDateKeys()
        {
            // 本地 2026-09-02 01:30（UTC 仍为 09-01）：若用 UTC 取键会把"今天"错配到昨天。
            var clock = new DateTime(2026, 9, 2, 1, 30, 0);
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var daily = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                daily[clock.AddDays(-i).ToString("yyyy-MM-dd")] = 7 - i;
            }

            var stat = new PluginUsageStats
            {
                PluginId = "plugin.a",
                TotalExecutions = 28,
                SuccessCount = 28,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = daily,
                SlotUsage = new Dictionary<int, int> { { 1, 28 } },
                HourlyUsage = new Dictionary<int, int> { { 1, 28 } }
            };

            var readModel = CreateReadModel(plugins, new Dictionary<string, PluginUsageStats> { { stat.PluginId, stat } }, clock);
            await readModel.LoadAsync();

            var projection = readModel.Project(AnalyticsTimeRange.AllTime, SortColumn.Executions, ascending: false);
            var trend = projection.Rows[0].TrendData;

            trend.Should().HaveCount(7);
            trend[6].Date.Should().Be("09-02");
            trend[6].Count.Should().Be(7); // 今天(本地) 7 次，落在 09-02 键上
            trend[0].Date.Should().Be("08-27");
        }
    }
}
