using System;
using System.Windows;
using System.Windows.Controls;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Controls;

namespace Pulsar.Services
{
    /// <summary>
    /// 托盘右键菜单的一次性构建（C2 拆分自 TrayIconService）。纯输入→纯输出：
    /// 快照状态（主题、自启动勾选态）+ 动作回调进，ContextMenu 出——不含任何
    /// 服务定位、注册表或窗口生命周期逻辑，可脱离托盘宿主直接测试。
    /// [E2E] AutomationId（Pulsar.Tray.*）是语言无关的 UIA 锚点，改动需同步 E2E 断言。
    /// </summary>
    public sealed class TrayMenuBuilder
    {
        private readonly IThemeService _themeService;

        public TrayMenuBuilder(IThemeService themeService)
        {
            _themeService = themeService;
        }

        /// <summary>构建所需的全部输入：本地化 + 状态快照 + 动作回调。</summary>
        public sealed record TrayMenuRequest(
            ILocalizationService Loc,
            AppTheme CurrentTheme,
            bool AutoStartEnabled,
            Action OpenSettings,
            Action ToggleTheme,
            Action ToggleAutoStart,
            Action RestartApp,
            Action ExitApp);

        public ContextMenu Build(TrayMenuRequest request)
        {
            var contextMenu = new ContextMenu();
            _themeService.ApplyContextMenuTheme(contextMenu, request.CurrentTheme);

            var settingsItem = new System.Windows.Controls.MenuItem
            {
                Header = request.Loc["Tray.Settings"],
                Icon = new SymbolIcon(SymbolRegular.Settings24)
            };
            // [E2E] Stable UIA id (language-independent; never look up by text).
            System.Windows.Automation.AutomationProperties.SetAutomationId(settingsItem, "Pulsar.Tray.Settings");
            settingsItem.Click += (_, e) => request.OpenSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            var toggleThemeItem = new System.Windows.Controls.MenuItem
            {
                Header = request.Loc["Tray.ToggleTheme"],
                Icon = new SymbolIcon(SymbolRegular.DarkTheme24),
                IsCheckable = true,
                IsChecked = request.CurrentTheme == AppTheme.Light
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(toggleThemeItem, "Pulsar.Tray.ToggleTheme");
            toggleThemeItem.Click += (_, e) => request.ToggleTheme();
            contextMenu.Items.Add(toggleThemeItem);

            var autoStartItem = new System.Windows.Controls.MenuItem
            {
                Header = request.Loc["Tray.AutoStart"],
                Icon = new SymbolIcon(SymbolRegular.Power24),
                IsCheckable = true,
                IsChecked = request.AutoStartEnabled
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(autoStartItem, "Pulsar.Tray.AutoStart");
            autoStartItem.Click += (_, e) => request.ToggleAutoStart();
            contextMenu.Items.Add(autoStartItem);

            var restartItem = new System.Windows.Controls.MenuItem
            {
                Header = request.Loc["Tray.Restart"],
                Icon = new SymbolIcon(SymbolRegular.ArrowRepeatAll24)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(restartItem, "Pulsar.Tray.Restart");
            restartItem.Click += (_, e) => request.RestartApp();
            contextMenu.Items.Add(restartItem);

            contextMenu.Items.Add(new Separator());

            var exitItem = new System.Windows.Controls.MenuItem
            {
                Header = request.Loc["Tray.Exit"],
                Icon = new SymbolIcon(SymbolRegular.DoorArrowLeft24)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(exitItem, "Pulsar.Tray.Exit");
            exitItem.Click += (_, e) => request.ExitApp();
            contextMenu.Items.Add(exitItem);

            return contextMenu;
        }
    }
}
