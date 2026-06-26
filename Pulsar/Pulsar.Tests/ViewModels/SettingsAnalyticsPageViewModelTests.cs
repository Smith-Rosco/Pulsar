using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class SettingsAnalyticsPageViewModelTests
    {
        private readonly Mock<IPluginUsageTracker> _usageTrackerMock = new();
        private readonly Mock<IPluginRegistry> _registryMock = new();
        private readonly Mock<ILogger<SettingsAnalyticsPageViewModel>> _loggerMock = new();
        private readonly ILocalizationService _loc;
        private readonly Mock<IPluginRecommendationEngine> _recEngineMock = new();

        public SettingsAnalyticsPageViewModelTests()
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
            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
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
                HourlyUsage = new Dictionary<int, int> { { DateTime.UtcNow.Hour, totalExecs } },
                DailyStats = dailyStats
            };
        }

        [Fact]
        public async Task LoadAsync_PopulatesMostUsedPlugins_FromTrackerData()
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

            var allStats = stats.ToDictionary(s => s.PluginId);

            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(stats);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc,
                _recEngineMock.Object);

            await vm.LoadAsync();

            vm.MostUsedPlugins.Should().HaveCount(3);
            vm.MostUsedPlugins[0].PluginId.Should().Be("plugin.a");
            vm.MostUsedPlugins[0].DisplayName.Should().Be("Plugin A");
            vm.MostUsedPlugins[0].Rank.Should().Be(1);
            vm.MostUsedPlugins[0].TotalExecutions.Should().Be(100);
            vm.HasData.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_PopulatesSlotHeatmapAndHourlyHeatmap()
        {
            var plugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.a", "Plugin A")
            };

            var stats = new List<PluginUsageStats>
            {
                CreateStats("plugin.a", 100, favoriteSlot: 2)
            };

            var allStats = stats.ToDictionary(s => s.PluginId);

            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(stats);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc,
                _recEngineMock.Object);

            await vm.LoadAsync();

            vm.SlotHeatmap.Should().NotBeEmpty();
            vm.HasHeatmap.Should().BeTrue();
            vm.HourlyHeatmap.Should().HaveCount(24);
            vm.HasHourlyHeatmap.Should().BeTrue();
        }

        [Fact]
        public async Task LoadAsync_ComputesSummaryMetrics()
        {
            var plugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.a", "Plugin A"),
                CreatePlugin("plugin.b", "Plugin B")
            };

            var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var day2Key = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            var day3Key = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd");

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
                HourlyUsage = new Dictionary<int, int> { { DateTime.UtcNow.Hour, 100 } }
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
                HourlyUsage = new Dictionary<int, int> { { DateTime.UtcNow.Hour, 50 } }
            };

            var stats = new List<PluginUsageStats> { statA, statB };
            var allStats = stats.ToDictionary(s => s.PluginId);

            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(stats);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc,
                _recEngineMock.Object);

            await vm.LoadAsync();

            vm.TotalOverallExecutions.Should().Be(150);
            vm.ActivePluginCount.Should().Be(2);
            vm.TotalTodayExecutions.Should().Be(15);
            vm.TotalWeekExecutions.Should().Be(45);
        }

        [Fact]
        public async Task LoadAsync_HandlesEmptyData_Gracefully()
        {
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(new List<IPulsarPlugin>());
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(new List<PluginUsageStats>());
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>());

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc);

            await vm.LoadAsync();

            vm.HasData.Should().BeFalse();
            vm.MostUsedPlugins.Should().BeEmpty();
            vm.HasError.Should().BeFalse();
        }

        [Fact]
        public async Task LoadAsync_SetsErrorState_OnException()
        {
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Throws(new InvalidOperationException("test error"));

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc);

            await vm.LoadAsync();

            vm.HasError.Should().BeTrue();
            vm.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshCommand_ReinvokesLoadAsync()
        {
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(new List<IPulsarPlugin>());
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(new List<PluginUsageStats>());
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>());

            var vm = new SettingsAnalyticsPageViewModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loggerMock.Object,
                _loc);

            await vm.LoadAsync();
            vm.HasData.Should().BeFalse();

            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stats = new List<PluginUsageStats> { CreateStats("plugin.a", 100) };
            var allStats = stats.ToDictionary(s => s.PluginId);

            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetMostUsedPlugins(20)).Returns(stats);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);

            await vm.RefreshCommand.ExecuteAsync(null);

            vm.HasData.Should().BeTrue();
            vm.MostUsedPlugins.Should().HaveCount(1);
        }
    }
}
