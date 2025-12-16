using System;
using System.Drawing; // 用于 Icon
using Pulsar.Services.Interfaces;
using Forms = System.Windows.Forms; // [关键] 为 WinForms 建立别名，避免与 WPF 冲突

namespace Pulsar.Services
{
    public class TrayIconService : ITrayService
    {
        private Forms.NotifyIcon? _notifyIcon;
        private readonly IConfigService _configService;

        public TrayIconService(IConfigService configService)
        {
            _configService = configService;
        }

        public void Initialize()
        {
            // 1. 创建 NotifyIcon
            _notifyIcon = new Forms.NotifyIcon
            {
                // 使用系统托盘图标，避免找不到图标文件导致崩溃
                Icon = SystemIcons.Application,
                Text = "Pulsar (Ctrl+Q)",
                Visible = true
            };

            // 2. 构建右键菜单 (使用 Forms 别名)
            var contextMenu = new Forms.ContextMenuStrip();

            // 菜单项: 重载配置
            var reloadItem = new Forms.ToolStripMenuItem("Reload Config");
            reloadItem.Click += async (s, e) =>
            {
                await _configService.LoadAsync();
                ShowNotification("Configuration Reloaded", "Your settings have been updated.");
            };

            // 菜单项: 退出
            var exitItem = new Forms.ToolStripMenuItem("Exit Pulsar");
            exitItem.Click += (s, e) =>
            {
                // [修复] 显式使用 System.Windows.Application 来消除歧义
                System.Windows.Application.Current.Shutdown();
            };

            contextMenu.Items.Add(reloadItem);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 3. 双击事件
            _notifyIcon.DoubleClick += (s, e) =>
            {
                ShowNotification("Pulsar", "Settings Window coming in Phase 7!");
            };
        }

        private void ShowNotification(string title, string message)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}