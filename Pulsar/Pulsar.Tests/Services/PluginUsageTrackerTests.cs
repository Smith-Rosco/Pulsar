using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;

namespace Pulsar.Tests.Services
{
    public class PluginUsageTrackerTests : IDisposable
    {
        private readonly Mock<ILogger<PluginUsageTracker>> _loggerMock = new();
        private readonly string _testFilePath;

        public PluginUsageTrackerTests()
        {
            _testFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "PluginUsageStats.json");
            CleanupTestFile();
        }

        public void Dispose()
        {
            CleanupTestFile();
        }

        private void CleanupTestFile()
        {
            try { if (File.Exists(_testFilePath)) File.Delete(_testFilePath); }
            catch { /* ignore */ }
        }

        [Fact]
        public void RecordExecution_SingleCall_UpdatesAllFields()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            tracker.RecordExecution("test.plugin.1", true, 150, "default", 2, "Task");

            var stats = tracker.GetStats("test.plugin.1");

            stats.PluginId.Should().Be("test.plugin.1");
            stats.TotalExecutions.Should().Be(1);
            stats.SuccessCount.Should().Be(1);
            stats.FailureCount.Should().Be(0);
            stats.AverageExecutionTimeMs.Should().Be(150);
            stats.TotalExecutionTimeMs.Should().Be(150);
            stats.LastUsed.Should().NotBeNull();
            stats.FirstUsed.Should().NotBeNull();

            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            stats.DailyStats.Should().ContainKey(todayKey);
            stats.DailyStats[todayKey].Should().Be(1);

            stats.SlotUsage.Should().ContainKey(2);
            stats.SlotUsage[2].Should().Be(1);
            stats.TaskModeExecutions.Should().Be(1);
            stats.ActionModeExecutions.Should().Be(0);

