using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// TrayMenuBuilder（C2）首次让托盘菜单可测：结构、AutomationId、状态绑定与
    /// 回调路由全部断言在此，不再依赖真实 TaskbarIcon。WPF 控件在 STA 线程构造
    /// （StaTestRunner 项目惯例）。
    /// </summary>
    public class TrayMenuBuilderTests
    {
        [Fact]
        public void Build_ShouldProduceMenu_WithStableAutomationIdsAndCorrectOrder()
        {
            StaTestRunner.RunInSta(() =>
            {
                var builder = CreateBuilder();
                var request = CreateRequest();

                var menu = builder.Build(request);

                var items = menu.Items.Cast<object>().ToList();
                items.Should().HaveCount(7, "settings / sep / toggleTheme / autoStart / restart / sep / exit");
                items[1].Should().BeOfType<Separator>();
                items[5].Should().BeOfType<Separator>();

                var ids = items.OfType<System.Windows.Controls.MenuItem>()
                    .Select(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item))
                    .ToList();
                ids.Should().Equal(
                    "Pulsar.Tray.Settings",
                    "Pulsar.Tray.ToggleTheme",
                    "Pulsar.Tray.AutoStart",
                    "Pulsar.Tray.Restart",
                    "Pulsar.Tray.Exit");
            });
        }

        [Fact]
        public void Build_ShouldBindToggleStates_FromRequestSnapshot()
        {
            StaTestRunner.RunInSta(() =>
            {
                var builder = CreateBuilder();

                var lightMenu = builder.Build(CreateRequest(currentTheme: AppTheme.Light, autoStartEnabled: true));
                var darkMenu = builder.Build(CreateRequest(currentTheme: AppTheme.Dark, autoStartEnabled: false));

                FindItem(lightMenu, "Pulsar.Tray.ToggleTheme")!.IsChecked.Should().BeTrue("light theme is the checked state");
                FindItem(darkMenu, "Pulsar.Tray.ToggleTheme")!.IsChecked.Should().BeFalse();
                FindItem(lightMenu, "Pulsar.Tray.AutoStart")!.IsChecked.Should().BeTrue();
                FindItem(darkMenu, "Pulsar.Tray.AutoStart")!.IsChecked.Should().BeFalse();
            });
        }

        [Fact]
        public void Build_ShouldRouteClicks_ToRequestCallbacks()
        {
            StaTestRunner.RunInSta(() =>
            {
                var builder = CreateBuilder();
                var openSettings = 0;
                var toggleTheme = 0;
                var toggleAutoStart = 0;
                var restart = 0;
                var exit = 0;
                var request = CreateRequest(
                    openSettings: () => openSettings++,
                    toggleTheme: () => toggleTheme++,
                    toggleAutoStart: () => toggleAutoStart++,
                    restartApp: () => restart++,
                    exitApp: () => exit++);
                var menu = builder.Build(request);

                Click(FindItem(menu, "Pulsar.Tray.Settings"));
                Click(FindItem(menu, "Pulsar.Tray.ToggleTheme"));
                Click(FindItem(menu, "Pulsar.Tray.AutoStart"));
                Click(FindItem(menu, "Pulsar.Tray.Restart"));
                Click(FindItem(menu, "Pulsar.Tray.Exit"));

                openSettings.Should().Be(1);
                toggleTheme.Should().Be(1);
                toggleAutoStart.Should().Be(1);
                restart.Should().Be(1);
                exit.Should().Be(1);
            });
        }

        private static TrayMenuBuilder CreateBuilder()
        {
            var themeService = new Mock<IThemeService>();
            return new TrayMenuBuilder(themeService.Object);
        }

        private static TrayMenuBuilder.TrayMenuRequest CreateRequest(
            AppTheme currentTheme = AppTheme.Light,
            bool autoStartEnabled = false,
            Action? openSettings = null,
            Action? toggleTheme = null,
            Action? toggleAutoStart = null,
            Action? restartApp = null,
            Action? exitApp = null)
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);

            return new TrayMenuBuilder.TrayMenuRequest(
                loc.Object,
                currentTheme,
                autoStartEnabled,
                openSettings ?? (() => { }),
                toggleTheme ?? (() => { }),
                toggleAutoStart ?? (() => { }),
                restartApp ?? (() => { }),
                exitApp ?? (() => { }));
        }

        private static System.Windows.Controls.MenuItem? FindItem(ContextMenu menu, string automationId)
        {
            return menu.Items.OfType<System.Windows.Controls.MenuItem>()
                .FirstOrDefault(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == automationId);
        }

        private static void Click(System.Windows.Controls.MenuItem? item)
        {
            item.Should().NotBeNull();
            item!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        }
    }
}
