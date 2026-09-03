using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// M2 叙事约定（openspec home-screen-entry-reorder）：设置导航顺序必须让
    /// 工作台条目（槽位 / 插件）领先，统计 / 关于等系统支持条目殿后，且跨会话稳定。
    /// </summary>
    public class SettingsPageCatalogTests
    {
        private static SettingsPageCatalog CreateCatalog()
        {
            return new SettingsPageCatalog(
                new LocalizationService(NullLogger<LocalizationService>.Instance));
        }

        [Fact]
        public void Pages_ShouldLeadWithWorkbenchEntries_AndTrailWithSystemEntries()
        {
            var catalog = CreateCatalog();

            catalog.Pages.Select(p => p.Id).Should().Equal(
                SettingsPageIds.Slots,
                SettingsPageIds.Plugins,
                SettingsPageIds.General,
                SettingsPageIds.Analytics,
                SettingsPageIds.About);
        }

        [Fact]
        public void WorkbenchGroup_ShouldContainSlotsAndPlugins_AndAppearFirst()
        {
            var catalog = CreateCatalog();

            var workbenchIds = catalog.Pages
                .Where(p => string.Equals(p.GroupId, SettingsPageGroupIds.Workbench, System.StringComparison.Ordinal))
                .Select(p => p.Id)
                .ToList();

            workbenchIds.Should().Equal(SettingsPageIds.Slots, SettingsPageIds.Plugins);

            var firstSystemIndex = catalog.Pages
                .Select((page, index) => (page, index))
                .First(pair => string.Equals(pair.page.GroupId, SettingsPageGroupIds.System, System.StringComparison.Ordinal))
                .index;
            var lastWorkbenchIndex = catalog.Pages
                .Select((page, index) => (page, index))
                .Last(pair => string.Equals(pair.page.GroupId, SettingsPageGroupIds.Workbench, System.StringComparison.Ordinal))
                .index;

            (lastWorkbenchIndex < firstSystemIndex).Should().BeTrue("工作台组必须整体排在系统组之前");
        }

        [Fact]
        public void SystemGroup_ShouldContainGeneralAnalyticsAbout()
        {
            var catalog = CreateCatalog();

            var systemIds = catalog.Pages
                .Where(p => string.Equals(p.GroupId, SettingsPageGroupIds.System, System.StringComparison.Ordinal))
                .Select(p => p.Id)
                .ToList();

            systemIds.Should().Equal(
                SettingsPageIds.General,
                SettingsPageIds.Analytics,
                SettingsPageIds.About);
        }

        [Fact]
        public void DefaultPageId_ShouldResolveToLeadingWorkbenchPage()
        {
            var catalog = CreateCatalog();

            catalog.DefaultPageId.Should().Be(catalog.Pages[0].Id);
            catalog.DefaultPageId.Should().Be(SettingsPageIds.Slots);
        }

        [Fact]
        public void Pages_ShouldBeStableAcrossCatalogInstances()
        {
            var first = CreateCatalog();
            var second = CreateCatalog();

            first.Pages.Select(p => p.Id).Should().Equal(second.Pages.Select(p => p.Id),
                "导航顺序必须与配置编辑状态无关，跨会话保持稳定");
        }
    }
}
