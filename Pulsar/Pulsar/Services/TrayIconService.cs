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
        private Forms.ContextMenuStrip? _contextMenu;
        private readonly IServiceProvider _serviceProvider;

        public TrayIconService(IServiceProvider serviceProvider)
        {
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
            BuildContextMenu();

            // 3. 单击打开设置（仅左键）
            _notifyIcon.MouseClick += OnTrayIconMouseClick;
            
            // 4. 双击打开设置（保留兼容性）
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

        private void BuildContextMenu()
        {
            _contextMenu = new Forms.ContextMenuStrip();

            // 1. Settings
            var settingsItem = new Forms.ToolStripMenuItem("Settings", null, OnSettingsClicked);
            _contextMenu.Items.Add(settingsItem);

            // 2. Exit
            _contextMenu.Items.Add(new Forms.ToolStripSeparator());
            
            var exitItem = new Forms.ToolStripMenuItem("Exit Pulsar");
            exitItem.Click += (s, e) =>
            {
                Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            _contextMenu.Items.Add(exitItem);

            if (_notifyIcon != null)
            {
                _notifyIcon.ContextMenuStrip = _contextMenu;
            }
        }

        private void OnTrayIconMouseClick(object? sender, Forms.MouseEventArgs e)
        {
            // 只在左键单击时打开设置，右键由 ContextMenuStrip 自动处理
            if (e.Button == Forms.MouseButtons.Left)
            {
                OnSettingsClicked(sender, e);
            }
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            // 切换到 WPF UI 线程
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // [Fix] Check for existing instance (Singleton behavior for Transient window)
                var window = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                
                    if (window == null)
                    {
                        window = _serviceProvider.GetRequiredService<SettingsWindow>();
                        window.Show();
                    }
                    else
                    {
                        // [Fix] Ensure window is visible even if it was hidden (closing the window usually hides it)
                        window.Show();
                        
                        if (window.WindowState == System.Windows.WindowState.Minimized)
                        {
                            window.WindowState = System.Windows.WindowState.Normal;
                        }
                        window.Activate();
                        window.Focus();
                    }
            });
        }

        public void ShowNotification(string title, string message, Forms.ToolTipIcon icon)
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
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
