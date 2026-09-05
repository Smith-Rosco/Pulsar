using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
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

        private SettingsAnalyticsPageViewModel CreateViewModel(
            Dictionary<string, PluginUsageStats> allStats,
            List<IPulsarPlugin>? plugins = null,
            IPluginLogService? logService = null,
            IDialogService? dialogService = null)
        {
            plugins ??= new List<IPulsarPlugin> { CreatePlugin("plugin.a", "Plugin A") };
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(plugins);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(allStats);

            var readModel = new UsageStatsReadModel(
                _usageTrackerMock.Object,
                _registryMock.Object,
                _loc,
                clock: () => new DateTime(2026, 9, 2, 12, 0, 0));

            return new SettingsAnalyticsPageViewModel(
                readModel,
                _runtimeOpsMock.Object,
                _loggerMock.Object,
                _loc,
                logService: logService,
                dialogService: dialogService);
        }

        private static PluginUsageStats CreateStats(string id, int totalExecs, int todayCount)
        {
            return new PluginUsageStats
            {
                PluginId = id,
                TotalExecutions = totalExecs,
                SuccessCount = totalExecs,
                AverageExecutionTimeMs = 100,
                LastUsed = DateTime.UtcNow,
                DailyStats = new Dictionary<string, int>
                {
                    { "2026-09-02", todayCount },
                    { "2026-09-01", totalExecs - todayCount }
                },
                SlotUsage = new Dictionary<int, int> { { 1, totalExecs } },
                HourlyUsage = new Dictionary<int, int> { { 9, totalExecs } }
            };
        }

        [Fact]
        public async Task LoadAsync_ThenChangingTimeRange_RepositionsRowsAndKpis()
        {
            var allStats = new Dictionary<string, PluginUsageStats>
            {
                { "plugin.a", CreateStats("plugin.a", 100, todayCount: 5) },
                { "plugin.b", CreateStats("plugin.b", 50, todayCount: 0) }
            };

            var vm = CreateViewModel(allStats);
            await vm.LoadAsync();

            vm.MostUsedPlugins.Should().HaveCount(2);
            vm.TotalOverallExecutions.Should().Be(150);

            // 切到"今天"：plugin.b 今日 0 次被过滤，仅剩 plugin.a（今日 5 次）。
            vm.TimeRange = AnalyticsTimeRange.Today;
            vm.MostUsedPlugins.Should().HaveCount(1);
            vm.MostUsedPlugins[0].PluginId.Should().Be("plugin.a");
            vm.TotalOverallExecutions.Should().Be(5);
            vm.HasData.Should().BeTrue();
        }

        [Fact]
        public async Task ViewLogs_WithNoServices_DoesNotThrow()
        {
            var vm = CreateViewModel(
                new Dictionary<string, PluginUsageStats> { { "plugin.a", CreateStats("plugin.a", 10, 5) } });
            await vm.LoadAsync();

            var act = () => vm.ViewLogsCommand.Execute("plugin.a");
            act.Should().NotThrow();
        }

        [Fact]
        public async Task ViewLogs_OpensLogViewerDialog_ForPlugin()
        {
            var logServiceMock = new Mock<IPluginLogService>();
            logServiceMock
                .Setup(l => l.GetLogs(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<PluginLogLevel?>()))
                .Returns(new List<PluginLogEntry>());
            logServiceMock
                .Setup(l => l.GetRecentErrors(It.IsAny<string>(), It.IsAny<int>()))
                .Returns(new List<PluginLogEntry>());

            var dialogServiceMock = new Mock<IDialogService>();
            dialogServiceMock
                .Setup(d => d.ShowCustomAsync(
                    It.IsAny<string>(),
                    It.IsAny<PluginLogViewerViewModel>(),
                    It.IsAny<DialogButtons>(),
                    It.IsAny<DialogSizeConstraints>()))
                .ReturnsAsync(DialogResult.Confirmed);

            var vm = CreateViewModel(
                new Dictionary<string, PluginUsageStats> { { "plugin.a", CreateStats("plugin.a", 10, 5) } },
                logService: logServiceMock.Object,
                dialogService: dialogServiceMock.Object);
            await vm.LoadAsync();

            await vm.ViewLogsCommand.ExecuteAsync("plugin.a");

            dialogServiceMock.Verify(
                d => d.ShowCustomAsync(
                    It.IsAny<string>(),
                    It.Is<PluginLogViewerViewModel>(v => v.PluginName == "Plugin A"),
                    It.IsAny<DialogButtons>(),
                    It.IsAny<DialogSizeConstraints>()),
                Times.Once);
        }
    }
}
