using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels.Settings
{
    /// <summary>
    /// M2 叙事约定（openspec home-screen-entry-reorder）：插件管理列表必须把
    /// 办公三支柱（宏 / 网页脚本 / 安全填写）排在最前分组，系统/工具插件归入殿后分组。
    /// </summary>
    public class PluginManagerViewModelGroupingTests
    {
        private static PluginDescriptor CreateDescriptor(
            string id,
            string displayName,
            bool canDisable)
        {
            return new PluginDescriptor
            {
                Id = id,
                DisplayName = displayName,
                Version = "1.0.0",
                Author = "Pulsar Team",
                Description = $"{displayName} description.",
                Icon = "E8F9",
                CanDisable = canDisable,
                Tier = canDisable ? PluginTier.Extension : PluginTier.Core,
                IsExternal = false,
                ImplementationType = null,
                Dependencies = Array.Empty<string>(),
                Metadata = new PluginMetadata
                {
                    Id = id,
                    Display = new DisplayInfo
                    {
                        Name = displayName,
                        Description = $"{displayName} description.",
                        IconKey = "E8F9",
                        Category = "Productivity"
                    },
                    UI = new UIHints
                    {
                        Badge = canDisable ? "Extension" : "Core",
                        AccentColor = "#32CD32"
                    },
                    Capabilities = new PluginCapabilities
                    {
                        CanDisable = canDisable,
                        Tier = canDisable ? PluginTier.Extension : PluginTier.Core
                    }
                },
                IsConfigurable = false
            };
        }

        private static PluginManagerViewModel CreateViewModel(params PluginDescriptor[] descriptors)
        {
            var loc = new LocalizationService(NullLogger<LocalizationService>.Instance);
            loc.SetLanguage("en");

            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.GetAllPluginDescriptors()).Returns(descriptors);
            registry.Setup(r => r.GetPlugin(It.IsAny<string>())).Returns((IPulsarPlugin?)null);
            registry.Setup(r => r.IsPluginEnabled(It.IsAny<string>())).Returns(true);

            return new PluginManagerViewModel(
                registry.Object,
                new Mock<IPluginRuntimeOps>().Object,
                new Mock<IConfigService>().Object,
                localizationService: loc);
        }

        private static PluginDescriptor[] CreateMixedCatalog() =>
        [
            // 刻意打乱注册顺序，验证排序完全由 WorkbenchPillarCatalog 决定
            CreateDescriptor("com.pulsar.winswitcher", "Window Switcher", canDisable: false),
            CreateDescriptor("com.pulsar.command", "Command Runner", canDisable: false),
            CreateDescriptor("com.pulsar.vbarunner", "Macro Runner", canDisable: true),
            CreateDescriptor("com.pulsar.bookmarklet", "Web Scripts", canDisable: true),
            CreateDescriptor("com.pulsar.pki", "Secure Form Fill", canDisable: false)
        ];

        [Fact]
        public void GroupedPlugins_ShouldLeadWithPillarGroup_AndTrailWithSystemGroup()
        {
            var vm = CreateViewModel(CreateMixedCatalog());

            vm.GroupedPlugins.Select(g => g.GroupId).Should().Equal(
                PluginGroupIds.Pillars,
                PluginGroupIds.System);
        }

        [Fact]
        public void PillarGroup_ShouldContainThreePillars_InNarrativeOrder_RegardlessOfRegistrationOrder()
        {
            var vm = CreateViewModel(CreateMixedCatalog());

            var pillarGroup = vm.GroupedPlugins.Single(g => g.GroupId == PluginGroupIds.Pillars);
            pillarGroup.Plugins.Select(p => p.Id).Should().Equal(
                "com.pulsar.vbarunner",
                "com.pulsar.bookmarklet",
                "com.pulsar.pki");
        }

        [Fact]
        public void SystemGroup_ShouldContainRemainingPlugins_InDeterministicNameOrder()
        {
            var vm = CreateViewModel(CreateMixedCatalog());

            var systemGroup = vm.GroupedPlugins.Single(g => g.GroupId == PluginGroupIds.System);
            systemGroup.Plugins.Select(p => p.Id).Should().Equal(
                "com.pulsar.command",
                "com.pulsar.winswitcher");
        }

        [Fact]
        public void FlatPluginList_ShouldOrderPillarsFirst_WithDeterministicBackgroundOrder()
        {
            var vm = CreateViewModel(CreateMixedCatalog());

            vm.Plugins.Select(p => p.Id).Should().Equal(
                "com.pulsar.vbarunner",
                "com.pulsar.bookmarklet",
                "com.pulsar.pki",
                "com.pulsar.command",
                "com.pulsar.winswitcher");

            vm.SelectedPlugin.Should().NotBeNull();
            vm.SelectedPlugin!.Id.Should().Be("com.pulsar.vbarunner", "默认选中项应落在支柱插件上");
        }

        [Fact]
        public void Grouping_ShouldRespectSearchFilter()
        {
            var vm = CreateViewModel(CreateMixedCatalog());
            vm.SearchText = "Secure";

            // 搜索只命中支柱插件时，应只剩支柱组（空的系统组不应出现）
            vm.GroupedPlugins.Should().ContainSingle("搜索过滤后应只剩命中的分组");
            vm.GroupedPlugins[0].GroupId.Should().Be(PluginGroupIds.Pillars);
            vm.GroupedPlugins[0].Plugins.Select(p => p.Id).Should().Equal("com.pulsar.pki");
        }
    }
}
