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

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// 插件使用详情（分析页下钻）对话框 VM 的单测。
    ///
    /// 该 VM 是纯投影：数据全部来自已构建的 <see cref="AnalyticsItem"/> 内存快照，
    /// 静态元数据经 <see cref="IPluginRegistry.GetDescriptor"/> 补全。故测试重点是
    /// 「格式化/聚合/降级」而非 IO。
    /// </summary>
    public class PluginAnalyticsDetailViewModelTests
    {
        private readonly ILocalizationService _loc;

        public PluginAnalyticsDetailViewModelTests()
        {
            _loc = new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
        }

        private static AnalyticsItem CreateItem(
            string pluginId = "plugin.a",
            string displayName = "Plugin A",
            Dictionary<int, int>? slotUsage = null,
            List<DailyTrendItem>? trend = null,
            int taskModeCount = 0,
            int actionModeCount = 0,
            string modeSummary = "",
            double successRate = 100.0,
            string successRateColor = "Green")
        {
            return new AnalyticsItem
            {
                PluginId = pluginId,
                DisplayName = displayName,
                TotalExecutions = 42,
                SuccessRate = successRate,
                SuccessRateColor = successRateColor,
                TaskModeCount = taskModeCount,
                ActionModeCount = actionModeCount,
                ModeSummary = modeSummary,
                TotalFormatted = "42",
                DurationFormatted = "100 ms",
                LastUsedFormatted = "2026-09-10",
                SlotUsage = slotUsage ?? new Dictionary<int, int>(),
                TrendData = trend ?? new List<DailyTrendItem>()
            };
        }

        private static PluginDescriptor CreateDescriptor(string id, string icon = "sparkle")
        {
            return new PluginDescriptor
            {
                Id = id,
                DisplayName = "Plugin A",
                Version = "2.3.4",
                Author = "Nova",
                Description = "A test plugin",
                Icon = icon,
                CanDisable = true,
                Tier = PluginTier.Extension,
                Dependencies = Array.Empty<string>(),
                Metadata = new Pulsar.Core.Plugin.Metadata.PluginMetadata
                {
                    Id = id,
                    Display = new Pulsar.Core.Plugin.Metadata.DisplayInfo
                    {
                        Name = "Plugin A",
                        Description = "A test plugin",
                        IconKey = icon,
                        Category = "Tests",
                        Version = "2.3.4",
                        Author = "Nova",
                        License = "MIT"
                    },
                    Schema = null,
                    UI = new Pulsar.Core.Plugin.Metadata.UIHints
                    {
                        Badge = "Test",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 0
                    },
                    Capabilities = new Pulsar.Core.Plugin.Metadata.PluginCapabilities()
                },
                IsConfigurable = false
            };
        }

        // ---- 头部映射 ----

        [Fact]
        public void Constructor_MapsIdentityAndFormattedMetrics()
        {
            var item = CreateItem(successRate: 87.5, successRateColor: "Orange");

            var vm = new PluginAnalyticsDetailViewModel(item);

            vm.PluginId.Should().Be("plugin.a");
            vm.DisplayName.Should().Be("Plugin A");
            vm.TotalFormatted.Should().Be("42");
            vm.DurationFormatted.Should().Be("100 ms");
            vm.LastUsedFormatted.Should().Be("2026-09-10");
            vm.SuccessRateFormatted.Should().Be("87.5%");
            vm.SuccessRateColor.Should().Be("Orange");
        }

        [Fact]
        public void Constructor_WithoutRegistry_LeavesMetadataHidden()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());

            vm.HasMetadata.Should().BeFalse();
            vm.IconGlyph.Should().BeEmpty();
            vm.VersionText.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_WithDescriptor_PopulatesMetadata()
        {
            var registryMock = new Mock<IPluginRegistry>();
            registryMock.Setup(r => r.GetDescriptor("plugin.a")).Returns(CreateDescriptor("plugin.a"));

            var vm = new PluginAnalyticsDetailViewModel(
                CreateItem(),
                localization: _loc,
                pluginRegistry: registryMock.Object);

            vm.HasMetadata.Should().BeTrue();
            vm.VersionText.Should().Be("2.3.4");
            vm.AuthorText.Should().Be("Nova");
            vm.Description.Should().Be("A test plugin");
            vm.TierText.Should().Be(nameof(PluginTier.Extension));
            vm.IconGlyph.Should().NotBeEmpty();
        }

        [Fact]
        public void Constructor_RegistryReturnsNullDescriptor_LeavesMetadataHidden()
        {
            var registryMock = new Mock<IPluginRegistry>();
            registryMock.Setup(r => r.GetDescriptor(It.IsAny<string>())).Returns((PluginDescriptor?)null);

            var vm = new PluginAnalyticsDetailViewModel(
                CreateItem(),
                localization: _loc,
                pluginRegistry: registryMock.Object);

            vm.HasMetadata.Should().BeFalse();
        }

        // ---- 槽位分布 ----

        [Fact]
        public void SlotUsage_IsOrderedBySlotIndex_AndShareIsPercentOfTotal()
        {
            var item = CreateItem(slotUsage: new Dictionary<int, int>
            {
                { 3, 10 },
                { 1, 30 },
                { 2, 60 }
            });

            var vm = new PluginAnalyticsDetailViewModel(item);

            vm.HasSlotUsage.Should().BeTrue();
            vm.SlotUsage.Should().HaveCount(3);
            vm.SlotUsage[0].SlotIndex.Should().Be(1);
            vm.SlotUsage[1].SlotIndex.Should().Be(2);
            vm.SlotUsage[2].SlotIndex.Should().Be(3);
            vm.SlotUsage[0].Count.Should().Be(30);
            vm.SlotUsage[0].CountFormatted.Should().Be("30");
            // 占比以 item.TotalExecutions（CreateItem 固定 42）为分母，
            // 而非 SlotUsage 各行之和——保持与分析页表格同源。
            vm.SlotUsage[0].ShareFormatted.Should().Be("71%");
            vm.SlotUsage[1].ShareFormatted.Should().Be("143%");
        }

        [Fact]
        public void SlotUsage_Empty_ReportsNoUsage()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());

            vm.HasSlotUsage.Should().BeFalse();
            vm.SlotUsage.Should().BeEmpty();
        }

        [Fact]
        public void SlotUsageRow_WithZeroTotal_DoesNotDivideByZero()
        {
            var row = new SlotUsageRow(1, 0, 0);

            row.ShareFormatted.Should().Be("0%");
        }

        // ---- 模式分布 ----

        [Fact]
        public void ModeSplit_ReflectsCounts()
        {
            var vm = new PluginAnalyticsDetailViewModel(
                CreateItem(taskModeCount: 7, actionModeCount: 3, modeSummary: "Task 70%"));

            vm.TaskModeCount.Should().Be(7);
            vm.ActionModeCount.Should().Be(3);
            vm.ModeSummary.Should().Be("Task 70%");
            vm.HasModeSplit.Should().BeTrue();
        }

        [Fact]
        public void ModeSplit_AllZero_ReportsNoSplit()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());

            vm.HasModeSplit.Should().BeFalse();
        }

        // ---- 趋势 ----

        [Fact]
        public void Trend_WithData_SummarizesTotalExecutions()
        {
            var trend = new List<DailyTrendItem>
            {
                new() { Date = "2026-09-09", Count = 4, MaxCount = 4 },
                new() { Date = "2026-09-10", Count = 6, MaxCount = 4 }
            };

            var vm = new PluginAnalyticsDetailViewModel(CreateItem(trend: trend), localization: _loc);

            vm.HasTrend.Should().BeTrue();
            vm.TrendData.Should().HaveCount(2);
            vm.TrendSummary.Should().Contain("10");
        }

        [Fact]
        public void Trend_AllZeroCounts_LeavesSummaryEmpty()
        {
            var trend = new List<DailyTrendItem>
            {
                new() { Date = "2026-09-09", Count = 0, MaxCount = 0 },
                new() { Date = "2026-09-10", Count = 0, MaxCount = 0 }
            };

            var vm = new PluginAnalyticsDetailViewModel(CreateItem(trend: trend), localization: _loc);

            vm.HasTrend.Should().BeTrue();
            vm.TrendSummary.Should().BeEmpty();
        }

        [Fact]
        public void Trend_Empty_ReportsNoTrend()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem(), localization: _loc);

            vm.HasTrend.Should().BeFalse();
            vm.TrendSummary.Should().BeEmpty();
        }

        [Fact]
        public void Trend_WithoutLocalization_UsesFallbackTemplate()
        {
            var trend = new List<DailyTrendItem>
            {
                new() { Date = "2026-09-10", Count = 3, MaxCount = 3 }
            };

            var vm = new PluginAnalyticsDetailViewModel(CreateItem(trend: trend));

            vm.TrendSummary.Should().Be("Executions in last 7 days: 3");
        }

        // ---- 推荐 ----

        [Fact]
        public void Recommendations_FromEngine_AreProjected()
        {
            var engineMock = new Mock<IPluginRecommendationEngine>();
            engineMock
                .Setup(e => e.GetRecommendationsForPlugin("plugin.a"))
                .Returns(new List<PluginRecommendation>
                {
                    new() { PluginId = "plugin.a", Title = "Tip", Message = "Use a hotkey" }
                });

            var vm = new PluginAnalyticsDetailViewModel(
                CreateItem(),
                localization: _loc,
                recommendationEngine: engineMock.Object);

            vm.HasRecommendations.Should().BeTrue();
            vm.Recommendations.Should().HaveCount(1);
            vm.Recommendations[0].Title.Should().Be("Tip");
        }

        [Fact]
        public void Recommendations_WithoutEngine_AreEmpty()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem(), localization: _loc);

            vm.HasRecommendations.Should().BeFalse();
            vm.Recommendations.Should().BeEmpty();
        }

        // ---- 关闭契约 ----

        [Fact]
        public void CloseCommand_InvokesRequestCloseWithCancelled()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());
            DialogResult? captured = null;
            vm.RequestClose = r => captured = r;

            vm.CloseCommand.Execute(null);

            captured.Should().Be(DialogResult.Cancelled);
        }

        [Fact]
        public void CloseCommand_WithoutRequestClose_DoesNotThrow()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());

            var act = () => vm.CloseCommand.Execute(null);

            act.Should().NotThrow();
        }

        [Fact]
        public async Task CanCloseAsync_AlwaysTrue()
        {
            var vm = new PluginAnalyticsDetailViewModel(CreateItem());

            (await vm.CanCloseAsync(DialogResult.Confirmed)).Should().BeTrue();
            (await vm.CanCloseAsync(DialogResult.Cancelled)).Should().BeTrue();
        }
    }
}
