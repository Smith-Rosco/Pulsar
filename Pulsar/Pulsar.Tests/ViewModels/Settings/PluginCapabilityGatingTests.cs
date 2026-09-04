using System;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Plugins.Extensions.BookmarkletRunner;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels.Settings
{
    /// <summary>
    /// 候选 F（2026-09-04）：通用插件卡片 VM 不再按插件 ID 特判 —— 「卡片能做什么」
    /// 由插件 metadata 的能力声明决定（SupportsScriptEditor / HasBuiltinExamples /
    /// HasCustomConfigDialog / SupportsWindowInspector）。本组测试证明：
    /// 1) 可见性/路由完全跟随 metadata 能力，即使 Id 是历史上被硬编码的字符串也不生效；
    /// 2) 两个内置插件在自己的 GetMetadata() 里如实声明能力。
    /// </summary>
    public class PluginCapabilityGatingTests
    {
        private const string BookmarkletId = "com.pulsar.bookmarklet";
        private const string WinSwitcherId = "com.pulsar.winswitcher";

        private static PluginDescriptor CreateDescriptor(
            string id,
            PluginCapabilities? capabilities = null)
        {
            capabilities ??= new PluginCapabilities();

            return new PluginDescriptor
            {
                Id = id,
                DisplayName = "Generic Card",
                Version = "1.0.0",
                Author = "Pulsar Team",
                Description = "Description",
                Icon = "E8F9",
                CanDisable = true,
                Tier = PluginTier.Extension,
                ImplementationType = null,
                Dependencies = Array.Empty<string>(),
                Metadata = new PluginMetadata
                {
                    Id = id,
                    Display = new DisplayInfo
                    {
                        Name = "Generic Card",
                        Description = "Description",
                        IconKey = "E8F9",
                        Category = "Productivity"
                    },
                    UI = new UIHints
                    {
                        Badge = "Card",
                        AccentColor = "#888888"
                    },
                    Capabilities = capabilities
                },
                IsConfigurable = false
            };
        }

        private static PluginViewModel CreateCard(PluginDescriptor descriptor)
        {
            var registry = new Mock<IPluginRegistry>();
            registry.Setup(r => r.GetPlugin(descriptor.Id)).Returns((IPulsarPlugin?)null);
            registry.Setup(r => r.IsPluginEnabled(descriptor.Id)).Returns(true);

            return new PluginViewModel(
                descriptor,
                registry.Object,
                new Mock<IPluginRuntimeOps>().Object,
                new Mock<IConfigService>().Object,
                Mock.Of<ILocalizationService>(),
                metadataRegistry: null);
        }

        // ── 能力标志驱动可见性，而不是插件 ID ────────────────────────────────

        [Fact]
        public void ScriptEditorAndExampleLibrary_AreHidden_EvenForBookmarkletId_WhenNotDeclared()
        {
            // 回归锚点：旧实现按 Id == "com.pulsar.bookmarklet" 直接显示入口；
            // 若通用 VM 仍暗藏该知识，此用例会失败。
            var vm = CreateCard(CreateDescriptor(BookmarkletId));

            vm.IsScriptEditorVisible.Should().BeFalse("能力未声明时，即使 Id 是历史硬编码字符串也不应显示脚本编辑器");
            vm.IsExampleLibraryVisible.Should().BeFalse("能力未声明时，即使 Id 是历史硬编码字符串也不应显示示例库");
        }

        [Fact]
        public void ScriptEditorAndExampleLibrary_AreVisible_WhenCapabilitiesDeclared()
        {
            var vm = CreateCard(CreateDescriptor(BookmarkletId, new PluginCapabilities
            {
                SupportsScriptEditor = true,
                HasBuiltinExamples = true
            }));

            vm.IsScriptEditorVisible.Should().BeTrue();
            vm.IsExampleLibraryVisible.Should().BeTrue();
        }

        [Fact]
        public void CustomConfigDialogAndInspector_AreHidden_EvenForWinSwitcherId_WhenNotDeclared()
        {
            var vm = CreateCard(CreateDescriptor(WinSwitcherId));

            vm.HasCustomConfigDialog.Should().BeFalse("能力未声明时，即使 Id 是 WinSwitcher 也不应走自定义对话框");
            vm.SupportsWindowInspector.Should().BeFalse();
        }

        [Fact]
        public void CustomConfigDialogAndInspector_AreVisible_WhenCapabilitiesDeclared()
        {
            var vm = CreateCard(CreateDescriptor(WinSwitcherId, new PluginCapabilities
            {
                HasCustomConfigDialog = true,
                SupportsWindowInspector = true
            }));

            vm.HasCustomConfigDialog.Should().BeTrue();
            vm.SupportsWindowInspector.Should().BeTrue();
        }

        [Fact]
        public void UnknownPlugin_DefaultsToNoCardFeatures()
        {
            var vm = CreateCard(CreateDescriptor("com.pulsar.some-other-plugin"));

            vm.IsScriptEditorVisible.Should().BeFalse();
            vm.IsExampleLibraryVisible.Should().BeFalse();
            vm.HasCustomConfigDialog.Should().BeFalse();
            vm.SupportsWindowInspector.Should().BeFalse();
        }

        // ── 内置插件在自身 metadata 中如实声明能力 ───────────────────────────

        [Fact]
        public void WinSwitcherMetadata_DeclaresCustomConfigDialogAndWindowInspector()
        {
            var metadata = new WinSwitcherPlugin().GetMetadata();

            metadata.Id.Should().Be(WinSwitcherId);
            metadata.Capabilities.HasCustomConfigDialog.Should().BeTrue("WinSwitcher 的 Configure 必须走自定义黑名单对话框");
            metadata.Capabilities.SupportsWindowInspector.Should().BeTrue("WinSwitcher 的设置对话框要提供 Window Inspector 入口");
            metadata.Capabilities.SupportsScriptEditor.Should().BeFalse();
            metadata.Capabilities.HasBuiltinExamples.Should().BeFalse();
        }

        [Fact]
        public void BookmarkletMetadata_DeclaresScriptEditorAndExampleLibrary()
        {
            var metadata = new BookmarkletRunnerPlugin().GetMetadata();

            metadata.Id.Should().Be(BookmarkletId);
            metadata.Capabilities.SupportsScriptEditor.Should().BeTrue("Web Scripts 卡片要暴露应用内脚本编辑器入口");
            metadata.Capabilities.HasBuiltinExamples.Should().BeTrue("Web Scripts 卡片要暴露内置示例库入口");
            metadata.Capabilities.HasCustomConfigDialog.Should().BeFalse();
            metadata.Capabilities.SupportsWindowInspector.Should().BeFalse();
        }
    }
}
