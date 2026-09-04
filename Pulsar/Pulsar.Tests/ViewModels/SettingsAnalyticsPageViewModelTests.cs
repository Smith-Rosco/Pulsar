using System;
using System.Collections.Generic;
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
        private readonly Mock<IPluginRuntimeOps> _runtimeOpsMock = new();
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

        private void SetupTracker(IEnumerable<IPulsarPlugin> plugins, Dictionary<string, PluginUsageStats> allStats)
        {
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(new List<IPulsarPlugin>(plugins));
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);
        }

        private SettingsAnalyticsPageViewModel CreateViewModel(
            IPluginRecommendationEngine? recommendationEngine = null)
        {
            var readModel = new UsageStatsReadModel(_usageTrackerMock.Object, _registryMock.Object, _loc);
            return new SettingsAnalyticsPageViewModel(
                readModel,
                _runtimeOpsMock.Object,
                _loggerMock.Object,
                _loc,
                recommendationEngine);
        }

        [Fact]
        public async Task LoadAsync_RefreshesData_FromReadModel()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stats = new Dictionary<string, PluginUsageStats>
            {
                { "plugin.a", CreateStats("plugin.a", 100) }
            };
            SetupTracker(plugins, stats);

            var vm = CreateViewModel(_recEngineMock.Object);
            await vm.LoadAsync();

            vm.HasData.Should().BeTrue();
            vm.MostUsedPlugins.Should().HaveCount(1);
            vm.SlotHeatmap.Should().NotBeEmpty();
            vm.HourlyHeatmap.Should().HaveCount(24);
            vm.TotalOverallExecutions.Should().Be(100);
        }

        [Fact]
        public async Task LoadAsync_HandlesEmptyData_Gracefully()
        {
            SetupTracker(new List<IPulsarPlugin>(), new Dictionary<string, PluginUsageStats>());

            var vm = CreateViewModel();
            await vm.LoadAsync();

            vm.HasData.Should().BeFalse();
            vm.MostUsedPlugins.Should().BeEmpty();
            vm.HasError.Should().BeFalse();
        }

        [Fact]
        public async Task LoadAsync_SetsErrorState_OnException()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Throws(new InvalidOperationException("test error"));

            var vm = CreateViewModel();
            await vm.LoadAsync();

            vm.HasError.Should().BeTrue();
            vm.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task TimeRangeChange_RepositionsRows_FromReadModel()
        {
            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stat = CreateStats("plugin.a", 30, today: 5);
            SetupTracker(plugins, new Dictionary<string, PluginUsageStats> { { "plugin.a", stat } });

            var vm = CreateViewModel();
            await vm.LoadAsync();

            vm.TimeRange = AnalyticsTimeRange.Today;
            vm.MostUsedPlugins.Should().HaveCount(1);
            vm.MostUsedPlugins[0].TotalExecutions.Should().Be(5);
        }

        [Fact]
        public async Task RefreshCommand_ReinvokesLoadAsync()
        {
            SetupTracker(new List<IPulsarPlugin>(), new Dictionary<string, PluginUsageStats>());

            var vm = CreateViewModel();
            await vm.LoadAsync();
            vm.HasData.Should().BeFalse();

            var plugins = new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            var stats = new Dictionary<string, PluginUsageStats> { { "plugin.a", CreateStats("plugin.a", 100) } };
            SetupTracker(plugins, stats);

            await vm.RefreshCommand.ExecuteAsync(null);

            vm.HasData.Should().BeTrue();
            vm.MostUsedPlugins.Should().HaveCount(1);
        }

        [Fact]
        public async Task LoadAsync_PopulatesRecommendations_WhenEngineProvided()
        {
            SetupTracker(new List<IPulsarPlugin>(), new Dictionary<string, PluginUsageStats>());

            var rec = new PluginRecommendation
            {
                Type = RecommendationType.DisableUnusedPlugin,
                Title = "Title",
                PluginId = "plugin.a"
            };
            _recEngineMock.Setup(r => r.GetRecommendations()).Returns(new List<PluginRecommendation> { rec });

            var vm = CreateViewModel(_recEngineMock.Object);
            await vm.LoadAsync();

            vm.HasRecommendations.Should().BeTrue();
            vm.Recommendations.Should().HaveCount(1);
        }
    }
}
