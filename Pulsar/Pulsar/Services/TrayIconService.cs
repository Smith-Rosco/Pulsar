using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Views;

namespace Pulsar.Services
{
    public class TrayIconService : ITrayService
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly IServiceProvider _serviceProvider;

        public TrayIconService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "Pulsar (Ctrl+Q)",
                Visibility = Visibility.Visible
            };

            TryLoadCustomIcon();
            BuildContextMenu();
            _taskbarIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;
        }

        private void TryLoadCustomIcon()
        {
            if (_taskbarIcon == null) return;

            try
            {
                var iconUri = new Uri("pack://application:,,,/Pulsar.ico");
                var streamInfo = Application.GetResourceStream(iconUri);

                if (streamInfo != null)
                {
                    _taskbarIcon.Icon = new Icon(streamInfo.Stream);
                    return;
                }
            }
            catch
            {
            }

            try
            {
                _taskbarIcon.Icon = SystemIcons.Application;
            }
            catch
            {
            }
        }

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = "Settings" };
            settingsItem.Click += OnSettingsClicked;
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit Pulsar" };
            exitItem.Click += (s, e) =>
            {
                Dispose();
                Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            if (_taskbarIcon != null)
            {
                _taskbarIcon.ContextMenu = contextMenu;
            }
        }

        private void OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            OnSettingsClicked(sender, e);
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();

                if (window == null)
                {
                    window = _serviceProvider.GetRequiredService<SettingsWindow>();
                    window.Show();
                }
                else
                {
                    window.Show();

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }
                    window.Activate();
                    window.Focus();
                }
            });
        }

        public void ShowNotification(string title, string message, PulsarNotificationIcon icon)
        {
            if (_taskbarIcon != null)
            {
                var balloonIcon = icon switch
                {
                    PulsarNotificationIcon.Info => BalloonIcon.Info,
                    PulsarNotificationIcon.Warning => BalloonIcon.Warning,
                    PulsarNotificationIcon.Error => BalloonIcon.Error,
                    _ => BalloonIcon.None
                };
                _taskbarIcon.ShowBalloonTip(title, message, balloonIcon);
            }
        }

        public void Dispose()
        {
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }
        }
    }
}
