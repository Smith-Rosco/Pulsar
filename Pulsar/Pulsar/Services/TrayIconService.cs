using System;
using System.Drawing; // System.Drawing.Primitives 或 System.Drawing.Common
using System.Linq;
using Microsoft.Extensions.DependencyInjection; // 用于 IServiceProvider
using Pulsar.Services.Interfaces;
using Pulsar.Views;
using Forms = System.Windows.Forms; // 别名

namespace Pulsar.Services
{
    public class TrayIconService : ITrayService
    {
        private Forms.NotifyIcon? _notifyIcon;
        private readonly IConfigService _configService;
        private readonly IServiceProvider _serviceProvider;

        public TrayIconService(IConfigService configService, IServiceProvider serviceProvider)
        {
            _configService = configService;
            _serviceProvider = serviceProvider;
        }

        public void Initialize()
        {
            // 1. 创建 NotifyIcon
            _notifyIcon = new Forms.NotifyIcon
            {
                // 先不设置图标，稍后尝试加载自定义图标
                Text = "Pulsar (Ctrl+Q)",
                Visible = false // 加载完图标再显示，避免闪烁
            };

            // [Fix] 加载自定义图标逻辑
            TryLoadCustomIcon();

            // 2. 构建右键菜单
            var contextMenu = new Forms.ContextMenuStrip();
            var settingsItem = new Forms.ToolStripMenuItem("Settings", null, OnSettingsClicked);

            var reloadItem = new Forms.ToolStripMenuItem("Reload Config");
            reloadItem.Click += async (s, e) =>
            {
                await _configService.LoadAsync();
                ShowNotification("Configuration Reloaded", "Your settings have been updated.");
            };

            var exitItem = new Forms.ToolStripMenuItem("Exit Pulsar");
            exitItem.Click += (s, e) =>
            {
                Dispose();
                System.Windows.Application.Current.Shutdown();
            };

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(reloadItem);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 3. 双击打开设置
            _notifyIcon.DoubleClick += OnSettingsClicked;

            // 最后显示图标
            _notifyIcon.Visible = true;
        }

        private void TryLoadCustomIcon()
        {
            if (_notifyIcon == null) return;

            try
            {
                // 使用 WPF Pack URI 定位根目录下的资源
                // 格式: pack://application:,,,/程序集名称;component/路径 (如果是同一程序集，可以省略组件部分)
                var iconUri = new Uri("pack://application:,,,/Pulsar.ico");

                // 获取资源流
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);

                if (streamInfo != null)
                {
                    // 将流转换为 System.Drawing.Icon
                    _notifyIcon.Icon = new Icon(streamInfo.Stream);
                }
                else
                {
                    // 加载失败，使用默认图标
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                // 如果发生任何异常（如文件未找到、属性未设置为Resource），回退到系统图标
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            // 切换到 WPF UI 线程
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 防止重复打开
                var existing = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                if (existing != null)
                {
                    existing.Activate();
                    if (existing.WindowState == System.Windows.WindowState.Minimized)
                        existing.WindowState = System.Windows.WindowState.Normal;
                }
                else
                {
                    // 使用 DI 创建新实例 (Transient)
                    var window = _serviceProvider.GetRequiredService<SettingsWindow>();
                    window.Show();
                }
            });
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