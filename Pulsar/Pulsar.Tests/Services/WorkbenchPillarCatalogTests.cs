using System;
using FluentAssertions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// M2 叙事约定（openspec home-screen-entry-reorder）：三支柱（宏 / 网页脚本 / 安全填写）
    /// 必须排在系统条目之前，优先级映射以 WorkbenchPillarCatalog 为单一事实来源。
    /// </summary>
    public class WorkbenchPillarCatalogTests
    {
        [Fact]
        public void PillarPluginIds_ShouldMapThreePillars_InNarrativeOrder()
        {
            WorkbenchPillarCatalog.PillarPluginIds.Should().Equal(
                "com.pulsar.vbarunner",
                "com.pulsar.bookmarklet",
                "com.pulsar.pki");
        }

        [Fact]
        public void GetPluginPriority_ShouldReturnPillarsAheadOfSystemEntries()
        {
            var macro = WorkbenchPillarCatalog.GetPluginPriority("com.pulsar.vbarunner");
            var webScripts = WorkbenchPillarCatalog.GetPluginPriority("com.pulsar.bookmarklet");
            var secureFill = WorkbenchPillarCatalog.GetPluginPriority("com.pulsar.pki");
            var winSwitcher = WorkbenchPillarCatalog.GetPluginPriority("com.pulsar.winswitcher");
            var command = WorkbenchPillarCatalog.GetPluginPriority("com.pulsar.command");

            (macro < webScripts).Should().BeTrue("宏支柱应排在网页脚本支柱之前");
            (webScripts < secureFill).Should().BeTrue("网页脚本支柱应排在安全填写支柱之前");
            (secureFill < winSwitcher).Should().BeTrue("全部支柱应排在系统插件之前");
            winSwitcher.Should().Be(WorkbenchPillarCatalog.BackgroundPriority);
            command.Should().Be(WorkbenchPillarCatalog.BackgroundPriority, "系统插件统一落在背景桶，成员顺序由调用者的次级排序决定");
        }

        [Fact]
        public void IsPillarPlugin_ShouldClassifyPillarsAndBackground()
        {
            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.vbarunner").Should().BeTrue();
            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.bookmarklet").Should().BeTrue();
            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.pki").Should().BeTrue();

            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.winswitcher").Should().BeFalse();
            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.command").Should().BeFalse();
            WorkbenchPillarCatalog.IsPillarPlugin(null).Should().BeFalse();
            WorkbenchPillarCatalog.IsPillarPlugin("com.pulsar.unknown").Should().BeFalse();
        }

        [Fact]
        public void GetScenarioPriority_ShouldLeadWithOfficeScenarios_AndPushGenericDemoToBackground()
        {
            WorkbenchPillarCatalog.GetScenarioPriority("excel").Should().Be(0);
            WorkbenchPillarCatalog.GetScenarioPriority("browser").Should().Be(1);
            WorkbenchPillarCatalog.GetScenarioPriority("notepad").Should().Be(WorkbenchPillarCatalog.BackgroundPriority);
        }

        [Fact]
        public void GetPagePriority_ShouldLeadWithWorkbenchPages_AndPushSystemPagesToBackground()
        {
            WorkbenchPillarCatalog.GetPagePriority(SettingsPageIds.Slots).Should().Be(0);
            WorkbenchPillarCatalog.GetPagePriority(SettingsPageIds.Plugins).Should().Be(1);

            WorkbenchPillarCatalog.GetPagePriority(SettingsPageIds.General)
                .Should().Be(WorkbenchPillarCatalog.BackgroundPriority);
            WorkbenchPillarCatalog.GetPagePriority(SettingsPageIds.Analytics)
                .Should().Be(WorkbenchPillarCatalog.BackgroundPriority);
            WorkbenchPillarCatalog.GetPagePriority(SettingsPageIds.About)
                .Should().Be(WorkbenchPillarCatalog.BackgroundPriority);
        }

        [Fact]
        public void PriorityLookup_ShouldBeCaseInsensitive()
        {
            WorkbenchPillarCatalog.GetPluginPriority("COM.PULSAR.PKI").Should().Be(2);
            WorkbenchPillarCatalog.GetScenarioPriority("Excel").Should().Be(0);
        }
    }
}