            var currentHour = DateTime.UtcNow.Hour;
            stats.HourlyUsage.Should().ContainKey(currentHour);
            stats.HourlyUsage[currentHour].Should().Be(1);
        }

        [Fact]
        public void RecordExecution_Failure_IncrementsFailureCount()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            tracker.RecordExecution("test.plugin.2", false, 200, "default", 1, "Action");

            var stats = tracker.GetStats("test.plugin.2");

            stats.TotalExecutions.Should().Be(1);
            stats.SuccessCount.Should().Be(0);
            stats.FailureCount.Should().Be(1);
            stats.ActionModeExecutions.Should().Be(1);
            stats.TaskModeExecutions.Should().Be(0);
        }

        [Fact]
        public async Task ConcurrentRecording_IsThreadSafe()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            const int ThreadCount = 4;
            const int CallsPerThread = 100;
            var tasks = new Task[ThreadCount];

            for (int t = 0; t < ThreadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < CallsPerThread; i++)
                    {
                        tracker.RecordExecution("conc.plugin", true, 10, "profile");
                    }
                });
            }

            await Task.WhenAll(tasks);

            var stats = tracker.GetStats("conc.plugin");
            stats.TotalExecutions.Should().Be(ThreadCount * CallsPerThread);
            stats.SuccessCount.Should().Be(ThreadCount * CallsPerThread);
            stats.FailureCount.Should().Be(0);
        }

        [Fact]
        public void GetStats_ReturnsClonedCopy_DoesNotMutateInternalState()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            tracker.RecordExecution("clone.test", true, 100, "default", 1, "Task");

            var clonedStats = tracker.GetStats("clone.test");
            clonedStats.TotalExecutions = 999;
            clonedStats.PluginId = "mutated.id";

            var originalStats = tracker.GetStats("clone.test");
            originalStats.TotalExecutions.Should().Be(1);
            originalStats.PluginId.Should().Be("clone.test");
        }

        [Fact]
        public void GetAllStats_ReturnsAllPlugins()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            tracker.RecordExecution("plugin.a", true, 10);
            tracker.RecordExecution("plugin.b", true, 20);
            tracker.RecordExecution("plugin.c", true, 30);

            var allStats = tracker.GetAllStats();

            allStats.Should().ContainKeys("plugin.a", "plugin.b", "plugin.c");
            allStats.Count.Should().Be(3);
        }

        [Fact]
        public void GetMostUsedPlugins_ReturnsTopN_SortedByTotalExecutions()
        {
            using var tracker = new PluginUsageTracker(_loggerMock.Object);
            for (int i = 1; i <= 10; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    tracker.RecordExecution($"plugin.{i}", true, 10);
                }
            }

            var top5 = tracker.GetMostUsedPlugins(5);

            top5.Should().HaveCount(5);
            top5[0].TotalExecutions.Should().Be(10);
            top5[0].PluginId.Should().Be("plugin.10");
            top5[4].TotalExecutions.Should().Be(6);
            top5[4].PluginId.Should().Be("plugin.6");
        }

        [Fact]
        public async Task GetUnusedPlugins_FiltersByLastUsedThreshold()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, "PluginUsageStats.json");

            var preloadedStats = new List<PluginUsageStats>
            {
                new PluginUsageStats
                {
                    PluginId = "used.recently",
                    TotalExecutions = 10,
                    LastUsed = DateTime.UtcNow
                },
                new PluginUsageStats
                {
                    PluginId = "never.used",
                    TotalExecutions = 5,
                    LastUsed = DateTime.UtcNow.AddDays(-60)
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(preloadedStats,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            await File.WriteAllTextAsync(filePath, json);

            try
            {
                using var tracker = new PluginUsageTracker(_loggerMock.Object);
                await tracker.LoadAsync();

                var unused = tracker.GetUnusedPlugins(30);

                unused.Should().Contain("never.used");
                unused.Should().NotContain("used.recently");
            }
            finally
            {
                CleanupTestFile();
            }
        }

        [Fact]
        public async Task SaveAsync_LoadAsync_Roundtrip_PreservesAllData()
        {
            CleanupTestFile();
            using (var tracker = new PluginUsageTracker(_loggerMock.Object))
            {
                tracker.RecordExecution("roundtrip.test", true, 150, "profile", 2, "Task");
                tracker.RecordExecution("roundtrip.test", false, 200, "profile", 1, "Action");
                await tracker.SaveAsync();
            }

            using (var tracker2 = new PluginUsageTracker(_loggerMock.Object))
            {
                await tracker2.LoadAsync();
                var stats = tracker2.GetStats("roundtrip.test");

                stats.TotalExecutions.Should().Be(2);
                stats.SuccessCount.Should().Be(1);
                stats.FailureCount.Should().Be(1);
                stats.TotalExecutionTimeMs.Should().Be(350);
                stats.TaskModeExecutions.Should().Be(1);
                stats.ActionModeExecutions.Should().Be(1);
                stats.SlotUsage.Should().ContainKey(2);
                stats.SlotUsage.Should().ContainKey(1);
            }
        }

        [Fact]
        public async Task CleanupDailyStats_RemovesEntriesOlderThan30Days()
        {
            var oldDate = DateTime.UtcNow.AddDays(-35).ToString("yyyy-MM-dd");
            var recentDate = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd");
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var preloadedStats = new PluginUsageStats
            {
                PluginId = "cleanup.test",
                TotalExecutions = 5,
                DailyStats = new Dictionary<string, int>
                {
                    { oldDate, 5 },
                    { recentDate, 3 }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(
                new List<PluginUsageStats> { preloadedStats },
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(
                Path.Combine(dir, "PluginUsageStats.json"), json);

            try
            {
                using var tracker = new PluginUsageTracker(_loggerMock.Object);
                await tracker.LoadAsync();
                tracker.RecordExecution("cleanup.test", true, 10);

                var result = tracker.GetStats("cleanup.test");
                result.DailyStats.Should().NotContainKey(oldDate);
                result.DailyStats.Should().ContainKey(recentDate);
                result.DailyStats.Should().ContainKey(today);
            }
            finally
            {
                CleanupTestFile();
            }
        }

        [Fact]
        public async Task SaveAsync_PersistsData_Reloadable()
        {
            CleanupTestFile();
            using (var tracker = new PluginUsageTracker(_loggerMock.Object))
            {
                tracker.RecordExecution("persist.test", true, 50);
                await tracker.SaveAsync();
            }

            using (var tracker2 = new PluginUsageTracker(_loggerMock.Object))
            {
                await tracker2.LoadAsync();
                var stats = tracker2.GetStats("persist.test");
                stats.TotalExecutions.Should().Be(1);
            }
        }
    }
}
