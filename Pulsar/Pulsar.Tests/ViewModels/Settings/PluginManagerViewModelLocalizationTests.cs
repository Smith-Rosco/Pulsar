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
    /// 插件页本地化回归测试：中文设定下，插件卡片展示的名称/描述必须是本地化文本，
    /// 而不是元数据中的英文原文。
    /// </summary>
    public class PluginManagerViewModelLocalizationTests
    {
        private const string TestPluginId = "com.pulsar.winswitcher";

        private static LocalizationService CreateLocalization(string language)
        {
            var loc = new LocalizationService(NullLogger<LocalizationService>.Instance);
            loc.SetLanguage(language);
            return loc;
        }

        private static PluginDescriptor CreateDescriptor()
        {
            return new PluginDescriptor
            {
                Id = TestPluginId,
                DisplayName = "App Switch",
                Version = "1.0.0",
                Author = "Pulsar Team",
                Description = "Switch to an existing app, launch one directly, or switch first and launch only when needed.",
                Icon = "E8F9",
                CanDisable = false,
                Tier = PluginTier.Core,
                ImplementationType = null,
                Dependencies = Array.Empty<string>(),
                Metadata = new PluginMetadata
                {
                    Id = TestPluginId,
                    Display = new DisplayInfo
                    {
                        Name = "App Switch",
                        Description = "Switch to an existing app, launch one directly, or switch first and launch only when needed.",
                        IconKey = "E8F9",
                        Category = "Productivity"
                    },
                    UI = new UIHints
                    {
                        Badge = "Core",
                        AccentColor = "#32CD32"
                    },
                    Capabilities = new PluginCapabilities
                    {
                        CanDisable = false,
                        Tier = PluginTier.Core
                    }
                },
                IsConfigurable = false
            };
        }

        private static (PluginManagerViewModel Vm, ILocalizationService Loc) CreateViewModel(string language)
        {
            var loc = CreateLocalization(language);
            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.GetAllPluginDescriptors()).Returns(new[] { CreateDescriptor() });
            registry.Setup(r => r.GetPlugin(TestPluginId)).Returns((IPulsarPlugin?)null);
            registry.Setup(r => r.IsPluginEnabled(TestPluginId)).Returns(true);

            var configService = new Mock<IConfigService>();
            var vm = new PluginManagerViewModel(
                registry.Object,
                configService.Object,
                localizationService: loc);

            return (vm, loc);
        }

        [Fact]
        public void PluginName_ShouldBeLocalized_UnderChineseSettings()
        {
            var (vm, loc) = CreateViewModel("zh-CN");
            var expected = PluginLocalization.LocalizePluginName(loc, "App Switch");

            vm.Plugins.Single().Name.Should().Be(expected, "插件卡片标题必须经过 PluginLocalization 约定转换");
            expected.Should().Be("应用切换");
        }

        [Fact]
        public void PluginDescription_ShouldBeLocalized_UnderChineseSettings()
        {
            var (vm, loc) = CreateViewModel("zh-CN");
            var expected = PluginLocalization.LocalizePluginDescription(
                loc,
                "Switch to an existing app, launch one directly, or switch first and launch only when needed.",
                "App Switch");

            vm.Plugins.Single().Description.Should().Be(expected, "插件卡片描述必须经过 PluginLocalization 约定转换");
            expected.Should().NotBe("Switch to an existing app, launch one directly, or switch first and launch only when needed.");
        }

        [Fact]
        public void PluginName_ShouldRemainEnglish_UnderEnglishSettings()
        {
            var (vm, _) = CreateViewModel("en");

            vm.Plugins.Single().Name.Should().Be("App Switch");
        }

        [Fact]
        public void PluginCategory_ShouldBeLocalized_UnderChineseSettings()
        {
            var (vm, _) = CreateViewModel("zh-CN");

            vm.Plugins.Single().Category.Should().Be("生产力");
        }

        [Fact]
        public void HealthStatusText_ShouldBeLocalized_UnderChineseSettings()
        {
            var (vm, _) = CreateViewModel("zh-CN");

            // 测试未注入健康监控，HealthReport.Status 为默认值 Healthy
            vm.Plugins.Single().HealthStatusText.Should().Be("健康");
        }

        [Fact]
        public void DescriptionKey_IsDerivedFromPluginName_NotDescriptionText()
        {
            var loc = CreateLocalization("zh-CN");
            var description = "Run custom scripts in legacy intranet web pages that don't support browser extensions or userscripts.";

            // 新约定：键来自插件显示名 → 命中 Plugin.Description.WebScripts
            var byName = PluginLocalization.LocalizePluginDescription(loc, description, "Web Scripts");

            // 旧 bug：若键来自描述文本（超长怪键），永远无法命中任何 resx 键
            var byDescription = PluginLocalization.LocalizePluginDescription(loc, description, description);

            byName.Should().NotBe(description, "按显示名推导键应命中 Web Scripts 的中文描述");
            byDescription.Should().Be(description, "按描述文本推导键（旧行为）不应命中任何 resx 键");
        }
    }
}
